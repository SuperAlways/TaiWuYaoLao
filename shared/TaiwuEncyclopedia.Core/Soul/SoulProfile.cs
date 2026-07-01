namespace TaiwuEncyclopedia.Core.Soul;

/// <summary>跨档全局 soul（玩家偏好/技术水平/提问习惯）。Task 11 完善字段。</summary>
public sealed class SoulProfile
{
    /// <summary>摘要。</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>受保护字段列表。</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:不要公开泛型列表", Justification = "Task 2 spec 明确要求 List<string>，Task 3 JSON 序列化和 Task 11 SoulManager 依赖此类型")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:集合属性应为只读", Justification = "Task 2 spec 明确要求 setter，Task 11 SoulManager 可能需要重新赋值列表")]
    public System.Collections.Generic.List<string> ProtectedFields { get; set; } = [];
}
