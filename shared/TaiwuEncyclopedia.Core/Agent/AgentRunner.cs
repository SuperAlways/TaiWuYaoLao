using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TaiwuEncyclopedia.Core.Context;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Session;
using TaiwuEncyclopedia.Core.Soul;
using TaiwuEncyclopedia.Core.Tools;

namespace TaiwuEncyclopedia.Core.Agent;

/// <summary>
/// Agent 编排入口。搬 v0.5 AgentRunner + agent_loop。
/// RunAsync 是 IAsyncEnumerable，yield 事件序列：
/// StartEvent → [ToolCallEvent / ToolResultEvent / FinalChunkEvent]* → EndEvent
/// </summary>
public sealed class AgentRunner
{
    private readonly OpenAiCompatibleClient _llmClient;
    private readonly LlmConfig _llmConfig;
    private readonly ToolRegistry _registry;
    private readonly ToolExecutor _executor;
    private readonly ContextManager _ctx;
    private readonly SoulManager _soul;
    private readonly SessionManager _session;
    private readonly PromptBuilder _prompts;
    private readonly int _maxIter;

    /// <summary>
    /// 初始化 AgentRunner。
    /// </summary>
    /// <param name="llmClient">LLM 客户端。</param>
    /// <param name="llmConfig">LLM 配置。</param>
    /// <param name="registry">工具注册表。</param>
    /// <param name="executor">工具执行器。</param>
    /// <param name="contextManager">上下文管理器。</param>
    /// <param name="soulManager">魂管理器。</param>
    /// <param name="sessionManager">会话管理器。</param>
    /// <param name="promptBuilder">提示构建器。</param>
    /// <param name="maxIter">最大迭代次数（默认 6）。</param>
    public AgentRunner(
        OpenAiCompatibleClient llmClient,
        LlmConfig llmConfig,
        ToolRegistry registry,
        ToolExecutor executor,
        ContextManager contextManager,
        SoulManager soulManager,
        SessionManager sessionManager,
        PromptBuilder promptBuilder,
        int maxIter = 6)
    {
        _llmClient = llmClient;
        _llmConfig = llmConfig;
        _registry = registry;
        _executor = executor;
        _ctx = contextManager;
        _soul = soulManager;
        _session = sessionManager;
        _prompts = promptBuilder;
        _maxIter = maxIter;
    }

    /// <summary>
    /// 运行 Agent，返回事件流。
    /// </summary>
    /// <param name="query">用户查询。</param>
    /// <param name="worldId">世界 ID。</param>
    /// <param name="personaId">persona ID（可选）。</param>
    /// <param name="history">历史消息（可选）。</param>
    /// <returns>Agent 事件的异步枚举。</returns>
    public async IAsyncEnumerable<AgentEvent> RunAsync(
        string query,
        int worldId,
        string? personaId = null,
        List<LlmMessage>? history = null)
    {
        var stopwatch = Stopwatch.StartNew();
        history ??= new List<LlmMessage>();

        // 1. 构建 messages
        var systemPrompt = _prompts.BuildSystemPrompt(personaId);
        var soulSummary = await _soul.GetSoulSummaryAsync(worldId);
        var messages = _ctx.BuildInitialMessages(systemPrompt, history, soulSummary, query);

        // 2. L2 collapse 检查
        messages = await _ctx.CollapseIfNeededAsync(messages, worldId);

        // 3. 构建 tools schema
        var toolsSchema = _registry.BuildOpenaiTools();

        yield return new StartEvent { WorldId = worldId };

        // 4. ReAct 循环
        var finalAnswerParts = new List<string>();
        var totalIterations = 0;
        List<ToolCall>? prevToolCalls = null;

        for (int iteration = 0; iteration < _maxIter; iteration++)
        {
            // 4.1 THINKING 调用（非流式，带 tools）
            LlmResponse response;
            try
            {
                response = await _llmClient.Chat(
                    AgentLLMRole.Thinking, messages, _llmConfig, tools: toolsSchema);
            }
            catch
            {
                // force_compress 重试
                messages = _ctx.ForceCompress(messages);
                response = await _llmClient.Chat(
                    AgentLLMRole.Thinking, messages, _llmConfig, tools: toolsSchema);
            }

            // 4.2 无 tool_calls → 最终答案
            if (response.ToolCalls == null || response.ToolCalls.Count == 0)
            {
                totalIterations = iteration;
                if (!string.IsNullOrEmpty(response.Content))
                {
                    messages.Add(new LlmMessage { Role = "assistant", Content = response.Content });
                }
                messages.Add(new LlmMessage
                {
                    Role = "user",
                    Content = "请根据以上思考过程和检索到的资料，以选中 persona 的口吻给出最终回答。",
                });

                await foreach (var chunk in _llmClient.StreamChat(AgentLLMRole.Answer, messages, _llmConfig))
                {
                    finalAnswerParts.Add(chunk);
                    yield return new FinalChunkEvent { Content = chunk, Iteration = iteration };
                }
                break;
            }

            // 4.3 Jaccard 循环检测
            if (LoopDetector.IsLoopSimilar(response.ToolCalls, prevToolCalls))
            {
                totalIterations = iteration;
                messages.Add(new LlmMessage { Role = "assistant", Content = response.Content ?? "" });
                messages.Add(new LlmMessage
                {
                    Role = "user",
                    Content = "你似乎在重复检索相同的内容。请根据已检索到的资料，以选中 persona 的口吻给出最终回答。",
                });

                await foreach (var chunk in _llmClient.StreamChat(AgentLLMRole.Answer, messages, _llmConfig))
                {
                    finalAnswerParts.Add(chunk);
                    yield return new FinalChunkEvent { Content = chunk, Iteration = iteration };
                }
                break;
            }

            // 4.4 有 tool_calls → yield tool_call → 执行 → yield tool_result → 回写
            foreach (var tc in response.ToolCalls)
            {
                var args = ParseArgs(tc.Function.Arguments);
                yield return new ToolCallEvent
                {
                    Name = tc.Function.Name,
                    Args = args,
                    DisplayText = $"🔧 {tc.Function.Name}",
                    Iteration = iteration,
                };
            }

            // 回写 assistant 消息（含 tool_calls）
            messages.Add(new LlmMessage
            {
                Role = "assistant",
                Content = response.Content ?? "",
                ToolCalls = response.ToolCalls,
            });

            // 并行执行工具
            var contextParams = new Dictionary<string, object> { ["world_id"] = worldId };
            var results = await _executor.ExecuteAsync(response.ToolCalls, contextParams);

            // 回写 tool results + yield tool_result 事件
            for (int i = 0; i < response.ToolCalls.Count; i++)
            {
                var tc = response.ToolCalls[i];
                var result = results[i];
                messages.Add(new LlmMessage
                {
                    Role = "tool",
                    ToolCallId = tc.Id,
                    Content = result.Content,
                });
                yield return new ToolResultEvent { Name = tc.Function.Name, Iteration = iteration };
            }

            prevToolCalls = response.ToolCalls;
            totalIterations = iteration + 1;
        }

        // 5. 兜底轮（max_iter 耗尽且未收敛）
        if (finalAnswerParts.Count == 0)
        {
            messages.Add(new LlmMessage
            {
                Role = "user",
                Content = "你已用完所有工具调用轮次。请根据已检索到的资料和思考过程，以选中 persona 的口吻给出最终回答。如果资料不足，诚实说明并给出已有的判断。",
            });

            await foreach (var chunk in _llmClient.StreamChat(AgentLLMRole.Answer, messages, _llmConfig))
            {
                finalAnswerParts.Add(chunk);
                yield return new FinalChunkEvent { Content = chunk, Iteration = _maxIter };
            }
        }

        // 6. 保存会话
        var finalAnswer = string.Join("", finalAnswerParts);
        try
        {
            await _session.SaveConversationAsync(worldId, query, finalAnswer);
        }
        catch { /* 保存失败不阻塞 */ }

        // 7. yield EndEvent
        yield return new EndEvent
        {
            ThinkingTimeMs = (int)stopwatch.Elapsed.TotalMilliseconds,
            TotalIterations = totalIterations + 1, // +1 计 ANSWER 直答轮
        };
    }

    private static Dictionary<string, object> ParseArgs(string? arguments)
    {
        if (string.IsNullOrEmpty(arguments)) return new();
        try
        {
            return JObject.Parse(arguments).ToObject<Dictionary<string, object>>() ?? new();
        }
        catch { return new(); }
    }
}
