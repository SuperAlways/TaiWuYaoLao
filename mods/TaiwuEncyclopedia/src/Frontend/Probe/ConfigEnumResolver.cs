using System;
using TaiwuEncyclopedia.Core.Probe;

namespace TaiwuEncyclopedia;

/// <summary>IConfigEnumResolver Frontend 实现。
/// CommonUtils 游戏API(复杂翻译) + Config 查表(类型/内力/特性) + 其他(ReadingState/Location/门派详情)。</summary>
public sealed class ConfigEnumResolver : IConfigEnumResolver
{
    public string? ResolveSkillTypeName(sbyte type)
    {
        try { return Config.CombatSkillType.Instance[type]?.Name; }
        catch { return null; }
    }

    public string? ResolveNeiliTypeName(sbyte destType)
    {
        try { return Config.NeiliType.Instance[destType]?.Name; }
        catch { return null; }
    }

    public string? ResolveStanceName(sbyte behavior)
    {
        try { return CommonUtils.GetBehaviorString(behavior); }
        catch { return null; }
    }

    public string? ResolveFavorLevel(short favor)
    {
        try { return CommonUtils.GetFavorString(favor); }
        catch { return null; }
    }

    public string? ResolveFiveElementName(sbyte type)
    {
        try { return CommonUtils.GetFiveElementsNameByType(type); }
        catch { return null; }
    }

    public string? ResolveOrgGradeTitle(object orgInfo, sbyte gender, short age, int templateId)
    {
        try { return CommonUtils.GetOrganizationGradeString((GameData.Domains.Character.OrganizationInfo)orgInfo, gender, age, templateId); }
        catch { return null; }
    }

    public string? ResolveCharmLevel(short charm, sbyte gender, short age, short clothDisplayId, bool isFixedCharacter, bool faceVisible)
    {
        try { return CommonUtils.GetCharmLevelText(charm, gender, age, clothDisplayId, isFixedCharacter, faceVisible); }
        catch { return null; }
    }

    public string? ResolveAlertnessLevel(int alertness)
    {
        try { return CommonUtils.GetAlertnessNameByValue(alertness); }
        catch { return null; }
    }

    public string? ResolveFameName(sbyte fameType)
    {
        try { return CommonUtils.GetFameString(fameType); }
        catch { return null; }
    }

    public string? ResolveHappinessName(sbyte happiness)
    {
        try { return CommonUtils.GetHappinessString(happiness); }
        catch { return null; }
    }

    public string? ResolveGenderName(sbyte gender)
    {
        try { return CommonUtils.GetGenderString((GameData.EDisplayGender)gender); }
        catch { return null; }
    }

    public string? ResolveSkillGrowthName(int growthType, short actualAge)
    {
        try { return CommonUtils.GetSkillGrowthString(growthType, actualAge); }
        catch { return null; }
    }

    public string? ResolveFeatureName(short featureId)
    {
        try { return Config.CharacterFeature.Instance[featureId]?.Name; }
        catch { return null; }
    }

    public string? ResolveLocationText(object location)
    {
        try
        {
            var loc = (GameData.Domains.Map.Location)location;
            // 简化: 用游戏 API 取地名, 参照 jianghu-youling ResolveLocationText
            var areaName = SingletonObject.getInstance<GameData.Domains.Map.MapDomain>()?.GetStateAndAreaNameByAreaId(loc.AreaId);
            return areaName?.Item2 ?? $"区域{loc.AreaId}";
        }
        catch { return null; }
    }

    public string? ResolveOrgDesc(sbyte orgTemplateId)
    {
        try { return Config.Organization.Instance[orgTemplateId]?.Desc; }
        catch { return null; }
    }

    public string? ResolveOrgVow(sbyte orgTemplateId)
    {
        try { return Config.Organization.Instance[orgTemplateId]?.VowSpecialHint; }
        catch { return null; }
    }

    public string? ResolveOrgStory(sbyte orgTemplateId)
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

    public string? ResolveReadingStateText(ushort readingState)
    {
        try
        {
            int read = GameData.Domains.CombatSkill.CombatSkillStateHelper.GetReadPagesCount(readingState);
            int total = GameData.Domains.CombatSkill.CombatSkillStateHelper.TotalPagesCount;
            return total > 0 ? $"已读{read}/{total}页" : $"已读{read}页";
        }
        catch { return null; }
    }
}
