using System.Collections.Generic;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Context;
using TaiwuEncyclopedia.Core.Llm;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Context;

public class ContextManagerTest
{
    [Fact]
    public void BuildInitialMessagesSystemSoulHistoryUser()
    {
        var cm = new ContextManager();
        var messages = cm.BuildInitialMessages(
            systemPrompt: "SYS",
            history: new List<LlmMessage> { new() { Role = "user", Content = "old" } },
            soulSummary: "门派: 少林",
            userQuery: "怎么打剑冢");

        messages.Should().HaveCount(4);
        messages[0].Role.Should().Be("system");
        messages[0].Content.Should().Be("SYS");
        messages[1].Role.Should().Be("user");
        messages[1].Content.Should().Contain("PLAYER_SOUL");
        messages[1].Content.Should().Contain("门派: 少林");
        messages[2].Role.Should().Be("user");
        messages[2].Content.Should().Be("old");
        messages[3].Role.Should().Be("user");
        messages[3].Content.Should().Contain("怎么打剑冢");
        messages[3].Content.Should().Contain("必须先调用工具检索验证");
    }

    [Fact]
    public void BuildInitialMessagesEmptySoulAndHistory()
    {
        var cm = new ContextManager();
        var messages = cm.BuildInitialMessages("SYS", new List<LlmMessage>(), "", "hi");
        messages.Should().HaveCount(2); // system + user_query
        messages[0].Role.Should().Be("system");
        messages[1].Role.Should().Be("user");
        messages[1].Content.Should().Contain("hi");
        messages[1].Content.Should().Contain("必须先调用工具检索验证");
    }

    [Fact]
    public void ForceCompressTruncatesLongestToolResult()
    {
        var cm = new ContextManager();
        var longContent = new string('x', 100001);
        var messages = new List<LlmMessage>
        {
            new() { Role = "user", Content = "q" },
            new() { Role = "tool", ToolCallId = "1", Content = longContent },
            new() { Role = "tool", ToolCallId = "2", Content = "short" },
        };
        var result = cm.ForceCompress(messages);
        result[1].Content!.Length.Should().BeLessThan(100011); // 100000 + "\n... [已截断]" (10 chars)
        result[1].Content.Should().Contain("已截断");
        result[2].Content.Should().Be("short"); // 短的不截
    }

    [Fact]
    public void ForceCompressDoesNotTruncateBelowThreshold()
    {
        var cm = new ContextManager();
        // 5000 chars: 旧阈值 2000 会截断，新阈值 100000 不截断
        var content = new string('y', 5000);
        var messages = new List<LlmMessage>
        {
            new() { Role = "user", Content = "q" },
            new() { Role = "tool", ToolCallId = "1", Content = content },
        };
        var result = cm.ForceCompress(messages);
        result[1].Content!.Should().Be(content);  // 原样返回，不截断
        result[1].Content.Should().NotContain("已截断");
    }

    [Fact]
    public void ForceCompressNoToolResultReturnsOriginal()
    {
        var cm = new ContextManager();
        var messages = new List<LlmMessage>
        {
            new() { Role = "user", Content = "q" },
        };
        var result = cm.ForceCompress(messages);
        result.Should().BeSameAs(messages);
    }

    [Fact]
    public void BuildInitialMessagesWithSummary_InjectsSummaryAsSystem()
    {
        var cm = new ContextManager();
        var messages = cm.BuildInitialMessages(
            systemPrompt: "SYS",
            history: new List<LlmMessage>(),
            soulSummary: null,
            userQuery: "继续问",
            summary: "历史摘要内容");

        messages.Should().HaveCount(3); // system + summary + user_query
        messages[0].Role.Should().Be("system");
        messages[0].Content.Should().Be("SYS");
        messages[1].Role.Should().Be("system");
        messages[1].Content.Should().Contain("【历史摘要】");
        messages[1].Content.Should().Contain("历史摘要内容");
        messages[2].Role.Should().Be("user");
        messages[2].Content.Should().Contain("继续问");
        messages[2].Content.Should().Contain("必须先调用工具检索验证");
    }

    [Fact]
    public void BuildInitialMessagesWithNullSummary_OmitsSummaryMessage()
    {
        var cm = new ContextManager();
        var messages = cm.BuildInitialMessages("SYS", new List<LlmMessage>(), "", "hi", summary: null);
        messages.Should().HaveCount(2); // system + user_query，无 summary
        messages[0].Role.Should().Be("system");
        messages[1].Role.Should().Be("user");
        messages[1].Content.Should().Contain("hi");
        messages[1].Content.Should().Contain("必须先调用工具检索验证");
    }

    [Fact]
    public void BuildInitialMessages_InjectsSearchNudgeBeforeQuery()
    {
        var cm = new ContextManager();

        var messages = cm.BuildInitialMessages(
            "system prompt",
            new List<LlmMessage>(),
            null,
            "百花谷有啥功法",
            null);

        // 最后一条 user 消息应包含检索引导
        LlmMessage? lastUser = null;
        foreach (var m in messages)
            if (m.Role == "user") lastUser = m;
        lastUser.Should().NotBeNull();
        lastUser!.Content.Should().Contain("必须先调用工具检索验证");
        lastUser.Content.Should().Contain("百花谷有啥功法");
        // 引导文本在查询之前
        var nudgeIdx = lastUser.Content!.IndexOf("必须先调用工具检索验证");
        var queryIdx = lastUser.Content.IndexOf("百花谷有啥功法");
        nudgeIdx.Should().BeLessThan(queryIdx);
    }

    [Fact]
    public void ShouldCompress_BelowThreshold_ReturnsFalse()
    {
        var cm = new ContextManager(collapseThresholdTokens: 80000);
        var history = new List<LlmMessage> { new() { Role = "user", Content = "短问题" } };
        var needs = cm.ShouldCompress(null, history, "sys", null, "q");
        needs.Should().BeFalse();
    }

    [Fact]
    public void ShouldCompress_AtOrAboveThreshold_ReturnsTrue()
    {
        var cm = new ContextManager(collapseThresholdTokens: 1); // 极低阈值强制触发
        var history = new List<LlmMessage> { new() { Role = "user", Content = "x" } };
        var needs = cm.ShouldCompress(null, history, "sys", null, "q");
        needs.Should().BeTrue();
    }

    [Fact]
    public void CompressResult_NotTriggered_KeepsOldSummaryAndHistory()
    {
        var history = new List<LlmMessage> { new() { Role = "user", Content = "h" } };
        var r = CompressResult.NotTriggered("old", history);
        r.Summary.Should().Be("old");
        r.History.Should().BeSameAs(history);
        r.BoundaryPending.Should().BeFalse();
        r.NewSummary.Should().BeNull();
    }

    [Fact]
    public void CompressResult_Done_ClearsHistoryAndSetsNewSummary()
    {
        var r = CompressResult.Done("新摘要");
        r.Summary.Should().Be("新摘要");
        r.History.Should().BeEmpty();
        r.BoundaryPending.Should().BeTrue();
        r.NewSummary.Should().Be("新摘要");
    }

    [Fact]
    public void CompressResult_Failed_KeepsOldSummaryAndHistory()
    {
        var history = new List<LlmMessage> { new() { Role = "user", Content = "h" } };
        var r = CompressResult.Failed("old", history);
        r.Summary.Should().Be("old");
        r.History.Should().BeSameAs(history);
        r.BoundaryPending.Should().BeFalse();
        r.NewSummary.Should().BeNull();
    }
}
