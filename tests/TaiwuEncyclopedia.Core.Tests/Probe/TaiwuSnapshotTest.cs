using FluentAssertions;
using TaiwuEncyclopedia.Core.Probe.Dto;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Probe;

public class TaiwuSnapshotTest
{
    [Fact]
    public void Defaults_AreEmptyOrNull()
    {
        var s = new TaiwuSnapshot();
        s.Name.Should().Be("");
        s.FiveElementsProportion.Should().HaveCount(5);
        s.CurMainAttributes.Should().HaveCount(6);
        s.Resources.Should().HaveCount(8);
        s.CombatSkillQualifications.Should().HaveCount(14);
        s.Errors.Should().BeEmpty();
        s.StanceName.Should().BeNull();
        s.GenderName.Should().BeNull();
    }

    [Fact]
    public void KeyArrays_DefaultNull()
    {
        var s = new TaiwuSnapshot();
        s.PersonalityKeys.Should().BeNull();
        s.MainAttributeKeys.Should().BeNull();
        s.ResourceKeys.Should().BeNull();
        s.CombatSkillTypeKeys.Should().BeNull();
        s.LifeSkillTypeKeys.Should().BeNull();
        s.FiveElementKeys.Should().BeNull();
    }
}