using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Probe;
using TaiwuEncyclopedia.Core.Probe.Dto;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Probe;

// 最小 IGameStateProvider 假实现: ok 返固定 snapshot, degraded/throw 由构造控制
internal sealed class FakeGameStateProvider : IGameStateProvider
{
    private readonly CombatSkillsSnapshot _snapshot;
    private readonly bool _throwOnRead;
    public FakeGameStateProvider(CombatSkillsSnapshot snapshot, bool throwOnRead = false) { _snapshot = snapshot; _throwOnRead = throwOnRead; }
    public Task<CombatSkillsSnapshot> GetCombatSkills(IProbeErrorCollector collector)
    {
        if (_throwOnRead) throw new System.Exception("core api gone");
        return Task.FromResult(_snapshot);
    }
}

// 测试用 tool: 继承 ProbeToolBase, 只实现 ProbeReadAsync
internal sealed class TestCombatTool : ProbeToolBase
{
    public TestCombatTool(IGameStateProvider gs) : base("probe_combat_skills", "test", timeout: 15, gs, "P-CS") { }
    protected override async Task<object> ProbeReadAsync(IGameStateProvider gs, IProbeErrorCollector collector, Dictionary<string, object> args, CancellationToken ct)
        => await gs.GetCombatSkills(collector);
}

public class ProbeToolBaseTest
{
    [Fact]
    public async Task Ok_WhenReadSucceeds()
    {
        var snap = new CombatSkillsSnapshot { Learned = new[] { new LearnedSkillRaw { TemplateId = 1, Name = "X" } } };
        var tool = new TestCombatTool(new FakeGameStateProvider(snap));
        var result = await tool.ExecuteAsync(new(), default);
        result["status"].Should().Be("ok");
        result["error_code"].Should().Be("");
        ((CombatSkillsSnapshot)result["snapshot"]).Learned.Should().HaveCount(1);
    }

    [Fact]
    public async Task Unavailable_WhenReadThrows()
    {
        var tool = new TestCombatTool(new FakeGameStateProvider(null!, throwOnRead: true));
        var result = await tool.ExecuteAsync(new(), default);
        result["status"].Should().Be("unavailable");
        result["error_code"].Should().Be("P-CS-000");
    }

    [Fact]
    public void RequiresSaveGame_True()
    {
        var tool = new TestCombatTool(new FakeGameStateProvider(null!));
        tool.RequiresSaveGame.Should().BeTrue();
    }
}
