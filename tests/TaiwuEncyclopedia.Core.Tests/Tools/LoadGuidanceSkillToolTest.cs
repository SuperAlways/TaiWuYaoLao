using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Skills;
using TaiwuEncyclopedia.Core.Tools;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Tools;

public class LoadGuidanceSkillToolTest
{
    private static SkillManager MakeSm()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-gs-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "registry.yaml"), @"
guidance:
  - id: combat-build
    cn_name: 战斗 build 指引
    file: guidance/combat-build.md
    relevant_chapters: []
");
        Directory.CreateDirectory(Path.Combine(dir, "guidance"));
        File.WriteAllText(Path.Combine(dir, "guidance", "combat-build.md"), "# 战斗 build\n引导内容");
        return new SkillManager(dir);
    }

    [Fact]
    public async Task LoadReturnsSkillContent()
    {
        var tool = new LoadGuidanceSkillTool(MakeSm());
        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["skill"] = "combat-build",
        });
        result["content"].ToString().Should().Contain("引导内容");
    }

    [Fact]
    public async Task MissingSkillReturnsError()
    {
        var tool = new LoadGuidanceSkillTool(MakeSm());
        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["skill"] = "nonexistent",
        });
        result.Should().ContainKey("error");
    }
}
