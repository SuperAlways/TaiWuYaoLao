using System.Collections.Generic;

namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>单轮 LLM 调用的 token 用量。</summary>
public sealed class RoundUsage
{
    /// <summary>角色名称（"thinking" / "answer" / "intent"）。</summary>
    public string Role { get; set; } = "";

    /// <summary>Prompt token 数量。</summary>
    public int PromptTokens { get; set; }

    /// <summary>Completion token 数量。</summary>
    public int CompletionTokens { get; set; }

    /// <summary>缓存读取 token 数量。</summary>
    public int CacheReadTokens { get; set; }
}

/// <summary>跨轮次 token 累计。AgentRunner 结束时读总用量存 react_trace。</summary>
public sealed class TokenTracker
{
    /// <summary>总 Prompt token 数量。</summary>
    public int PromptTokens { get; private set; }

    /// <summary>总 Completion token 数量。</summary>
    public int CompletionTokens { get; private set; }

    /// <summary>总缓存读取 token 数量。</summary>
    public int CacheReadTokens { get; private set; }

    /// <summary>各轮次用量明细。</summary>
    public List<RoundUsage> Rounds { get; } = new();

    /// <summary>记录一轮 LLM 调用的 token 用量。</summary>
    /// <param name="promptTokens">Prompt token 数量。</param>
    /// <param name="completionTokens">Completion token 数量。</param>
    /// <param name="cacheReadTokens">缓存读取 token 数量。</param>
    /// <param name="role">角色名称。</param>
    public void Track(int promptTokens, int completionTokens, int cacheReadTokens, string role)
    {
        PromptTokens += promptTokens;
        CompletionTokens += completionTokens;
        CacheReadTokens += cacheReadTokens;
        Rounds.Add(new RoundUsage
        {
            Role = role,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            CacheReadTokens = cacheReadTokens,
        });
    }
}
