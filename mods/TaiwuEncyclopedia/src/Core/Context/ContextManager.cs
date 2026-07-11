using System.Collections.Generic;
using System.Threading.Tasks;
using TaiwuEncyclopedia.Core.Agent;
using TaiwuEncyclopedia.Core.Diagnostics;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Soul;
using TaiwuEncyclopedia.Core.Util;

namespace TaiwuEncyclopedia.Core.Context;

/// <summary>
/// 上下文管理：组装初始消息 + 强制压缩兜底。搬 v0.5 ContextManager。
/// v1.0 变化：user_id → worldId (SoulProfile 跨档 + SoulWorld 按 WorldId)。
/// </summary>
public sealed class ContextManager
{
    private readonly SoulManager? _soulManager;
    private readonly ILlmClient? _llmClient;
    private readonly LlmConfig? _llmConfig;
    private readonly int _collapseThreshold;
    private readonly IAgentTrace? _trace;

    /// <summary>
    /// 创建 ContextManager 实例。
    /// </summary>
    /// <param name="soulManager">SoulManager 实例</param>
    /// <param name="llmClient">LLM 客户端</param>
    /// <param name="llmConfig">LLM 配置</param>
    /// <param name="collapseThresholdTokens">压缩阈值 token 数</param>
    public ContextManager(
        SoulManager? soulManager = null,
        ILlmClient? llmClient = null,
        LlmConfig? llmConfig = null,
        int collapseThresholdTokens = 80000,
        IAgentTrace? trace = null)
    {
        _soulManager = soulManager;
        _llmClient = llmClient;
        _llmConfig = llmConfig;
        _collapseThreshold = collapseThresholdTokens;
        _trace = trace;
    }

    /// <summary>构建 Agent 循环初始 messages。soul 注入位置：system 之后、历史之前（缓存友好）。</summary>
    /// <param name="systemPrompt">系统提示词</param>
    /// <param name="history">历史消息列表</param>
    /// <param name="soulSummary">玩家灵魂摘要</param>
    /// <param name="userQuery">用户当前查询</param>
    /// <param name="summary">历史对话摘要（可选）</param>
    /// <returns>构建好的初始消息列表</returns>
    public List<LlmMessage> BuildInitialMessages(
        string systemPrompt,
        List<LlmMessage> history,
        string? soulSummary,
        string userQuery,
        string? summary = null)
    {
        var messages = new List<LlmMessage>
        {
            new() { Role = "system", Content = systemPrompt },
        };
        if (!string.IsNullOrEmpty(soulSummary))
        {
            messages.Add(new LlmMessage
            {
                Role = "user",
                Content = $"【PLAYER_SOUL】\n{soulSummary}",
            });
        }
        if (!string.IsNullOrEmpty(summary))
        {
            messages.Add(new LlmMessage { Role = "system", Content = $"【历史摘要】\n{summary}" });
        }
        if (history != null) messages.AddRange(history);
        messages.Add(new LlmMessage { Role = "user", Content = userQuery });
        return messages;
    }

    /// <summary>检测是否需要压缩（不执行压缩）。token 估算含 system+soul+oldSummary+history+query。</summary>
    public bool ShouldCompress(
        string? oldSummary,
        List<LlmMessage> history,
        string systemPrompt,
        string? soulSummary,
        string query)
    {
        var projected = TokenEstimator.EstimateTokens(systemPrompt)
            + TokenEstimator.EstimateTokens(soulSummary)
            + TokenEstimator.EstimateTokens(oldSummary)
            + TokenEstimator.EstimateTokensForMessages(history)
            + TokenEstimator.EstimateTokens(query);
        var triggered = projected >= _collapseThreshold;
        _trace?.ContextStep("compression_check", 0, new Dictionary<string, object>
        {
            ["triggered"] = triggered,
            ["projectedTokens"] = projected,
            ["threshold"] = _collapseThreshold,
            ["oldSummaryChars"] = oldSummary?.Length ?? 0,
        });
        return triggered;
    }

    /// <summary>执行压缩：oldSummary + history → 新摘要。返回 CompressResult。</summary>
    public async Task<CompressResult> CompressAsync(
        string? oldSummary,
        List<LlmMessage> history,
        int worldId)
    {
        if (_soulManager == null || _llmClient == null || _llmConfig == null)
            return CompressResult.NotTriggered(oldSummary, history);

        var historyText = FormatHistory(history);
        var newSummary = await _soulManager.UpdateFromCompressAsync(
            worldId, historyText, _llmClient, _llmConfig, oldSummary, _trace);

        if (string.IsNullOrEmpty(newSummary))
        {
            _trace?.ContextStep("compression_summary", 0, new Dictionary<string, object>
            {
                ["success"] = false,
            });
            return CompressResult.Failed(oldSummary, history);
        }

        _trace?.ContextStep("compression_summary", 0, new Dictionary<string, object>
        {
            ["success"] = true,
            ["newSummaryChars"] = newSummary!.Length,
            ["summary"] = newSummary,
        });
        return CompressResult.Done(newSummary!);
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

    /// <summary>
    /// API 报 context too long 时的兜底：截断最长 tool result，不摘要。
    /// L1 循环内的异常路径，不常态。
    /// </summary>
    /// <param name="messages">当前消息列表</param>
    /// <returns>处理后的消息列表</returns>
    public List<LlmMessage> ForceCompress(List<LlmMessage> messages)
    {
        int longestIdx = -1;
        int longestLen = 100000; // 只截断超过 100000 字的（≈30000 tokens，匹配 LightRAG max_total_tokens）
        for (int i = 0; i < messages.Count; i++)
        {
            if (messages[i].Role == "tool")
            {
                var content = messages[i].Content ?? "";
                if (content.Length > longestLen)
                {
                    longestLen = content.Length;
                    longestIdx = i;
                }
            }
        }
        if (longestIdx >= 0)
        {
            var original = messages[longestIdx].Content ?? "";
            messages[longestIdx].Content = original[..100000] + "\n... [已截断]";
            _trace?.ContextStep("force_compress", 0, new Dictionary<string, object>
            {
                ["truncatedIndex"] = longestIdx,
                ["originalChars"] = original.Length,
                ["truncatedChars"] = messages[longestIdx].Content.Length,
            });
        }
        return messages;
    }
}

/// <summary>压缩检测结果。携带摘要、历史、是否待追加边界。</summary>
public sealed class CompressResult
{
    /// <summary>用于注入 messages 的摘要（压缩成功=新摘要，否则=旧摘要）。</summary>
    public string? Summary { get; init; }

    /// <summary>压缩后剩余的历史（压缩成功=空列表，否则=原 history）。</summary>
    public List<LlmMessage> History { get; init; } = new();

    /// <summary>是否需要在轮末追加边界消息。</summary>
    public bool BoundaryPending { get; init; }

    /// <summary>新摘要（仅压缩成功时非 null，用于 AppendBoundaryAsync）。</summary>
    public string? NewSummary { get; init; }

    public static CompressResult NotTriggered(string? oldSummary, List<LlmMessage> history) => new()
    {
        Summary = oldSummary,
        History = history,
        BoundaryPending = false,
        NewSummary = null,
    };

    public static CompressResult Failed(string? oldSummary, List<LlmMessage> history) => new()
    {
        Summary = oldSummary,
        History = history,
        BoundaryPending = false,
        NewSummary = null,
    };

    public static CompressResult Done(string newSummary) => new()
    {
        Summary = newSummary,
        History = new List<LlmMessage>(),
        BoundaryPending = true,
        NewSummary = newSummary,
    };
}
