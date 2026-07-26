using Newtonsoft.Json;

namespace TaiwuEncyclopedia.Core.Probe.Dto;

public sealed class NpcSnapshot
{
    [JsonProperty("角色ID")] public int CharId { get; set; }
    [JsonProperty("名号")] public string Name { get; set; } = "";
    [JsonProperty("性别值")] public int GenderRaw { get; set; }
    [JsonProperty("性别")] public string? GenderName { get; set; }
    [JsonProperty("年龄")] public int Age { get; set; }
    [JsonProperty("立场值")] public int StanceRaw { get; set; }
    [JsonProperty("立场")] public string? StanceName { get; set; }
    [JsonProperty("好感值")] public short FavorRaw { get; set; }
    [JsonProperty("好感")] public string? FavorLevel { get; set; }
    [JsonProperty("关系位")] public ushort RelationBits { get; set; }
    [JsonProperty("关系")] public string[]? RelationFlags { get; set; }
    [JsonProperty("门派ID")] public int SectTemplateId { get; set; }
    [JsonProperty("门派")] public string? SectName { get; set; }
    [JsonProperty("门派简介")] public string? SectDesc { get; set; }
    [JsonProperty("门派誓约")] public string? SectVow { get; set; }
    [JsonProperty("门派背景")] public string? SectStory { get; set; }
    [JsonProperty("品级值")] public int GradeRaw { get; set; }
    [JsonProperty("品级")] public string? GradeLevel { get; set; }
    [JsonProperty("身份头衔")] public string? OrgFullTitle { get; set; }
    [JsonProperty("精纯值")] public int ConsummateLevel { get; set; }
    [JsonProperty("精纯品级")] public string? ConsummateGrade { get; set; }
    [JsonProperty("魅力值")] public int Charm { get; set; }
    [JsonProperty("魅力等级")] public string? CharmLevel { get; set; }
    [JsonProperty("戒心值")] public int Alertness { get; set; }
    [JsonProperty("戒心")] public string? AlertnessLevel { get; set; }
    [JsonProperty("特性ID")] public int[] FeatureIds { get; set; } = System.Array.Empty<int>();
    [JsonProperty("五行名")] public string[]? FiveElementKeys { get; set; }
    [JsonProperty("特性")] public string[]? FeatureNames { get; set; }
    [JsonProperty("当前气血")] public int Health { get; set; }
    [JsonProperty("气血上限")] public int MaxHealth { get; set; }
    [JsonProperty("心情值")] public int Happiness { get; set; }
    [JsonProperty("心情")] public string? HappinessName { get; set; }
    [JsonProperty("名誉值")] public int Fame { get; set; }
    [JsonProperty("名誉")] public string? FameName { get; set; }
    [JsonProperty("七元赋性值")] public sbyte[] Personalities { get; set; } = System.Array.Empty<sbyte>();
    [JsonProperty("七元赋性")] public string[]? PersonalityKeys { get; set; }
    [JsonProperty("所在地")] public string? LocationText { get; set; }
    [JsonProperty("存活状态值")] public int AliveState { get; set; }
    [JsonProperty("存活状态")] public string? AliveStateName { get; set; }
    [JsonProperty("蛊毒满身")] public bool CompletelyInfected { get; set; }
    [JsonProperty("影响力")] public int InfluencePower { get; set; }
    // 属性层
    [JsonProperty("当前主属性")] public short[] CurMainAttributes { get; set; } = new short[6];
    [JsonProperty("主属性名")] public string[]? MainAttributeKeys { get; set; }
    [JsonProperty("主属性上限")] public short[] MaxMainAttributes { get; set; } = new short[6];
    [JsonProperty("命中")] public int[] AtkHit { get; set; } = new int[4];
    [JsonProperty("命中名")] public string[]? AtkHitKeys { get; set; }
    [JsonProperty("破体")] public int AtkPenetrateOuter { get; set; }
    [JsonProperty("破气")] public int AtkPenetrateInner { get; set; }
    [JsonProperty("化解")] public int[] DefHit { get; set; } = new int[4];
    [JsonProperty("化解名")] public string[]? DefHitKeys { get; set; }
    [JsonProperty("御体")] public int DefPenetrateOuter { get; set; }
    [JsonProperty("御气")] public int DefPenetrateInner { get; set; }
    [JsonProperty("架势恢复")] public int StanceRecovery { get; set; }
    [JsonProperty("提气恢复")] public int BreathRecovery { get; set; }
    [JsonProperty("移动速度")] public int MoveSpeed { get; set; }
    [JsonProperty("步伐稳健")] public int FlawRecovery { get; set; }
    [JsonProperty("施展速度")] public int CastSpeed { get; set; }
    [JsonProperty("引气冲关")] public int BlockedAcupointRecovery { get; set; }
    [JsonProperty("武具运用")] public int AttackSpeed { get; set; }
    [JsonProperty("攻击速度")] public int WeaponSwitchSpeed { get; set; }
    [JsonProperty("内功发挥")] public int InnerRatio { get; set; }
    [JsonProperty("调息吐纳")] public int QiDisorderRecovery { get; set; }
    [JsonProperty("外毒抗")] public int PoisonResistOuter { get; set; }
    [JsonProperty("内毒抗")] public int PoisonResistInner { get; set; }
    // 资质层
    [JsonProperty("武学资质")] public short[] CombatSkillQualifications { get; set; } = new short[14];
    [JsonProperty("武学类型名")] public string[]? CombatSkillTypeKeys { get; set; }
    [JsonProperty("武学造诣")] public short[] CombatSkillAttainments { get; set; } = new short[14];
    [JsonProperty("武学成长值")] public int CombatSkillGrowthType { get; set; }
    [JsonProperty("武学成长")] public string? CombatSkillGrowthName { get; set; }
    [JsonProperty("技艺资质")] public short[] LifeSkillQualifications { get; set; } = System.Array.Empty<short>();
    [JsonProperty("技艺类型名")] public string[]? LifeSkillTypeKeys { get; set; }
    [JsonProperty("技艺造诣")] public short[] LifeSkillAttainments { get; set; } = System.Array.Empty<short>();
    [JsonProperty("技艺成长值")] public int LifeSkillGrowthType { get; set; }
    [JsonProperty("技艺成长")] public string? LifeSkillGrowthName { get; set; }
    [JsonProperty("神力")] public int DivinePower { get; set; }
    [JsonProperty("鬼术")] public int GhostTechnique { get; set; }
    [JsonProperty("错误")] public string[] Errors { get; set; } = System.Array.Empty<string>();
}
