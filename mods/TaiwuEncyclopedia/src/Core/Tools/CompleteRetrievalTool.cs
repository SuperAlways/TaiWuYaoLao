using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TaiwuEncyclopedia.Core.Tools;

/// <summary>
/// 检索完成信号工具。Think 阶段调此工具标记检索完成，
/// 交接 topics_found/missing 给 final 阶段。无实际检索逻辑。
/// </summary>
public sealed class CompleteRetrievalTool : ToolBase
{
    public CompleteRetrievalTool()
        : base("complete_retrieval",
            "确认所有必要信息已检索完毕。调用此工具标记检索完成，系统将自动进入回答阶段。" +
            "重点说明本次检索中未查清的信息（missing 字段），帮助后续阶段判断哪些内容不该编造。")
    {
        SetParameters(new Dictionary<string, Dictionary<string, object>>
        {
            ["confirmed"] = new Dictionary<string, object>
            {
                ["type"] = "boolean",
                ["description"] = "是否确认检索完毕。填 true 表示信息足够，系统自动进入回答阶段。"
            },
            ["topics_found"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "已检索到的主题关键词（20字以内，如\"毒术,暗器功法\"）。仅列关键词，不要写句子。"
            },
            ["missing"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "未检索到的信息（如\"具体伤害数值待查\"）。这是最重要的字段——后续回答阶段据此判断哪些内容不该编造。无则填\"无\"。"
            },
        });
    }

    /// <summary>纯信号工具，无实际逻辑。直接返回参数供 AgentLoop 交接。</summary>
    public override Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> args, CancellationToken ct = default)
    {
        return Task.FromResult(args);
    }
}
