using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Storage;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Storage;

public class AtomicFileTest
{
    private static string NewPath() =>
        Path.Combine(Path.GetTempPath(), "yaolao-atomic-" + System.Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public async Task WriteAndReadRoundtrip()
    {
        var path = NewPath();
        var data = new TestData { Name = "产业", Count = 10 };
        await AtomicFile.WriteJsonAsync(path, data);

        var read = await AtomicFile.ReadJsonAsync<TestData>(path);
        read!.Name.Should().Be("产业");
        read.Count.Should().Be(10);
    }

    [Fact]
    public async Task OverwriteReplacesContent()
    {
        var path = NewPath();
        await AtomicFile.WriteJsonAsync(path, new TestData { Name = "v1", Count = 1 });
        await AtomicFile.WriteJsonAsync(path, new TestData { Name = "v2", Count = 2 });

        var read = await AtomicFile.ReadJsonAsync<TestData>(path);
        read!.Name.Should().Be("v2");
        read.Count.Should().Be(2);
    }

    [Fact]
    public async Task OverwriteLeavesNoTmpFile()
    {
        var path = NewPath();
        await AtomicFile.WriteJsonAsync(path, new TestData { Name = "v1", Count = 1 });
        await AtomicFile.WriteJsonAsync(path, new TestData { Name = "v2", Count = 2 });

        File.Exists(path + ".tmp").Should().BeFalse();
    }

    [Fact]
    public async Task OverwritePreservesFileCreationTime()
    {
        // File.Replace 保留目标文件 CreationTime；Delete+Move 会让 CreationTime 变成 tmp 的创建时间。
        // 这是 File.Replace 与 Delete+Move 唯一可单元观测的差异（原子性窗口本身无法模拟崩溃测试）。
        // 假设 NTFS（本机 Windows + Unity 目标一致）。
        var path = NewPath();
        await AtomicFile.WriteJsonAsync(path, new TestData { Name = "v1", Count = 1 });
        var originalCreation = File.GetCreationTime(path);
        await Task.Delay(500);
        await AtomicFile.WriteJsonAsync(path, new TestData { Name = "v2", Count = 2 });

        File.GetCreationTime(path).Should().Be(originalCreation);
    }

    private sealed class TestData
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }
}
