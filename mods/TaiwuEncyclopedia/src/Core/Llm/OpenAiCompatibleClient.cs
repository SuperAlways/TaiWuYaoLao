using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>
/// OpenAI 兼容 LLM 客户端。支持 FC（tool_calls）+ 流式。
/// HttpClient 用 HttpMessageHandler 可 mock（spec 第 486 行）。
/// LLM 失败重试 1 次（spec 第 429 行），仅对 5xx / 网络错误重试，4xx 不重试。
/// </summary>
public sealed class OpenAiCompatibleClient
{
    private readonly HttpClient _http;

    /// <summary>Token 用量追踪器。</summary>
    public TokenTracker Tracker { get; } = new();

    /// <summary>StreamChat 最近一次调用的 usage（末尾 chunk 填充）。Chat 用 LlmResponse.Usage。</summary>
    public TokenUsage? LastStreamUsage { get; private set; }

    // 静态共享 HttpClient（非 mock 场景）。构造函数传 handler=null 时用此实例。
    private static readonly HttpClient _sharedHttp = new() { Timeout = System.TimeSpan.FromSeconds(120) };

    /// <summary>使用共享 HttpClient 创建客户端。</summary>
    public OpenAiCompatibleClient() : this(null) { }

    /// <summary>使用自定义 HttpMessageHandler 创建客户端（用于测试 mock）。</summary>
    /// <param name="handler">自定义 HTTP 消息处理器。</param>
    public OpenAiCompatibleClient(HttpMessageHandler? handler)
    {
        _http = handler != null ? new HttpClient(handler) : _sharedHttp;
    }

    /// <summary>非流式 LLM 调用，支持工具调用。</summary>
    /// <param name="role">调用角色。</param>
    /// <param name="messages">消息列表。</param>
    /// <param name="config">LLM 配置。</param>
    /// <param name="tools">工具定义列表。</param>
    /// <param name="toolChoice">工具选择策略（默认为 "auto"）。</param>
    /// <returns>LLM 响应。</returns>
    public async Task<LlmResponse> Chat(
        AgentLLMRole role,
        List<LlmMessage> messages,
        LlmConfig config,
        List<Dictionary<string, object>>? tools = null,
        string toolChoice = "auto")
    {
        var body = BuildRequestBody(config.Model, messages, stream: false, tools: tools, toolChoice: toolChoice);
        // 连接测试只需验证 auth+模型可达,限制 max_tokens=1 避免模型生成完整回复拖慢测试。
        if (role == AgentLLMRole.Testing) body["max_tokens"] = 1;
        var resp = await SendWithRetry(config, body);
        var json = await resp.Content.ReadAsStringAsync();
        return ParseChatResponse(json, role);
    }

    /// <summary>流式 LLM 调用，yield 返回内容 chunks。</summary>
    /// <param name="role">调用角色。</param>
    /// <param name="messages">消息列表。</param>
    /// <param name="config">LLM 配置。</param>
    /// <returns>内容 chunks 的异步枚举。</returns>
    public async IAsyncEnumerable<string> StreamChat(
        AgentLLMRole role,
        List<LlmMessage> messages,
        LlmConfig config)
    {
        LastStreamUsage = null;
        var body = BuildRequestBody(config.Model, messages, stream: true);
        var resp = await SendWithRetry(config, body);
        using var stream = await resp.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;
            var data = line.Substring(6);
            if (data == "[DONE]") yield break;

            var chunk = JObject.Parse(data);
            var usage = chunk["usage"];
            var choices = chunk["choices"] as JArray;
            if (usage != null && (choices == null || choices.Count == 0))
            {
                TrackUsage(usage, role);
                LastStreamUsage = ParseUsage(usage);
                continue;
            }
            if (choices == null || choices.Count == 0) continue;

            var delta = choices[0]["delta"]?["content"]?.ToString();
            if (!string.IsNullOrEmpty(delta))
            {
                yield return delta!;
            }
        }
    }

    /// <summary>构建 OpenAI chat completions 请求体。</summary>
    /// <param name="model">模型名称。</param>
    /// <param name="messages">消息列表。</param>
    /// <param name="stream">是否流式调用。</param>
    /// <param name="tools">工具定义列表。</param>
    /// <param name="toolChoice">工具选择策略。</param>
    /// <returns>请求体字典。</returns>
    private Dictionary<string, object> BuildRequestBody(
        string model,
        List<LlmMessage> messages,
        bool stream,
        List<Dictionary<string, object>>? tools = null,
        string? toolChoice = null)
    {
        var body = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = messages,
            ["stream"] = stream,
        };
        if (stream) body["stream_options"] = new { include_usage = true };
        if (tools != null)
        {
            body["tools"] = tools;
            body["tool_choice"] = toolChoice ?? "auto";
        }
        return body;
    }

    /// <summary>发送请求并在 5xx 错误时重试一次。</summary>
    /// <param name="config">LLM 配置。</param>
    /// <param name="body">请求体。</param>
    /// <returns>HTTP 响应。</returns>
    private async Task<HttpResponseMessage> SendWithRetry(LlmConfig config, Dictionary<string, object> body)
    {
        var url = config.BaseUrl.TrimEnd('/') + "/chat/completions";
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(body);

        HttpResponseMessage? resp = null;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            // StringContent 只能被一个 request 消费，每次迭代重建
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            req.Headers.Add("Authorization", "Bearer " + config.ApiKey);

            try
            {
                resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            }
            catch (HttpRequestException)
            {
                // 网络错误：第 0 次重试，第 1 次直接抛出
                if (attempt == 1)
                {
                    throw;
                }
                // 指数退避（500ms）
                await Task.Delay(500);
                continue;
            }

            // 4xx 不重试（配置错误/鉴权失败，重试无用）
            if ((int)resp.StatusCode >= 400 && (int)resp.StatusCode < 500)
            {
                return resp;
            }
            // 5xx 或成功 → 重试后返回
            if (resp.IsSuccessStatusCode)
            {
                return resp;
            }
            // 5xx：第 0 次重试，第 1 次直接返回（不再重试）
            if (attempt == 1)
            {
                return resp;
            }
            // 指数退避（500ms）
            await Task.Delay(500);
        }
        return resp!;
    }

    /// <summary>解析非流式响应。</summary>
    /// <param name="json">响应 JSON 字符串。</param>
    /// <param name="role">调用角色（用于 token 追踪）。</param>
    /// <returns>解析后的 LLM 响应。</returns>
    private LlmResponse ParseChatResponse(string json, AgentLLMRole role)
    {
        var obj = JObject.Parse(json);
        var msg = obj["choices"]?[0]?["message"];
        var resp = new LlmResponse
        {
            Content = msg?["content"]?.ToString(),
        };
        var toolCallsArr = msg?["tool_calls"] as JArray;
        if (toolCallsArr != null && toolCallsArr.Count > 0)
        {
            resp.ToolCalls = new List<ToolCall>();
            foreach (var tc in toolCallsArr)
            {
                resp.ToolCalls.Add(new ToolCall
                {
                    Id = tc["id"]?.ToString() ?? "",
                    Type = tc["type"]?.ToString() ?? "function",
                    Function = new ToolCallFunction
                    {
                        Name = tc["function"]?["name"]?.ToString() ?? "",
                        Arguments = tc["function"]?["arguments"]?.ToString() ?? "",
                    },
                });
            }
        }
        var usage = obj["usage"];
        if (usage != null)
        {
            TrackUsage(usage, role);
            resp.Usage = ParseUsage(usage);
        }
        return resp;
    }

    /// <summary>记录 token 用量到 Tracker。</summary>
    /// <param name="usage">OpenAI usage JSON 对象。</param>
    /// <param name="role">调用角色。</param>
    private void TrackUsage(JToken? usage, AgentLLMRole role)
    {
        if (usage == null) return;
        int p = usage["prompt_tokens"]?.Value<int>() ?? 0;
        int c = usage["completion_tokens"]?.Value<int>() ?? 0;
        // DeepSeek: prompt_cache_hit_tokens 顶层；OpenAI: prompt_tokens_details.cached_tokens
        int cr = usage["prompt_cache_hit_tokens"]?.Value<int>()
                 ?? usage["prompt_tokens_details"]?["cached_tokens"]?.Value<int>()
                 ?? 0;
        Tracker.Track(p, c, cr, role.ToString().ToLowerInvariant());
    }

    /// <summary>解析 OpenAI usage JSON 对象为 TokenUsage。</summary>
    /// <param name="usage">OpenAI usage JSON 对象。</param>
    /// <returns>解析后的 TokenUsage，若 usage 为 null 则返回 null。</returns>
    private static TokenUsage? ParseUsage(JToken? usage)
    {
        if (usage == null) return null;
        int p = usage["prompt_tokens"]?.Value<int>() ?? 0;
        int c = usage["completion_tokens"]?.Value<int>() ?? 0;
        int cr = usage["prompt_cache_hit_tokens"]?.Value<int>()
                 ?? usage["prompt_tokens_details"]?["cached_tokens"]?.Value<int>()
                 ?? 0;
        return new TokenUsage { PromptTokens = p, CompletionTokens = c, CacheHitTokens = cr };
    }
}
