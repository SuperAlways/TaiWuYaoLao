using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaiwuEncyclopedia.Core.Context;
using TaiwuEncyclopedia.Core.Diagnostics;
using TaiwuEncyclopedia.Core.Rag;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Session;
using TaiwuEncyclopedia.Core.Soul;
using TaiwuEncyclopedia.Core.Tools;

namespace TaiwuEncyclopedia.Core.Agent;

/// <summary>
/// Agent 编排入口。搬 v0.5 AgentRunner + agent_loop。
/// RunAsync 是 IAsyncEnumerable，yield 事件序列：
/// StartEvent → [ToolCallEvent / ToolResultEvent / FinalChunkEvent] → EndEvent
/// </summary>
public sealed class AgentRunner
{
    private readonly ILlmClient _llmClient;
    private readonly LlmConfig _llmConfig;
    private readonly ToolRegistry _registry;
    private readonly ToolExecutor _executor;
    private readonly ContextManager _ctx;
    private readonly SoulManager _soul;
    private readonly SessionManager _session;
    private readonly PromptBuilder _prompts;
    private readonly int _maxIter;
    private readonly IAgentTrace _trace;
    private readonly List<Dictionary<string, object>>? _toolsSchema;

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
    /// <param name="trace">追踪器（可选）。</param>
    public AgentRunner(
        ILlmClient llmClient,
        LlmConfig llmConfig,
        ToolRegistry registry,
        ToolExecutor executor,
        ContextManager contextManager,
        SoulManager soulManager,
        SessionManager sessionManager,
        PromptBuilder promptBuilder,
        int maxIter = 6,
        IAgentTrace? trace = null)
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
        _trace = trace ?? NullAgentTrace.Instance;
        _toolsSchema = registry?.BuildOpenaiTools();
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
        var collectedRefs = new List<Reference>();

        // 0. 开始 trace 会话（必须最先，生成 sessionId）
        _trace.BeginSession(query, worldId, personaId);

        // 1. 加载组件
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var systemPrompt = _prompts.BuildSystemPrompt(personaId);
        _trace.ContextStep("load_system_prompt", (int)sw.ElapsedMilliseconds, new Dictionary<string, object>
        {
            ["chars"] = systemPrompt.Length,
            ["personaId"] = personaId ?? "",
        });

        sw.Restart();
        var soulSummary = await _soul.GetSoulSummaryAsync(worldId);
        _trace.ContextStep("get_soul_summary", (int)sw.ElapsedMilliseconds, new Dictionary<string, object>
        {
            ["chars"] = soulSummary?.Length ?? 0,
        });

        string? oldSummary;
        if (history == null)
        {
            sw.Restart();
            var (s, msgs) = await _session.LoadForAgentAsMessagesAsync(worldId);
            oldSummary = s;
            history = msgs;
            _trace.ContextStep("load_history", (int)sw.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["oldSummaryChars"] = oldSummary?.Length ?? 0,
                ["newMsgCount"] = history.Count,
                ["historyRounds"] = history.Count / 2,
            });
        }
        else
        {
            oldSummary = null;
        }

        // 2. 压缩检测 + 执行（委托 ContextManager）
        CompressResult? compress = null;
        if (_ctx.ShouldCompress(oldSummary, history, systemPrompt, soulSummary, query))
        {
            yield return new StatusEvent { Message = "正在压缩历史对话，需要约 20 秒..." };
            compress = await _ctx.CompressAsync(oldSummary, history, worldId);
        }

        string? summary = compress?.Summary ?? oldSummary;
        var effectiveHistory = compress?.History ?? history;

        // 3. 组装提示词
        sw.Restart();
        var messages = _ctx.BuildInitialMessages(systemPrompt, effectiveHistory, soulSummary, query, summary);
        var totalChars = 0;
        foreach (var m in messages) totalChars += (m.Content ?? "").Length;
        _trace.ContextStep("build_initial_messages", (int)sw.ElapsedMilliseconds, new Dictionary<string, object>
        {
            ["messagesCount"] = messages.Count,
            ["totalChars"] = totalChars,
            ["messages"] = messages,
        });

        yield return new StartEvent { WorldId = worldId };

        // 4. ReAct 循环
        var finalAnswerParts = new List<string>();
        var loopResult = new AgentLoopResult();
        var thinkingBuilder = new StringBuilder();
        int totalPrompt = 0, totalCompletion = 0, totalCacheHit = 0;

        await foreach (var evt in AgentLoop.Run(
            _llmClient, _executor, _ctx, _toolsSchema, messages, _llmConfig,
            worldId, _maxIter, collectedRefs, finalAnswerParts, loopResult,
            trace: _trace,
            thinkingBuilder: thinkingBuilder))
        {
            if (evt is UsageEvent u)
            {
                totalPrompt += u.PromptTokens;
                totalCompletion += u.CompletionTokens;
                totalCacheHit += u.CacheHitTokens;
            }
            yield return evt;
        }

        // 5. Top-5 references
        var topRefs = collectedRefs
            .OrderByDescending(r => r.HitCount)
            .Take(5)
            .ToList();
        yield return new ReferencesEvent { References = topRefs };

        // 6. 保存会话
        var finalAnswer = string.Join("", finalAnswerParts);
        var thinkingContent = thinkingBuilder.Length > 0 ? thinkingBuilder.ToString().TrimEnd() : null;
        try
        {
            await _session.SaveConversationAsync(worldId, query, finalAnswer, topRefs,
                thinkingContent: thinkingContent);
        }
        catch (System.Exception ex) { CoreLog.Write("TE.Session", $"SaveConversationAsync failed: {ex.Message}"); }

        // 7. 追加压缩边界
        if (compress is { BoundaryPending: true, NewSummary: not null })
        {
            try { await _session.AppendBoundaryAsync(worldId, compress.NewSummary!); }
            catch (System.Exception ex) { CoreLog.Write("TE.Session", $"AppendBoundaryAsync failed: {ex.Message}"); }
        }

        // 8. 结束 trace 会话
        var totalUsage = new TokenUsage
        {
            PromptTokens = totalPrompt,
            CompletionTokens = totalCompletion,
            CacheHitTokens = totalCacheHit,
        };
        _trace.EndSession((int)stopwatch.Elapsed.TotalMilliseconds, loopResult.TotalIterations + 1,
            finalAnswer.Length, totalUsage);

        // 9. yield EndEvent
        yield return new EndEvent
        {
            ThinkingTimeMs = (int)stopwatch.Elapsed.TotalMilliseconds,
            TotalIterations = loopResult.TotalIterations + 1,
        };
    }

}
