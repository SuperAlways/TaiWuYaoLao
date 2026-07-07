namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>单次 LLM 调用的 token 用量。</summary>
public sealed class TokenUsage
{
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int CacheHitTokens { get; init; }
}
