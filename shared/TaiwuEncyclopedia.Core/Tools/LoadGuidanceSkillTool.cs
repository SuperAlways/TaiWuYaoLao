using System.Collections.Generic;
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
    public override Task<Dictionary<string, object>> ExecuteAsync(Dictionary<string, object> args)
    {
        var skill = args.GetValueOrDefault("skill")?.ToString() ?? "";
        var content = _sm.LoadGuidanceSkill(skill);
        return Task.FromResult(content != null
            ? new Dictionary<string, object> { ["skill"] = skill, ["content"] = content }
            : new Dictionary<string, object> { ["error"] = $"引导骨架未找到: {skill}（该骨架可能尚未编写，请基于已有知识回答）" });
    }
}
