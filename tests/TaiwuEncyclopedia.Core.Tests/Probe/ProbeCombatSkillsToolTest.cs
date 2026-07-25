using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Probe;
using TaiwuEncyclopedia.Core.Probe.Dto;
using TaiwuEncyclopedia.Core.Probe.Tools;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Probe;

public class ProbeCombatSkillsToolTest
{
    private sealed class FakeProvider : IGameStateProvider
    {
        private readonly CombatSkillsSnapshot _s;
        public FakeProvider(CombatSkillsSnapshot s) => _s = s;
        public Task<CombatSkillsSnapshot> GetCombatSkills(IProbeErrorCollector collector) => Task.FromResult(_s);
        public Task<TaiwuSnapshot> GetTaiwu(IProbeErrorCollector collector) => Task.FromResult(new TaiwuSnapshot());
        public Task<NpcSnapshot> GetNpcDetail(int charId, IProbeErrorCollector collector) => Task.FromResult(new NpcSnapshot());
        public Task<InventorySnapshot> GetInventory(int charId, IProbeErrorCollector collector) => Task.FromResult(new InventorySnapshot());
    }

    private sealed class FakeConfig : IConfigEnumResolver
    {
        public string? ResolveSkillTypeName(sbyte type) => type switch { 3 => "拳掌", _ => null };
        public string? ResolveNeiliTypeName(sbyte destType) => null;
        public string? ResolveStanceName(sbyte behavior) => null;
        public string? ResolveFavorLevel(short favor) => null;
        public string? ResolveFiveElementName(sbyte type) => null;
        public string? ResolveOrgGradeTitle(object orgInfo, sbyte gender, short age, int templateId) => null;
        public string? ResolveCharmLevel(short charm, sbyte gender, short age, short clothDisplayId, bool isFixedCharacter, bool faceVisible) => null;
        public string? ResolveAlertnessLevel(int alertness) => null;
        public string? ResolveFameName(sbyte fameType) => null;
        public string? ResolveHappinessName(sbyte happiness) => null;
        public string? ResolveGenderName(sbyte gender) => null;
        public string? ResolveSkillGrowthName(int growthType, short actualAge) => null;
        public string? ResolveFeatureName(short featureId) => null;
        public string? ResolveLocationText(object location) => null;
        public string? ResolveOrgDesc(sbyte orgTemplateId) => null;
        public string? ResolveOrgVow(sbyte orgTemplateId) => null;
        public string? ResolveOrgStory(sbyte orgTemplateId) => null;
        public string? ResolveReadingStateText(ushort readingState) => null;
    }

    [Fact]
    public void Metadata_NameAndNoParams()
    {
        var resolver = new ProbeEnumResolver(new FakeConfig());
        var tool = new ProbeCombatSkillsTool(new FakeProvider(new()), resolver);
        tool.Metadata.Name.Should().Be("probe_combat_skills");
        tool.Metadata.Parameters.Should().BeEmpty();
        tool.RequiresSaveGame.Should().BeTrue();
    }

    [Fact]
    public async Task Execute_ReturnsOkWithSnapshot()
    {
        var snap = new CombatSkillsSnapshot { Learned = new[] { new LearnedSkillRaw { TemplateId = 5, Name = "狮子吼", GradeRaw = 0, SkillTypeRaw = 8 } } };
        var resolver = new ProbeEnumResolver(new FakeConfig());
        var tool = new ProbeCombatSkillsTool(new FakeProvider(snap), resolver);
        var result = await tool.ExecuteAsync(new(), default);
        result["status"].Should().Be("ok");
        result["probe"].Should().Be("probe_combat_skills");
        ((CombatSkillsSnapshot)result["snapshot"]).Learned.Should().HaveCount(1);
    }

    [Fact]
    public async Task Execute_TranslatesGradeAndType()  // 验证 tool 调了 Core 翻译
    {
        var snap = new CombatSkillsSnapshot { Learned = new[] { new LearnedSkillRaw { GradeRaw = 0, SkillTypeRaw = 3 } } };
        var resolver = new ProbeEnumResolver(new FakeConfig());
        var tool = new ProbeCombatSkillsTool(new FakeProvider(snap), resolver);
        await tool.ExecuteAsync(new(), default);
        // Execute 后 snap 的 level 字段应被 Core 翻译填上(原地修改)
        snap.Learned[0].GradeLevel.Should().Be("九品");
        snap.Learned[0].SkillTypeName.Should().Be("拳掌");
    }
}
