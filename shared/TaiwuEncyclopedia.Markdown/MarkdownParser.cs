using System.Text;

namespace TaiwuEncyclopedia.Markdown;

/// <summary>
/// Markdown → TMP 富文本字符串解析器。纯逻辑，不依赖 Unity/TMP。
/// 参考 FancyTextRendering 逐行处理架构（简化版）。
/// </summary>
public static class MarkdownParser
{
    /// <summary>解析 markdown 为 TMP 富文本字符串。</summary>
    public static string Parse(string markdown, MarkdownRenderingSettings? settings = null)
    {
        if (string.IsNullOrEmpty(markdown)) return "";
        settings ??= MarkdownRenderingSettings.Default;

        var sb = new StringBuilder();
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var rendered = RenderLine(line, settings);
            if (rendered != null)
            {
                sb.Append(rendered);
                if (i < lines.Length - 1) sb.Append('\n');
            }
        }
        return sb.ToString();
    }

    private static string? RenderLine(string line, MarkdownRenderingSettings settings)
    {
        int level = 0;
        while (level < line.Length && line[level] == '#' && level < 6) level++;
        if (level > 0 && line.Length > level && line[level] == ' ')
        {
            var text = line.Substring(level + 1);
            var size = settings.HeadingSizes[level - 1];
            return $"<size={size}><b>{Renderers.InlineRenderer.Render(text)}</b></size>";
        }
        return Renderers.InlineRenderer.Render(line);
    }
}
