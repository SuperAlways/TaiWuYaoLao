// mods/TaiwuEncyclopedia/src/Core/Diagnostics/NullAgentTrace.cs
using TaiwuEncyclopedia.Core.Llm;

namespace TaiwuEncyclopedia.Core.Diagnostics;

public sealed class NullAgentTrace : IAgentTrace
{
    public static readonly NullAgentTrace Instance = new();
    public void BeginRequest(string query, int worldId) { }
    public void LlmRequest(AgentLLMRole role, int messagesCount, int toolsCount) { }
    public void LlmResponse(string finishReason, int promptTokens, int completionTokens) { }
    public void ToolCall(string name, string args, int iteration) { }
    public void ToolResult(string name, string resultSummary, int iteration) { }
    public void EndRequest(int totalIterations, int totalTokens) { }
}
