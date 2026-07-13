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
        bool inCodeBlock = false;
        var codeBuf = new StringBuilder();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // 代码块围栏
            if (line.TrimStart().StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    sb.Append(Renderers.CodeBlockRenderer.Render(codeBuf.ToString()));
                    if (i < lines.Length - 1) sb.Append('\n');
                    codeBuf.Clear();
                    inCodeBlock = false;
                }
                else
                {
                    inCodeBlock = true;
                }
                continue;
            }
            if (inCodeBlock)
            {
                if (codeBuf.Length > 0) codeBuf.Append('\n');
                codeBuf.Append(line);
                continue;
            }

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
        // 分割线
        if (line.Trim() == "---" || line.Trim() == "***") return "<mark>————————————————</mark>";

        // 标题
        int level = 0;
        while (level < line.Length && line[level] == '#' && level < 6) level++;
        if (level > 0 && line.Length > level && line[level] == ' ')
        {
            var text = line.Substring(level + 1);
            var size = settings.HeadingSizes[level - 1];
            return $"<size={size}><b>{Renderers.InlineRenderer.Render(text)}</b></size>";
        }

        // 引用
        var q = Renderers.QuoteRenderer.TryRender(line);
        if (q != null) return q;

        // 列表
        var l = Renderers.ListsRenderer.TryRender(line);
        if (l != null) return l;

        return Renderers.InlineRenderer.Render(line);
    }
}
