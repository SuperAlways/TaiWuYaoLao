using System.Collections.Generic;

namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>LLM 非流式调用返回。流式调用通过 IAsyncEnumerable&lt;string&gt; 直接 yield chunks。</summary>
public sealed class LlmResponse
{
    /// <summary>响应内容。</summary>
    public string? Content { get; set; }

    /// <summary>工具调用列表。</summary>
    public List<ToolCall>? ToolCalls { get; set; }
}
