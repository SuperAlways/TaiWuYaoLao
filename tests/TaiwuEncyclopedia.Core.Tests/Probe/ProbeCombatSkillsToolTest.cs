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
    }

    [Fact]
    public void Metadata_NameAndNoParams()
    {
        var tool = new ProbeCombatSkillsTool(new FakeProvider(new()));
        tool.Metadata.Name.Should().Be("probe_combat_skills");
        tool.Metadata.Parameters.Should().BeEmpty();
        tool.RequiresSaveGame.Should().BeTrue();
    }

    [Fact]
    public async Task Execute_ReturnsOkWithSnapshot()
    {
        var snap = new CombatSkillsSnapshot { Learned = new[] { new LearnedSkillRaw { TemplateId = 5, Name = "狮子吼", GradeRaw = 0, SkillTypeRaw = 8 } } };
        var tool = new ProbeCombatSkillsTool(new FakeProvider(snap));
        var result = await tool.ExecuteAsync(new(), default);
        result["status"].Should().Be("ok");
        result["probe"].Should().Be("probe_combat_skills");
        ((CombatSkillsSnapshot)result["snapshot"]).Learned.Should().HaveCount(1);
    }

    [Fact]
    public async Task Execute_TranslatesGradeAndType()  // 验证 tool 调了 Core 翻译
    {
        var snap = new CombatSkillsSnapshot { Learned = new[] { new LearnedSkillRaw { GradeRaw = 0, SkillTypeRaw = 3 } } };
        var tool = new ProbeCombatSkillsTool(new FakeProvider(snap));
        await tool.ExecuteAsync(new(), default);
        // Execute 后 snap 的 level 字段应被 Core 翻译填上(原地修改)
        snap.Learned[0].GradeLevel.Should().Be("九品");
        snap.Learned[0].SkillTypeName.Should().Be("拳掌");
    }
}
