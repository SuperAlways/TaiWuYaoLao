using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Soul;
using TaiwuEncyclopedia.Core.Storage;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Soul;

public class SoulManagerTest
{
    [Fact]
    public async Task GetSoulSummaryEmptyReturnsEmptyString()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-soul-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSoulStore(root);
        var sm = new SoulManager(store);

        var summary = await sm.GetSoulSummaryAsync(1);
        summary.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSoulSummaryWithDataReturnsFormatted()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-soul-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSoulStore(root);
        await store.SaveProfileAsync(new SoulProfile { Playstyle = "苟道流", TechnicalLevel = "老手" });
        await store.SaveWorldAsync(1, new SoulWorld { Sect = "少林", Stage = "中期" });

        var sm = new SoulManager(store);
        var summary = await sm.GetSoulSummaryAsync(1);
        summary.Should().Contain("玩法偏好: 苟道流");
        summary.Should().Contain("门派: 少林");
    }

    [Fact]
    public async Task SetPlayerFieldsMarksAsProtected()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-soul-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSoulStore(root);
        var sm = new SoulManager(store);

        await sm.SetPlayerFieldsAsync(new Dictionary<string, string> { ["Playstyle"] = "速通" });

        var profile = await store.LoadProfileAsync();
        profile.Playstyle.Should().Be("速通");
        profile.ProtectedFields.Should().Contain("Playstyle");
    }

    [Fact]
    public async Task UpdateFromCompressLlmFailureReturnsEmptySummary()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-soul-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSoulStore(root);
        var sm = new SoulManager(store);
        // 用 null llmClient 模拟失败（实际调用会 NPE → catch → 返回空）
        var llmClient = new OpenAiCompatibleClient(new FailingHandler());
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        var summary = await sm.UpdateFromCompressAsync(1, "some history", llmClient, config);
        summary.Should().BeEmpty();
    }

    private sealed class FailingHandler : System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken ct)
            => throw new System.Net.Http.HttpRequestException("connection failed");
    }
}
