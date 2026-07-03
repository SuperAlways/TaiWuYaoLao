using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TaiwuEncyclopedia.Core.Skills;

/// <summary>
/// 百晓册 + 引导 skill + persona 的清单管理与名称映射。
/// v0.5 硬编码 → v1.0 registry.yaml 动态注册（spec 第 218 行）。
/// </summary>
public class SkillManager
{
    private readonly string _skillsDir;
    private readonly SkillRegistry _registry;
    private readonly Dictionary<string, string> _chapterCn = new();
    private readonly Dictionary<string, string> _guidanceCn = new();
    private readonly Dictionary<string, string> _personaFile = new();
    private readonly Dictionary<string, (string Path, string Type)> _conceptIndex = new();

    /// <summary>初始化 SkillManager 实例。</summary>
    /// <param name="skillsDir">技能文件根目录路径。</param>
    public SkillManager(string skillsDir)
    {
        _skillsDir = skillsDir;
        _registry = LoadRegistry(Path.Combine(skillsDir, "registry.yaml"));
        foreach (var ch in _registry.Background) _chapterCn[ch.Id] = ch.CnName;
        foreach (var g in _registry.Guidance) _guidanceCn[g.Id] = g.CnName;
        foreach (var p in _registry.Personas) _personaFile[p.Id] = p.File;

        LoadConceptIndex();
    }

    private static SkillRegistry LoadRegistry(string path)
    {
        if (!File.Exists(path)) return new SkillRegistry();
        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        return deserializer.Deserialize<SkillRegistry>(yaml) ?? new SkillRegistry();
    }

    /// <summary>百晓册章节 enum 列表（供 LoadBackgroundSkillTool 的 chapter 参数 enum 约束）。</summary>
    public List<string> GetChapterEnum() => new(_chapterCn.Keys);

    /// <summary>引导 skill enum 列表（供 LoadGuidanceSkillTool 的 skill 参数 enum 约束）。</summary>
    public List<string> GetGuidanceEnum() => new(_guidanceCn.Keys);

    /// <summary>persona id 列表（供 ConfigPanel 下拉选择）。</summary>
    public List<string> GetPersonaIds() => new(_personaFile.Keys);

    /// <summary>获取章节中文名称。</summary>
    /// <param name="chapter">章节 ID。</param>
    /// <returns>章节中文名称，若未注册则返回原 ID。</returns>
    public string ChapterCnName(string chapter) => _chapterCn.GetValueOrDefault(chapter, chapter);

    /// <summary>获取引导 skill 中文名称。</summary>
    /// <param name="skill">引导 skill ID。</param>
    /// <returns>引导 skill 中文名称，若未注册则返回原 ID。</returns>
    public string GuidanceCnName(string skill) => _guidanceCn.GetValueOrDefault(skill, skill);

    /// <summary>读百晓册章节概览 md（二段式第一段）。</summary>
    /// <param name="chapterId">章节 ID。</param>
    /// <returns>概览内容，若文件不存在则返回 null。</returns>
    public string? LoadChapterOverview(string chapterId)
    {
        var entry = _registry.Background.Find(x => x.Id == chapterId);
        if (entry == null) return null;
        var path = Path.Combine(_skillsDir, entry.OverviewFile);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>读百晓册章节详尽 md（二段式第二段）。section 可空，空则读整个 detail_dir。</summary>
    /// <param name="chapterId">章节 ID。</param>
    /// <param name="section">可选的 section 名称，不包含 .md 扩展名。</param>
    /// <returns>详尽内容，若文件不存在则返回 null。</returns>
    public string? LoadChapterDetail(string chapterId, string? section = null)
    {
        var entry = _registry.Background.Find(x => x.Id == chapterId);
        if (entry == null) return null;
        if (string.IsNullOrEmpty(section))
        {
            // 读整个 detail_dir 下所有 md 拼接
            var dir = Path.Combine(_skillsDir, entry.DetailDir);
            if (!Directory.Exists(dir)) return null;
            var parts = new List<string>();
            foreach (var f in Directory.GetFiles(dir, "*.md").OrderBy(f => f))
            {
                parts.Add(File.ReadAllText(f));
            }
            return parts.Count > 0 ? string.Join("\n\n---\n\n", parts) : null;
        }
        var path = Path.Combine(_skillsDir, entry.DetailDir, section + ".md");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>读引导 skill md。</summary>
    /// <param name="skillId">引导 skill ID。</param>
    /// <returns>引导内容，若文件不存在则返回 null。</returns>
    public string? LoadGuidanceSkill(string skillId)
    {
        var entry = _registry.Guidance.Find(x => x.Id == skillId);
        if (entry == null) return null;
        var path = Path.Combine(_skillsDir, entry.File);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>读 persona md。</summary>
    /// <param name="personaId">persona ID。</param>
    /// <returns>persona 内容，若文件不存在则返回 null。</returns>
    public string? LoadPersona(string personaId)
    {
        if (!_personaFile.TryGetValue(personaId, out var file)) return null;
        var path = Path.Combine(_skillsDir, file);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>读百晓册总纲 overview.md（段1，常驻 system prompt 用）。</summary>
    /// <returns>总纲内容，若文件不存在则返回 null。</returns>
    public string? LoadOverview()
    {
        var path = Path.Combine(_skillsDir, "background", "overview.md");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>读通用回答规则 md。</summary>
    /// <returns>规则内容，若未配置或文件不存在则返回 null。</returns>
    public string? LoadAnswerRules()
    {
        var file = _registry.AnswerRulesFile;
        if (string.IsNullOrEmpty(file)) return null;
        var path = Path.Combine(_skillsDir, file);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>读回答格式 md。</summary>
    /// <returns>格式内容，若未配置或文件不存在则返回 null。</returns>
    public string? LoadOutputStyle()
    {
        var file = _registry.OutputStyleFile;
        if (string.IsNullOrEmpty(file)) return null;
        var path = Path.Combine(_skillsDir, file);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>加载 concept_index.json 到内存字典。</summary>
    private void LoadConceptIndex()
    {
        var indexPath = Path.Combine(_skillsDir, "concept_index.json");
        if (!File.Exists(indexPath)) return;
        var json = File.ReadAllText(indexPath);
        var obj = JObject.Parse(json);
        foreach (var prop in obj.Properties())
        {
            var path = prop.Value?["path"]?.ToString();
            var type = prop.Value?["type"]?.ToString();
            if (path != null && type != null)
            {
                _conceptIndex[prop.Name] = (path, type);
            }
        }
    }

    /// <summary>统一概念查询。查 concept_index 命中则返回文件全文。</summary>
    /// <param name="name">概念名，支持 "分类/名" 消歧格式。</param>
    /// <returns>概念对应 md 文件全文，未找到则返回 null。</returns>
    public string? LookupConcept(string name)
    {
        if (!_conceptIndex.TryGetValue(name, out var entry)) return null;
        var fullPath = Path.Combine(_skillsDir, entry.Path);
        return File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
    }
}
