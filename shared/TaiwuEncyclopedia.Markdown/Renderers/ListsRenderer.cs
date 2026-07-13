using System.Text.RegularExpressions;

namespace TaiwuEncyclopedia.Markdown.Renderers;

/// <summary>列表行渲染：无序 → • ，有序 → 保留序号。</summary>
public static class ListsRenderer
{
    private static readonly Regex _unordered = new(@"^\s*[-*]\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex _ordered = new(@"^\s*(\d+)\.\s+(.+)$", RegexOptions.Compiled);

    /// <summary>若是列表行返回渲染结果，否则返回 null。</summary>
    public static string? TryRender(string line)
    {
        var m = _unordered.Match(line);
        if (m.Success) return "• " + InlineRenderer.Render(m.Groups[1].Value);

        var om = _ordered.Match(line);
        if (om.Success) return $"{om.Groups[1].Value}. " + InlineRenderer.Render(om.Groups[2].Value);

        return null;
    }
}
