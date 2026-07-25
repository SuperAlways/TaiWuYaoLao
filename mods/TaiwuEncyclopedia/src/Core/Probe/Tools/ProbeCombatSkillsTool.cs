using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TaiwuEncyclopedia.Core.Probe.Tools;

/// <summary>combat_skills 探针 tool。无参, 读太吾功法全貌。
/// 调 GetCombatSkills(已含 Frontend ReadingState 翻译) + Core ProbeEnumResolver(Grade/Type)。</summary>
public sealed class ProbeCombatSkillsTool : ProbeToolBase
{
    private readonly ProbeEnumResolver _resolver;

    public ProbeCombatSkillsTool(IGameStateProvider gs, ProbeEnumResolver resolver)
        : base("probe_combat_skills",
               "探查太吾功法全貌：已学功法(修习度/正逆练/阅读进度/品级)、威力、大成状态。用于功法搭配、突破、内力分配建议。",
               timeout: 15, gs, errorCodePrefix: "P-CS")
    {
        _resolver = resolver;
    }

    protected override async Task<object> ProbeReadAsync(
        IGameStateProvider gs, IProbeErrorCollector collector,
        Dictionary<string, object> args, CancellationToken ct)
    {
        var snapshot = await gs.GetCombatSkills(collector);
        _resolver.Resolve(snapshot);
        return snapshot;
    }
}
