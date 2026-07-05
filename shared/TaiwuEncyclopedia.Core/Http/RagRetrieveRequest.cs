using System.Collections.Generic;
using Newtonsoft.Json;

namespace TaiwuEncyclopedia.Core.Http;

/// <summary>调 taiwuasker RAG API 的请求体。参照 v0.5 RetrieveLightragContextTool 参数。
/// [JsonProperty] 显式映射 snake_case —— taiwuasker 是 Pydantic(Python),期望 snake_case 字段名。</summary>
public sealed class RagRetrieveRequest
{
    /// <summary>检索查询关键词</summary>
    [JsonProperty("query")]
    public string Query { get; set; } = string.Empty;

    /// <summary>检索模式，默认 hybrid</summary>
    [JsonProperty("mode")]
    public string Mode { get; set; } = "hybrid";

    /// <summary>高级关键词列表</summary>
    [JsonProperty("hl_keywords")]
    public List<string> HlKeywords { get; set; } = new();

    /// <summary>低级关键词列表</summary>
    [JsonProperty("ll_keywords")]
    public List<string> LlKeywords { get; set; } = new();

    /// <summary>返回结果数量，默认15</summary>
    [JsonProperty("top_k")]
    public int TopK { get; set; } = 15;

    /// <summary>分块检索返回数量，默认8</summary>
    [JsonProperty("chunk_top_k")]
    public int ChunkTopK { get; set; } = 8;
}