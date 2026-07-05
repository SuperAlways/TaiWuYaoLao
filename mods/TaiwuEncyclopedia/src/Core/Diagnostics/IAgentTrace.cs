// mods/TaiwuEncyclopedia/src/Core/Diagnostics/IAgentTrace.cs
using TaiwuEncyclopedia.Core.Llm;

namespace TaiwuEncyclopedia.Core.Diagnostics;

public interface IAgentTrace
{
    void BeginRequest(string query, int worldId);
    void LlmRequest(AgentLLMRole role, int messagesCount, int toolsCount);
    void LlmResponse(string finishReason, int promptTokens, int completionTokens);
    void ToolCall(string name, string args, int iteration);
    void ToolResult(string name, string resultSummary, int iteration);
    void EndRequest(int totalIterations, int totalTokens);
}
