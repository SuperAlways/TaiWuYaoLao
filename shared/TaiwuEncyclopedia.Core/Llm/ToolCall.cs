using Newtonsoft.Json;

namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>LLM 返回的一次工具调用。</summary>
public sealed class ToolCall
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = "function";

    [JsonProperty("function")]
    public ToolCallFunction Function { get; set; } = new();
}

public sealed class ToolCallFunction
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("arguments")] public string Arguments { get; set; } = string.Empty;
}