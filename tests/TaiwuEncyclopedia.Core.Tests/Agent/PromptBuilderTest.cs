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
        File.WriteAllText(Path.Combine(dir, "background", "overview.md"), "# 百晓册总纲\n全书综述");
        Directory.CreateDirectory(Path.Combine(dir, "personas"));
        File.WriteAllText(Path.Combine(dir, "personas", "sword-will.md"), "# 剑中虚影\n你是天道残识");
        File.WriteAllText(Path.Combine(dir, "answer-rules.md"), @"# 通用回答规则

## 信息处理
规则内容

## 玩家保护
保护内容

## RAG 检索策略
检索内容
");
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

    /// <summary>
    /// system prompt 包含 complete_retrieval 工具引导指令。
    /// </summary>
    [Fact]
    public void BuildSystemPrompt_ContainsCompleteRetrievalGuidance()
    {
        var sm = MakeSm();
        var pb = new PromptBuilder(sm, "sword-will");
        var prompt = pb.BuildSystemPrompt();
        prompt.Should().Contain("complete_retrieval");
        prompt.Should().Contain("5 个工具");
        prompt.Should().NotContain("不要在此阶段直接给出最终回答");
    }

    // === BuildThinkPrompt / BuildFinalPrompt 新方法测试 ===

    [Fact]
    public void BuildThinkPrompt_ContainsToolsAndKnowledge_ButNotPersona()
    {
        var sm = MakeSm();
        var pb = new PromptBuilder(sm, "sword-will");
        var prompt = pb.BuildThinkPrompt();
        // 应包含：工具规范、百晓册、检索策略、complete_retrieval 引导
        prompt.Should().Contain("工具使用规范");
        prompt.Should().Contain("百晓册阅读策略");
        prompt.Should().Contain("百晓册总纲");
        prompt.Should().Contain("检索助手");
        prompt.Should().Contain("complete_retrieval");
        prompt.Should().Contain("完成检索");
        // 不应包含：persona、回答规则、回答格式、旧的强制约束
        prompt.Should().NotContain("剑中虚影");
        prompt.Should().NotContain("规则内容");
        prompt.Should().NotContain("格式内容");
        prompt.Should().NotContain("检索完毕");
        prompt.Should().NotContain("不要在此阶段直接给出最终回答");
    }

    [Fact]
    public void BuildThinkPrompt_ContainsRagStrategy_ButNotAnswerRules()
    {
        var sm = MakeSm();
        var pb = new PromptBuilder(sm, "sword-will");
        var prompt = pb.BuildThinkPrompt();
        // RAG 检索策略（answer-rules.md 的第三段）应在 think prompt 中
        prompt.Should().Contain("RAG 检索策略");
        // 但信息处理、玩家保护不应在 think prompt 中
        prompt.Should().NotContain("信息处理");
        prompt.Should().NotContain("玩家保护");
    }

    [Fact]
    public void BuildFinalPrompt_ContainsPersonaAndRules_ButNotTools()
    {
        var sm = MakeSm();
        var pb = new PromptBuilder(sm, "sword-will");
        var prompt = pb.BuildFinalPrompt();
        // 应包含：persona、信息处理、玩家保护、回答格式
        prompt.Should().Contain("剑中虚影");
        prompt.Should().Contain("规则内容");
        prompt.Should().Contain("格式内容");
        // 不应包含：工具规范、百晓册
        prompt.Should().NotContain("工具使用规范");
        prompt.Should().NotContain("百晓册总纲");
        prompt.Should().NotContain("百晓册阅读策略");
    }

    [Fact]
    public void BuildFinalPrompt_CachesByPersonaId()
    {
        var pb = new PromptBuilder(MakeSm(), "sword-will");
        var p1 = pb.BuildFinalPrompt("sword-will");
        var p2 = pb.BuildFinalPrompt("sword-will");
        p1.Should().BeSameAs(p2);
    }

    [Fact]
    public void BuildThinkPrompt_ShouldNotCache_BecauseStatic()
    {
        var pb = new PromptBuilder(MakeSm(), "sword-will");
        var p1 = pb.BuildThinkPrompt();
        var p2 = pb.BuildThinkPrompt();
        // Think prompt 内容完全静态，每次返回同一份即可
        p1.Should().Be(p2); // 值相等即可，不要求 BeSameAs
    }
}
