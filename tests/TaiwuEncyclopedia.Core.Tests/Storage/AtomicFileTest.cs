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
        // 特征测试：File.Replace 保留目标文件 CreationTime。
        // 注意：NTFS 隧道效应会让 Delete+Move 也保留原 CreationTime（同目录同名 15s 内重建），
        // 所以本测试在 NTFS 上无法区分新旧实现——它是特征锁定（锁定 CreationTime 被保留这一属性），
        // 而非 RED 区分测试。File.Replace 的原子性（单次 OS 操作 vs Delete+Move 两步窗口）靠代码审查保证，
        // 无法用单测模拟崩溃。
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
