using System;
using System.Collections.Generic;
using System.IO;

namespace TaiwuBaixiaoceImporter;

/// <summary>清洗脚本入口。从源百晓册目录生成 Skills/ 下的 background/ + glossary/ + concept_index.json + registry.yaml。</summary>
public static class Program
{
    /// <summary>CLI 入口。</summary>
    /// <param name="args">命令行参数：--source &lt;源目录&gt; --output &lt;输出目录&gt;</param>
    public static int Main(string[] args)
    {
        var source = "";
        var output = "";
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--source") source = args[i + 1];
            if (args[i] == "--output") output = args[i + 1];
        }
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(output))
        {
            Console.Error.WriteLine("用法: TaiwuBaixiaoceImporter --source <源目录> --output <输出目录>");
            return 1;
        }

        Console.WriteLine($"源目录: {source}");
        Console.WriteLine($"输出目录: {output}");

        var conceptBuilder = new ConceptIndexBuilder();

        // 1. 清洗并复制 10 章背景 md
        foreach (var ch in ChapterMapping.All)
        {
            var chapterSrcDir = Path.Combine(source, ch.SourceDir);
            if (!Directory.Exists(chapterSrcDir))
            {
                Console.WriteLine($"跳过（源目录不存在）: {ch.SourceDir}");
                continue;
            }
            var detailDir = Path.Combine(output, "background", ch.Id, "detail");
            Directory.CreateDirectory(detailDir);

            var mdFiles = Directory.GetFiles(chapterSrcDir, "*.md", SearchOption.AllDirectories);
            foreach (var mdFile in mdFiles)
            {
                var content = File.ReadAllText(mdFile);
                var relPath = Path.GetRelativePath(source, mdFile).Replace('\\', '/');
                var (cleaned, links) = MarkdownCleaner.Clean(content, relPath);
                var filename = Path.GetFileName(mdFile);
                File.WriteAllText(Path.Combine(detailDir, filename), cleaned);
                foreach (var link in links)
                {
                    conceptBuilder.Add(link, relPath);
                }
            }
            Console.WriteLine($"章节 {ch.CnName}: {mdFiles.Length} md");
        }

        // 2. 原样复制词条
        var glossarySrc = Path.Combine(source, "词条");
        var glossaryDst = Path.Combine(output, "glossary");
        if (Directory.Exists(glossarySrc))
        {
            var subdirs = Directory.GetDirectories(glossarySrc);
            foreach (var subdir in subdirs)
            {
                var name = Path.GetFileName(subdir);
                var dstSubdir = Path.Combine(glossaryDst, name);
                Directory.CreateDirectory(dstSubdir);
                var mdFiles = Directory.GetFiles(subdir, "*.md");
                foreach (var mdFile in mdFiles)
                {
                    var filename = Path.GetFileName(mdFile);
                    File.Copy(mdFile, Path.Combine(dstSubdir, filename), overwrite: true);
                }
            }
            Console.WriteLine($"词条: {subdirs.Length} 子类");
        }

        // 3. 输出 concept_index.json
        var conceptJson = conceptBuilder.Build();
        File.WriteAllText(Path.Combine(output, "concept_index.json"), conceptJson);
        Console.WriteLine($"concept_index.json 已生成");

        // 4. 输出 registry.yaml
        var registry = RegistryGenerator.Generate();
        File.WriteAllText(Path.Combine(output, "registry.yaml"), registry);
        Console.WriteLine($"registry.yaml 已生成");

        Console.WriteLine("完成。");
        return 0;
    }
}
