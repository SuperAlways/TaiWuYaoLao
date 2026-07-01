using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Soul;
using TaiwuEncyclopedia.Core.Storage;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Storage;

public class JsonSoulStoreTest
{
    [Fact]
    public async Task ProfileRoundtrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-test-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSoulStore(root);

        var profile = new SoulProfile { Summary = "玩家偏好剑系" };
        await store.SaveProfileAsync(profile);

        var loaded = await store.LoadProfileAsync();
        loaded.Summary.Should().Be("玩家偏好剑系");
    }

    [Fact]
    public async Task WorldPerWorldIdIsolated()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-test-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSoulStore(root);

        await store.SaveWorldAsync(1, new SoulWorld { Summary = "world1 soul" });
        await store.SaveWorldAsync(2, new SoulWorld { Summary = "world2 soul" });

        var w1 = await store.LoadWorldAsync(1);
        var w2 = await store.LoadWorldAsync(2);
        w1.Summary.Should().Be("world1 soul");
        w2.Summary.Should().Be("world2 soul");
    }

    [Fact]
    public async Task CorruptProfileReturnsEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-test-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Soul"));
        await File.WriteAllTextAsync(Path.Combine(root, "Soul", "Profile.json"), "{BROKEN");

        var store = new JsonSoulStore(root);
        var profile = await store.LoadProfileAsync();
        profile.Should().NotBeNull();
        profile.Summary.Should().BeEmpty();
    }

    [Fact]
    public async Task MissingWorldReturnsEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-test-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSoulStore(root);
        var world = await store.LoadWorldAsync(99);
        world.Should().NotBeNull();
        world.Summary.Should().BeEmpty();
    }
}
