using FluentAssertions;
using TaiwuEncyclopedia.Core.Probe;
using TaiwuEncyclopedia.Core.Probe.Dto;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Probe;

public class ProbeEnumResolverTest
{
    // Fake IConfigEnumResolver for testing Core pure tables
    private sealed class FakeConfigResolver : IConfigEnumResolver
    {
        public string? ResolveSkillTypeName(sbyte type) => $"SKILL_{type}";
        public string? ResolveNeiliTypeName(sbyte destType) => $"NEILI_{destType}";
        public string? ResolveStanceName(sbyte behavior) => $"STANCE_{behavior}";
        public string? ResolveFavorLevel(short favor) => $"FAVOR_{favor}";
        public string? ResolveFiveElementName(sbyte type) => $"FE_{type}";
        public string? ResolveOrgGradeTitle(object orgInfo, sbyte gender, short age, int templateId) => "ORG";
        public string? ResolveCharmLevel(short charm, sbyte gender, short age, short clothDisplayId, bool isFixedCharacter, bool faceVisible) => "CHARM";
        public string? ResolveAlertnessLevel(int alertness) => "ALERT";
        public string? ResolveFameName(sbyte fameType) => "FAME";
        public string? ResolveHappinessName(sbyte happiness) => "HAPPY";
        public string? ResolveGenderName(sbyte gender) => gender == 1 ? "男" : "女";
        public string? ResolveSkillGrowthName(int growthType, short actualAge) => "GROWTH";
        public string? ResolveFeatureName(short featureId) => $"FEAT_{featureId}";
        public string? ResolveLocationText(object location) => "LOC";
        public string? ResolveOrgDesc(sbyte orgTemplateId) => "DESC";
        public string? ResolveOrgVow(sbyte orgTemplateId) => "VOW";
        public string? ResolveOrgStory(sbyte orgTemplateId) => "STORY";
        public string? ResolveReadingStateText(ushort readingState) => $"READ_{readingState}";
    }

    [Fact]
    public void Grade_0_Is九品()
    {
        ProbeEnumResolver.TranslateGrade(0).Should().Be("九品");
        ProbeEnumResolver.TranslateGrade(8).Should().Be("一品");
        ProbeEnumResolver.TranslateGrade(9).Should().BeNull();
    }

    [Fact]
    public void Relation_BitFlags_Decoded()
    {
        ProbeEnumResolver.TranslateRelation(0).Should().Equal("无特殊关系");
        ProbeEnumResolver.TranslateRelation(1024).Should().Equal("夫妻");
        var r = ProbeEnumResolver.TranslateRelation(1024 | 8192);
        r.Should().Contain("夫妻").And.Contain("挚友");
    }

    [Fact]
    public void Mastered_And_Practice()
    {
        ProbeEnumResolver.TranslateMastered(true).Should().Be("大成");
        ProbeEnumResolver.TranslateMastered(false).Should().Be("");
        ProbeEnumResolver.TranslatePractice(true, false).Should().Be("正练");
        ProbeEnumResolver.TranslatePractice(false, true).Should().Be("逆练");
    }

    [Fact]
    public void EquipmentPart_Translated()
    {
        ProbeEnumResolver.TranslateEquipmentPart(0).Should().Be("头");
        ProbeEnumResolver.TranslateEquipmentPart(5).Should().Be("武器");
        ProbeEnumResolver.TranslateEquipmentPart(99).Should().Be("槽位99");
    }

    [Fact]
    public void Resolve_CombatSkills_FillsLevelFields()
    {
        var resolver = new ProbeEnumResolver(new FakeConfigResolver());
        var snap = new CombatSkillsSnapshot
        {
            Learned = new[] { new LearnedSkillRaw { GradeRaw = 0, SkillTypeRaw = 7, SkillFiveElements = 2, Mastered = true, IsPositive = true, IsReverse = false, ReadingStateRaw = 994 } }
        };
        resolver.Resolve(snap);
        snap.Learned[0].GradeLevel.Should().Be("九品");
        snap.Learned[0].SkillTypeName.Should().Be("SKILL_7");
        snap.Learned[0].SkillFiveElementName.Should().Be("FE_2");
        snap.Learned[0].MasteredText.Should().Be("大成");
        snap.Learned[0].PracticeText.Should().Be("正练");
        snap.Learned[0].PagesRead.Should().Be("READ_994");
    }
}
