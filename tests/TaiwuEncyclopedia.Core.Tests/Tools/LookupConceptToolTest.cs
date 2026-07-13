using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Skills;
using TaiwuEncyclopedia.Core.Tools;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Tools;

public class LookupConceptToolTest
{
    private static SkillManager MakeSm()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-lc-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "concept_index.json"), @"{
  ""易筋经"": { ""path"": ""glossary/功法/易筋经.md"", ""type"": ""glossary"" },
  ""门派支持"": { ""path"": ""background/menpai/detail/门派-门派概述-门派支持.md"", ""type"": ""section"" }
}");
        Directory.CreateDirectory(Path.Combine(dir, "glossary", "功法"));
        File.WriteAllText(Path.Combine(dir, "glossary", "功法", "易筋经.md"), "# 易筋经\n一品内功");
        Directory.CreateDirectory(Path.Combine(dir, "background", "menpai", "detail"));
        File.WriteAllText(Path.Combine(dir, "background", "menpai", "detail", "门派-门派概述-门派支持.md"), "# 门派支持\n支持度");
        return new SkillManager(dir);
    }

    [Fact]
    public async Task ExecuteReturnsGlossaryContent()
    {
        var tool = new LookupConceptTool(MakeSm());
        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["name"] = "易筋经",
        });
        result["name"].ToString().Should().Be("易筋经");
        result["content"].ToString().Should().Contain("一品内功");
    }

    [Fact]
    public async Task ExecuteReturnsSectionContent()
    {
        var tool = new LookupConceptTool(MakeSm());
        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["name"] = "门派支持",
        });
        result["content"].ToString().Should().Contain("支持度");
    }

    [Fact]
    public async Task ExecuteReturnsErrorForMissingConcept()
    {
        var tool = new LookupConceptTool(MakeSm());
        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["name"] = "不存在的概念",
        });
        result.Should().ContainKey("error");
    }
}