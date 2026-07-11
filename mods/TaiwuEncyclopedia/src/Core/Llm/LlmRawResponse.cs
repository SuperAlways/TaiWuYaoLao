namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>描述一次 LLM HTTP 原始响应（纯 DTO，零依赖）。</summary>
public sealed class LlmRawResponse
{
    public int StatusCode { get; init; }
    public string? Body { get; init; }
    public string? ErrorPhrase { get; init; }
    public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;
}
