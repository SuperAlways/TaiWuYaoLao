using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace TaiwuEncyclopedia.Core.Skills;

/// <summary>百晓册章节清单项。</summary>
public sealed class BackgroundChapterManifest
{
    /// <summary>章节唯一标识符。</summary>
    [YamlMember(Alias = "id")] public string Id { get; set; } = "";

    /// <summary>章节中文名称。</summary>
    [YamlMember(Alias = "cn_name")] public string CnName { get; set; } = "";

    /// <summary>章节概览文件相对路径。</summary>
    [YamlMember(Alias = "overview_file")] public string OverviewFile { get; set; } = "";

    /// <summary>章节详尽内容目录相对路径。</summary>
    [YamlMember(Alias = "detail_dir")] public string DetailDir { get; set; } = "";
}

/// <summary>引导 skill 清单项。</summary>
public sealed class GuidanceSkillManifest
{
    /// <summary>引导 skill 唯一标识符。</summary>
    [YamlMember(Alias = "id")] public string Id { get; set; } = "";

    /// <summary>引导 skill 中文名称。</summary>
    [YamlMember(Alias = "cn_name")] public string CnName { get; set; } = "";

    /// <summary>引导 skill 文件相对路径。</summary>
    [YamlMember(Alias = "file")] public string File { get; set; } = "";

    /// <summary>相关章节 ID 列表。</summary>
    [YamlMember(Alias = "relevant_chapters")] public List<string> RelevantChapters { get; set; } = new();
}

/// <summary>persona 清单项。</summary>
public sealed class PersonaManifest
{
    /// <summary>persona 唯一标识符。</summary>
    [YamlMember(Alias = "id")] public string Id { get; set; } = "";

    /// <summary>persona 中文名称。</summary>
    [YamlMember(Alias = "cn_name")] public string CnName { get; set; } = "";

    /// <summary>前端切换时的简介(独立于 file 指向的系统提示词全文)。</summary>
    [YamlMember(Alias = "description")] public string Description { get; set; } = "";

    /// <summary>persona 文件相对路径。</summary>
    [YamlMember(Alias = "file")] public string File { get; set; } = "";
}

/// <summary>registry.yaml 整体结构。</summary>
public sealed class SkillRegistry
{
    /// <summary>百晓册章节列表。</summary>
    [YamlMember(Alias = "background")] public List<BackgroundChapterManifest> Background { get; set; } = new();

    /// <summary>引导 skill 列表。</summary>
    [YamlMember(Alias = "guidance")] public List<GuidanceSkillManifest> Guidance { get; set; } = new();

    /// <summary>persona 列表。</summary>
    [YamlMember(Alias = "personas")] public List<PersonaManifest> Personas { get; set; } = new();

    /// <summary>通用回答规则文件相对路径。</summary>
    [YamlMember(Alias = "answer_rules_file")] public string? AnswerRulesFile { get; set; }

    /// <summary>回答格式文件相对路径。</summary>
    [YamlMember(Alias = "output_style_file")] public string? OutputStyleFile { get; set; }
}
