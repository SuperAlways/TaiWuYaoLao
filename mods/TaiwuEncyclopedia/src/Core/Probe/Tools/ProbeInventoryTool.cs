using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TaiwuEncyclopedia.Core.Probe.Tools;

/// <summary>背包/装备/资源探查探针。可传 charId(默认太吾), 读背包+装备+资源。</summary>
public sealed class ProbeInventoryTool : ProbeToolBase
{
    public ProbeInventoryTool(IGameStateProvider gs)
        : base("probe_inventory", "探查背包+装备+资源。可传charId查他人(默认太吾)。用于送礼选择、材料利用、装备对比。", timeout: 30, gs, errorCodePrefix: "P-INV")
    {
        SetParameters(new Dictionary<string, Dictionary<string, object>>
        {
            ["charId"] = new() { ["type"] = "integer", ["required"] = false, ["description"] = "角色ID,省略=太吾自己" },
        });
    }

    protected override async Task<object> ProbeReadAsync(
        IGameStateProvider gs, IProbeErrorCollector collector,
        Dictionary<string, object> args, CancellationToken ct)
    {
        var charId = args.TryGetValue("charId", out var v) ? System.Convert.ToInt32(v) : 0;
        if (charId <= 0) charId = -1; // -1 = 太吾自己(Frontend 处理)
        return await gs.GetInventory(charId, collector);
    }
}
