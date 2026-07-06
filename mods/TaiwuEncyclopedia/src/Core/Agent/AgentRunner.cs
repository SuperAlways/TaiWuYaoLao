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
using TaiwuEncyclopedia.Core.Http;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Session;
using TaiwuEncyclopedia.Core.Soul;
using TaiwuEncyclopedia.Core.Tools;
using TaiwuEncyclopedia.Core.Util;

namespace TaiwuEncyclopedia.Core.Agent;

/// <summary>
/// Agent 编排入口。搬 v0.5 AgentRunner + agent_loop。
/// RunAsync 是 IAsyncEnumerable，yield 事件序列：
/// StartEvent → [ToolCallEvent / ToolResultEvent / FinalChunkEvent] → EndEvent
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
    private readonly int _collapseThreshold;
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
    /// <param name="collapseThresholdTokens">压缩阈值 token 数（默认 80000）。</param>
    /// <param name="trace">追踪器（可选）。</param>
    public AgentRunner(
        OpenAiCompatibleClient llmClient,
        LlmConfig llmConfig,
        ToolRegistry registry,
        ToolExecutor executor,
        ContextManager contextManager,
        SoulManager soulManager,
        SessionManager sessionManager,
        PromptBuilder promptBuilder,
        int maxIter = 6,
        int collapseThresholdTokens = 80000,
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
        _collapseThreshold = collapseThresholdTokens;
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

        // 1. 加载组件
        var systemPrompt = _prompts.BuildSystemPrompt(personaId);
        var soulSummary = await _soul.GetSoulSummaryAsync(worldId);

        string? oldSummary;
        if (history == null)
        {
            var (s, newMsgs) = await _session.LoadForAgentAsync(worldId);
            oldSummary = s;
            history = newMsgs
                .Where(m => m.Role == "user" || m.Role == "assistant")
                .Select(m => new LlmMessage { Role = m.Role, Content = m.Content })
                .ToList();
        }
        else
        {
            oldSummary = null;
        }

        // 2. 检测是否需要压缩（组装前）
        bool boundaryPending = false;
        string? newSummary = null;
        var projectedTokens = TokenEstimator.EstimateTokens(systemPrompt)
            + TokenEstimator.EstimateTokens(soulSummary)
            + TokenEstimator.EstimateTokens(oldSummary)
            + TokenEstimator.EstimateTokensForMessages(history)
            + TokenEstimator.EstimateTokens(query);

        if (projectedTokens >= _collapseThreshold)
        {
            yield return new StatusEvent { Message = "正在压缩历史对话，需要约 20 秒..." };
            var historyText = FormatHistory(history);
            newSummary = await _soul.UpdateFromCompressAsync(worldId, historyText, _llmClient, _llmConfig, oldSummary);
            if (!string.IsNullOrEmpty(newSummary))
            {
                history = new List<LlmMessage>(); // 全部被摘要吸收
                boundaryPending = true;
            }
            else
            {
                newSummary = null; // 压缩失败，退化
            }
        }

        string? summary = newSummary ?? oldSummary;

        // 3. 组装提示词
        var messages = _ctx.BuildInitialMessages(systemPrompt, history, soulSummary, query, summary);

        yield return new StartEvent { WorldId = worldId };

        // 4. ReAct 循环
        var finalAnswerParts = new List<string>();
        var loopResult = new AgentLoopResult();
        var thinkingBuilder = new StringBuilder();

        await foreach (var evt in AgentLoop.Run(
            _llmClient, _executor, _ctx, _toolsSchema, messages, _llmConfig,
            worldId, _maxIter, collectedRefs, finalAnswerParts, loopResult,
            thinkingBuilder: thinkingBuilder))
        {
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
        if (boundaryPending && !string.IsNullOrEmpty(newSummary))
        {
            try { await _session.AppendBoundaryAsync(worldId, newSummary!); }
            catch (System.Exception ex) { CoreLog.Write("TE.Session", $"AppendBoundaryAsync failed: {ex.Message}"); }
        }

        // 8. yield EndEvent
        yield return new EndEvent
        {
            ThinkingTimeMs = (int)stopwatch.Elapsed.TotalMilliseconds,
            TotalIterations = loopResult.TotalIterations + 1,
        };
    }

    private static string FormatHistory(List<LlmMessage> history)
    {
        var parts = new List<string>();
        foreach (var m in history)
        {
            parts.Add($"{m.Role}: {m.Content}");
        }
        return string.Join("\n", parts);
    }
}
