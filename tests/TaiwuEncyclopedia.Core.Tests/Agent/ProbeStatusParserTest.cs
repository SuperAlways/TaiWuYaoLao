using FluentAssertions;
using TaiwuEncyclopedia.Core.Agent;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Agent;

public class ProbeStatusParserTest
{
    [Fact]
    public void Parses_ProbeResult_Json()
    {
        var json = "{\"probe\":\"probe_combat_skills\",\"status\":\"degraded\",\"failed_api\":\"GetCombatSkillDisplayData\",\"error_code\":\"P-CS-002\",\"missing_fields\":[],\"error\":\"boom\",\"snapshot\":{}}";
        var ok = ProbeStatusParser.TryGetProbeStatus(json, out var status, out var code, out var api, out var probe);
        ok.Should().BeTrue();
        status.Should().Be("degraded");
        code.Should().Be("P-CS-002");
        api.Should().Be("GetCombatSkillDisplayData");
        probe.Should().Be("probe_combat_skills");
    }

    [Fact]
    public void ReturnsFalse_ForNonProbeResult()
    {
        // 普通工具 result 无 status 字段
        var json = "{\"error\":\"工具 xxx 不存在\"}";
        ProbeStatusParser.TryGetProbeStatus(json, out _, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void ReturnsFalse_ForOkStatus()
    {
        var json = "{\"probe\":\"probe_combat_skills\",\"status\":\"ok\"}";
        ProbeStatusParser.TryGetProbeStatus(json, out var status, out _, out _, out _).Should().BeTrue();
        status.Should().Be("ok");  // 解析成功但 ok, AgentLoop 不提示
    }
}
