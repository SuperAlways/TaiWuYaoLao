using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Storage;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Storage;

public class JsonKeyValueStoreTest
{
    [Fact]
    public async Task SetGetRoundtrip()
    {
        var path = Path.Combine(Path.GetTempPath(), "yaolao-kv-" + System.Guid.NewGuid().ToString("N") + ".json");
        var store = new JsonKeyValueStore(path);

        await store.SetAsync("k1", "v1");
        await store.SetAsync("k2", "v2");

        (await store.GetAsync("k1")).Should().Be("v1");
        (await store.GetAsync("k2")).Should().Be("v2");
        (await store.GetAsync("missing")).Should().BeNull();
    }

    [Fact]
    public async Task DeleteRemovesKey()
    {
        var path = Path.Combine(Path.GetTempPath(), "yaolao-kv-" + System.Guid.NewGuid().ToString("N") + ".json");
        var store = new JsonKeyValueStore(path);
        await store.SetAsync("k", "v");

        await store.DeleteAsync("k");

        (await store.GetAsync("k")).Should().BeNull();
    }

    [Fact]
    public async Task PersistsAcrossInstances()
    {
        var path = Path.Combine(Path.GetTempPath(), "yaolao-kv-" + System.Guid.NewGuid().ToString("N") + ".json");
        var store1 = new JsonKeyValueStore(path);
        await store1.SetAsync("k", "v");

        var store2 = new JsonKeyValueStore(path);
        (await store2.GetAsync("k")).Should().Be("v");
    }

    [Fact]
    public async Task CorruptFileReturnsEmpty()
    {
        var path = Path.Combine(Path.GetTempPath(), "yaolao-kv-" + System.Guid.NewGuid().ToString("N") + ".json");
        await File.WriteAllTextAsync(path, "{BROKEN");

        var store = new JsonKeyValueStore(path);
        var all = await store.GetAllAsync();
        all.Should().BeEmpty();
    }
}
