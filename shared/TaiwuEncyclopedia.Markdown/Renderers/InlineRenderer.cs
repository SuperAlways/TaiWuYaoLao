using System.Text.RegularExpressions;

namespace TaiwuEncyclopedia.Markdown.Renderers;

/// <summary>行内 markdown 标签 → TMP 富文本。处理加粗/斜体/行内代码/链接。</summary>
public static class InlineRenderer
{
    // 顺序敏感：先处理 *** 再 ** 再 *，避免误匹配
    private static readonly (Regex, string)[] _rules =
    {
        (new Regex(@"\*\*\*(.+?)\*\*\*", RegexOptions.Compiled), "<i><b>$1</b></i>"),
        (new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled), "<b>$1</b>"),
        (new Regex(@"\*(.+?)\*", RegexOptions.Compiled), "<i>$1</i>"),
        (new Regex(@"`(.+?)`", RegexOptions.Compiled), "<mark>$1</mark>"),
        (new Regex(@"\[(.+?)\]\(([^)]+)\)", RegexOptions.Compiled), "<link=\"$2\"><color=#4a9eff>$1</color></link>"),
    };

    /// <summary>对一行文本应用所有行内规则。</summary>
    public static string Render(string line)
    {
        if (string.IsNullOrEmpty(line)) return line;
        foreach (var (regex, replacement) in _rules)
        {
            line = regex.Replace(line, replacement);
        }
        return line;
    }
}
