using System.Collections.Generic;
using Newtonsoft.Json;

namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>LLM 消息（OpenAI chat completions 格式）。</summary>
public sealed class LlmMessage
{
    [JsonProperty("role")] public string Role { get; set; } = string.Empty;
    [JsonProperty("content")] public string? Content { get; set; }
    [JsonProperty("tool_call_id")] public string? ToolCallId { get; set; }

    [JsonProperty("tool_calls")]
    public List<ToolCall>? ToolCalls { get; set; }
}