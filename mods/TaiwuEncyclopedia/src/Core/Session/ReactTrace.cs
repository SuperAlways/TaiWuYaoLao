using System.Collections.Generic;

namespace TaiwuEncyclopedia.Core.Session;

/// <summary>ReAct 循环全链路追踪,存 assistant 消息的 ExtData["react_trace"]。</summary>
public sealed class ReactTrace
{
    public int TotalIterations { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int CacheReadTokens { get; set; }
    public List<TraceRound> Rounds { get; set; } = new();
}

public sealed class TraceRound
{
    public int Iteration { get; set; }
    public string Role { get; set; } = "";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public string? FinishReason { get; set; }
    public List<TraceToolCall>? ToolCalls { get; set; }
}

public sealed class TraceToolCall
{
    public string Name { get; set; } = "";
    public string? Args { get; set; }
    public string? ResultSummary { get; set; }
    public int? ResultCount { get; set; }
}
