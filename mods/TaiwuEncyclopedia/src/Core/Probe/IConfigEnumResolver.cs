using TaiwuEncyclopedia.Core.Probe.Dto;

namespace TaiwuEncyclopedia.Core.Probe;

/// <summary>Config/CommonUtils 查表翻译接口(Frontend 实现, Core 不引游戏DLL)。
/// 复杂翻译(多参数/阈值映射)走游戏 CommonUtils, Config 查走 Config 表。
/// Core 不给 API 的简单翻译在 ProbeEnumResolver 纯表里。</summary>
public interface IConfigEnumResolver
{
    string? ResolveSkillTypeName(sbyte type);
    string? ResolveNeiliTypeName(sbyte destType);
    string? ResolveStanceName(sbyte behavior);
    string? ResolveFavorLevel(short favor);
    string? ResolveFiveElementName(sbyte type);
    string? ResolveOrgGradeTitle(object orgInfo, sbyte gender, short age, int templateId);
    string? ResolveCharmLevel(short charm, sbyte gender, short age, short clothDisplayId, bool isFixedCharacter, bool faceVisible);
    string? ResolveAlertnessLevel(int alertness);
    string? ResolveFameName(sbyte fameType);
    string? ResolveHappinessName(sbyte happiness);
    string? ResolveGenderName(sbyte gender);
    string? ResolveSkillGrowthName(int growthType, short actualAge);
    string? ResolveFeatureName(short featureId);
    string? ResolveLocationText(object location);
    string? ResolveOrgDesc(sbyte orgTemplateId);
    string? ResolveOrgVow(sbyte orgTemplateId);
    string? ResolveOrgStory(sbyte orgTemplateId);
    string? ResolveReadingStateText(ushort readingState);
}
