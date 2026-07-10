namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>描述一次 LLM HTTP 请求（纯 DTO，零依赖）。</summary>
public sealed class LlmRequest
{
    public string Url { get; init; } = "";
    public string Method { get; init; } = "POST";
    public string? Body { get; init; }
    public string? ApiKey { get; init; }
    public int TimeoutMs { get; init; } = 15_000;
    public bool Stream { get; init; }
}
