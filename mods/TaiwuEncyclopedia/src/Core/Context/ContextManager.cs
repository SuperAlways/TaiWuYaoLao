using System.Collections.Generic;
using System.Threading.Tasks;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Soul;

namespace TaiwuEncyclopedia.Core.Context;

/// <summary>
/// 上下文管理：组装初始消息 + 强制压缩兜底。搬 v0.5 ContextManager。
/// v1.0 变化：user_id → worldId (SoulProfile 跨档 + SoulWorld 按 WorldId)。
/// </summary>
public sealed class ContextManager
{
    private readonly SoulManager? _soulManager;
    private readonly OpenAiCompatibleClient? _llmClient;
    private readonly LlmConfig? _llmConfig;

    /// <summary>
    /// 创建 ContextManager 实例。
    /// </summary>
    /// <param name="soulManager">SoulManager 实例</param>
    /// <param name="llmClient">LLM 客户端</param>
    /// <param name="llmConfig">LLM 配置</param>
    public ContextManager(
        SoulManager? soulManager = null,
        OpenAiCompatibleClient? llmClient = null,
        LlmConfig? llmConfig = null)
    {
        _soulManager = soulManager;
        _llmClient = llmClient;
        _llmConfig = llmConfig;
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
            messages[longestIdx].Content = (messages[longestIdx].Content ?? "").Substring(0, 100000) + "\n... [已截断]";
        }
        return messages;
    }
}
