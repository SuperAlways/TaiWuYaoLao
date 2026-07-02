using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace TaiwuBaixiaoceImporter;

/// <summary>聚合链接条目，解析目标路径，输出 concept_index.json。</summary>
public sealed class ConceptIndexBuilder
{
    private readonly Dictionary<string, ConceptEntry> _entries = new();
    private readonly HashSet<string> _conflictReported = new();

    /// <summary>警告回调。默认写 Console.Error。同名冲突时调用。</summary>
    public System.Action<string>? Warning { get; set; } = System.Console.Error.WriteLine;

    /// <summary>添加一个链接条目，自动解析目标路径。</summary>
    /// <param name="link">链接条目（文本 + 去锚点路径）。</param>
    /// <param name="sourceFileRelativePath">源文件相对路径（相对源根目录，如 "门派/门派一览/少林派.md"）。</param>
    public void Add(LinkEntry link, string sourceFileRelativePath)
    {
        var resolved = ResolvePath(link.TargetPath, sourceFileRelativePath);
        if (resolved == null) return;

        if (_entries.TryGetValue(link.Text, out var existing))
        {
            if (existing.Path != resolved.Path && !_conflictReported.Contains(link.Text))
            {
                _conflictReported.Add(link.Text);
                Warning?.Invoke($"概念名冲突: '{link.Text}' → '{existing.Path}' vs '{resolved.Path}'，需人工确认消歧");
            }
            return;
        }
        _entries[link.Text] = resolved;
    }

    /// <summary>构建 concept_index.json 字符串。</summary>
    /// <returns>JSON 文本。</returns>
    public string Build()
    {
        return JsonConvert.SerializeObject(_entries, Formatting.Indented);
    }

    private static ConceptEntry? ResolvePath(string targetPath, string sourceFileRelativePath)
    {
        // targetPath 是相对源文件的路径，如 "../../../词条/功法/易筋经.md"
        // sourceFileRelativePath 是源文件相对源根的路径，如 "门派/门派一览/少林派.md"
        // 解析：组合后规范化，得到相对源根的路径
        var sourceDir = Path.GetDirectoryName(sourceFileRelativePath) ?? "";
        var combined = Path.Combine(sourceDir, targetPath);
        var normalized = NormalizeRelative(combined);

        var parts = normalized.Split('/');

        // 词条路径：词条/<子类>/<文件名>.md → glossary/<子类>/<文件名>
        if (parts.Length >= 3 && parts[0] == "词条")
        {
            var subdir = parts[1];
            var filename = parts[^1];
            return new ConceptEntry($"glossary/{subdir}/{filename}", "glossary");
        }

        // 章节路径：<章节目录>/<...>/<文件名>.md → background/<chapter-id>/detail/<文件名>
        var mapping = ChapterMapping.ResolveBySourceDir(parts[0]);
        if (mapping != null)
        {
            var filename = parts[^1];
            return new ConceptEntry($"background/{mapping.Id}/detail/{filename}", "section");
        }

        return null;
    }

    private static string NormalizeRelative(string combined)
    {
        var parts = new List<string>();
        foreach (var seg in combined.Replace('\\', '/').Split('/'))
        {
            if (seg == "" || seg == ".") continue;
            if (seg == "..")
            {
                if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
                continue;
            }
            parts.Add(seg);
        }
        return string.Join('/', parts);
    }

    private sealed class ConceptEntry
    {
        [JsonProperty("path")] public string Path { get; set; } = "";
        [JsonProperty("type")] public string Type { get; set; } = "";

        public ConceptEntry(string path, string type)
        {
            Path = path;
            Type = type;
        }
    }
}