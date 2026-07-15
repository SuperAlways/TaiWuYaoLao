using TaiwuEncyclopedia.Core.Probe.Dto;

namespace TaiwuEncyclopedia.Core.Probe;

/// <summary>探针数值->语义翻译(Core 查表部分)。ReadingState 位运算在 Frontend(依赖游戏DLL)。
/// 表值方向以反编译基线 + UI 校验为准。游戏更新若改枚举, 改本类常量字典 + 更新契约。</summary>
public static class ProbeValueTranslator
{
    // Grade (sbyte 0..8): 游戏品级, 0=最低(九品) 8=最高(一品). 待 UI 校验方向。
    private static readonly string?[] GradeNames =
    {
        "九品", "八品", "七品", "六品", "五品", "四品", "三品", "二品", "一品"
    };

    // SkillType (sbyte): 对应 Config.CombatSkillType 14 常量。键=常量值, 值=中文名。
    // 常量值方向待 step2 UI 校验(实测 Type=3 应为拳掌)。错则改键值。
    private static readonly Dictionary<int, string> SkillTypeNames = new()
    {
        [0] = "内功",      // Neigong
        [1] = "身法",      // Posing
        [2] = "绝技",      // Stunt
        [3] = "拳掌",      // FistAndPalm
        [4] = "指法",      // Finger
        [5] = "腿法",      // Leg
        [6] = "暗器",      // Throw
        [7] = "剑法",      // Sword
        [8] = "刀法",      // Blade
        [9] = "长兵",      // Polearm
        [10] = "杂学",     // Special
        [11] = "软兵",     // Whip
        [12] = "射御",     // ControllableShot
        [13] = "乐理",     // CombatMusic
    };

    public static string? TranslateGrade(sbyte grade) =>
        (grade >= 0 && grade < GradeNames.Length) ? GradeNames[grade] : null;

    public static string? TranslateSkillType(sbyte type) =>
        SkillTypeNames.TryGetValue(type, out var name) ? name : null;

    /// <summary>填 snapshot 所有 LearnedSkillRaw 的 GradeLevel/SkillTypeName(raw 保留)。
    /// PagesRead(ReadingState 翻译)不在此填, 由 Frontend 翻译注入(依赖游戏位运算API)。</summary>
    public static void Translate(CombatSkillsSnapshot snapshot)
    {
        foreach (var r in snapshot.Learned)
        {
            r.GradeLevel ??= TranslateGrade((sbyte)r.GradeRaw);
            r.SkillTypeName ??= TranslateSkillType((sbyte)r.SkillTypeRaw);
        }
    }
}