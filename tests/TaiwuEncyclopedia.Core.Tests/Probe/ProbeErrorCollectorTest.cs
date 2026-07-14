using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Probe;
using TaiwuEncyclopedia.Core.Probe.Dto;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Probe;

public class ProbeErrorCollectorTest
{
    [Fact]
    public void Empty_WhenNoFailures()
    {
        var c = new ProbeErrorCollector();
        c.Failures.Should().BeEmpty();
    }

    [Fact]
    public void AddFailed_RecordsFailure()
    {
        var c = new ProbeErrorCollector();
        c.AddFailed("GetCombatSkillDisplayData", "P-CS-002", new InvalidOperationException("boom"));
        c.Failures.Should().HaveCount(1);
        c.Failures[0].ApiName.Should().Be("GetCombatSkillDisplayData");
        c.Failures[0].ErrorCode.Should().Be("P-CS-002");
        c.Failures[0].ErrorMessage.Should().Be("boom");
    }

    [Fact]
    public async Task ProbeBase_TryRead_Success_ReturnsValue_NoFailure()
    {
        var c = new ProbeErrorCollector();
        var v = await ProbeBase.TryRead("combat_skills", "GetLearnedCombatSkillByType", "P-CS-001", c,
            () => Task.FromResult(42));
        v.Should().Be(42);
        c.Failures.Should().BeEmpty();
    }

    [Fact]
    public async Task ProbeBase_TryRead_Failure_ReturnsDefault_RecordsFailure()
    {
        var c = new ProbeErrorCollector();
        var v = await ProbeBase.TryRead<CombatSkillsSnapshot>("combat_skills", "GetLearnedCombatSkillByType", "P-CS-001", c,
            () => throw new InvalidOperationException("boom"));
        v.Should().BeNull();  // 引用类型失败返回 default=null
        c.Failures.Should().HaveCount(1);
        c.Failures[0].ErrorCode.Should().Be("P-CS-001");
    }
}