using Newtonsoft.Json.Linq;

namespace TaiwuEncyclopedia.Core.Rag;

/// <summary>
/// 将 RAG API 响应 JSON 解析为 RagRetrieveResult。
/// 从 RagTransportHost.RetrieveAsync 中提取，供 IRagClient 的不同实现复用。
/// </summary>
public static class RagResponseParser
{
    /// <summary>
    /// 解析 RAG API 响应 JSON 字符串为 RagRetrieveResult。
    /// 解析失败时设置 Error = "parse_failed"，不抛异常。
    /// </summary>
    public static RagRetrieveResult Parse(string json)
    {
        var result = new RagRetrieveResult();
        try
        {
            var obj = JObject.Parse(json);
            result.Context = obj["context"]?.ToString() ?? "";

            var refsArr = obj["references"] as JArray;
            if (refsArr != null)
            {
                foreach (var r in refsArr)
                {
                    result.References.Add(new Reference
                    {
                        FullDocId = r["full_doc_id"]?.ToString() ?? "",
                        FilePath = r["file_path"]?.ToString() ?? "",
                        SourceUrl = r["source_url"]?.ToString() ?? "",
                        SourceType = r["source_type"]?.ToString() ?? "",
                        KnowledgeType = r["knowledge_type"]?.ToString() ?? "",
                        Author = r["author"]?.ToString() ?? "",
                        GameVersion = r["game_version"]?.ToString() ?? "",
                        Snippet = r["snippet"]?.ToString() ?? "",
                        HitCount = r["hit_count"]?.Value<int>() ?? 0,
                    });
                }
            }
        }
        catch
        {
            result.Error = "parse_failed";
        }
        return result;
    }
}
