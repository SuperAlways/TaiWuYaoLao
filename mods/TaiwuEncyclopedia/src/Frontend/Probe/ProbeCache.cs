using System.Collections.Generic;
using System.Threading.Tasks;
using TaiwuEncyclopedia.Core.Probe;
using TaiwuEncyclopedia.Core.Probe.Dto;

namespace TaiwuEncyclopedia;

/// <summary>探针缓存。F8 打开->缓存, F8 关上->Clear。懒加载: 首次调用才拉, 后续复用。</summary>
public sealed class ProbeCache
{
    private TaiwuSnapshot? _taiwu;
    private CombatSkillsSnapshot? _combatSkills;
    private readonly Dictionary<int, NpcSnapshot> _npcCache = new();
    private readonly Dictionary<int, InventorySnapshot> _inventoryCache = new();

    public void Clear()
    {
        _taiwu = null;
        _combatSkills = null;
        _npcCache.Clear();
        _inventoryCache.Clear();
    }

    public async Task<TaiwuSnapshot> GetTaiwuAsync(IGameStateProvider gs, IProbeErrorCollector c)
    {
        if (_taiwu != null) return _taiwu;
        _taiwu = await gs.GetTaiwu(c);
        return _taiwu;
    }

    public async Task<CombatSkillsSnapshot> GetCombatSkillsAsync(IGameStateProvider gs, IProbeErrorCollector c)
    {
        if (_combatSkills != null) return _combatSkills;
        _combatSkills = await gs.GetCombatSkills(c);
        return _combatSkills;
    }

    public async Task<NpcSnapshot> GetNpcAsync(IGameStateProvider gs, int charId, IProbeErrorCollector c)
    {
        if (_npcCache.TryGetValue(charId, out var cached)) return cached;
        var snap = await gs.GetNpcDetail(charId, c);
        _npcCache[charId] = snap;
        return snap;
    }

    public async Task<InventorySnapshot> GetInventoryAsync(IGameStateProvider gs, int charId, IProbeErrorCollector c)
    {
        if (charId <= 0) charId = SingletonObject.getInstance<BasicGameData>().TaiwuCharId;
        if (_inventoryCache.TryGetValue(charId, out var cached)) return cached;
        var snap = await gs.GetInventory(charId, c);
        _inventoryCache[charId] = snap;
        return snap;
    }
}