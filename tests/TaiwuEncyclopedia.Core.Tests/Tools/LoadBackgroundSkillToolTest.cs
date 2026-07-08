using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Skills;
using TaiwuEncyclopedia.Core.Tools;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Tools;

public class LoadBackgroundSkillToolTest
{
    private static SkillManager MakeSm()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-bg-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "registry.yaml"), @"
background:
  - id: 战斗
    overview_file: background/战斗/战斗概述.md
    detail_dir: background/战斗/detail
");
        Directory.CreateDirectory(Path.Combine(dir, "background", "战斗", "detail"));
        File.WriteAllText(Path.Combine(dir, "background", "战斗", "战斗概述.md"), "# 战斗\n概述");
        File.WriteAllText(Path.Combine(dir, "background", "战斗", "detail", "gong-fa.md"), "功法详尽");
        return new SkillManager(dir);
    }

    [Fact]
    public async Task OverviewReturnsSummaryContent()
    {
        var tool = new LoadBackgroundSkillTool(MakeSm());
        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["chapter"] = "战斗",
            ["depth"] = "overview",
        });
        result["content"].ToString().Should().Contain("概述");
    }

    [Fact]
    public async Task DetailWithSectionReturnsSpecificFile()
    {
        var tool = new LoadBackgroundSkillTool(MakeSm());
        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["chapter"] = "战斗",
            ["depth"] = "detail",
            ["section"] = "gong-fa",
        });
        result["content"].ToString().Should().Contain("功法详尽");
    }

    [Fact]
    public async Task MissingChapterReturnsError()
    {
        var tool = new LoadBackgroundSkillTool(MakeSm());
        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["chapter"] = "nonexistent",
        });
        result.Should().ContainKey("error");
    }
}
