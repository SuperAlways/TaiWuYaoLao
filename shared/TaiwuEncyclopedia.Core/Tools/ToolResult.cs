namespace TaiwuEncyclopedia.Core.Tools;

/// <summary>工具执行结果，用于 role=tool 消息。Content 是 JSON string。</summary>
public sealed class ToolResult
{
    /// <summary>工具调用 ID。</summary>
    public string CallId { get; set; } = "";

    /// <summary>工具执行结果内容（JSON 字符串）。</summary>
    public string Content { get; set; } = "";
}
