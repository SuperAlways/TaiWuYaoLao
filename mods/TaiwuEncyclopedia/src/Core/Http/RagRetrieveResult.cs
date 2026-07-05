using System.Collections.Generic;

namespace TaiwuEncyclopedia.Core.Http;

/// <summary>RAG 检索返回结果（context 文本 + 富化 references + 错误标识）。</summary>
public sealed class RagRetrieveResult
{
    /// <summary>检索到的上下文文本（已格式化，可直接回写 LLM）。</summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>富化后的参考文献列表（Top-5，按 hit_count desc）。</summary>
    public List<Reference> References { get; set; } = new();

    /// <summary>错误标识：null=成功，"timeout"=超时，"unreachable"=连接失败。让 agent 区分"无结果"与"服务不可用"。</summary>
    public string? Error { get; set; }
}
