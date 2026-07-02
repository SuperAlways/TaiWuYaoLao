using System.IO;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Agent;
using TaiwuEncyclopedia.Core.Skills;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Agent;

/// <summary>
/// PromptBuilder 测试。
/// </summary>
public class PromptBuilderTest
{
    private static SkillManager MakeSm()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-pb-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "registry.yaml"), @"
background:
  - id: taiwu-wiki-zhan-dou
    cn_name: 战斗
    overview_file: background/zhan-dou/overview.md
    detail_dir: background/zhan-dou/detail
personas:
  - id: ring-elder
    cn_name: 戒指老爷爷
    file: personas/ring-elder.md
");
        Directory.CreateDirectory(Path.Combine(dir, "background", "zhan-dou", "detail"));
        File.WriteAllText(Path.Combine(dir, "background", "zhan-dou", "overview.md"), "# 战斗\n战斗系统概述");
        Directory.CreateDirectory(Path.Combine(dir, "personas"));
        File.WriteAllText(Path.Combine(dir, "personas", "ring-elder.md"), "# 戒指老爷爷\n你是隐士残魂");
        return new SkillManager(dir);
    }

    /// <summary>
    /// 构建的 prompt 包含三个部分。
    /// </summary>
    [Fact]
    public void BuildSystemPromptContainsThreeSections()
    {
        var pb = new PromptBuilder(MakeSm(), "ring-elder");
        var prompt = pb.BuildSystemPrompt();
        prompt.Should().Contain("百晓册总纲");
        prompt.Should().Contain("戒指老爷爷");
        prompt.Should().Contain("工具使用规范");
    }

    /// <summary>
    /// 多次调用返回缓存的结果。
    /// </summary>
    [Fact]
    public void BuildSystemPromptCachesResult()
    {
        var pb = new PromptBuilder(MakeSm(), "ring-elder");
        var p1 = pb.BuildSystemPrompt();
        var p2 = pb.BuildSystemPrompt();
        p1.Should().BeSameAs(p2); // 同一引用 = 缓存生效
    }

    /// <summary>
    /// 构建的 prompt 包含 overview.md 文件内容和 lookup_concept 工具。
    /// </summary>
    [Fact]
    public void BuildSystemPromptIncludesOverviewFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-pb-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "registry.yaml"), @"background:
  - id: zhan-dou
    cn_name: 战斗
    overview_file: background/zhan-dou/overview.md
    detail_dir: background/zhan-dou/detail
personas:
  - id: ring-elder
    cn_name: 戒指老爷爷
    file: personas/ring-elder.md
");
        Directory.CreateDirectory(Path.Combine(dir, "background", "zhan-dou", "detail"));
        File.WriteAllText(Path.Combine(dir, "background", "zhan-dou", "overview.md"), @"# 战斗
章节概述");
        // 总纲独立文件
        File.WriteAllText(Path.Combine(dir, "background", "overview.md"), @"# 百晓册总纲
全书综述");
        Directory.CreateDirectory(Path.Combine(dir, "personas"));
        File.WriteAllText(Path.Combine(dir, "personas", "ring-elder.md"), @"# 戒指老爷爷");

        var sm = new SkillManager(dir);
        var pb = new PromptBuilder(sm);
        var prompt = pb.BuildSystemPrompt();
        prompt.Should().Contain("全书综述");
        prompt.Should().Contain("lookup_concept");
    }
}
