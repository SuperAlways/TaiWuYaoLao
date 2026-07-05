using System.Collections.Generic;
using Newtonsoft.Json;

namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>LLM 消息（OpenAI chat completions 格式）。</summary>
public sealed class LlmMessage
{
    /// <summary>角色（system/user/assistant/tool）。</summary>
    [JsonProperty("role")] public string Role { get; set; } = string.Empty;

    /// <summary>消息内容。</summary>
    [JsonProperty("content")] public string? Content { get; set; }

    /// <summary>工具调用 ID（tool 角色消息回传时用）。</summary>
    [JsonProperty("tool_call_id")] public string? ToolCallId { get; set; }

    /// <summary>assistant 发起的工具调用列表。</summary>
    [JsonProperty("tool_calls")]
    public List<ToolCall>? ToolCalls { get; set; }
}