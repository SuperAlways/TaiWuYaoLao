using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TaiwuBaixiaoceImporter;

/// <summary>链接条目：链接文本 + 去锚点后的源相对路径。</summary>
public sealed record LinkEntry(string Text, string TargetPath);

/// <summary>两遍清洗：Pass 1 去 HTML span 标签；Pass 2 替换 .md 链接为 [查:xxx]。</summary>
public static class MarkdownCleaner
{
    // Pass 1: 去所有 <span ...> 和 </span> 标签
    private static readonly Regex _spanPattern = new(
        @"</?span[^>]*>",
        RegexOptions.Compiled);

    // Pass 2: 匹配 [文本](路径.md) 或 [文本](路径.md#锚点)
    private static readonly Regex _linkPattern = new(
        @"\[([^\]]+)\]\(([^)]+\.md(?:#[^)]*)?)\)",
        RegexOptions.Compiled);

    /// <summary>清洗一段 md 文本，返回清洗后文本和提取的链接条目。</summary>
    /// <param name="md">源 md 文本。</param>
    /// <param name="sourceFilePath">源文件相对路径（用于调试，目前未使用）。</param>
    /// <returns>清洗后文本和链接条目列表。</returns>
    public static (string CleanedText, List<LinkEntry> Links) Clean(string md, string sourceFilePath)
    {
        // Pass 1: 去 span 标签（包括 broken span 如 <span>[文本</span>](path)）
        var pass1 = _spanPattern.Replace(md, "");

        // Pass 2: 替换 .md 链接为 [查:文本]，收集链接条目
        var links = new List<LinkEntry>();
        var pass2 = _linkPattern.Replace(pass1, match =>
        {
            var text = match.Groups[1].Value;
            var rawPath = match.Groups[2].Value;
            // 去锚点
            var anchorIdx = rawPath.IndexOf('#');
            var path = anchorIdx >= 0 ? rawPath.Substring(0, anchorIdx) : rawPath;
            links.Add(new LinkEntry(text, path));
            return $"[查:{text}]";
        });

        return (pass2, links);
    }
}