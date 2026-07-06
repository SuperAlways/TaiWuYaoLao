using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Session;
using TaiwuEncyclopedia.Core.Storage;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Storage;

public class JsonSessionStoreTest
{
    [Fact]
    public async Task AppendAndLoadRoundtrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-test-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);

        await store.AppendMessageAsync(1, new MessageRecord { Role = "user", Content = "hello" });
        await store.AppendMessageAsync(1, new MessageRecord { Role = "assistant", Content = "hi" });

        var messages = await store.LoadRecentAsync(1, 10);
        messages.Should().HaveCount(2);
        messages[0].Content.Should().Be("hello");
        messages[1].Content.Should().Be("hi");
    }

    [Fact]
    public async Task LoadRecentRespectsLimit()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-test-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);

        for (int i = 0; i < 5; i++)
        {
            await store.AppendMessageAsync(1, new MessageRecord { Role = "user", Content = i.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        }

        var messages = await store.LoadRecentAsync(1, 3);
        messages.Should().HaveCount(3);
        messages[0].Content.Should().Be("2");
        messages[2].Content.Should().Be("4");
    }

    [Fact]
    public async Task DifferentWorldIdIsolated()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-test-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);

        await store.AppendMessageAsync(1, new MessageRecord { Role = "user", Content = "world1" });
        await store.AppendMessageAsync(2, new MessageRecord { Role = "user", Content = "world2" });

        var m1 = await store.LoadRecentAsync(1, 10);
        var m2 = await store.LoadRecentAsync(2, 10);
        m1.Should().HaveCount(1);
        m1[0].Content.Should().Be("world1");
        m2.Should().HaveCount(1);
        m2[0].Content.Should().Be("world2");
    }

    [Fact]
    public async Task CorruptFileReturnsEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-test-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Sessions", "Worlds"));
        await File.WriteAllTextAsync(Path.Combine(root, "Sessions", "Worlds", "world-1.json"), "{INVALID JSON");

        var store = new JsonSessionStore(root);
        var messages = await store.LoadRecentAsync(1, 10);
        messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ClearRemovesMessages()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-test-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        await store.AppendMessageAsync(1, new MessageRecord { Role = "user", Content = "x" });

        await store.ClearAsync(1);

        var messages = await store.LoadRecentAsync(1, 10);
        messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ListConversationsAsyncReturnsAllWorldFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-list-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        await store.AppendMessageAsync(1, new MessageRecord { Role = "user", Content = "q1" });
        await store.AppendMessageAsync(2, new MessageRecord { Role = "user", Content = "q2" });

        var list = await store.ListConversationsAsync();

        list.Should().HaveCount(2);
        list.Should().Contain(m => m.WorldId == 1 && m.Count == 1);
        list.Should().Contain(m => m.WorldId == 2 && m.Count == 1);
    }

    [Fact]
    public async Task RenameConversationAsyncUpdatesNameOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-rename-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        await store.AppendMessageAsync(1, new MessageRecord { Role = "user", Content = "q" });

        await store.RenameConversationAsync(1, "剑冢攻坚档");

        var list = await store.ListConversationsAsync();
        list[0].Name.Should().Be("剑冢攻坚档");
        // 对话流不受影响
        var msgs = await store.LoadRecentAsync(1, 10);
        msgs.Should().HaveCount(1);
        msgs[0].Content.Should().Be("q");
    }

    [Fact]
    public async Task RenameConversationAsyncEmptyNameClearsName()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-clear-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        await store.AppendMessageAsync(1, new MessageRecord { Role = "user", Content = "q" });
        await store.RenameConversationAsync(1, "临时名");
        await store.RenameConversationAsync(1, "");

        var list = await store.ListConversationsAsync();
        list[0].Name.Should().Be("");
    }

    [Fact]
    public async Task LoadForAgentAsync_NoBoundary_ReturnsNullSummaryAndAllMessages()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-agent-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        await store.AppendMessageAsync(1, new MessageRecord { Role = "user", Content = "q1" });
        await store.AppendMessageAsync(1, new MessageRecord { Role = "assistant", Content = "a1" });

        var (oldSummary, newMessages) = await store.LoadForAgentAsync(1);

        oldSummary.Should().BeNull();
        newMessages.Should().HaveCount(2);
        newMessages[0].Content.Should().Be("q1");
        newMessages[1].Content.Should().Be("a1");
    }

    [Fact]
    public async Task LoadForAgentAsync_WithBoundary_ReturnsSummaryAndMessagesAfterBoundary()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-agent-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        await store.AppendMessageAsync(1, new MessageRecord { Role = "user", Content = "old q" });
        await store.AppendBoundaryAsync(1, "旧摘要内容");
        await store.AppendMessageAsync(1, new MessageRecord { Role = "user", Content = "new q" });
        await store.AppendMessageAsync(1, new MessageRecord { Role = "assistant", Content = "new a" });

        var (oldSummary, newMessages) = await store.LoadForAgentAsync(1);

        oldSummary.Should().Be("旧摘要内容");
        newMessages.Should().HaveCount(2);
        newMessages[0].Content.Should().Be("new q");
        newMessages[1].Content.Should().Be("new a");
    }

    [Fact]
    public async Task LoadForAgentAsync_MultipleBoundaries_ReturnsLastSummary()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-agent-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        await store.AppendBoundaryAsync(1, "summary_v1");
        await store.AppendMessageAsync(1, new MessageRecord { Role = "user", Content = "q" });
        await store.AppendBoundaryAsync(1, "summary_v2");

        var (oldSummary, newMessages) = await store.LoadForAgentAsync(1);

        oldSummary.Should().Be("summary_v2");
        newMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task AppendBoundaryAsync_AppendsSystemMessageWithFlag()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-agent-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        await store.AppendMessageAsync(1, new MessageRecord { Role = "user", Content = "q" });

        await store.AppendBoundaryAsync(1, "摘要");

        var all = await store.LoadRecentAsync(1, int.MaxValue);
        all.Should().HaveCount(2);
        all[1].Role.Should().Be("system");
        all[1].IsCompactBoundary.Should().BeTrue();
        all[1].BoundarySummary.Should().Be("摘要");
        all[1].Content.Should().Contain("摘要");
    }
}
