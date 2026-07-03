using System.Text;

namespace TaiwuBaixiaoceImporter;

/// <summary>生成 registry.yaml（10 章清单 + 现有 guidance/personas 保留）。</summary>
public static class RegistryGenerator
{
    /// <summary>生成 registry.yaml 文本。</summary>
    /// <returns>YAML 文本。</returns>
    public static string Generate()
    {
        var sb = new StringBuilder();
        sb.AppendLine("answer_rules_file: answer-rules.md");
        sb.AppendLine("output_style_file: output-style.md");
        sb.AppendLine("background:");
        foreach (var ch in ChapterMapping.All)
        {
            sb.AppendLine($"  - id: {ch.Id}");
            sb.AppendLine($"    cn_name: {ch.CnName}");
            sb.AppendLine($"    overview_file: background/{ch.Id}/overview.md");
            sb.AppendLine($"    detail_dir: background/{ch.Id}/detail");
        }
        sb.AppendLine();
        sb.AppendLine("guidance:");
        sb.AppendLine("  - id: combat-build");
        sb.AppendLine("    cn_name: 战斗 build 指引");
        sb.AppendLine("    file: guidance/combat-build.md");
        sb.AppendLine("    relevant_chapters: [zhandou]");
        sb.AppendLine();
        sb.AppendLine("personas:");
        sb.AppendLine("  - id: player-grudge");
        sb.AppendLine("    cn_name: 玩家怨念");
        sb.AppendLine("    file: personas/player-grudge.md");
        sb.AppendLine("  - id: sword-will");
        sb.AppendLine("    cn_name: 剑中虚影");
        sb.AppendLine("    file: personas/sword-will.md");
        return sb.ToString();
    }
}
