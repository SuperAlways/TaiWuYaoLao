using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>
/// 从 OpenAiCompatibleClient 提取的 JSON 构建/解析逻辑。
/// BuildBody: 构造 chat completions 请求体 JSON。
/// ParseResponse: 解析非流式响应。
/// ParseChunk: 解析流式 SSE chunk。
/// </summary>
public static class ChatResponseParser
{
    /// <summary>构建 OpenAI chat completions 请求体 JSON 字符串。</summary>
    public static string BuildBody(
        string model,
        List<LlmMessage> messages,
        bool stream,
        List<Dictionary<string, object>>? tools = null,
        string toolChoice = "auto",
        int maxTokens = 0)
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
            body["tool_choice"] = toolChoice;
        }
        if (maxTokens > 0) body["max_tokens"] = maxTokens;
        return JsonConvert.SerializeObject(body, Formatting.None);
    }

    /// <summary>解析非流式 chat completions 响应。</summary>
    public static LlmResponse ParseResponse(string json, AgentLLMRole role)
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
            resp.Usage = ParseUsage(usage);

        return resp;
    }

    /// <summary>解析流式 SSE chunk。仅含 usage 时返回 usage-only chunk；空 choices 返回 null；有 finish_reason 但无内容时返回 finish-reason-only chunk。</summary>
    public static StreamChunk? ParseChunk(string json)
    {
        var chunk = JObject.Parse(json);
        var usage = chunk["usage"];
        var choices = chunk["choices"] as JArray;

        if (usage != null && (choices == null || choices.Count == 0))
            return new StreamChunk { Usage = ParseUsage(usage) };

        if (choices == null || choices.Count == 0)
            return null;

        var content = choices[0]["delta"]?["content"]?.ToString();
        var finishReason = choices[0]["finish_reason"]?.ToString();

        if (string.IsNullOrEmpty(content) && string.IsNullOrEmpty(finishReason))
            return null;

        return new StreamChunk
        {
            Content = string.IsNullOrEmpty(content) ? null : content,
            FinishReason = string.IsNullOrEmpty(finishReason) ? null : finishReason,
        };
    }

    /// <summary>解析 OpenAI usage JSON 对象为 TokenUsage。</summary>
    private static TokenUsage ParseUsage(JToken usage)
    {
        return new TokenUsage
        {
            PromptTokens = usage["prompt_tokens"]?.Value<int>() ?? 0,
            CompletionTokens = usage["completion_tokens"]?.Value<int>() ?? 0,
            CacheHitTokens = usage["prompt_cache_hit_tokens"]?.Value<int>()
                             ?? usage["prompt_tokens_details"]?["cached_tokens"]?.Value<int>()
                             ?? 0,
        };
    }
}

/// <summary>流式 chunk 最小 DTO（携带 content、finish_reason 或 usage）。</summary>
public sealed class StreamChunk
{
    /// <summary>增量内容片段。</summary>
    public string? Content { get; init; }

    /// <summary>结束原因（如 "stop"、"tool_calls"），仅末尾 chunk 携带。</summary>
    public string? FinishReason { get; init; }

    /// <summary>末尾 chunk 的 token 用量。</summary>
    public TokenUsage? Usage { get; init; }
}
