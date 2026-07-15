using GameData.Domains.CombatSkill;
using TaiwuEncyclopedia.Core.Probe;
using TaiwuEncyclopedia.Core.Probe.Dto;

namespace TaiwuEncyclopedia;

/// <summary>ReadingState 位翻译(Frontend, 依赖游戏 CombatSkillStateHelper 静态API)。
/// Core 不能引用游戏DLL, 故此层在 Frontend。填 PagesRead="已读N/M页"。</summary>
public static class ProbeReadingStateTranslator
{
    public static void Translate(CombatSkillsSnapshot snapshot)
    {
        int total = CombatSkillStateHelper.TotalPagesCount;
        foreach (var r in snapshot.Learned)
        {
            if (r.PagesRead != null) continue;  // 已填不覆盖
            int read = CombatSkillStateHelper.GetReadPagesCount(r.ReadingStateRaw);
            r.PagesRead = total > 0 ? $"已读{read}/{total}页" : $"已读{read}页";
        }
    }
}