using System.Collections.Generic;

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
    public int WorldId { get; set; }

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
    public string Name { get; set; } = "";

    /// <summary>
    /// 工具调用参数。
    /// </summary>
    public Dictionary<string, object> Args { get; set; } = new();

    /// <summary>
    /// 显示文本。
    /// </summary>
    public string DisplayText { get; set; } = "";

    /// <summary>
    /// 当前迭代次数。
    /// </summary>
    public int Iteration { get; set; }

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
    public string Name { get; set; } = "";

    /// <summary>
    /// 当前迭代次数。
    /// </summary>
    public int Iteration { get; set; }

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
    public string Content { get; set; } = "";

    /// <summary>
    /// 当前迭代次数。
    /// </summary>
    public int Iteration { get; set; }

    /// <summary>
    /// 初始化 FinalChunkEvent。
    /// </summary>
    public FinalChunkEvent() { Type = "final_chunk"; }
}

/// <summary>
/// Agent 运行结束事件。
/// </summary>
public sealed class EndEvent : AgentEvent
{
    /// <summary>
    /// 总思考时间（毫秒）。
    /// </summary>
    public int ThinkingTimeMs { get; set; }

    /// <summary>
    /// 总迭代次数。
    /// </summary>
    public int TotalIterations { get; set; }

    /// <summary>
    /// 初始化 EndEvent。
    /// </summary>
    public EndEvent() { Type = "end"; }
}
