using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaiwuEncyclopedia.Core.Llm;

namespace TaiwuEncyclopedia.Core.Agent;

/// <summary>
/// LLM 客户端接口。Core 定义契约，Frontend 层用 UnityWebRequest 实现。
/// AgentRunner 通过此接口解耦 HTTP 传输。
/// </summary>
public interface ILlmClient
{
    /// <summary>流式 LLM 调用，yield 返回内容 chunks。</summary>
    IAsyncEnumerable<string> StreamChatAsync(
        LlmConfig config, List<LlmMessage> messages, CancellationToken ct);

    /// <summary>非流式 LLM 调用。</summary>
    Task<LlmResponse> ChatAsync(
        AgentLLMRole role, LlmConfig config, List<LlmMessage> messages,
        List<Dictionary<string, object>>? tools = null);
}
