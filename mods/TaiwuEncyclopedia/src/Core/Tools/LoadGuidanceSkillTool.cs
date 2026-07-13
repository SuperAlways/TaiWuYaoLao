using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaiwuEncyclopedia.Core.Skills;

namespace TaiwuEncyclopedia.Core.Tools;

/// <summary>加载引导 skill（思考骨架，非知识）。搬 v0.5 LoadGuidanceSkillTool。</summary>
public sealed class LoadGuidanceSkillTool : ToolBase
{
    private readonly SkillManager _sm;

    /// <summary>初始化 LoadGuidanceSkillTool 实例。</summary>
    /// <param name="skillManager">技能管理器。</param>
    public LoadGuidanceSkillTool(SkillManager skillManager) : base(
        name: "load_guidance_skill",
        description: "加载引导骨架。引导骨架教你怎么思考一类问题（问题特征/追问维度/检索建议/回答骨架），不是知识本身。当你判断玩家问题属于某个引导类别时加载对应骨架，按骨架组织你的思考和检索策略。",
        timeout: 10)
    {
        _sm = skillManager;
        SetParameters(new Dictionary<string, Dictionary<string, object>>
        {
            ["skill"] = new()
            {
                ["type"] = "string",
                ["required"] = true,
                ["enum"] = _sm.GetGuidanceEnum(),
                ["description"] = "引导骨架名称",
            },
        });
    }

    /// <summary>执行引导技能加载。</summary>
    /// <param name="args">加载参数。</param>
    /// <returns>加载结果字典。</returns>
    public override Task<Dictionary<string, object>> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var skill = args.GetValueOrDefault("skill")?.ToString() ?? "";
        var content = _sm.LoadGuidanceSkill(skill);
        if (content == null)
            return Task.FromResult(new Dictionary<string, object> { ["error"] = $"引导骨架未找到: {skill}" });

        var entry = _sm.GetGuidanceEntry(skill);
        var skillCn = _sm.GuidanceCnName(skill);
        var chapters = entry?.RelevantChapters is { Count: > 0 } chs
            ? string.Join("、", chs)
            : null;
        var chapterHint = chapters != null
            ? $"\n\n本骨架关联百晓册章节：{chapters}。若尚未阅读相关章节概述，请先调用 load_background_skill 了解基础机制。"
            : "";

        var directive = $"[系统指令] 你已加载「{skillCn}」引导骨架。请严格按骨架的「问题特征」确认问题类型、「追问维度」引导玩家补充信息、「检索建议」调 retrieve_rag 或 load_background_skill、「回答骨架」组织最终回答。{chapterHint}\n\n---\n\n{content}";
        return Task.FromResult(new Dictionary<string, object> { ["skill"] = skill, ["content"] = directive });
    }
}
