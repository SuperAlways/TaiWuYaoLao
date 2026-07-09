using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>
/// GET /v1/models 拉取模型列表。纯 HTTP，零 Unity 依赖。
/// 参考 WorldTalk ModelCatalogClient.Fetch 模式。
/// </summary>
public static class ModelCatalogClient
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    public static Task<ModelCatalogResult> FetchModelsAsync(string baseUrl, string apiKey)
        => FetchModelsAsync(baseUrl, apiKey, null, DefaultTimeout);

    /// <summary>注入 handler 用于测试。</summary>
    public static async Task<ModelCatalogResult> FetchModelsAsync(
        string baseUrl, string apiKey, HttpMessageHandler? handler, TimeSpan? timeout = null)
    {
        try
        {
            string? url = EndpointResolver.BuildModelsUrl(baseUrl);
            if (url == null)
                return new() { Success = false, Error = "Base URL 格式无效" };

            using var http = handler != null ? new HttpClient(handler) : new HttpClient();
            http.Timeout = timeout ?? DefaultTimeout;
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await http.GetAsync(url);
            string body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new()
                {
                    Success = false,
                    Error = ClassifyError(response.StatusCode)
                };
            }

            var models = ParseModelsResponse(body);
            return new() { Success = true, Models = models };
        }
        catch (HttpRequestException)
        {
            return new() { Success = false, Error = "网络错误" };
        }
        catch (TaskCanceledException)
        {
            return new() { Success = false, Error = "网络错误（超时）" };
        }
    }

    private static string ClassifyError(HttpStatusCode status) => (int)status switch
    {
        401 or 403 => "Key 无效或无权访问模型列表接口",
        404 or 405 => "服务商未提供模型列表接口，请手动填写模型名",
        >= 500 and < 600 => "API 基址不可用或服务商暂时不可用",
        _ => $"HTTP {(int)status}"
    };

    private static List<string> ParseModelsResponse(string body)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var root = JObject.Parse(body);

            // Try "data" array then "models" array
            foreach (var key in new[] { "data", "models" })
            {
                var arr = root[key] as JArray;
                if (arr != null)
                {
                    foreach (var item in arr)
                    {
                        if (item.Type == JTokenType.String)
                            ids.Add(item.Value<string>()!);
                        else if (item.Type == JTokenType.Object)
                        {
                            var idVal = item["id"]?.Value<string>();
                            if (idVal != null)
                                ids.Add(idVal);
                            else
                            {
                                var nameVal = item["name"]?.Value<string>();
                                if (nameVal != null)
                                    ids.Add(nameVal);
                            }
                        }
                    }
                    break; // only parse first matching array
                }
            }
        }
        catch { /* parse failure → empty list */ }

        var result = ids.ToList();
        result.Sort(StringComparer.Ordinal); // 字母序
        return result;
    }
}
