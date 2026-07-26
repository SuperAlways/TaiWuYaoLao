namespace TaiwuEncyclopedia.Core.Probe.Dto;

public sealed class NpcSnapshot
{
    public int CharId { get; set; }
    public string Name { get; set; } = "";
    public int GenderRaw { get; set; }
    public string? GenderName { get; set; }
    public int Age { get; set; }
    public int StanceRaw { get; set; }
    public string? StanceName { get; set; }
    public short FavorRaw { get; set; }
    public string? FavorLevel { get; set; }
    public ushort RelationBits { get; set; }
    public string[]? RelationFlags { get; set; }
    public int SectTemplateId { get; set; }
    public string? SectName { get; set; }
    public string? SectDesc { get; set; }
    public string? SectVow { get; set; }
    public string? SectStory { get; set; }
    public int GradeRaw { get; set; }
    public string? GradeLevel { get; set; }
    public int ConsummateLevel { get; set; }
    public int Charm { get; set; }
    public string? CharmLevel { get; set; }
    public int Alertness { get; set; }
    public string? AlertnessLevel { get; set; }
    public int[] FeatureIds { get; set; } = System.Array.Empty<int>();
    public string[]? FeatureNames { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int Happiness { get; set; }
    public string? HappinessName { get; set; }
    public int Fame { get; set; }
    public string? FameName { get; set; }
    public sbyte[] Personalities { get; set; } = System.Array.Empty<sbyte>();
    public string? LocationText { get; set; }
    public int AliveState { get; set; }
    public bool CompletelyInfected { get; set; }
    public int InfluencePower { get; set; }
    // 属性层(同 TaiwuSnapshot 属性字段)
    public short[] CurMainAttributes { get; set; } = new short[6];
    public short[] MaxMainAttributes { get; set; } = new short[6];
    public int AtkHitOuter { get; set; }
    public int AtkHitInner { get; set; }
    public int AtkPenetrateOuter { get; set; }
    public int AtkPenetrateInner { get; set; }
    public int DefHitOuter { get; set; }
    public int DefHitInner { get; set; }
    public int DefPenetrateOuter { get; set; }
    public int DefPenetrateInner { get; set; }
    public int MoveSpeed { get; set; }
    public int CastSpeed { get; set; }
    public int AttackSpeed { get; set; }
    public int InnerRatio { get; set; }
    public int PoisonResistOuter { get; set; }
    public int PoisonResistInner { get; set; }
    // 资质层(同 TaiwuSnapshot 资质字段)
    public short[] CombatSkillQualifications { get; set; } = new short[14];
    public short[] CombatSkillAttainments { get; set; } = new short[14];
    public int CombatSkillGrowthType { get; set; }
    public string? CombatSkillGrowthName { get; set; }
    public short[] LifeSkillQualifications { get; set; } = System.Array.Empty<short>();
    public short[] LifeSkillAttainments { get; set; } = System.Array.Empty<short>();
    public int LifeSkillGrowthType { get; set; }
    public string? LifeSkillGrowthName { get; set; }
    public int DivinePower { get; set; }
    public int GhostTechnique { get; set; }
    public string[] Errors { get; set; } = System.Array.Empty<string>();
}