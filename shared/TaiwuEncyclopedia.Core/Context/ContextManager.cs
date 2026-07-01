using System.Collections.Generic;
using System.Threading.Tasks;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Util;

namespace TaiwuEncyclopedia.Core.Context;

/// <summary>
/// 上下文管理：L1 循环内不压缩，L2 会话内超阈值才摘要。搬 v0.5 ContextManager。
/// v1.0 变化：user_id → worldId（SoulProfile 跨档 + SoulWorld 按 WorldId）。
/// </summary>
public sealed class ContextManager
{
    private readonly object? _soulManager; // Task 11 注入，本 Task 先用 object? 占位
    private readonly OpenAiCompatibleClient? _llmClient;
    private readonly int _maxHistoryRounds;
    private readonly int _collapseThreshold;

    /// <summary>
    /// 创建 ContextManager 实例。
    /// </summary>
    /// <param name="soulManager">SoulManager 实例（Task 11 后使用，当前可为 null）</param>
    /// <param name="llmClient">LLM 客户端（Task 11 后使用，当前可为 null）</param>
    /// <param name="maxHistoryRounds">保留的最大历史轮数</param>
    /// <param name="collapseThresholdTokens">触发摘要的 token 阈值</param>
    public ContextManager(
        object? soulManager = null,
        OpenAiCompatibleClient? llmClient = null,
        int maxHistoryRounds = 5,
        int collapseThresholdTokens = 40000)
    {
        _soulManager = soulManager;
        _llmClient = llmClient;
        _maxHistoryRounds = maxHistoryRounds;
        _collapseThreshold = collapseThresholdTokens;
    }

    /// <summary>构建 Agent 循环初始 messages。soul 注入位置：system 之后、历史之前（缓存友好）。</summary>
    /// <param name="systemPrompt">系统提示词</param>
    /// <param name="history">历史消息列表</param>
    /// <param name="soulSummary">玩家灵魂摘要</param>
    /// <param name="userQuery">用户当前查询</param>
    /// <returns>构建好的初始消息列表</returns>
    public List<LlmMessage> BuildInitialMessages(
        string systemPrompt,
        List<LlmMessage> history,
        string? soulSummary,
        string userQuery)
    {
        var messages = new List<LlmMessage>
        {
            new() { Role = "system", Content = systemPrompt },
        };
        if (history != null) messages.AddRange(history);
        if (!string.IsNullOrEmpty(soulSummary))
        {
            messages.Add(new LlmMessage
            {
                Role = "user",
                Content = $"【PLAYER_SOUL】\n{soulSummary}",
            });
        }
        messages.Add(new LlmMessage { Role = "user", Content = userQuery });
        return messages;
    }

    /// <summary>
    /// L2 会话内：历史超阈值时做一次摘要 + 更新 soul。
    /// 只在重建 messages 时调用，不在 FC 循环内。
    /// soulManager/llmClient 未注入时直接返回原 messages（skeleton 模式）。
    /// </summary>
    /// <param name="messages">当前消息列表</param>
    /// <param name="worldId">世界 ID</param>
    /// <returns>处理后的消息列表</returns>
    public async Task<List<LlmMessage>> CollapseIfNeededAsync(List<LlmMessage> messages, int worldId)
    {
        var totalTokens = TokenEstimator.EstimateTokensForMessages(messages);
        if (totalTokens < _collapseThreshold)
        {
            return messages;
        }
        if (_soulManager == null || _llmClient == null)
        {
            return messages;
        }

        // 分解 messages：system(0) + [soul(1)] + history + user_query(-1)
        var systemMsg = messages[0];
        var userQuery = messages[^1];

        int? soulIdx = null;
        if (messages.Count > 2)
        {
            var second = messages[1];
            if (second.Role == "user" && (second.Content ?? "").Contains("PLAYER_SOUL"))
            {
                soulIdx = 1;
            }
        }

        int historyStart = soulIdx ?? 1;
        var history = messages.GetRange(historyStart, messages.Count - historyStart - 1);

        // 保留最近 maxHistoryRounds 轮（每轮 = user + assistant = 2 条）
        int keepCount = System.Math.Min(history.Count, _maxHistoryRounds * 2);
        var recent = keepCount > 0 ? history.GetRange(history.Count - keepCount, keepCount) : new List<LlmMessage>();
        var early = keepCount < history.Count ? history.GetRange(0, history.Count - keepCount) : new List<LlmMessage>();

        if (early.Count == 0)
        {
            return messages;
        }

        // 格式化早期历史为文本
        var earlyParts = new List<string>();
        foreach (var m in early)
        {
            var c = (m.Content ?? "")[..System.Math.Min(500, (m.Content ?? "").Length)];
            earlyParts.Add($"{m.Role}: {c}");
        }
        var earlyText = string.Join("\n", earlyParts);

        // TODO Task 11: 调 SoulManager.UpdateFromCompressAsync(worldId, earlyText, _llmClient)
        // 当前 skeleton 阶段 soul_manager 类型未定，先跳过摘要逻辑
        // Task 11 完成后替换为真实调用

        // skeleton 模式：不摘要，直接返回原 messages
        return messages;
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
        int longestLen = 2000; // 只截断超过 2000 字的
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
            messages[longestIdx].Content = (messages[longestIdx].Content ?? "")[..2000] + "\n... [已截断]";
        }
        return messages;
    }
}
