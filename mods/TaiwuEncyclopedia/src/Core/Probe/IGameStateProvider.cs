using System.Threading.Tasks;
using TaiwuEncyclopedia.Core.Probe.Dto;

namespace TaiwuEncyclopedia.Core.Probe;

/// <summary>游戏状态读取接口。Core 定义, Frontend 用游戏原生 AsyncCall 实现。
/// step1 只含 combat_skills 一个方法; 后续探针追加 GetNpcDetail/GetVillage 等。</summary>
public interface IGameStateProvider
{
    /// <summary>读太吾功法全貌。字段级失败收集到 collector, 返回值纯数据(失败字段为默认值)。</summary>
    Task<CombatSkillsSnapshot> GetCombatSkills(IProbeErrorCollector collector);
}
