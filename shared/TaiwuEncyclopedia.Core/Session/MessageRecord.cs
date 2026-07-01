namespace TaiwuEncyclopedia.Core.Session;

/// <summary>对话流中的一条消息记录。</summary>
public sealed class MessageRecord
{
    /// <summary>角色（user / assistant / tool）。</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>消息内容。</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>工具名称。</summary>
    public string? ToolName { get; set; }

    /// <summary>工具调用 ID。</summary>
    public string? ToolCallId { get; set; }

    /// <summary>时间戳。</summary>
    public System.DateTime Timestamp { get; set; } = System.DateTime.UtcNow;
}
