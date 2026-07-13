using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaiwuEncyclopedia.Core.Rag;

namespace TaiwuEncyclopedia.Core.Tools;

/// <summary>
/// 调 taiwuasker RAG 检索。搬 v0.5 RetrieveLightragContextTool，但改 HTTP 调用。
/// 服务端钳制 top_k [5,40] / chunk_top_k [3,20]（v0.5 同款边界）。
/// </summary>
public sealed class RetrieveRagTool : ToolBase
{
    private readonly IRagClient _ragClient;

    /// <summary>是否启用 RAG 远程检索。关闭时 ExecuteAsync 直接返回 rag_disabled。</summary>
    public bool RagEnabled { get; set; } = true;

    /// <summary>初始化 RetrieveRagTool 实例。</summary>
    /// <param name="ragClient">RAG 检索客户端。</param>
    public RetrieveRagTool(IRagClient ragClient) : base(
        name: "retrieve_rag",
        description: "从太吾绘卷知识库中检索信息。返回实体、关系、原文片段。适用于需要查阅游戏机制、攻略、资料的场景。",
        timeout: 60)
    {
        _ragClient = ragClient;
        SetParameters(new Dictionary<string, Dictionary<string, object>>
        {
            ["query"] = new()
            {
                ["type"] = "string",
                ["required"] = true,
                ["description"] = "检索查询文本。根据玩家问题和已加载的 Skill 信息生成最可能命中资料的查询语句。",
            },
            ["mode"] = new()
            {
                ["type"] = "string",
                ["required"] = false,
                ["enum"] = new List<string> { "local", "global", "hybrid", "mix", "naive" },
                ["default"] = "hybrid",
                ["description"] = "检索模式：local=查具体名词；global=查体系框架；hybrid=混合(默认)；mix=图谱+向量综合；naive=纯原文片段",
            },
            ["hl_keywords"] = new()
            {
                ["type"] = "array",
                ["required"] = false,
                ["items"] = new { type = "string" },
                ["description"] = "高层关键词（宏观概念），驱动关系检索。",
            },
            ["ll_keywords"] = new()
            {
                ["type"] = "array",
                ["required"] = false,
                ["items"] = new { type = "string" },
                ["description"] = "低层关键词（具体名词），驱动实体检索。",
            },
            ["top_k"] = new()
            {
                ["type"] = "integer",
                ["required"] = false,
                ["minimum"] = 5,
                ["maximum"] = 40,
                ["default"] = 15,
                ["description"] = "检索实体/关系数量。建议 local=10-15, global=15-20, hybrid=15(默认)。",
            },
            ["chunk_top_k"] = new()
            {
                ["type"] = "integer",
                ["required"] = false,
                ["minimum"] = 3,
                ["maximum"] = 20,
                ["default"] = 8,
                ["description"] = "最终返回的原文片段数量上限（rerank 后）。建议 5-10。",
            },
        });
    }

    /// <summary>执行 RAG 检索。</summary>
    /// <param name="args">检索参数。</param>
    /// <returns>检索结果字典。</returns>
    public override async Task<Dictionary<string, object>> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
    {
        if (!RagEnabled)
        {
            return new Dictionary<string, object>
            {
                ["context"] = "",
                ["references"] = new List<Reference>(),
                ["retrieval_info"] = new Dictionary<string, object> { ["source"] = "rag_disabled" },
                ["error"] = "rag_disabled",
            };
        }

        var query = args.GetValueOrDefault("query")?.ToString() ?? "";
        var mode = args.GetValueOrDefault("mode")?.ToString() ?? "hybrid";
        var topK = 15;
        var chunkTopK = 8;
        if (args.TryGetValue("top_k", out var tk)) topK = System.Convert.ToInt32(tk);
        if (args.TryGetValue("chunk_top_k", out var ctk)) chunkTopK = System.Convert.ToInt32(ctk);
        // 服务端钳制
        topK = System.Math.Max(5, System.Math.Min(topK, 40));
        chunkTopK = System.Math.Max(3, System.Math.Min(chunkTopK, 20));

        // 空 query 守卫: LLM 偶尔漏填,直接返回错误,省一次 HTTP 422
        if (string.IsNullOrWhiteSpace(query))
            return new Dictionary<string, object> { ["error"] = "retrieve_rag 需要非空 query 参数,请重新调用并填入有效检索关键词。" };

        var hlKeywords = ToStringList(args.GetValueOrDefault("hl_keywords"));
        var llKeywords = ToStringList(args.GetValueOrDefault("ll_keywords"));

        Core.Diagnostics.CoreLog.Write("TE.RAG", $"retrieve_rag: query='{(query?.Length > 80 ? query[..80] : query)}' mode={mode}");
        var ragResult = await _ragClient.RetrieveAsync(new RagRetrieveRequest
        {
            Query = query,
            Mode = mode,
            HlKeywords = hlKeywords,
            LlKeywords = llKeywords,
            TopK = topK,
            ChunkTopK = chunkTopK,
        }, ct);

        Core.Diagnostics.CoreLog.Write("TE.RAG",
            string.IsNullOrEmpty(ragResult.Error)
                ? $"retrieve_rag OK: ctx={ragResult.Context?.Length ?? 0}ch refs={ragResult.References?.Count ?? 0}"
                : $"retrieve_rag FAIL: {(ragResult.Error?.Length > 120 ? ragResult.Error[..120] : ragResult.Error)}");

        return new Dictionary<string, object>
        {
            ["context"] = ragResult.Context,
            ["references"] = ragResult.References,
            ["retrieval_info"] = new Dictionary<string, object>
            {
                ["mode"] = mode,
                ["query"] = query,
                ["top_k"] = topK,
                ["chunk_top_k"] = chunkTopK,
            },
            ["error"] = ragResult.Error ?? string.Empty,
        };
    }

    private static List<string> ToStringList(object? obj)
    {
        if (obj is Newtonsoft.Json.Linq.JArray arr)
        {
            var list = new List<string>();
            foreach (var item in arr) list.Add(item.ToString());
            return list;
        }
        if (obj is List<string> ls) return ls;
        return new List<string>();
    }
}
