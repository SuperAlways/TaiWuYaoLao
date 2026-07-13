using System.Linq;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Llm;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Llm;

public class ApiProviderPresetsTest
{
    [Fact]
    public void All_ContainsNineProvidersPlusCustom()
    {
        var all = ApiProviderPresets.All;
        all.Should().HaveCount(10);
        all[0].Id.Should().Be("deepseek");
        all[8].Id.Should().Be("siliconflow");
        all[9].Id.Should().Be("custom");
        all[9].BaseUrl.Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://api.deepseek.com/v1", "deepseek")]
    [InlineData("https://api.deepseek.com", "deepseek")]
    [InlineData("https://api.openai.com/v1", "openai")]
    [InlineData("https://api.x.ai/v1", "grok")]
    [InlineData("https://generativelanguage.googleapis.com/v1beta/openai", "gemini")]
    [InlineData("https://api.minimaxi.com/v1", "minimax")]
    [InlineData("https://dashscope.aliyuncs.com/compatible-mode/v1", "qwen")]
    [InlineData("https://ark.cn-beijing.volces.com/api/v3", "volcengine")]
    [InlineData("https://api.siliconflow.cn/v1", "siliconflow")]
    public void Match_FindsPresetByNormalizedUrl(string url, string expectedId)
    {
        var preset = ApiProviderPresets.Match(url);
        preset.Should().NotBeNull();
        preset!.Id.Should().Be(expectedId);
    }

    [Fact]
    public void Match_UnknownUrl_ReturnsNull()
    {
        ApiProviderPresets.Match("https://my-custom-api.example.com/v1").Should().BeNull();
    }

    [Fact]
    public void EachPreset_HasNonNullDisplayName()
    {
        foreach (var p in ApiProviderPresets.All)
            p.DisplayName.Should().NotBeNullOrEmpty();
    }
}
