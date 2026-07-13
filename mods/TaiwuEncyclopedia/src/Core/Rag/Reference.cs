using Newtonsoft.Json;

namespace TaiwuEncyclopedia.Core.Rag;

/// <summary>参考文献项（对齐 taiwuasker /api/retrieve 响应的 references 元素）。</summary>
public sealed class Reference
{
    /// <summary>文档唯一 ID（文章级，跨 chunk 共享）。</summary>
    [JsonProperty("full_doc_id")]
    public string FullDocId { get; set; } = string.Empty;

    /// <summary>源文件路径。</summary>
    [JsonProperty("file_path")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>来源 URL（非空时前端可点击跳转）。</summary>
    [JsonProperty("source_url")]
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>来源类型（wiki/bbs/官方等）。</summary>
    [JsonProperty("source_type")]
    public string SourceType { get; set; } = string.Empty;

    /// <summary>知识类型（机制/攻略/剧情等）。</summary>
    [JsonProperty("knowledge_type")]
    public string KnowledgeType { get; set; } = string.Empty;

    /// <summary>作者。</summary>
    [JsonProperty("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>适用游戏版本。</summary>
    [JsonProperty("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    /// <summary>命中文本片段（前 200 字）。</summary>
    [JsonProperty("snippet")]
    public string Snippet { get; set; } = string.Empty;

    /// <summary>命中次数（跨轮累积，用于排序）。</summary>
    [JsonProperty("hit_count")]
    public int HitCount { get; set; }
}
