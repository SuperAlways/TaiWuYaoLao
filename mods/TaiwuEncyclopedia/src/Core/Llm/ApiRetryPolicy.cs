using System;

namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>重试决策：Retry 退避重试 / Fail 抛出 / TellPlayer 熔断告知玩家。</summary>
public enum RetryDecision { Retry, Fail, TellPlayer }

/// <summary>
/// API 重试纯函数决策引擎。Level 是决策基（spec §3.2 表）：
/// Retry=warn、Fail/TellPlayer=error、背景=info。不实现 GetLevel-直接内联 level。
/// </summary>
public static class ApiRetryPolicy
{
    private const int BASE_DELAY_MS = 500;
    public const int MAX_RETRIES = 4;
    public const int MAX_529_RETRIES = 3;
    private static readonly object _rngLock = new();
    private static readonly Random _rng = new();

    /// <summary>判断角色是否为前景（用户等待，重试）。背景 Intent/Testing 不重试。</summary>
    public static bool IsForeground(AgentLLMRole role) => role switch
    {
        AgentLLMRole.Thinking => true,
        AgentLLMRole.Answer => true,
        AgentLLMRole.Intent => false,
        AgentLLMRole.Testing => false,
        _ => false,
    };

    /// <summary>从 HTTP 状态码分类错误。</summary>
    public static ApiErrorType ClassifyStatus(int statusCode) => statusCode switch
    {
        408 => ApiErrorType.Timeout,
        429 => ApiErrorType.RateLimit,
        529 => ApiErrorType.Overload,
        401 or 403 => ApiErrorType.AuthError,
        >= 500 => ApiErrorType.ServerError,
        >= 400 => ApiErrorType.ClientError,
        _ => ApiErrorType.Success,
    };

    /// <summary>指数退避延迟 = BASE * 2^attempt × random(0.75~1.0)。</summary>
    public static TimeSpan GetDelay(int attempt)
    {
        double jitter;
        lock (_rngLock)
        {
            jitter = BASE_DELAY_MS * Math.Pow(2, attempt) * (_rng.NextDouble() * 0.25 + 0.75);
        }
        return TimeSpan.FromMilliseconds(jitter);
    }

    /// <summary>返回 (决策, 中文消息, Level)。consecutive529s 由调用方持有、跨调用累积。</summary>
    public static (RetryDecision Decision, string Message, string Level) Evaluate(
        ApiErrorType error, int attempt, ref int consecutive529s, bool isForeground)
    {
        if (!isForeground)
            return (RetryDecision.Fail, "", "info");

        if (error == ApiErrorType.Overload)
        {
            consecutive529s++;
            if (consecutive529s >= MAX_529_RETRIES)
                return (RetryDecision.TellPlayer, "AI 服务持续过载，请稍后再试", "error");
        }
        else
        {
            consecutive529s = 0;
        }

        if (attempt >= MAX_RETRIES)
            return (RetryDecision.Fail, GetFailMessage(error), "error");

        return error switch
        {
            ApiErrorType.Timeout or ApiErrorType.NetworkError or ApiErrorType.RateLimit
                or ApiErrorType.Overload or ApiErrorType.ServerError or ApiErrorType.ContextTooLong
                => (RetryDecision.Retry, GetRetryMessage(error), "warn"),
            _ => (RetryDecision.Fail, GetFailMessage(error), "error"),
        };
    }

    public static string GetRetryMessage(ApiErrorType error) => error switch
    {
        ApiErrorType.Timeout => "模型响应超时，正在重试...",
        ApiErrorType.NetworkError => "网络波动，正在重试...",
        ApiErrorType.RateLimit => "AI 服务繁忙，排队等待中...",
        ApiErrorType.Overload => "AI 服务过载，稍等重试中...",
        ApiErrorType.ServerError => "AI 服务暂时异常，正在重试...",
        ApiErrorType.ContextTooLong => "对话历史过长，正在压缩后重试...",
        _ => "正在重试...",
    };

    public static string GetFailMessage(ApiErrorType error) => error switch
    {
        ApiErrorType.Timeout => "AI 服务响应超时，请稍后重试",
        ApiErrorType.NetworkError => "无法连接 AI 服务，请检查网络后重试",
        ApiErrorType.RateLimit => "AI 服务持续繁忙，请稍后重试",
        ApiErrorType.Overload => "AI 服务持续过载，请稍后再试",
        ApiErrorType.AuthError => "API Key 无效或已过期，请在设置中更新",
        ApiErrorType.ClientError => "请求参数有误，请检查模型名称和 API 地址配置",
        ApiErrorType.ContextTooLong => "对话上下文过长，请尝试清除历史后重试",
        ApiErrorType.ServerError => "AI 服务异常，请稍后重试",
        _ => "未知错误，请稍后重试，如持续出现请查看日志",
    };
}
