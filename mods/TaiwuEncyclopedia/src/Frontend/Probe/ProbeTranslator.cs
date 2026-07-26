using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GameData.Domains.CombatSkill;
using GameData.Domains.Map;
using TaiwuEncyclopedia.Core.Probe.Dto;

namespace TaiwuEncyclopedia;

/// <summary>探针翻译统一入口(Frontend)。全部 20 项翻译集中在此。
/// 5 项纯表(Grade/Relation/Mastered/正逆练/装备部位) + 10 项 CommonUtils + 3 项 Config + 2 项其他。
/// 在 GameStateProvider 内部读完 raw 后调。</summary>
public static class ProbeTranslator
{
    // ========== 富文本去标签 ==========

    private static string? Strip(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return Regex.Replace(s, "<[^>]+>", "").Trim();
    }

    // ========== Core 纯表 5 项 ==========

    private static readonly string?[] GradeNames = { "九品","八品","七品","六品","五品","四品","三品","二品","一品" };

    public static string? TranslateGrade(sbyte grade) =>
        (grade >= 0 && grade < GradeNames.Length) ? GradeNames[grade] : null;

    public static string[] TranslateRelation(ushort bits)
    {
        if (bits == 0) return new[] { "无特殊关系" };
        var l = new List<string>();
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

    // ========== CommonUtils 10 项 ==========

    public static string? ResolveStanceName(sbyte behavior)
    { try { return Strip(CommonUtils.GetBehaviorString(behavior)); } catch { return null; } }

    public static string? ResolveFavorLevel(short favor)
    { try { return Strip(CommonUtils.GetFavorString(favor)); } catch { return null; } }

    public static string? ResolveFiveElementName(sbyte type)
    { try { return Strip(CommonUtils.GetFiveElementsNameByType(type)); } catch { return null; } }

    public static string? ResolveOrgGradeTitle(object orgInfo, sbyte gender, short age, int templateId)
    { try { return Strip(CommonUtils.GetOrganizationGradeString((GameData.Domains.Character.OrganizationInfo)orgInfo, gender, age, templateId)); } catch { return null; } }

    public static string? ResolveCharmLevel(short charm, sbyte gender, short age, short clothDisplayId, bool isFixedCharacter, bool faceVisible)
    { try { return Strip(CommonUtils.GetCharmLevelText(charm, gender, age, clothDisplayId, isFixedCharacter, faceVisible)); } catch { return null; } }

    public static string? ResolveAlertnessLevel(int alertness)
    { try { return Strip(CommonUtils.GetAlertnessNameByValue(alertness)); } catch { return null; } }

    public static string? ResolveFameName(sbyte fameType)
    { try { return Strip(CommonUtils.GetFameString(fameType)); } catch { return null; } }

    public static string? ResolveHappinessName(sbyte happiness)
    { try { return Strip(CommonUtils.GetHappinessString(happiness)); } catch { return null; } }

    public static string? ResolveGenderName(sbyte gender)
    { try { return gender == 1 ? "男" : gender == 0 ? "女" : "?"; } catch { return null; } }

    public static string? ResolveSkillGrowthName(int growthType, short actualAge)
    { try { return Strip(CommonUtils.GetSkillGrowthString(growthType, actualAge)); } catch { return null; } }

    // ========== Config 查 3 项 ==========

    public static string? ResolveSkillTypeName(sbyte type)
    { try { return Config.CombatSkillType.Instance[type]?.Name; } catch { return null; } }

    public static string? ResolveNeiliTypeName(sbyte destType)
    { try { return Config.NeiliType.Instance[destType]?.Name; } catch { return null; } }

    public static string? ResolveFeatureName(short featureId)
    { try { return Config.CharacterFeature.Instance[featureId]?.Name; } catch { return null; } }

    // ========== 其他 2 项 ==========

    public static string? ResolveReadingStateText(ushort readingState)
    {
        try
        {
            int read = CombatSkillStateHelper.GetReadPagesCount(readingState);
            int total = CombatSkillStateHelper.TotalPagesCount;
            return total > 0 ? $"已读{read}/{total}页" : $"已读{read}页";
        }
        catch { return null; }
    }

    public static string? ResolveLocationText(GameData.Domains.Map.Location location)
    {
        try { return $"区域{location.AreaId}"; }
        catch { return null; }
    }

    // ========== 门派详情(Config.Organization) ==========

    public static string? ResolveOrgName(sbyte orgTemplateId)
    { try { return Config.Organization.Instance[orgTemplateId]?.Name; } catch { return null; } }

    public static string? ResolveOrgDesc(sbyte orgTemplateId)
    { try { return Config.Organization.Instance[orgTemplateId]?.Desc; } catch { return null; } }

    public static string? ResolveOrgVow(sbyte orgTemplateId)
    { try { return Config.Organization.Instance[orgTemplateId]?.VowSpecialHint; } catch { return null; } }

    public static string? ResolveOrgStory(sbyte orgTemplateId)
    {
        try
        {
            var item = Config.Organization.Instance[orgTemplateId];
            if (item?.IsSect == true && item.SectMainStory != null)
                return item.SectMainStory.UnlockStoryDesc;
            return null;
        }
        catch { return null; }
    }

    // ========== Translate 方法(填 level 字段) ==========

    public static void Translate(TaiwuSnapshot s)
    {
        s.GradeLevel ??= TranslateGrade((sbyte)s.GradeRaw);
        s.GenderName ??= ResolveGenderName((sbyte)s.GenderRaw);
        s.StanceName ??= ResolveStanceName((sbyte)s.StanceRaw);
        s.FameName ??= ResolveFameName((sbyte)s.Fame);
        // HappinessName: jianghu-youling 不翻译, 直接给 raw 值; CommonUtils 返回"不详"不可靠
        // s.HappinessName 留 null, LLM 从 Happiness raw 值理解
        s.CharmLevel ??= ResolveCharmLevel((short)s.Charm, (sbyte)s.GenderRaw, (short)s.Age, 0, false, true);
        s.AlertnessLevel ??= ResolveAlertnessLevel(s.Alertness);
        s.NeiliTypeName ??= ResolveNeiliTypeName((sbyte)s.NeiliTypeRaw);
        s.FiveElementName ??= ResolveFiveElementName((sbyte)s.FiveElementIndex);
        s.CombatSkillGrowthName ??= ResolveSkillGrowthName(s.CombatSkillGrowthType, (short)s.Age);
        s.LifeSkillGrowthName ??= ResolveSkillGrowthName(s.LifeSkillGrowthType, (short)s.Age);
        // FeatureNames
        if (s.FeatureIds != null && s.FeatureIds.Length > 0 && s.FeatureNames == null)
        {
            var names = new List<string>();
            foreach (var fid in s.FeatureIds)
            {
                var n = ResolveFeatureName((short)fid);
                if (!string.IsNullOrEmpty(n)) names.Add(n);
            }
            s.FeatureNames = names.ToArray();
        }
        // SectName/Desc/Vow/Story
        if (s.SectTemplateId > 0)
        {
            s.SectName ??= ResolveOrgName((sbyte)s.SectTemplateId);
            s.SectDesc ??= ResolveOrgDesc((sbyte)s.SectTemplateId);
            s.SectVow ??= ResolveOrgVow((sbyte)s.SectTemplateId);
            s.SectStory ??= ResolveOrgStory((sbyte)s.SectTemplateId);
        }
    }

    public static void Translate(NpcSnapshot s)
    {
        s.GradeLevel ??= TranslateGrade((sbyte)s.GradeRaw);
        s.GenderName ??= ResolveGenderName((sbyte)s.GenderRaw);
        s.StanceName ??= ResolveStanceName((sbyte)s.StanceRaw);
        s.FavorLevel ??= ResolveFavorLevel(s.FavorRaw);
        s.RelationFlags ??= TranslateRelation(s.RelationBits);
        s.CharmLevel ??= ResolveCharmLevel((short)s.Charm, (sbyte)s.GenderRaw, (short)s.Age, 0, false, true);
        s.AlertnessLevel ??= ResolveAlertnessLevel(s.Alertness);
        s.FameName ??= ResolveFameName((sbyte)s.Fame);
        // HappinessName: jianghu-youling 不翻译, 直接给 raw 值; CommonUtils 返回"不详"不可靠
        // s.HappinessName 留 null, LLM 从 Happiness raw 值理解
        s.CombatSkillGrowthName ??= ResolveSkillGrowthName(s.CombatSkillGrowthType, (short)s.Age);
        s.LifeSkillGrowthName ??= ResolveSkillGrowthName(s.LifeSkillGrowthType, (short)s.Age);
        if (s.FeatureIds != null && s.FeatureIds.Length > 0 && s.FeatureNames == null)
        {
            var names = new List<string>();
            foreach (var fid in s.FeatureIds)
            {
                var n = ResolveFeatureName((short)fid);
                if (!string.IsNullOrEmpty(n)) names.Add(n);
            }
            s.FeatureNames = names.ToArray();
        }
        if (s.SectTemplateId > 0)
        {
            s.SectName ??= ResolveOrgName((sbyte)s.SectTemplateId);
            s.SectDesc ??= ResolveOrgDesc((sbyte)s.SectTemplateId);
            s.SectVow ??= ResolveOrgVow((sbyte)s.SectTemplateId);
            s.SectStory ??= ResolveOrgStory((sbyte)s.SectTemplateId);
        }
    }

    public static void Translate(CombatSkillsSnapshot s)
    {
        foreach (var r in s.Learned)
        {
            r.GradeLevel ??= TranslateGrade((sbyte)r.GradeRaw);
            r.SkillTypeName ??= ResolveSkillTypeName((sbyte)r.SkillTypeRaw);
            r.SkillFiveElementName ??= ResolveFiveElementName((sbyte)r.SkillFiveElements);
            r.MasteredText ??= TranslateMastered(r.Mastered);
            r.PracticeText ??= TranslatePractice(r.IsPositive, r.IsReverse);
            r.PagesRead ??= ResolveReadingStateText(r.ReadingStateRaw);
        }
    }

    public static void Translate(InventorySnapshot s)
    {
        // inventory 的物品品级复用 Grade 纯表, 装备部位用 EquipmentPart
        // 目前 inventory 物品的 Grade 是 sbyte(ItemTemplateHelper.GetGrade), 装备 Slot 是 int
        // 翻译在 GameStateProvider 读取时就地完成(物品名/品级/可交易已在读取时填)
        // 这里只翻译装备部位名
        // (InventorySnapshot 目前没有 level 字段需要翻译, raw 字段已在读取时填好)
    }
}