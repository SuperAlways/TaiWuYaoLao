using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TaiwuEncyclopedia.Core.Probe.Tools;

/// <summary>NPC探查探针。需传 charId, 读指定 NPC 的画像+属性+资质。</summary>
public sealed class ProbeNpcTool : ProbeToolBase
{
    private readonly ProbeEnumResolver _resolver;

    public ProbeNpcTool(IGameStateProvider gs, ProbeEnumResolver resolver)
        : base("probe_npc", "探查某NPC的画像+属性+资质。需传charId(从入口注入获取)。用于战斗(查敌人)、社交(查攻略对象)。", timeout: 30, gs, errorCodePrefix: "P-NPC")
    {
        _resolver = resolver;
        SetParameters(new Dictionary<string, Dictionary<string, object>>
        {
            ["charId"] = new() { ["type"] = "integer", ["required"] = true, ["description"] = "NPC角色ID(从入口注入获取)" },
        });
    }

    protected override async Task<object> ProbeReadAsync(
        IGameStateProvider gs, IProbeErrorCollector collector,
        Dictionary<string, object> args, CancellationToken ct)
    {
        var charId = System.Convert.ToInt32(args.GetValueOrDefault("charId") ?? 0);
        if (charId <= 0) return new { error = "charId 必填(从入口注入获取)" };
        var snap = await gs.GetNpcDetail(charId, collector);
        _resolver.Resolve(snap);
        return snap;
    }
}
