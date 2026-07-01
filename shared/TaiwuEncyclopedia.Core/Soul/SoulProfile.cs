using System.Collections.ObjectModel;

namespace TaiwuEncyclopedia.Core.Soul;

/// <summary>跨档全局 soul（玩家偏好/技术水平/提问习惯）。Task 11 完善字段。</summary>
public sealed class SoulProfile
{
    /// <summary>摘要。</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>受保护字段列表。</summary>
    public Collection<string> ProtectedFields { get; } = [];
}
