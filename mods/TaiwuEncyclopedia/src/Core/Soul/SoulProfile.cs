using System.Collections.Generic;

namespace TaiwuEncyclopedia.Core.Soul;

/// <summary>
/// 跨档全局 soul（玩家偏好/技术水平/提问习惯）。不随存档切换变化。
/// 搬 v0.5 soul_data 但拆分：跨档字段放这里，档内字段放 SoulWorld。
/// </summary>
public sealed class SoulProfile
{
    /// <summary>玩法偏好（苟道流/速通/全收集）。跨档稳定。</summary>
    public string Playstyle { get; set; } = "";

    /// <summary>技术水平（新手/老手/精通）。跨档稳定。</summary>
    public string TechnicalLevel { get; set; } = "";

    /// <summary>提问习惯（偏好问什么类型的问题）。跨档稳定。</summary>
    public string QuestionHabits { get; set; } = "";

    /// <summary>玩家主动填的字段列表，L2 提取不覆盖。</summary>
    public List<string> ProtectedFields { get; set; } = new();
}
