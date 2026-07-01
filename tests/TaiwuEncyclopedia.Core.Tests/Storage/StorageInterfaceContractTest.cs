using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Storage;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Storage;

/// <summary>
/// 验证三个存储接口的方法签名正确（能被 stub 实现并调用）。
/// 真实实现的 CRUD/损坏恢复测试在 Task 3。
/// </summary>
public class StorageInterfaceContractTest
{
    [Fact]
    public async Task SessionStoreStubCanAppendAndLoad()
    {
        ISessionStore store = new StubSessionStore();
        await store.AppendMessageAsync(1, new Core.Session.MessageRecord { Role = "user", Content = "hi" });
        var messages = await store.LoadRecentAsync(1, 10);
        messages.Should().HaveCount(1);
        messages[0].Content.Should().Be("hi");
    }

    [Fact]
    public async Task SoulStoreStubCanLoadEmptyProfile()
    {
        ISoulStore store = new StubSoulStore();
        var profile = await store.LoadProfileAsync();
        profile.Should().NotBeNull();
        profile.Summary.Should().BeEmpty();
    }

    [Fact]
    public async Task KeyValueStoreStubCanSetAndGet()
    {
        IKeyValueStore store = new StubKeyValueStore();
        await store.SetAsync("k", "v");
        var value = await store.GetAsync("k");
        value.Should().Be("v");
    }

    private sealed class StubSessionStore : ISessionStore
    {
        private readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<Core.Session.MessageRecord>> _storage = new();

        public System.Threading.Tasks.Task AppendMessageAsync(int worldId, Core.Session.MessageRecord message)
        {
            if (!_storage.TryGetValue(worldId, out var list))
            {
                list = new System.Collections.Generic.List<Core.Session.MessageRecord>();
                _storage[worldId] = list;
            }
            list.Add(message);
            return System.Threading.Tasks.Task.CompletedTask;
        }

        public System.Threading.Tasks.Task<System.Collections.Generic.List<Core.Session.MessageRecord>> LoadRecentAsync(int worldId, int limit)
        {
            if (!_storage.TryGetValue(worldId, out var list))
                return System.Threading.Tasks.Task.FromResult(new System.Collections.Generic.List<Core.Session.MessageRecord>());
            if (list.Count <= limit)
                return System.Threading.Tasks.Task.FromResult(list);
            return System.Threading.Tasks.Task.FromResult(list.GetRange(list.Count - limit, limit));
        }

        public System.Threading.Tasks.Task ClearAsync(int worldId)
        {
            _storage.Remove(worldId);
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }

    private sealed class StubSoulStore : ISoulStore
    {
        public System.Threading.Tasks.Task<Core.Soul.SoulProfile> LoadProfileAsync()
            => System.Threading.Tasks.Task.FromResult(new Core.Soul.SoulProfile());
        public System.Threading.Tasks.Task SaveProfileAsync(Core.Soul.SoulProfile profile) => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task<Core.Soul.SoulWorld> LoadWorldAsync(int worldId)
            => System.Threading.Tasks.Task.FromResult(new Core.Soul.SoulWorld());
        public System.Threading.Tasks.Task SaveWorldAsync(int worldId, Core.Soul.SoulWorld world) => System.Threading.Tasks.Task.CompletedTask;
    }

    private sealed class StubKeyValueStore : IKeyValueStore
    {
        private readonly System.Collections.Generic.Dictionary<string, string> _storage = new();

        public System.Threading.Tasks.Task<string?> GetAsync(string key)
        {
            _storage.TryGetValue(key, out var value);
            return System.Threading.Tasks.Task.FromResult(value);
        }

        public System.Threading.Tasks.Task SetAsync(string key, string value)
        {
            _storage[key] = value;
            return System.Threading.Tasks.Task.CompletedTask;
        }

        public System.Threading.Tasks.Task DeleteAsync(string key)
        {
            _storage.Remove(key);
            return System.Threading.Tasks.Task.CompletedTask;
        }

        public System.Threading.Tasks.Task<System.Collections.Generic.Dictionary<string, string>> GetAllAsync()
            => System.Threading.Tasks.Task.FromResult(new System.Collections.Generic.Dictionary<string, string>(_storage));
    }
}
