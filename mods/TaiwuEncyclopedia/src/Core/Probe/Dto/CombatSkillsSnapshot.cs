namespace TaiwuEncyclopedia.Core.Probe.Dto;

/// <summary>combat_skills 探针快照。混合 raw(阶段A填) + level(阶段B填, null 占位)。</summary>
public sealed class CombatSkillsSnapshot
{
    public LearnedSkillRaw[] Learned { get; set; } = System.Array.Empty<LearnedSkillRaw>();
    public string[] Errors { get; set; } = System.Array.Empty<string>();
}

/// <summary>单门已学功法的 raw 字段。level/text 字段阶段B填, 阶段A=null。</summary>
public sealed class LearnedSkillRaw
{
    public short TemplateId { get; set; }       // <- DisplayData.TemplateId
    public string Name { get; set; } = "";       // <- DisplayData.SkillConfig.Name
    public int GradeRaw { get; set; }            // <- DisplayData.SkillConfig.Grade (sbyte 0..8)
    public string? GradeLevel { get; set; }       // 阶段B "一品"等
    public int SkillTypeRaw { get; set; }        // <- DisplayData.Type (sbyte)
    public string? SkillTypeName { get; set; }    // 阶段B "剑法/拳法"
    public int PracticeLevel { get; set; }        // <- DisplayData.PracticeLevel (sbyte 修习度)
    public bool IsPositive { get; set; }          // <- !DisplayData.Revoked
    public bool IsReverse { get; set; }           // <- DisplayData.Revoked
    public ushort ReadingStateRaw { get; set; }   // <- DisplayData.ReadingState
    public string? PagesRead { get; set; }         // 阶段B "已读N页"
    public int Power { get; set; }                // <- DisplayData.Power
    public int MaxPower { get; set; }             // <- DisplayData.MaxPower
    public bool Mastered { get; set; }            // <- DisplayData.Mastered
}