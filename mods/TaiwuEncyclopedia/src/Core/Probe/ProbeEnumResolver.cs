using TaiwuEncyclopedia.Core.Probe.Dto;

namespace TaiwuEncyclopedia.Core.Probe;

/// <summary>探针翻译统一入口。Core 纯表 5 项(游戏不给API) + 持 IConfigEnumResolver(Frontend 注入)。</summary>
public sealed class ProbeEnumResolver
{
    private readonly IConfigEnumResolver _config;

    public ProbeEnumResolver(IConfigEnumResolver config) { _config = config; }

    // --- Core 纯表 5 项 ---

    private static readonly string?[] GradeNames = { "九品","八品","七品","六品","五品","四品","三品","二品","一品" };

    public static string? TranslateGrade(sbyte grade) =>
        (grade >= 0 && grade < GradeNames.Length) ? GradeNames[grade] : null;

    public static string[] TranslateRelation(ushort bits)
    {
        if (bits == 0) return new[] { "无特殊关系" };
        var l = new System.Collections.Generic.List<string>();
        if ((bits & (1|8|64)) > 0) l.Add("父母");
        if ((bits & (2|16|128)) > 0) l.Add("子女");
        if ((bits & (4|32|256)) > 0) l.Add("兄弟姐妹");
        if ((bits & 512) > 0) l.Add("义结金兰");
        if ((bits & 1024) > 0) l.Add("夫妻");
        if ((bits & (2048|4096)) > 0) l.Add("师徒");
        if ((bits & 8192) > 0) l.Add("挚友");
        if ((bits & 16384) > 0) l.Add("恋人");
        if ((bits & 32768) > 0) l.Add("仇敌");
        return l.Count > 0 ? l.ToArray() : new[] { "无特殊关系" };
    }

    public static string TranslateMastered(bool mastered) => mastered ? "大成" : "";
    public static string TranslatePractice(bool isPositive, bool isReverse) =>
        isReverse ? "逆练" : isPositive ? "正练" : "未知";

    private static readonly string[] EquipmentPartNames = { "头","身","腰","手","脚","武器","副手","饰品" };
    public static string? TranslateEquipmentPart(int slot) =>
        (slot >= 0 && slot < EquipmentPartNames.Length) ? EquipmentPartNames[slot] : $"槽位{slot}";

    // --- 填充方法(调 Core 纯表 + IConfigEnumResolver) ---

    public void Resolve(TaiwuSnapshot s)
    {
        s.GradeLevel ??= TranslateGrade((sbyte)s.GradeRaw);
        s.GenderName ??= _config.ResolveGenderName((sbyte)s.GenderRaw);
        s.StanceName ??= _config.ResolveStanceName((sbyte)s.StanceRaw);
        s.FameName ??= _config.ResolveFameName((sbyte)s.Fame);
        s.HappinessName ??= _config.ResolveHappinessName((sbyte)s.Happiness);
        s.CharmLevel ??= _config.ResolveCharmLevel((short)s.Charm, (sbyte)s.GenderRaw, (short)s.Age, 0, false, true);
        s.AlertnessLevel ??= _config.ResolveAlertnessLevel(s.Alertness);
        s.NeiliTypeName ??= _config.ResolveNeiliTypeName((sbyte)s.NeiliTypeRaw);
        s.FiveElementName ??= _config.ResolveFiveElementName((sbyte)s.FiveElementIndex);
        s.CombatSkillGrowthName ??= _config.ResolveSkillGrowthName(s.CombatSkillGrowthType, (short)s.Age);
        s.LifeSkillGrowthName ??= _config.ResolveSkillGrowthName(s.LifeSkillGrowthType, (short)s.Age);
        // FeatureNames/OrgName/OrgDesc 等需要 Config 查, 也在 _config 里
    }

    public void Resolve(NpcSnapshot s)
    {
        s.GradeLevel ??= TranslateGrade((sbyte)s.GradeRaw);
        s.GenderName ??= _config.ResolveGenderName((sbyte)s.GenderRaw);
        s.StanceName ??= _config.ResolveStanceName((sbyte)s.StanceRaw);
        s.FavorLevel ??= _config.ResolveFavorLevel(s.FavorRaw);
        s.RelationFlags ??= TranslateRelation(s.RelationBits);
        s.CharmLevel ??= _config.ResolveCharmLevel((short)s.Charm, (sbyte)s.GenderRaw, (short)s.Age, 0, false, true);
        s.AlertnessLevel ??= _config.ResolveAlertnessLevel(s.Alertness);
        s.FameName ??= _config.ResolveFameName((sbyte)s.Fame);
        s.HappinessName ??= _config.ResolveHappinessName((sbyte)s.Happiness);
        s.CombatSkillGrowthName ??= _config.ResolveSkillGrowthName(s.CombatSkillGrowthType, (short)s.Age);
        s.LifeSkillGrowthName ??= _config.ResolveSkillGrowthName(s.LifeSkillGrowthType, (short)s.Age);
    }

    public void Resolve(CombatSkillsSnapshot s)
    {
        foreach (var r in s.Learned)
        {
            r.GradeLevel ??= TranslateGrade((sbyte)r.GradeRaw);
            r.SkillTypeName ??= _config.ResolveSkillTypeName((sbyte)r.SkillTypeRaw);
            r.SkillFiveElementName ??= _config.ResolveFiveElementName((sbyte)r.SkillFiveElements);
            r.MasteredText ??= TranslateMastered(r.Mastered);
            r.PracticeText ??= TranslatePractice(r.IsPositive, r.IsReverse);
            r.PagesRead ??= _config.ResolveReadingStateText(r.ReadingStateRaw);
        }
    }
}
