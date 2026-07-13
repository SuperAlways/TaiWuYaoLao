using System.Text.RegularExpressions;

namespace TaiwuEncyclopedia.Markdown.Renderers;

/// <summary>引用块 &gt; → 缩进 + 色块标记。</summary>
public static class QuoteRenderer
{
    private static readonly Regex _quote = new(@"^\s*>\s?(.*)$", RegexOptions.Compiled);

    /// <summary>若是引用行返回渲染结果，否则 null。</summary>
    public static string? TryRender(string line)
    {
        var m = _quote.Match(line);
        if (!m.Success) return null;
        return "<color=#888888>│ </color>" + InlineRenderer.Render(m.Groups[1].Value);
    }
}
