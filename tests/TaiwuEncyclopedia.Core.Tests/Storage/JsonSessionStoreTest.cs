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
    public async Task Append_And_Load_Roundtrip()
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
    public async Task Load_Recent_Respects_Limit()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-test-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);

        for (int i = 0; i < 5; i++)
        {
            await store.AppendMessageAsync(1, new MessageRecord { Role = "user", Content = i.ToString() });
        }

        var messages = await store.LoadRecentAsync(1, 3);
        messages.Should().HaveCount(3);
        messages[0].Content.Should().Be("2");
        messages[2].Content.Should().Be("4");
    }

    [Fact]
    public async Task Different_WorldId_Isolated()
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
    public async Task Corrupt_File_Returns_Empty()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-test-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Sessions", "Worlds"));
        await File.WriteAllTextAsync(Path.Combine(root, "Sessions", "Worlds", "world-1.json"), "{INVALID JSON");

        var store = new JsonSessionStore(root);
        var messages = await store.LoadRecentAsync(1, 10);
        messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Clear_Removes_Messages()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-test-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSessionStore(root);
        await store.AppendMessageAsync(1, new MessageRecord { Role = "user", Content = "x" });

        await store.ClearAsync(1);

        var messages = await store.LoadRecentAsync(1, 10);
        messages.Should().BeEmpty();
    }
}
