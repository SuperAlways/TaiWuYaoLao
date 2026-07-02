using Newtonsoft.Json;

namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>LLM 返回的一次工具调用。</summary>
public sealed class ToolCall
{
    /// <summary>调用 ID。</summary>
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;

    /// <summary>类型（固定 "function"）。</summary>
    [JsonProperty("type")] public string Type { get; set; } = "function";

    /// <summary>函数调用详情。</summary>
    [JsonProperty("function")]
    public ToolCallFunction Function { get; set; } = new();
}

/// <summary>工具调用的函数信息。</summary>
public sealed class ToolCallFunction
{
    /// <summary>函数名。</summary>
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;

    /// <summary>参数（JSON 字符串）。</summary>
    [JsonProperty("arguments")] public string Arguments { get; set; } = string.Empty;
}