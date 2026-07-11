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

    [Fact]
    public async Task SaveConversationAsyncPersistsReferences()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-sess-refs-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        var sm = new SessionManager(store);

        var refs = new List<TaiwuEncyclopedia.Core.Rag.Reference>
        {
            new() { FullDocId = "doc-A", SourceUrl = "https://wiki.example.com/a", HitCount = 3 },
            new() { FullDocId = "doc-B", SourceUrl = "https://bbs.example.com/b", HitCount = 1 },
        };

        await sm.SaveConversationAsync(1, "问题", "答案", refs);
        var records = await store.LoadRecentAsync(1, 10);

        records.Should().HaveCount(2);
        records[0].Role.Should().Be("user");
        records[0].References.Should().BeNull(); // user 消息不带 references
        records[1].Role.Should().Be("assistant");
        records[1].References.Should().NotBeNull();
        records[1].References!.Should().HaveCount(2);
        records[1].References![0].SourceUrl.Should().Be("https://wiki.example.com/a");
        records[1].References![0].HitCount.Should().Be(3);
    }

    [Fact]
    public async Task SaveConversationAsyncStoresAutoNameOnFirstCall()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-auto-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        var sm = new SessionManager(store);

        await sm.SaveConversationAsync(1, "q", "a", autoName: "太吾·李");

        var list = await sm.ListConversationsAsync();
        list.Should().HaveCount(1);
        list[0].AutoName.Should().Be("太吾·李");
    }

    [Fact]
    public async Task SaveConversationAsyncDoesNotOverwriteAutoName()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-auto2-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        var sm = new SessionManager(store);

        await sm.SaveConversationAsync(1, "q1", "a1", autoName: "太吾·李");
        await sm.SaveConversationAsync(1, "q2", "a2", autoName: "太吾·王");

        var list = await sm.ListConversationsAsync();
        list[0].AutoName.Should().Be("太吾·李");
    }

    [Fact]
    public async Task RenameConversationAsyncUpdatesName()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-rename2-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        var sm = new SessionManager(store);
        await sm.SaveConversationAsync(1, "q", "a", autoName: "太吾·李");

        await sm.RenameConversationAsync(1, "我的剑冢档");

        var list = await sm.ListConversationsAsync();
        list[0].Name.Should().Be("我的剑冢档");
        list[0].AutoName.Should().Be("太吾·李");
    }

    [Fact]
    public void PregameWorldIdConstantIsMinusOne()
    {
        SessionManager.PregameWorldId.Should().Be(-1);
    }

    [Fact]
    public async Task SaveConversationPregameWorldIdStoresMainInterfaceAutoName()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-pregame-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        var sm = new SessionManager(store);

        await sm.SaveConversationAsync(
            SessionManager.PregameWorldId, "剑冢选哪个门派", "少林前期稳...",
            autoName: "主界面对话");

        var list = await sm.ListConversationsAsync();
        list.Should().Contain(m => m.WorldId == SessionManager.PregameWorldId);
        var pregame = list.Find(m => m.WorldId == SessionManager.PregameWorldId)!;
        pregame.AutoName.Should().Be("主界面对话");
        pregame.Count.Should().Be(2); // user + assistant = 2 messages
    }

    [Fact]
    public async Task LoadForAgentAsync_PassesThroughToStore()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-sm-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        var sm = new SessionManager(store);
        await sm.SaveConversationAsync(1, "q", "a");
        await sm.AppendBoundaryAsync(1, "摘要");
        // Add messages after boundary
        await sm.SaveConversationAsync(1, "new q", "new a");

        var (oldSummary, newMessages) = await sm.LoadForAgentAsync(1);

        oldSummary.Should().Be("摘要");
        newMessages.Should().HaveCount(2); // user + assistant saved after boundary
    }

    [Fact]
    public async Task LoadForAgentAsMessagesAsync_ConvertsRecordsToLlmMessages()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-sm2-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        var sm = new SessionManager(store);
        await sm.AppendBoundaryAsync(1, "摘要");
        await sm.SaveConversationAsync(1, "用户问题", "AI回答");

        var (oldSummary, messages) = await sm.LoadForAgentAsMessagesAsync(1);

        oldSummary.Should().Be("摘要");
        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be("user");
        messages[0].Content.Should().Be("用户问题");
        messages[1].Role.Should().Be("assistant");
        messages[1].Content.Should().Be("AI回答");
    }

    [Fact]
    public async Task LoadForAgentAsMessagesAsync_FiltersSystemMessages()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-sm2-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        var sm = new SessionManager(store);
        // 直接写一条 system 消息（模拟异常数据）
        await store.AppendMessageAsync(1, new MessageRecord { Role = "system", Content = "系统消息" });
        await store.AppendMessageAsync(1, new MessageRecord { Role = "user", Content = "用户消息" });

        var (oldSummary, messages) = await sm.LoadForAgentAsMessagesAsync(1);

        messages.Should().HaveCount(1);
        messages[0].Role.Should().Be("user");
    }
}