using System.Collections.Generic;
using TaiwuEncyclopedia.Core.Llm;

namespace TaiwuEncyclopedia.Core.Diagnostics;

/// <summary>
/// 空实现。AgentTrace=false 时使用，JIT 内联为零开销。
/// </summary>
public sealed class NullAgentTrace : IAgentTrace
{
    public static readonly NullAgentTrace Instance = new();

    public void BeginSession(string query, int worldId, string? personaId) { }
    public void ContextStep(string step, int durationMs, Dictionary<string, object> detail) { }
    public void LlmCall(int iteration, string role, string trigger,
        List<LlmMessage> messages, List<Dictionary<string, object>>? tools) { }
    public void LlmResponse(int iteration, string role, string? content,
        List<ToolCall>? toolCalls, string finishReason, TokenUsage? usage, int durationMs) { }
    public void ToolCall(string name, string callId, Dictionary<string, object> args, int iteration) { }
    public void ToolResult(string name, string callId, string content, int iteration) { }
    public void EndSession(int thinkingTimeMs, int totalIterations, int finalAnswerChars, TokenUsage totalUsage) { }
}
