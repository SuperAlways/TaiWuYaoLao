using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Session;
using TaiwuEncyclopedia.Core.Storage;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Session;

public class SessionManagerTest
{
    [Fact]
    public async Task SaveAndLoadRoundtrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-sess-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        var sm = new SessionManager(store);

        await sm.SaveConversationAsync(worldId: 1, userQuery: "怎么打剑冢", assistantAnswer: "先准备...");
        var history = await sm.LoadHistoryAsync(worldId: 1, limit: 10);

        history.Should().HaveCount(2);
        history[0].Role.Should().Be("user");
        history[0].Content.Should().Be("怎么打剑冢");
        history[1].Role.Should().Be("assistant");
        history[1].Content.Should().Be("先准备...");
    }

    [Fact]
    public async Task LoadHistoryEmptyReturnsEmptyList()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-sess-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        var sm = new SessionManager(store);

        var history = await sm.LoadHistoryAsync(worldId: 99, limit: 10);
        history.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleConversationsAppend()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-sess-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        var sm = new SessionManager(store);

        await sm.SaveConversationAsync(1, "q1", "a1");
        await sm.SaveConversationAsync(1, "q2", "a2");

        var history = await sm.LoadHistoryAsync(1, limit: 10);
        history.Should().HaveCount(4); // 2 rounds = 4 messages
        history[0].Content.Should().Be("q1");
        history[3].Content.Should().Be("a2");
    }

    [Fact]
    public async Task DifferentWorldIdIsolated()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-sess-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        var sm = new SessionManager(store);

        await sm.SaveConversationAsync(1, "w1q", "w1a");
        await sm.SaveConversationAsync(2, "w2q", "w2a");

        var h1 = await sm.LoadHistoryAsync(1, 10);
        var h2 = await sm.LoadHistoryAsync(2, 10);
        h1.Should().HaveCount(2);
        h1[0].Content.Should().Be("w1q");
        h2.Should().HaveCount(2);
        h2[0].Content.Should().Be("w2q");
    }

    [Fact]
    public async Task LoadHistoryRespectsLimit()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-sess-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        var sm = new SessionManager(store);

        for (int i = 0; i < 5; i++)
        {
            await sm.SaveConversationAsync(1, $"q{i}", $"a{i}");
        }

        var history = await sm.LoadHistoryAsync(1, limit: 4); // 最近 4 条
        history.Should().HaveCount(4);
        history[0].Content.Should().Be("q3"); // 最早的是 q3（q0-q2 被截掉）
    }
}