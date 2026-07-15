using FluentAssertions;
using TaiwuEncyclopedia.Core.Probe;
using TaiwuEncyclopedia.Core.Probe.Dto;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Probe;

public class ProbeValueTranslatorTest
{
    [Theory]
    [InlineData(0, "九品")]
    [InlineData(1, "八品")]
    [InlineData(2, "七品")]
    [InlineData(8, "一品")]
    public void TranslateGrade_GradeToLevelName(sbyte grade, string expected)
    {
        ProbeValueTranslator.TranslateGrade(grade).Should().Be(expected);
    }

    [Fact]
    public void TranslateGrade_OutOfRange_ReturnsNull()
    {
        ProbeValueTranslator.TranslateGrade((sbyte)9).Should().BeNull();
        ProbeValueTranslator.TranslateGrade((sbyte)(-1)).Should().BeNull();
    }

    [Theory]
    [InlineData(0, "内功")]    // Config.CombatSkillType.Neigong 值, 待 UI 校验方向
    [InlineData(3, "拳掌")]    // FistAndPalm, step1 实测 Type=3=太祖长拳(拳掌类)方向已确认
    public void TranslateSkillType_TypeToName(sbyte type, string expected)
    {
        ProbeValueTranslator.TranslateSkillType(type).Should().Be(expected);
    }

    [Fact]
    public void TranslateSkillType_Unknown_ReturnsNull()
    {
        ProbeValueTranslator.TranslateSkillType((sbyte)99).Should().BeNull();
    }

    [Fact]
    public void Translate_FillsLevelFields_KeepsRaw()
    {
        var snap = new CombatSkillsSnapshot
        {
            Learned = new[] { new LearnedSkillRaw { TemplateId = 1, Name = "X", GradeRaw = 0, SkillTypeRaw = 3 } }
        };
        ProbeValueTranslator.Translate(snap);
        snap.Learned[0].GradeLevel.Should().Be("九品");
        snap.Learned[0].SkillTypeName.Should().Be("拳掌");
        snap.Learned[0].GradeRaw.Should().Be(0);   // raw 保留
        snap.Learned[0].SkillTypeRaw.Should().Be(3);
    }

    [Fact]
    public void Translate_UnknownValues_LeavesNull()
    {
        var snap = new CombatSkillsSnapshot
        {
            Learned = new[] { new LearnedSkillRaw { GradeRaw = 99, SkillTypeRaw = 99 } }
        };
        ProbeValueTranslator.Translate(snap);
        snap.Learned[0].GradeLevel.Should().BeNull();   // 未知不瞎填
        snap.Learned[0].SkillTypeName.Should().BeNull();
    }
}