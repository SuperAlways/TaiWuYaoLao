using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Probe;
using TaiwuEncyclopedia.Core.Probe.Dto;
using TaiwuEncyclopedia.Core.Probe.Tools;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Probe;

public class ProbeToolsTest
{
    private sealed class FakeProvider : IGameStateProvider
    {
        public Task<CombatSkillsSnapshot> GetCombatSkills(IProbeErrorCollector c) => Task.FromResult(new CombatSkillsSnapshot());
        public Task<TaiwuSnapshot> GetTaiwu(IProbeErrorCollector c) => Task.FromResult(new TaiwuSnapshot());
        public Task<NpcSnapshot> GetNpcDetail(int charId, IProbeErrorCollector c) => Task.FromResult(new NpcSnapshot { CharId = charId });
        public Task<InventorySnapshot> GetInventory(int charId, IProbeErrorCollector c) => Task.FromResult(new InventorySnapshot());
    }

    [Fact]
    public void ProbeTaiwuTool_RequiresSaveGame()
    {
        var tool = new ProbeTaiwuTool(new FakeProvider());
        tool.RequiresSaveGame.Should().BeTrue();
        tool.Metadata.Name.Should().Be("probe_taiwu");
    }

    [Fact]
    public void ProbeNpcTool_HasCharIdParam()
    {
        var tool = new ProbeNpcTool(new FakeProvider());
        tool.Metadata.Parameters.Should().ContainKey("charId");
    }

    [Fact]
    public void ProbeInventoryTool_HasOptionalCharId()
    {
        var tool = new ProbeInventoryTool(new FakeProvider());
        tool.Metadata.Parameters.Should().ContainKey("charId");
        tool.Metadata.Name.Should().Be("probe_inventory");
    }

    [Fact]
    public void ProbeCombatSkillsTool_NoResolver()
    {
        var tool = new ProbeCombatSkillsTool(new FakeProvider());
        tool.Metadata.Name.Should().Be("probe_combat_skills");
    }
}
