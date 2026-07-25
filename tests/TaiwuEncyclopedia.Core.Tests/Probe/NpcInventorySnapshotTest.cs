using FluentAssertions;
using TaiwuEncyclopedia.Core.Probe.Dto;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Probe;

public class NpcInventorySnapshotTest
{
    [Fact]
    public void NpcSnapshot_Defaults()
    {
        var s = new NpcSnapshot();
        s.Name.Should().Be("");
        s.CurMainAttributes.Should().HaveCount(6);
        s.CombatSkillQualifications.Should().HaveCount(14);
        s.FavorLevel.Should().BeNull();
        s.Errors.Should().BeEmpty();
    }

    [Fact]
    public void InventorySnapshot_Defaults()
    {
        var s = new InventorySnapshot();
        s.Items.Should().BeEmpty();
        s.Resources.Should().HaveCount(8);
        s.Equipment.Should().BeEmpty();
        s.Errors.Should().BeEmpty();
    }
}