namespace TaiwuEncyclopedia.Markdown;

/// <summary>Markdown 渲染配置（字号/配色）。默认值面向 TMP。</summary>
public sealed class MarkdownRenderingSettings
{
    /// <summary>H1~H6 字号（px），索引 0=H1。默认递减。</summary>
    public int[] HeadingSizes { get; set; } = { 28, 24, 20, 18, 16, 16 };

    /// <summary>默认配置单例。</summary>
    public static MarkdownRenderingSettings Default { get; } = new();
}
