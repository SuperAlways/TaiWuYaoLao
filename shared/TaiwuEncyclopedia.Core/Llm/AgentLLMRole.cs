namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>
/// LLM 调用角色。搬 v0.5 AgentLLMRole。
/// THINKING=中间轮非流式带 tools；ANSWER=最终轮流式不带 tools；INTENT=soul 摘要用非流式。
/// </summary>
public enum AgentLLMRole
{
    /// <summary>中间轮，非流式，带 tools。</summary>
    Thinking,

    /// <summary>最终轮，流式，不带 tools。</summary>
    Answer,

    /// <summary>L2 摘要/soul 提取，非流式。</summary>
    Intent,

    /// <summary>连接测试用，非流式，不带 tools。</summary>
    Testing,
}
