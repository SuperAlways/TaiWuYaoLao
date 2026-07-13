namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>带错误类型 + Level 的轻量异常。Level 供 AgentLoop yield StatusEvent 用。</summary>
public sealed class ApiException : System.Exception
{
    public ApiErrorType ErrorType { get; }
    public string Level { get; }

    public ApiException(ApiErrorType type, string message, string level = "error")
        : base(message)
    {
        ErrorType = type;
        Level = level;
    }
}
