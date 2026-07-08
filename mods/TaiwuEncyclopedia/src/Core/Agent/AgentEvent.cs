using System.Collections.Generic;
using TaiwuEncyclopedia.Core.Http;

namespace TaiwuEncyclopedia.Core.Agent;

/// <summary>
/// Agent 事件基类。搬 v0.5 events.py。
/// </summary>
public abstract class AgentEvent
{
    /// <summary>
    /// 事件类型标识。
    /// </summary>
    public string Type { get; protected set; } = "";
}

/// <summary>
/// Agent 运行开始事件。
/// </summary>
public sealed class StartEvent : AgentEvent
{
    /// <summary>
    /// 世界 ID。
    /// </summary>
    public int WorldId { get; init; }

    /// <summary>
    /// 初始化 StartEvent。
    /// </summary>
    public StartEvent() { Type = "start"; }
}

/// <summary>
/// 工具调用事件。
/// </summary>
public sealed class ToolCallEvent : AgentEvent
{
    /// <summary>
    /// 工具名称。
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// 工具调用参数。
    /// </summary>
    public Dictionary<string, object> Args { get; init; } = new();

    /// <summary>
    /// 显示文本。
    /// </summary>
    public string DisplayText { get; init; } = "";

    /// <summary>
    /// 当前迭代次数。
    /// </summary>
    public int Iteration { get; init; }

    /// <summary>
    /// 初始化 ToolCallEvent。
    /// </summary>
    public ToolCallEvent() { Type = "tool_call"; }
}

/// <summary>
/// 工具执行结果事件。
/// </summary>
public sealed class ToolResultEvent : AgentEvent
{
    /// <summary>
    /// 工具名称。
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// 当前迭代次数。
    /// </summary>
    public int Iteration { get; init; }

    /// <summary>
    /// 初始化 ToolResultEvent。
    /// </summary>
    public ToolResultEvent() { Type = "tool_result"; }
}

/// <summary>
/// 最终答案分块事件（流式输出）。
/// </summary>
public sealed class FinalChunkEvent : AgentEvent
{
    /// <summary>
    /// 答案内容块。
    /// </summary>
    public string Content { get; init; } = "";

    /// <summary>
    /// 当前迭代次数。
    /// </summary>
    public int Iteration { get; init; }

    /// <summary>
    /// 初始化 FinalChunkEvent。
    /// </summary>
    public FinalChunkEvent() { Type = "final_chunk"; }
}

/// <summary>
/// 参考文献事件（循环结束前 yield，带跨轮累积去重后的 Top-5）。
/// </summary>
public sealed class ReferencesEvent : AgentEvent
{
    /// <summary>
    /// 参考文献列表（按 hit_count desc 排序，最多 5 条）。
    /// </summary>
    public List<Reference> References { get; init; } = new();

    /// <summary>
    /// 初始化 ReferencesEvent。
    /// </summary>
    public ReferencesEvent() { Type = "references"; }
}

/// <summary>
/// 状态通知事件(用于面板消息,非 Agent 自身产出)。
/// </summary>
public sealed class StatusEvent : AgentEvent
{
    /// <summary>
    /// 状态消息内容。
    /// </summary>
    public string Message { get; init; } = "";

    /// <summary>"info" | "warn" | "error"，默认 info。warn/error 由 API 重试层发射。</summary>
    public string Level { get; init; } = "info";

    /// <summary>
    /// 初始化 StatusEvent。
    /// </summary>
    public StatusEvent() { Type = "status"; }
}

/// <summary>
/// Agent 运行结束事件。
/// </summary>
public sealed class EndEvent : AgentEvent
{
    /// <summary>
    /// 总思考时间（毫秒）。
    /// </summary>
    public int ThinkingTimeMs { get; init; }

    /// <summary>
    /// 总迭代次数。
    /// </summary>
    public int TotalIterations { get; init; }

    /// <summary>
    /// 初始化 EndEvent。
    /// </summary>
    public EndEvent() { Type = "end"; }
}

/// <summary>单次 LLM 调用的 token 用量（UI 通道，yield 给 ChatPanel 显示）。</summary>
public sealed class UsageEvent : AgentEvent
{
    public int Iteration { get; set; }
    public string Role { get; set; } = "";        // "thinking" / "answer"
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int CacheHitTokens { get; set; }
}
