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
    private sealed class FakeConfig : IConfigEnumResolver
    {
        public string? ResolveSkillTypeName(sbyte t) => null; public string? ResolveNeiliTypeName(sbyte t) => null;
        public string? ResolveStanceName(sbyte b) => null; public string? ResolveFavorLevel(short f) => null;
        public string? ResolveFiveElementName(sbyte t) => null; public string? ResolveOrgGradeTitle(object o, sbyte g, short a, int t) => null;
        public string? ResolveCharmLevel(short c, sbyte g, short a, short cd, bool f, bool fv) => null;
        public string? ResolveAlertnessLevel(int a) => null; public string? ResolveFameName(sbyte f) => null;
        public string? ResolveHappinessName(sbyte h) => null; public string? ResolveGenderName(sbyte g) => null;
        public string? ResolveSkillGrowthName(int g, short a) => null; public string? ResolveFeatureName(short f) => null;
        public string? ResolveLocationText(object l) => null; public string? ResolveOrgDesc(sbyte o) => null;
        public string? ResolveOrgVow(sbyte o) => null; public string? ResolveOrgStory(sbyte o) => null;
        public string? ResolveReadingStateText(ushort r) => null;
    }

    [Fact]
    public void ProbeTaiwuTool_RequiresSaveGame()
    {
        var r = new ProbeEnumResolver(new FakeConfig());
        var tool = new ProbeTaiwuTool(new FakeProvider(), r);
        tool.RequiresSaveGame.Should().BeTrue();
        tool.Metadata.Name.Should().Be("probe_taiwu");
    }

    [Fact]
    public void ProbeNpcTool_HasCharIdParam()
    {
        var r = new ProbeEnumResolver(new FakeConfig());
        var tool = new ProbeNpcTool(new FakeProvider(), r);
        tool.Metadata.Parameters.Should().ContainKey("charId");
    }

    [Fact]
    public void ProbeInventoryTool_HasOptionalCharId()
    {
        var tool = new ProbeInventoryTool(new FakeProvider());
        tool.Metadata.Parameters.Should().ContainKey("charId");
        tool.Metadata.Name.Should().Be("probe_inventory");
    }
}
