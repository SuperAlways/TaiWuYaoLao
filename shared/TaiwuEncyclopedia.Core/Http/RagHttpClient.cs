using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TaiwuEncyclopedia.Core.Http;

/// <summary>
/// 包装 taiwuasker RAG API 调用。
/// 10s 超时；失败返回空上下文 + 警告不阻塞。
/// HttpMessageHandler 可 mock 用于测试。
/// </summary>
public sealed class RagHttpClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private static readonly HttpClient _sharedHttp = new() { Timeout = System.TimeSpan.FromSeconds(10) };

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
    /// <param name="timeoutSeconds">超时时间，默认10秒</param>
    public RagHttpClient(HttpMessageHandler? handler, string baseUrl, int timeoutSeconds = 10)
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
    /// <returns>检索到的上下文文本，失败或无结果时返回空字符串</returns>
    public async Task<string> RetrieveAsync(RagRetrieveRequest request)
    {
        try
        {
            var url = _baseUrl + "/api/retrieve";
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync(url, content);
            if (!resp.IsSuccessStatusCode)
            {
                return "";
            }
            var body = await resp.Content.ReadAsStringAsync();
            var obj = JObject.Parse(body);
            return obj["context"]?.ToString() ?? "";
        }
        catch
        {
            // RAG 失败不阻塞 agent，返回空上下文让 agent 降级
            return "";
        }
    }
}