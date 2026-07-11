using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaiwuEncyclopedia.Core.Skills;

namespace TaiwuEncyclopedia.Core.Tools;

/// <summary>
/// 百晓册分卷按需加载（二段式，spec 第 263 行）。
/// depth=overview → 章节概述 md（第一段）
/// depth=detail   → 章节详尽 md，可指定 section（第二段）
/// </summary>
public sealed class LoadBackgroundSkillTool : ToolBase
{
    private readonly SkillManager _sm;

    /// <summary>初始化 LoadBackgroundSkillTool 实例。</summary>
    /// <param name="skillManager">技能管理器。</param>
    public LoadBackgroundSkillTool(SkillManager skillManager) : base(
        name: "load_background_skill",
        description: "加载百晓册分卷资料。百晓册是游戏官方百科，涵盖所有系统。overview=章节概述（轻量，先看这个）；detail=章节详尽内容（需要细节时用，可指定 section）。先 overview 判断是否相关，再决定 detail。",
        timeout: 10)
    {
        _sm = skillManager;
        SetParameters(new Dictionary<string, Dictionary<string, object>>
        {
            ["chapter"] = new()
            {
                ["type"] = "string",
                ["required"] = true,
                ["enum"] = _sm.GetChapterEnum(),
                ["description"] = "百晓册章节名",
            },
            ["depth"] = new()
            {
                ["type"] = "string",
                ["required"] = false,
                ["enum"] = new List<string> { "overview", "detail" },
                ["default"] = "overview",
                ["description"] = "加载深度：overview=概述, detail=详尽",
            },
            ["section"] = new()
            {
                ["type"] = "string",
                ["required"] = false,
                ["description"] = "depth=detail 时可指定子章节名，空则读整个章节",
            },
        });
    }

    /// <summary>执行背景技能加载。</summary>
    /// <param name="args">加载参数。</param>
    /// <returns>加载结果字典。</returns>
    public override Task<Dictionary<string, object>> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var chapter = args.GetValueOrDefault("chapter")?.ToString() ?? "";
        var depth = args.GetValueOrDefault("depth")?.ToString() ?? "overview";
        var section = args.GetValueOrDefault("section")?.ToString();

        if (depth == "detail")
        {
            var content = _sm.LoadChapterDetail(chapter, string.IsNullOrEmpty(section) ? null : section);
            return Task.FromResult(content != null
                ? new Dictionary<string, object> { ["chapter"] = chapter, ["depth"] = "detail", ["content"] = content }
                : new Dictionary<string, object> { ["error"] = $"章节或子章节不存在: {chapter}/{section}" });
        }

        // overview
        var overview = _sm.LoadChapterOverview(chapter);
        return Task.FromResult(overview != null
            ? new Dictionary<string, object> { ["chapter"] = chapter, ["depth"] = "overview", ["content"] = overview }
            : new Dictionary<string, object> { ["error"] = $"章节不存在或概述缺失: {chapter}" });
    }
}
