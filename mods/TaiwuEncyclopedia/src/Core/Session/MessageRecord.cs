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

    /// <summary>参考文献列表（仅 assistant 消息携带，user 消息为 null）。</summary>
    public System.Collections.Generic.List<TaiwuEncyclopedia.Core.Http.Reference>? References { get; set; }

    /// <summary>扩展数据(react_trace 等),按需存入,默认 null。</summary>
    public System.Collections.Generic.Dictionary<string, object>? ExtData { get; set; }

    /// <summary>思考链工具调用显示文本(每行一个 display_text,纯文本无 emoji)。</summary>
    public string? ThinkingContent { get; set; }

    /// <summary>是否为压缩边界消息。</summary>
    public bool IsCompactBoundary { get; set; } = false;

    /// <summary>压缩边界摘要。</summary>
    public string? BoundarySummary { get; set; }
}
