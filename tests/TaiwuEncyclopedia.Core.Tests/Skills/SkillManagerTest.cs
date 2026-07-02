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
        // 写 registry.yaml
        File.WriteAllText(Path.Combine(dir, "registry.yaml"), @"
background:
  - id: taiwu-wiki-zhan-dou
    cn_name: 战斗
    overview_file: background/zhan-dou/overview.md
    detail_dir: background/zhan-dou/detail
guidance:
  - id: combat-build
    cn_name: 战斗 build 指引
    file: guidance/combat-build.md
    relevant_chapters: [taiwu-wiki-zhan-dou]
personas:
  - id: ring-elder
    cn_name: 戒指老爷爷
    file: personas/ring-elder.md
");
        // 写占位 md
        Directory.CreateDirectory(Path.Combine(dir, "background", "zhan-dou", "detail"));
        File.WriteAllText(Path.Combine(dir, "background", "zhan-dou", "overview.md"), "# 战斗\n概述内容");
        File.WriteAllText(Path.Combine(dir, "background", "zhan-dou", "detail", "gong-fa.md"), "功法详尽内容");
        Directory.CreateDirectory(Path.Combine(dir, "guidance"));
        File.WriteAllText(Path.Combine(dir, "guidance", "combat-build.md"), "# 战斗 build\n引导内容");
        Directory.CreateDirectory(Path.Combine(dir, "personas"));
        File.WriteAllText(Path.Combine(dir, "personas", "ring-elder.md"), "# 戒指老爷爷\npersona 内容");
        return dir;
    }

    [Fact]
    public void GetChapterEnumReturnsRegisteredChapters()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        sm.GetChapterEnum().Should().Contain("taiwu-wiki-zhan-dou");
    }

    [Fact]
    public void ChapterCnNameReturnsCnName()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        sm.ChapterCnName("taiwu-wiki-zhan-dou").Should().Be("战斗");
    }

    [Fact]
    public void GuidanceCnNameReturnsCnName()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        sm.GuidanceCnName("combat-build").Should().Be("战斗 build 指引");
    }

    [Fact]
    public void LoadChapterOverviewReturnsMdContent()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        var content = sm.LoadChapterOverview("taiwu-wiki-zhan-dou");
        content.Should().Contain("概述内容");
    }

    [Fact]
    public void LoadChapterDetailWithSectionReturnsSpecificFile()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        var content = sm.LoadChapterDetail("taiwu-wiki-zhan-dou", "gong-fa");
        content.Should().Contain("功法详尽内容");
    }

    [Fact]
    public void LoadGuidanceSkillReturnsMdContent()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        var content = sm.LoadGuidanceSkill("combat-build");
        content.Should().Contain("引导内容");
    }

    [Fact]
    public void LoadPersonaReturnsMdContent()
    {
        var sm = new SkillManager(MakeTempSkillsDir());
        var content = sm.LoadPersona("ring-elder");
        content.Should().Contain("persona 内容");
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
    public void MissingRegistryReturnsEmptyLists()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-empty-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var sm = new SkillManager(dir);
        sm.GetChapterEnum().Should().BeEmpty();
    }
}
