using System.Collections.Generic;
using TaiwuEncyclopedia.Core.Llm;

namespace TaiwuEncyclopedia.Core.Diagnostics;

/// <summary>
/// Agent 交互追踪接口。关闭时用 NullAgentTrace（空实现，零开销）。
/// BeginSession 必须最先调用（生成 sessionId），后续事件都带此 sessionId。
/// </summary>
public interface IAgentTrace
{
    /// <summary>开始一次对话会话。必须最先调用，生成 sessionId。</summary>
    void BeginSession(string query, int worldId, string? personaId);

    /// <summary>pre_react 各阶段记录。detail 含步骤相关数据（messages/tokens 等）。</summary>
    void ContextStep(string step, int durationMs, Dictionary<string, object> detail);

    /// <summary>LLM 调用前：完整 messages + tools schema。</summary>
    void LlmCall(int iteration, string role, string trigger,
        List<LlmMessage> messages, List<Dictionary<string, object>>? tools);

    /// <summary>LLM 返回后：content + toolCalls + finishReason + usage + 耗时。</summary>
    void LlmResponse(int iteration, string role, string? content,
        List<ToolCall>? toolCalls, string finishReason, TokenUsage? usage, int durationMs);

    /// <summary>工具调用前：name + callId + 完整 args。</summary>
    void ToolCall(string name, string callId, Dictionary<string, object> args, int iteration);

    /// <summary>工具返回后：name + callId + 完整 content。</summary>
    void ToolResult(string name, string callId, string content, int iteration);

    /// <summary>结束会话。</summary>
    void EndSession(int thinkingTimeMs, int totalIterations, int finalAnswerChars, TokenUsage totalUsage);
}
