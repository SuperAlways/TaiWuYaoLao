namespace TaiwuEncyclopedia.Core.Http;

/// <summary>参考文献项（对齐 taiwuasker /api/retrieve 响应的 references 元素）。</summary>
public sealed class Reference
{
    /// <summary>文档唯一 ID（文章级，跨 chunk 共享）。</summary>
    public string FullDocId { get; set; } = string.Empty;

    /// <summary>源文件路径。</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>来源 URL（非空时前端可点击跳转）。</summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>来源类型（wiki/bbs/官方等）。</summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>知识类型（机制/攻略/剧情等）。</summary>
    public string KnowledgeType { get; set; } = string.Empty;

    /// <summary>作者。</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>适用游戏版本。</summary>
    public string GameVersion { get; set; } = string.Empty;

    /// <summary>命中文本片段（前 200 字）。</summary>
    public string Snippet { get; set; } = string.Empty;

    /// <summary>命中次数（跨轮累积，用于排序）。</summary>
    public int HitCount { get; set; }
}
