using Newtonsoft.Json;

namespace TaiwuEncyclopedia.Core.Probe.Dto;

public sealed class CombatSkillsSnapshot
{
    [JsonProperty("已学功法")] public LearnedSkillRaw[] Learned { get; set; } = System.Array.Empty<LearnedSkillRaw>();
    [JsonProperty("错误")] public string[] Errors { get; set; } = System.Array.Empty<string>();
}

public sealed class LearnedSkillRaw
{
    [JsonProperty("模板ID")] public short TemplateId { get; set; }
    [JsonProperty("名称")] public string Name { get; set; } = "";
    [JsonProperty("品级值")] public int GradeRaw { get; set; }
    [JsonProperty("品级")] public string? GradeLevel { get; set; }
    [JsonProperty("功法类型值")] public int SkillTypeRaw { get; set; }
    [JsonProperty("功法类型")] public string? SkillTypeName { get; set; }
    [JsonProperty("修习等级")] public int PracticeLevel { get; set; }
    [JsonProperty("正练")] public bool IsPositive { get; set; }
    [JsonProperty("逆练")] public bool IsReverse { get; set; }
    [JsonProperty("运功中")] public bool IsEquipped { get; set; }
    [JsonProperty("阅读状态值")] public ushort ReadingStateRaw { get; set; }
    [JsonProperty("已读页数")] public string? PagesRead { get; set; }
    [JsonProperty("当前威力")] public int Power { get; set; }
    [JsonProperty("威力上限")] public int MaxPower { get; set; }
    [JsonProperty("大成")] public bool Mastered { get; set; }
    [JsonProperty("功法描述")] public string? SkillDesc { get; set; }
    [JsonProperty("功法五行值")] public int SkillFiveElements { get; set; }
    [JsonProperty("功法五行")] public string? SkillFiveElementName { get; set; }
    [JsonProperty("大成状态")] public string? MasteredText { get; set; }
    [JsonProperty("修习状态")] public string? PracticeText { get; set; }
    [JsonProperty("突破成功")] public bool BreakSuccess { get; set; }
}
