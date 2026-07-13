using System.IO;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Skills;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Skills;

public class SkillManagerTest
{
    private static string MakeTempSkillsDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-skills-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "registry.yaml"), @"
answer_rules_file: answer-rules.md
output_style_file: output-style.md
background:
  - id: 战斗
    overview_file: background/战斗/战斗概述.md
    detail_dir: background/战斗/detail
guidance:
  - id: 战斗 build 指引
    file: guidance/战斗-build-指引.md
    relevant_chapters: [战斗]
personas:
  - id: sword-will
    cn_name: 剑中虚影
    file: personas/sword-will.md
");
        Directory.CreateDirectory(Path.Combine(dir, "background", "战斗", "detail"));
        File.WriteAllText(Path.Combine(dir, "background", "战斗", "战斗概述.md"), "# 战斗\n概述内容");
        File.WriteAllText(Path.Combine(dir, "background", "战斗", "detail", "gong-fa.md"), "功法详尽内容");
        Directory.CreateDirectory(Path.Combine(dir, "guidance"));
        File.WriteAllText(Path.Combine(dir, "guidance", "战斗-build-指引.md"), "# 战斗 build\n引导内容");
        Directory.CreateDirectory(Path.Combine(dir, "personas"));
        File.WriteAllText(Path.Combine(dir, "personas", "sword-will.md"), "# 剑中虚影\npersona 内容");
        File.WriteAllText(Path.Combine(dir, "answer-rules.md"), "# 通用回答规则\n规则内容");
        File.WriteAllText(Path.Combine(dir, "output-style.md"), "# 回答格式\n格式内容");
        return dir;
    }

    [Fact]
    public void GetChapterEnumReturnsRegisteredChapters()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        sm.GetChapterEnum().Should().Contain("战斗");
    }

    [Fact]
    public void ChapterCnNameReturnsCnName()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        sm.ChapterCnName("战斗").Should().Be("战斗");
    }

    [Fact]
    public void GuidanceCnNameReturnsCnName()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        sm.GuidanceCnName("战斗 build 指引").Should().Be("战斗 build 指引");
    }

    [Fact]
    public void LoadChapterOverviewReturnsMdContent()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        var content = sm.LoadChapterOverview("战斗");
        content.Should().Contain("概述内容");
    }

    [Fact]
    public void LoadChapterDetailWithSectionReturnsSpecificFile()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        var content = sm.LoadChapterDetail("战斗", "gong-fa");
        content.Should().Contain("功法详尽内容");
    }

    [Fact]
    public void LoadGuidanceSkillReturnsMdContent()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        var content = sm.LoadGuidanceSkill("战斗 build 指引");
        content.Should().Contain("引导内容");
    }

    [Fact]
    public void LoadPersonaReturnsMdContent()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        var content = sm.LoadPersona("sword-will");
        content.Should().Contain("persona 内容");
    }

    [Fact]
    public void PersonaCnNameReturnsCnName()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        sm.PersonaCnName("sword-will").Should().Be("剑中虚影");
    }

    [Fact]
    public void PersonaCnNameReturnsIdForUnregistered()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        sm.PersonaCnName("unknown-id").Should().Be("unknown-id");
    }

    [Fact]
    public void LoadOverviewReturnsTopLevelSurvey()
    {
        var dir = MakeTempSkillsDir();
        File.WriteAllText(Path.Combine(dir, "background", "overview.md"), "# 百晓册总纲\n全书综述内容");
        var sm = new SkillManager(dir);
        var content = sm.LoadOverview();
        content.Should().Contain("全书综述内容");
    }

    [Fact]
    public void LoadOverviewReturnsNullWhenFileMissing()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        var content = sm.LoadOverview();
        content.Should().BeNull();
    }

    [Fact]
    public void LookupConceptReturnsGlossaryContent()
    {
        var dir = MakeTempSkillsDir();
        // 写 concept_index.json
        File.WriteAllText(Path.Combine(dir, "concept_index.json"), @"{
  ""易筋经"": { ""path"": ""glossary/功法/易筋经.md"", ""type"": ""glossary"" }
}");
        // 写对应的词条文件
        Directory.CreateDirectory(Path.Combine(dir, "glossary", "功法"));
        File.WriteAllText(Path.Combine(dir, "glossary", "功法", "易筋经.md"), "# 易筋经\n一品内功");
        var sm = new SkillManager(dir);
        var content = sm.LookupConcept("易筋经");
        content.Should().Contain("一品内功");
    }

    [Fact]
    public void LookupConceptReturnsSectionContent()
    {
        var dir = MakeTempSkillsDir();
        File.WriteAllText(Path.Combine(dir, "concept_index.json"), @"{
  ""门派支持"": { ""path"": ""background/menpai/detail/门派-门派概述-门派支持.md"", ""type"": ""section"" }
}");
        Directory.CreateDirectory(Path.Combine(dir, "background", "menpai", "detail"));
        File.WriteAllText(Path.Combine(dir, "background", "menpai", "detail", "门派-门派概述-门派支持.md"), "# 门派支持\n支持度效果");
        var sm = new SkillManager(dir);
        var content = sm.LookupConcept("门派支持");
        content.Should().Contain("支持度效果");
    }

    [Fact]
    public void LookupConceptReturnsNullForMissingConcept()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        var content = sm.LookupConcept("不存在的概念");
        content.Should().BeNull();
    }

    [Fact]
    public void LookupConceptReturnsNullWhenIndexMissing()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        // concept_index.json 不存在
        var content = sm.LookupConcept("易筋经");
        content.Should().BeNull();
    }

    [Fact]
    public void LoadAnswerRulesReturnsMdContent()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        var content = sm.LoadAnswerRules();
        content.Should().Contain("规则内容");
    }

    [Fact]
    public void LoadAnswerRulesReturnsNullWhenFileMissing()
    {
        var dir = MakeTempSkillsDir();
        File.Delete(Path.Combine(dir, "answer-rules.md"));
        var sm = new SkillManager(dir);
        sm.LoadAnswerRules().Should().BeNull();
    }

    [Fact]
    public void LoadAnswerRulesReturnsNullWhenNotConfigured()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-no-rules-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "registry.yaml"), @"
background: []
personas: []
");
        var sm = new SkillManager(dir);
        sm.LoadAnswerRules().Should().BeNull();
    }

    [Fact]
    public void LoadOutputStyleReturnsMdContent()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        var content = sm.LoadOutputStyle();
        content.Should().Contain("格式内容");
    }

    [Fact]
    public void LoadOutputStyleReturnsNullWhenFileMissing()
    {
        var dir = MakeTempSkillsDir();
        File.Delete(Path.Combine(dir, "output-style.md"));
        var sm = new SkillManager(dir);
        sm.LoadOutputStyle().Should().BeNull();
    }

    [Fact]
    public void LoadOutputStyleReturnsNullWhenNotConfigured()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-no-style-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "registry.yaml"), @"
background: []
personas: []
");
        var sm = new SkillManager(dir);
        sm.LoadOutputStyle().Should().BeNull();
    }

    private static SkillManager MakeSm()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-sm-chinese-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "registry.yaml"), @"
answer_rules_file: answer-rules.md
output_style_file: output-style.md
background:
  - id: 启程
    overview_file: background/启程/启程概述.md
    detail_dir: background/启程/detail
  - id: 世界
    overview_file: background/世界/世界概述.md
    detail_dir: background/世界/detail
  - id: 门派
    overview_file: background/门派/门派概述.md
    detail_dir: background/门派/detail
  - id: 人物
    overview_file: background/人物/人物概述.md
    detail_dir: background/人物/detail
  - id: 修习
    overview_file: background/修习/修习概述.md
    detail_dir: background/修习/detail
  - id: 战斗
    overview_file: background/战斗/战斗概述.md
    detail_dir: background/战斗/detail
  - id: 交互
    overview_file: background/交互/交互概述.md
    detail_dir: background/交互/detail
  - id: 产业
    overview_file: background/产业/产业概述.md
    detail_dir: background/产业/detail
  - id: 物品
    overview_file: background/物品/物品概述.md
    detail_dir: background/物品/detail
  - id: 游历
    overview_file: background/游历/游历概述.md
    detail_dir: background/游历/detail
guidance:
  - id: 战斗 build 指引
    file: guidance/战斗-build-指引.md
    relevant_chapters: [战斗]
  - id: 产业规划
    file: guidance/产业规划.md
    relevant_chapters: [产业]
  - id: 武学搭配
    file: guidance/武学搭配.md
    relevant_chapters: [修习, 战斗]
  - id: 剑冢攻略
    file: guidance/剑冢攻略.md
    relevant_chapters: [启程]
  - id: 战斗指导
    file: guidance/战斗指导.md
    relevant_chapters: [战斗]
  - id: NPC互动
    file: guidance/NPC互动.md
    relevant_chapters: [交互, 人物]
  - id: 开局选择
    file: guidance/开局选择.md
    relevant_chapters: [启程]
  - id: 门派探索
    file: guidance/门派探索.md
    relevant_chapters: [门派]
  - id: 问题诊断
    file: guidance/问题诊断.md
    relevant_chapters: []
  - id: 机制解释
    file: guidance/机制解释.md
    relevant_chapters: []
  - id: 风险预判
    file: guidance/风险预判.md
    relevant_chapters: []
personas:
  - id: sword-will
    cn_name: 剑中虚影
    file: personas/sword-will.md
");
        // Create minimal directory structure so SkillManager doesn't fail on file lookups
        foreach (var ch in new[] { "启程","世界","门派","人物","修习","战斗","交互","产业","物品","游历" })
        {
            Directory.CreateDirectory(Path.Combine(dir, "background", ch, "detail"));
            File.WriteAllText(Path.Combine(dir, "background", ch, ch + "概述.md"), "# " + ch);
        }
        Directory.CreateDirectory(Path.Combine(dir, "guidance"));
        foreach (var g in new[] { "战斗-build-指引","产业规划","武学搭配","剑冢攻略","战斗指导","NPC互动","开局选择","门派探索","问题诊断","机制解释","风险预判" })
        {
            File.WriteAllText(Path.Combine(dir, "guidance", g + ".md"), "# " + g);
        }
        Directory.CreateDirectory(Path.Combine(dir, "personas"));
        File.WriteAllText(Path.Combine(dir, "personas", "sword-will.md"), "# 剑中虚影");
        File.WriteAllText(Path.Combine(dir, "answer-rules.md"), "# 规则");
        File.WriteAllText(Path.Combine(dir, "output-style.md"), "# 格式");
        return new SkillManager(dir);
    }

    [Fact]
    public void MissingRegistryReturnsEmptyLists()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-empty-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var sm = new SkillManager(dir);
        sm.GetChapterEnum().Should().BeEmpty();
    }

    [Fact]
    public void GetChapterEnum_ReturnsChineseIds()
    {
        var sm = MakeSm();
        var chapters = sm.GetChapterEnum();
        chapters.Should().Contain("产业");
        chapters.Should().Contain("启程");
        chapters.Should().Contain("战斗");
        chapters.Should().HaveCount(10);
        chapters.Should().NotContain("chanye");
    }

    [Fact]
    public void GetGuidanceEnum_ReturnsChineseIds()
    {
        var sm = MakeSm();
        var guides = sm.GetGuidanceEnum();
        guides.Should().Contain("战斗 build 指引");
        guides.Should().HaveCount(11);
        guides.Should().NotContain("combat-build");
    }

    [Fact]
    public void ChapterCnName_ReturnsIdAsChineseName()
    {
        var sm = MakeSm();
        sm.ChapterCnName("产业").Should().Be("产业");
        sm.ChapterCnName("nonexistent").Should().Be("nonexistent");
    }

    [Fact]
    public void GuidanceCnName_ReturnsIdAsChineseName()
    {
        var sm = MakeSm();
        sm.GuidanceCnName("战斗 build 指引").Should().Be("战斗 build 指引");
        sm.GuidanceCnName("nonexistent").Should().Be("nonexistent");
    }
}
