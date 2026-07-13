using Newtonsoft.Json;

namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>单次 LLM 调用的 token 用量。</summary>
public sealed class TokenUsage
{
    [JsonProperty("promptTokens")]
    public int PromptTokens { get; init; }

    [JsonProperty("completionTokens")]
    public int CompletionTokens { get; init; }

    [JsonProperty("cacheHitTokens")]
    public int CacheHitTokens { get; init; }
}
