using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaiwuEncyclopedia.Core.Skills;

namespace TaiwuEncyclopedia.Core.Tools;

/// <summary>统一概念查询工具。正文中 [查:xxx] 标记处可调用，返回词条或章节 md 全文。</summary>
public sealed class LookupConceptTool : ToolBase
{
    private readonly SkillManager _sm;

    /// <summary>初始化 LookupConceptTool 实例。</summary>
    /// <param name="skillManager">技能管理器。</param>
    public LookupConceptTool(SkillManager skillManager) : base(
        name: "lookup_concept",
        description: "查询百晓册中的概念。可查词条数据（功法/武器/宝物等具体数值）或章节内容（机制说明/系统介绍）。正文中 [查:xxx] 标记处可调用。仅在需要深入了解时调用，不要批量加载——同一概念查一次即可。",
        timeout: 10)
    {
        _sm = skillManager;
        SetParameters(new Dictionary<string, Dictionary<string, object>>
        {
            ["name"] = new()
            {
                ["type"] = "string",
                ["required"] = true,
                ["description"] = "概念名，支持 分类/名 消歧格式（如 功法/狮子吼）",
            },
        });
    }

    /// <summary>执行概念查询。</summary>
    /// <param name="args">查询参数。</param>
    /// <returns>查询结果字典。</returns>
    public override Task<Dictionary<string, object>> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var name = args.GetValueOrDefault("name")?.ToString() ?? "";
        var content = _sm.LookupConcept(name);
        return Task.FromResult(content != null
            ? new Dictionary<string, object> { ["name"] = name, ["content"] = content }
            : new Dictionary<string, object> { ["error"] = $"未找到概念: {name}" });
    }
}