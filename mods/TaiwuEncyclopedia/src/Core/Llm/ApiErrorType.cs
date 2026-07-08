namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>LLM API 错误分类。spec §3.1。</summary>
public enum ApiErrorType
{
    Success,
    Timeout,
    NetworkError,
    RateLimit,
    Overload,
    AuthError,
    ClientError,
    ServerError,
    Unknown,
}
