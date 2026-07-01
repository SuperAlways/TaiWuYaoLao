using System.Collections.Generic;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Util;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Util;

public class TokenEstimatorTest
{
    [Fact]
    public void EmptyStringReturnsZero()
    {
        TokenEstimator.EstimateTokens("").Should().Be(0);
        TokenEstimator.EstimateTokens(null).Should().Be(0);
    }

    [Fact]
    public void ChineseChars05PerChar()
    {
        // 4 个中文字 → 4 * 0.5 = 2
        TokenEstimator.EstimateTokens("太吾绘卷").Should().Be(2);
    }

    [Fact]
    public void AsciiChars015PerChar()
    {
        // 10 个 ascii → 10 * 0.15 = 1.5 → int(1.5) = 1
        TokenEstimator.EstimateTokens("abcdefghij").Should().Be(1);
    }

    [Fact]
    public void MixedChineseAndAscii()
    {
        // "太吾abc" = 2 中文 + 3 ascii → 2*0.5 + 3*0.15 = 1 + 0.45 = 1.45 → 1
        TokenEstimator.EstimateTokens("太吾abc").Should().Be(1);
    }

    [Fact]
    public void MessagesIncludesContentPlus4PerMessage()
    {
        var messages = new List<LlmMessage>
        {
            new() { Role = "user", Content = "太吾" }, // 2*0.5=1 + 4 = 5
        };
        TokenEstimator.EstimateTokensForMessages(messages).Should().Be(5);
    }

    [Fact]
    public void MessagesIncludesToolCalls()
    {
        var messages = new List<LlmMessage>
        {
            new()
            {
                Role = "assistant",
                Content = "thinking",
                ToolCalls = new List<ToolCall>
                {
                    new()
                    {
                        Id = "call_1",
                        Function = new ToolCallFunction { Name = "retrieve_rag", Arguments = "{\"query\":\"太吾\"}" },
                    },
                },
            },
        };
        // content "thinking" = 8*0.15=1.2→1
        // tool_call name "retrieve_rag" = 13*0.15=1.95→1
        // tool_call arguments "{\"query\":\"太吾\"}" = "query"+":"+"太吾" = 12 ascii(1.8→1) + 2 chinese(1) = 2, 加上引号括号等约 16 chars → 16*0.15=2.4→2
        // + 4 per message
        // 总和约 1+1+2+4 = 8（允许偏差，关键是 tool_calls 被计入）
        var tokens = TokenEstimator.EstimateTokensForMessages(messages);
        tokens.Should().BeGreaterThan(6); // 若只算 content 会是 1+4=5，计入 tool_calls 应 > 6
    }
}