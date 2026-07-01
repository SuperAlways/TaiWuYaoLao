namespace TaiwuEncyclopedia.Core.Soul;

/// <summary>
/// 按 WorldId 的档内 soul（该档对话历史 + soul 演化）。
/// 同一存档不同世界会话独立（spec 第 131 行）。
/// </summary>
public sealed class SoulWorld
{
    /// <summary>该档门派（如少林、峨眉）。可能跨档不同。</summary>
    public string Sect { get; set; } = "";

    /// <summary>该档游戏阶段（开局/中期/剑冢前）。</summary>
    public string Stage { get; set; } = "";

    /// <summary>该档失败经历简述。</summary>
    public string Failures { get; set; } = "";

    /// <summary>该档对话历史摘要（L2 压缩时更新）。</summary>
    public string Summary { get; set; } = "";
}
