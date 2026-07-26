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
        if ((bits & 512) > 0) l.Add("结义");
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

    public static string? ResolveHappinessName(sbyte happiness) =>
        (happiness >= 0 && happiness < HappinessLevelNames.Length) ? HappinessLevelNames[happiness] : null;

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
        try
        {
            if (!location.IsValid()) return null;
            var wmm = SingletonObject.getInstance<WorldMapModel>();
            if (wmm == null) return null;
            var area = Strip(wmm.GetAreaName(location.AreaId));
            var block = Strip(wmm.GetBlockName(location));
            if (string.IsNullOrEmpty(area) && string.IsNullOrEmpty(block)) return null;
            if (string.IsNullOrEmpty(block) || area == block) return area;
            return string.IsNullOrEmpty(area) ? block : $"{area}·{block}";
        }
        catch { return null; }
    }

    public static string? ResolveAliveStateName(int aliveState) =>
        (aliveState >= 0 && aliveState < AliveStateNames.Length) ? AliveStateNames[aliveState] : "未知";

    // 精纯 0-18 -> 品级 (Config.ConsummateLevel 查表, 失败用 (level-2)/2 兜底). 参考 worldtalk ResolveConsummateGrade.
    public static string? ResolveConsummateGrade(int level)
    {
        if (level < 0) return null;
        int grade;
        try { grade = Config.ConsummateLevel.Instance[(sbyte)level]?.Grade ?? ((level - 2) / 2); }
        catch { grade = (level - 2) / 2; }
        grade = grade < 0 ? 0 : grade > 8 ? 8 : grade;
        return TranslateGrade((sbyte)grade);
    }

    // ========== 数组 Key 常量 ==========

    // 权威值: Language_CN/ui_language.txt LK_HappinessLevel_0..6
    private static readonly string[] HappinessLevelNames = { "悲极","痛苦","沮丧","寻常","开怀","欢喜","乐极" };
    // 共识: AliveState==1||==2 为已故 (江湖有灵/worldtalk 一致)
    private static readonly string[] AliveStateNames = { "存活","已故","已故" };

    // 权威值: 反编译 TaiwuEventTagHandler.PersonalityTypeName + Language_CN/ui_language.txt 交叉验证
    // (沉稳/热忱/勇毅/幸运/洞察 是按英文 key 字面硬翻的旧值, 全错; 游戏UI实际用 冷静/热情/勇壮/福缘/合道)
    private static readonly string[] PersonalityKeyNames = { "冷静","聪颖","热情","勇壮","坚毅","福缘","合道" };
    private static readonly string[] MainAttributeKeyNames = { "膂力","灵敏","定力","体质","根骨","悟性" };

    // 权威值: Language_CN/ui_language.txt LK_HitType_0..3 / LK_AvoidType_0..3
    private static readonly string[] AtkHitKeyNames = { "力道","精妙","迅疾","动心" };
    private static readonly string[] DefHitKeyNames = { "卸力","拆招","闪避","守心" };

    // 权威值: Language_CN/ui_language.txt LK_FiveElements_Type_0..4 (旧硬编码 金木水火土 是错的)
    private static readonly string[] FiveElementFallbackNames = { "金刚","紫霞","玄阴","纯阳","归元" };
    private static string[]? _fiveElementKeys;
    private static string[]? FiveElementKeysCache => _fiveElementKeys ??= BuildFiveElementKeys();
    private static string[]? BuildFiveElementKeys()
    {
        try
        {
            var keys = new string[5];
            for (int i = 0; i < 5; i++)
            {
                var name = Strip(LocalStringManager.Get($"LK_FiveElements_Type_{i}"));
                keys[i] = string.IsNullOrEmpty(name) ? FiveElementFallbackNames[i] : name;
            }
            return keys;
        }
        catch { return FiveElementFallbackNames; }
    }

    // 权威值: Config.ResourceType (8 项资源). 无硬编码兜底, 失败留 null.
    private static string[]? _resourceKeys;
    private static string[]? ResourceKeysCache => _resourceKeys ??= BuildResourceKeys();
    private static string[]? BuildResourceKeys()
    {
        try
        {
            var keys = new System.Collections.Generic.List<string>();
            for (sbyte i = 0; i < 8; i++)
            {
                var item = Config.ResourceType.Instance[i];
                if (item == null) break;
                keys.Add(item.Name);
            }
            return keys.Count > 0 ? keys.ToArray() : null;
        }
        catch { return null; }
    }

    // 权威值: Config.CombatSkillType (14 项功法类型). 失败留 null.
    private static string[]? _combatSkillTypeKeys;
    private static string[]? CombatSkillTypeKeysCache => _combatSkillTypeKeys ??= BuildCombatSkillTypeKeys();
    private static string[]? BuildCombatSkillTypeKeys()
    {
        try
        {
            var keys = new System.Collections.Generic.List<string>();
            for (sbyte i = 0; ; i++)
            {
                var item = Config.CombatSkillType.Instance[i];
                if (item == null) break;
                keys.Add(item.Name);
            }
            return keys.Count > 0 ? keys.ToArray() : null;
        }
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
        s.HappinessName ??= ResolveHappinessName((sbyte)s.Happiness);
        s.AliveStateName ??= ResolveAliveStateName(s.AliveState);
        s.ConsummateGrade ??= ResolveConsummateGrade(s.ConsummateLevel);
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
        // 填充平行 Key 数组
        s.PersonalityKeys ??= PersonalityKeyNames;
        s.MainAttributeKeys ??= MainAttributeKeyNames;
        s.ResourceKeys ??= ResourceKeysCache;
        s.CombatSkillTypeKeys ??= CombatSkillTypeKeysCache;
        s.FiveElementKeys ??= FiveElementKeysCache;
        s.AtkHitKeys ??= AtkHitKeyNames;
        s.DefHitKeys ??= DefHitKeyNames;
        // LifeSkillTypeKeys 从 Config 动态获取
        if (s.LifeSkillTypeKeys == null)
        {
            try
            {
                var keys = new System.Collections.Generic.List<string>();
                for (sbyte i = 0; ; i++)
                {
                    var item = Config.LifeSkillType.Instance[i];
                    if (item == null) break;
                    keys.Add(item.Name);
                }
                s.LifeSkillTypeKeys = keys.ToArray();
            }
            catch { }
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
        s.HappinessName ??= ResolveHappinessName((sbyte)s.Happiness);
        s.AliveStateName ??= ResolveAliveStateName(s.AliveState);
        s.ConsummateGrade ??= ResolveConsummateGrade(s.ConsummateLevel);
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
        // 填充平行 Key 数组
        s.PersonalityKeys ??= PersonalityKeyNames;
        s.MainAttributeKeys ??= MainAttributeKeyNames;
        s.CombatSkillTypeKeys ??= CombatSkillTypeKeysCache;
        s.FiveElementKeys ??= FiveElementKeysCache;
        s.AtkHitKeys ??= AtkHitKeyNames;
        s.DefHitKeys ??= DefHitKeyNames;
        if (s.LifeSkillTypeKeys == null)
        {
            try
            {
                var keys = new System.Collections.Generic.List<string>();
                for (sbyte i = 0; ; i++)
                {
                    var item = Config.LifeSkillType.Instance[i];
                    if (item == null) break;
                    keys.Add(item.Name);
                }
                s.LifeSkillTypeKeys = keys.ToArray();
            }
            catch { }
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