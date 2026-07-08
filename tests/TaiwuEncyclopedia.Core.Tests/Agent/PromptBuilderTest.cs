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
answer_rules_file: answer-rules.md
output_style_file: output-style.md
background:
  - id: 战斗
    overview_file: background/战斗/战斗概述.md
    detail_dir: background/战斗/detail
personas:
  - id: sword-will
    cn_name: 剑中虚影
    file: personas/sword-will.md
");
        Directory.CreateDirectory(Path.Combine(dir, "background", "战斗", "detail"));
        File.WriteAllText(Path.Combine(dir, "background", "战斗", "战斗概述.md"), "# 战斗\n战斗系统概述");
        Directory.CreateDirectory(Path.Combine(dir, "personas"));
        File.WriteAllText(Path.Combine(dir, "personas", "sword-will.md"), "# 剑中虚影\n你是天道残识");
        File.WriteAllText(Path.Combine(dir, "answer-rules.md"), "# 通用回答规则\n规则内容");
        File.WriteAllText(Path.Combine(dir, "output-style.md"), "# 回答格式\n格式内容");
        return new SkillManager(dir);
    }

    /// <summary>
    /// 构建的 prompt 包含五个部分。
    /// </summary>
    [Fact]
    public void BuildSystemPromptContainsFiveSections()
    {
        var pb = new PromptBuilder(MakeSm(), "sword-will");
        var prompt = pb.BuildSystemPrompt();
        prompt.Should().Contain("百晓册总纲");
        prompt.Should().Contain("规则内容");
        prompt.Should().Contain("格式内容");
        prompt.Should().Contain("工具使用规范");
        prompt.Should().Contain("剑中虚影");
    }

    /// <summary>
    /// 多次调用返回缓存的结果。
    /// </summary>
    [Fact]
    public void BuildSystemPromptCachesResult()
    {
        var pb = new PromptBuilder(MakeSm(), "sword-will");
        var p1 = pb.BuildSystemPrompt();
        var p2 = pb.BuildSystemPrompt();
        p1.Should().BeSameAs(p2);
    }

    /// <summary>
    /// 构建的 prompt 包含 overview.md 文件内容和 lookup_concept 工具。
    /// </summary>
    [Fact]
    public void BuildSystemPromptIncludesOverviewFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-pb-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "registry.yaml"), @"
answer_rules_file: answer-rules.md
output_style_file: output-style.md
background:
  - id: 战斗
    overview_file: background/战斗/战斗概述.md
    detail_dir: background/战斗/detail
personas:
  - id: sword-will
    cn_name: 剑中虚影
    file: personas/sword-will.md
");
        Directory.CreateDirectory(Path.Combine(dir, "background", "战斗", "detail"));
        File.WriteAllText(Path.Combine(dir, "background", "战斗", "战斗概述.md"), "# 战斗\n章节概述");
        File.WriteAllText(Path.Combine(dir, "background", "overview.md"), "# 百晓册总纲\n全书综述");
        Directory.CreateDirectory(Path.Combine(dir, "personas"));
        File.WriteAllText(Path.Combine(dir, "personas", "sword-will.md"), "# 剑中虚影");
        File.WriteAllText(Path.Combine(dir, "answer-rules.md"), "# 通用回答规则\n规则内容");
        File.WriteAllText(Path.Combine(dir, "output-style.md"), "# 回答格式\n格式内容");

        var sm = new SkillManager(dir);
        var pb = new PromptBuilder(sm);
        var prompt = pb.BuildSystemPrompt();
        prompt.Should().Contain("全书综述");
        prompt.Should().Contain("lookup_concept");
    }

    /// <summary>
    /// answer-rules 未配置时对应段跳过。
    /// </summary>
    [Fact]
    public void BuildSystemPromptSkipsAnswerRulesWhenNotConfigured()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-pb-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "registry.yaml"), @"
output_style_file: output-style.md
background: []
personas:
  - id: sword-will
    cn_name: 剑中虚影
    file: personas/sword-will.md
");
        Directory.CreateDirectory(Path.Combine(dir, "personas"));
        File.WriteAllText(Path.Combine(dir, "personas", "sword-will.md"), "# 剑中虚影");
        File.WriteAllText(Path.Combine(dir, "output-style.md"), "# 回答格式\n格式内容");

        var sm = new SkillManager(dir);
        var pb = new PromptBuilder(sm, "sword-will");
        var prompt = pb.BuildSystemPrompt();
        prompt.Should().NotContain("规则内容");
        prompt.Should().Contain("格式内容");
    }

    /// <summary>
    /// output-style 未配置时对应段跳过。
    /// </summary>
    [Fact]
    public void BuildSystemPromptSkipsOutputStyleWhenNotConfigured()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-pb-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "registry.yaml"), @"
answer_rules_file: answer-rules.md
background: []
personas:
  - id: sword-will
    cn_name: 剑中虚影
    file: personas/sword-will.md
");
        Directory.CreateDirectory(Path.Combine(dir, "personas"));
        File.WriteAllText(Path.Combine(dir, "personas", "sword-will.md"), "# 剑中虚影");
        File.WriteAllText(Path.Combine(dir, "answer-rules.md"), "# 通用回答规则\n规则内容");

        var sm = new SkillManager(dir);
        var pb = new PromptBuilder(sm, "sword-will");
        var prompt = pb.BuildSystemPrompt();
        prompt.Should().Contain("规则内容");
        prompt.Should().NotContain("格式内容");
    }

    /// <summary>
    /// 不传 personaId 时默认使用 sword-will。
    /// </summary>
    [Fact]
    public void BuildSystemPromptUsesSwordWillAsDefault()
    {
        var pb = new PromptBuilder(MakeSm());
        var prompt = pb.BuildSystemPrompt();
        prompt.Should().Contain("剑中虚影");
    }
}
