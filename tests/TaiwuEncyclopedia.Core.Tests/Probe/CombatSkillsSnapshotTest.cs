using FluentAssertions;
using TaiwuEncyclopedia.Core.Probe.Dto;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Probe;

public class CombatSkillsSnapshotTest
{
    [Fact]
    public void Snapshot_DefaultsToEmpty()
    {
        var s = new CombatSkillsSnapshot();
        s.Learned.Should().BeEmpty();
        s.Errors.Should().BeEmpty();
    }

    [Fact]
    public void LearnedSkillRaw_LevelFields_NullByDefault()
    {
        var r = new LearnedSkillRaw { TemplateId = 7, Name = "狮子吼", GradeRaw = 2 };
        r.GradeLevel.Should().BeNull();      // 阶段B才填
        r.SkillTypeName.Should().BeNull();
        r.PagesRead.Should().BeNull();
        r.TemplateId.Should().Be(7);
        r.Name.Should().Be("狮子吼");
        r.GradeRaw.Should().Be(2);
    }
}