using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TaiwuEncyclopedia.Core.Probe.Tools;

/// <summary>太吾自身全状态探针。无参, 读太吾画像/内力/属性/资源/资质/运功装备。</summary>
public sealed class ProbeTaiwuTool : ProbeToolBase
{
    private readonly ProbeEnumResolver _resolver;

    public ProbeTaiwuTool(IGameStateProvider gs, ProbeEnumResolver resolver)
        : base("probe_taiwu", "探查太吾自身全状态：画像/内力/属性/资源/资质/运功装备。用于功法搭配、战斗策略、经营规划建议。", timeout: 30, gs, errorCodePrefix: "P-TW")
    {
        _resolver = resolver;
    }

    protected override async Task<object> ProbeReadAsync(
        IGameStateProvider gs, IProbeErrorCollector collector,
        Dictionary<string, object> args, CancellationToken ct)
    {
        var snap = await gs.GetTaiwu(collector);
        _resolver.Resolve(snap);
        return snap;
    }
}
