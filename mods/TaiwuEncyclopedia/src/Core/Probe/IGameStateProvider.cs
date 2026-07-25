using System.Threading.Tasks;
using TaiwuEncyclopedia.Core.Probe.Dto;

namespace TaiwuEncyclopedia.Core.Probe;

/// <summary>游戏状态读取接口。Core 定义, Frontend 用游戏原生 AsyncCall 实现。
/// step1 只含 combat_skills 一个方法; 后续探针追加 GetNpcDetail/GetVillage 等。</summary>
public interface IGameStateProvider
{
    /// <summary>读太吾功法全貌。字段级失败收集到 collector, 返回值纯数据(失败字段为默认值)。</summary>
    Task<CombatSkillsSnapshot> GetCombatSkills(IProbeErrorCollector collector);
    /// <summary>读太吾自身全状态。字段级失败收集到 collector, 返回值纯数据(失败字段为默认值)。</summary>
    Task<TaiwuSnapshot> GetTaiwu(IProbeErrorCollector collector);
    /// <summary>读指定NPC的画像+属性+资质。字段级失败收集到 collector, 返回值纯数据(失败字段为默认值)。</summary>
    Task<NpcSnapshot> GetNpcDetail(int charId, IProbeErrorCollector collector);
    /// <summary>读背包+装备+资源。charId=-1 表示太吾自己。字段级失败收集到 collector, 返回值纯数据(失败字段为默认值)。</summary>
    Task<InventorySnapshot> GetInventory(int charId, IProbeErrorCollector collector);
}
