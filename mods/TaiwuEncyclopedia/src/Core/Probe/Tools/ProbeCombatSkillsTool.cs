using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TaiwuEncyclopedia.Core.Probe.Tools;

public sealed class ProbeCombatSkillsTool : ProbeToolBase
{
    public ProbeCombatSkillsTool(IGameStateProvider gs)
        : base("probe_combat_skills",
               "探查太吾功法全貌：已学功法(修习度/正逆练/阅读进度/品级)、威力、大成状态。用于功法搭配、突破、内力分配建议。",
               timeout: 15, gs, errorCodePrefix: "P-CS") { }

    protected override async Task<object> ProbeReadAsync(
        IGameStateProvider gs, IProbeErrorCollector collector,
        Dictionary<string, object> args, CancellationToken ct)
    {
        return await gs.GetCombatSkills(collector);
    }
}
