using System.Collections.Generic;

namespace TaiwuEncyclopedia.Core.Http;

/// <summary>调 taiwuasker RAG API 的请求体。参照 v0.5 RetrieveLightragContextTool 参数。</summary>
public sealed class RagRetrieveRequest
{
    /// <summary>检索查询关键词</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>检索模式，默认 hybrid</summary>
    public string Mode { get; set; } = "hybrid";

    /// <summary>高级关键词列表</summary>
    public List<string> HlKeywords { get; set; } = new();

    /// <summary>低级关键词列表</summary>
    public List<string> LlKeywords { get; set; } = new();

    /// <summary>返回结果数量，默认15</summary>
    public int TopK { get; set; } = 15;

    /// <summary>分块检索返回数量，默认8</summary>
    public int ChunkTopK { get; set; } = 8;
}