using System;
using System.Collections.Generic;
using TaiwuEncyclopedia.Core.Llm;

namespace TaiwuEncyclopedia.Core.Util;

/// <summary>
/// 纯字符启发式 token 估算（spec 5.2：中文 0.5/字，其他 0.15/字）。
/// 不用 tiktoken——cl100k_base 与 DeepSeek 不匹配，给出"精确的错误数字"比"粗糙的启发式"更危险。
/// 唯一消费者是 collapse_if_needed 的 40K 阈值判断，偏差 30% 完全可接受。
/// </summary>
public static class TokenEstimator
{
    public static int EstimateTokens(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }
        int chineseChars = 0;
        foreach (var c in text)
        {
            if (c >= '\u4e00' && c <= '\u9fff')
            {
                chineseChars++;
            }
        }
        int otherChars = text.Length - chineseChars;
        return (int)(chineseChars * 0.5 + otherChars * 0.15);
    }

    public static int EstimateTokensForMessages(List<LlmMessage> messages)
    {
        var total = 0;
        foreach (var msg in messages)
        {
            total += EstimateTokens(msg.Content ?? "");
            if (msg.ToolCalls != null)
            {
                foreach (var tc in msg.ToolCalls)
                {
                    total += EstimateTokens(tc.Function.Name);
                    total += EstimateTokens(tc.Function.Arguments);
                }
            }
            total += 4; // role 等元数据开销
        }
        return total;
    }
}