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
        messages[3].Content.Should().Be("怎么打剑冢");
    }

    [Fact]
    public void BuildInitialMessagesEmptySoulAndHistory()
    {
        var cm = new ContextManager();
        var messages = cm.BuildInitialMessages("SYS", new List<LlmMessage>(), "", "hi");
        messages.Should().HaveCount(2); // system + user_query
        messages[0].Role.Should().Be("system");
        messages[1].Role.Should().Be("user");
        messages[1].Content.Should().Be("hi");
    }

    [Fact]
    public async System.Threading.Tasks.Task CollapseIfNeededBelowThresholdReturnsOriginal()
    {
        var cm = new ContextManager(collapseThresholdTokens: 40000);
        var messages = new List<LlmMessage>
        {
            new() { Role = "system", Content = "SYS" },
            new() { Role = "user", Content = "hi" },
        };
        var result = await cm.CollapseIfNeededAsync(messages, worldId: 1);
        result.Should().BeSameAs(messages); // 未触发，返回原列表
    }

    [Fact]
    public async System.Threading.Tasks.Task CollapseIfNeededNoSoulManagerReturnsOriginal()
    {
        var cm = new ContextManager(collapseThresholdTokens: 1); // 极低阈值强制触发
        var messages = new List<LlmMessage>
        {
            new() { Role = "system", Content = "SYS" },
            new() { Role = "user", Content = "long enough to trigger" },
        };
        var result = await cm.CollapseIfNeededAsync(messages, worldId: 1);
        result.Should().BeSameAs(messages); // soul_manager 为 null，直接返回
    }

    [Fact]
    public void ForceCompressTruncatesLongestToolResult()
    {
        var cm = new ContextManager();
        var longContent = new string('x', 5000);
        var messages = new List<LlmMessage>
        {
            new() { Role = "user", Content = "q" },
            new() { Role = "tool", ToolCallId = "1", Content = longContent },
            new() { Role = "tool", ToolCallId = "2", Content = "short" },
        };
        var result = cm.ForceCompress(messages);
        result[1].Content!.Length.Should().BeLessThan(5000);
        result[1].Content.Should().Contain("已截断");
        result[2].Content.Should().Be("short"); // 短的不截
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
}
