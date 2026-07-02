using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TaiwuEncyclopedia.Core.Http;

/// <summary>
/// 包装 taiwuasker RAG API 调用。
/// 调 POST {baseUrl}/api/retrieve（taiwuasker 侧新建的纯检索路由）。
/// 60s 超时（小服务器慢检索）；失败返回带 Error 标识的 RagRetrieveResult，不抛异常。
/// HttpMessageHandler 可 mock 用于测试。
/// </summary>
public sealed class RagHttpClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    /// <summary>共享 HttpClient（无 handler 模式用），60s 超时。</summary>
    private static readonly HttpClient _sharedHttp = new() { Timeout = System.TimeSpan.FromSeconds(60) };

    /// <summary>
    /// 使用默认 HttpClient 初始化 RagHttpClient 实例
    /// </summary>
    /// <param name="baseUrl">taiwuasker API 基础地址</param>
    public RagHttpClient(string baseUrl) : this(null, baseUrl) { }

    /// <summary>
    /// 使用自定义 HttpMessageHandler 初始化 RagHttpClient 实例
    /// </summary>
    /// <param name="handler">自定义 HttpMessageHandler（用于测试 mock）</param>
    /// <param name="baseUrl">taiwuasker API 基础地址</param>
    /// <param name="timeoutSeconds">超时时间，默认60秒</param>
    public RagHttpClient(HttpMessageHandler? handler, string baseUrl, int timeoutSeconds = 60)
    {
        if (handler != null)
        {
            _http = new HttpClient(handler) { Timeout = System.TimeSpan.FromSeconds(timeoutSeconds) };
        }
        else
        {
            _http = _sharedHttp;
        }
        _baseUrl = baseUrl.TrimEnd('/');
    }

    /// <summary>
    /// 调用 taiwuasker /api/retrieve 接口进行 RAG 检索
    /// </summary>
    /// <param name="request">RAG 检索请求参数</param>
    /// <returns>检索结果（context + references + error 标识）；失败时 Context 为空、Error 标明原因</returns>
    public async Task<RagRetrieveResult> RetrieveAsync(RagRetrieveRequest request)
    {
        var result = new RagRetrieveResult();
        try
        {
            var url = _baseUrl + "/api/retrieve";
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync(url, content);
            if (!resp.IsSuccessStatusCode)
            {
                result.Error = "unreachable";
                return result;
            }
            var body = await resp.Content.ReadAsStringAsync();
            var obj = JObject.Parse(body);
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
            return result;
        }
        catch (System.Threading.Tasks.TaskCanceledException)
        {
            // HttpClient 超时抛 TaskCanceledException
            result.Error = "timeout";
            return result;
        }
        catch
        {
            // 连接失败、JSON 解析失败等
            result.Error = "unreachable";
            return result;
        }
    }
}
