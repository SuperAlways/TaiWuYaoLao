#pragma warning disable IDE0008, IDE0032, RCS1085, IDE0063, CA1031, CA1305, RCS1181, IDE0011, CA1054, IDE0074, IDE0058, CA1308
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaiwuEncyclopedia.Core.Agent;
using TaiwuEncyclopedia.Core.Llm;
using UnityEngine;
using UnityEngine.Networking;

namespace TaiwuEncyclopedia.Frontend.Networking;

/// <summary>
/// UnityWebRequest 实现的 LLM 传输层。实现 ILlmClient 接口，
/// 同时提供 FetchModels / TestConnection 协程供 ConfigPanel 使用。
/// </summary>
public sealed class LlmTransportHost : MonoBehaviour, ILlmClient
{
    private static LlmTransportHost? _instance;

    public static LlmTransportHost Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("TaiwuEncyclopedia_LlmTransportHost");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<LlmTransportHost>();
            }
            return _instance;
        }
    }

    // ===== ILlmClient: StreamChatAsync =====

    /// <summary>
    /// 流式 LLM 调用。通过 UnityWebRequest 下载完整 SSE 响应后逐 chunk 解析 yield。
    /// </summary>
    public async IAsyncEnumerable<StreamChunk> StreamChatAsync(
        LlmConfig config, List<LlmMessage> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var body = ChatResponseParser.BuildBody(config.Model, messages, stream: true);
        var url = EndpointResolver.BuildChatCompletionsUrl(config.BaseUrl)
                  ?? "https://api.deepseek.com/v1/chat/completions";

        var tcs = new TaskCompletionSource<string>();
        StartCoroutine(StreamChatCoroutine(url, config.ApiKey, body, tcs, ct));

        string rawText;
        try
        {
            rawText = await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            yield break;
        }

        ct.ThrowIfCancellationRequested();

        var lines = rawText.Split('\n');
        foreach (var line in lines)
        {
            if (!line.StartsWith("data: ")) continue;
            var data = line.Substring(6);
            if (data == "[DONE]") yield break;

            var chunk = ChatResponseParser.ParseChunk(data);
            if (chunk != null)
                yield return chunk;
        }
    }

    private IEnumerator StreamChatCoroutine(string url, string apiKey, string body,
        TaskCompletionSource<string> tcs, CancellationToken ct)
    {
        // Retry on initial connect for transient errors (not during streaming)
        int maxRetries = 2;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            using var request = UnityWebRequest.Post(url, body, "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 120;

            var op = request.SendWebRequest();
            while (!op.isDone)
            {
                if (ct.IsCancellationRequested)
                {
                    request.Abort();
                    tcs.TrySetCanceled(ct);
                    yield break;
                }
                yield return null;
            }

            if (ct.IsCancellationRequested)
            {
                tcs.TrySetCanceled(ct);
                yield break;
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                tcs.SetResult(request.downloadHandler.text);
                yield break;
            }

            // Bug 4: ConnectionError → responseCode=0, ClassifyStatus(0)=Success (wrong).
            // Check request.result first before ClassifyStatus.
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                // Network failure: retry if attempts remain, else fail
                if (attempt < maxRetries)
                {
                    var delay = ApiRetryPolicy.GetDelay(attempt);
                    yield return new WaitForSeconds((float)delay.TotalSeconds);
                    continue;
                }
                tcs.SetException(new ApiException(ApiErrorType.NetworkError, request.error ?? "网络错误", "error"));
                yield break;
            }

            var errorType = ApiRetryPolicy.ClassifyStatus((int)request.responseCode);
            if (errorType == ApiErrorType.AuthError || errorType == ApiErrorType.ClientError)
            {
                // No retry for auth/4xx
                tcs.SetException(new ApiException(errorType, request.error ?? "网络错误", "error"));
                yield break;
            }

            // Transient error: retry if attempts remain
            if (attempt < maxRetries)
            {
                var delay = ApiRetryPolicy.GetDelay(attempt);
                yield return new WaitForSeconds((float)delay.TotalSeconds);
                continue;
            }

            tcs.SetException(new ApiException(errorType, request.error ?? "网络错误", "error"));
            yield break;
        }
    }

    // ===== ILlmClient: ChatAsync =====

    /// <summary>非流式 LLM 调用。</summary>
    public async Task<LlmResponse> ChatAsync(
        AgentLLMRole role, LlmConfig config, List<LlmMessage> messages,
        List<Dictionary<string, object>>? tools = null)
    {
        var taskSource = new TaskCompletionSource<LlmResponse>();
        StartCoroutine(ChatCoroutine(role, config, messages, tools, taskSource));
        return await taskSource.Task;
    }

    private IEnumerator ChatCoroutine(AgentLLMRole role, LlmConfig config,
        List<LlmMessage> messages, List<Dictionary<string, object>>? tools,
        TaskCompletionSource<LlmResponse> tcs)
    {
        var body = ChatResponseParser.BuildBody(config.Model, messages, stream: false, tools);
        var url = EndpointResolver.BuildChatCompletionsUrl(config.BaseUrl)
                  ?? "https://api.deepseek.com/v1/chat/completions";

        int maxRetries = ApiRetryPolicy.IsForeground(role) ? 3 : 0;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            using var request = UnityWebRequest.Post(url, body, "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + config.ApiKey);
            request.timeout = 120;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var json = request.downloadHandler.text;
                var response = ChatResponseParser.ParseResponse(json, role);
                tcs.SetResult(response);
                yield break;
            }

            // Bug 4: ConnectionError → responseCode=0, ClassifyStatus(0)=Success (wrong).
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                if (attempt < maxRetries)
                {
                    var delay = ApiRetryPolicy.GetDelay(attempt);
                    yield return new WaitForSeconds((float)delay.TotalSeconds);
                    continue;
                }
                tcs.SetException(new ApiException(ApiErrorType.NetworkError, request.error ?? "网络错误", "error"));
                yield break;
            }

            var errorType = ApiRetryPolicy.ClassifyStatus((int)request.responseCode);
            if (errorType == ApiErrorType.AuthError || errorType == ApiErrorType.ClientError)
            {
                tcs.SetException(new ApiException(errorType, request.error ?? "网络错误", "error"));
                yield break;
            }

            if (attempt < maxRetries)
            {
                var delay = ApiRetryPolicy.GetDelay(attempt);
                yield return new WaitForSeconds((float)delay.TotalSeconds);
                continue;
            }

            tcs.SetException(new ApiException(errorType, request.error ?? "网络错误", "error"));
            yield break;
        }
    }

    // ===== FetchModels coroutine =====

    /// <summary>
    /// 获取可用模型列表协程。startGeneration / currentGeneration 用于过期检测。
    /// </summary>
    public IEnumerator FetchModels(string baseUrl, string apiKey,
        Action<ModelCatalogResult> onComplete, int startGeneration, Func<int> currentGeneration)
    {
        var url = EndpointResolver.BuildModelsUrl(baseUrl);
        if (url == null)
        {
            onComplete(new ModelCatalogResult { Success = false, Error = "Base URL 格式无效" });
            yield break;
        }

        using var request = UnityWebRequest.Get(url);
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);
        request.timeout = 15;

        yield return request.SendWebRequest();

        if (startGeneration != currentGeneration())
        {
            onComplete(new ModelCatalogResult { Success = false, Error = "请求已过期" });
            yield break;
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            // Bug 4: ConnectionError → responseCode=0, ClassifyError(0) returns misleading "HTTP 0".
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                onComplete(new ModelCatalogResult { Success = false, Error = "无法连接 API 服务，请检查网络" });
                yield break;
            }
            var error = ModelCatalogParser.ClassifyError((int)request.responseCode);
            onComplete(new ModelCatalogResult { Success = false, Error = error });
            yield break;
        }

        var json = request.downloadHandler.text;
        var models = ModelCatalogParser.Parse(json);
        onComplete(new ModelCatalogResult
        {
            Success = models.Count > 0,
            Error = models.Count == 0 ? "未获取到可用模型" : null,
            Models = models
        });
    }

    // ===== TestConnection coroutine =====

    /// <summary>测试 LLM 连接。onComplete(success, message, latencyMs)。</summary>
    public IEnumerator TestConnection(string baseUrl, string apiKey, string model,
        Action<bool, string, long> onComplete)
    {
        var messages = new List<LlmMessage> { new() { Role = "user", Content = "ping" } };
        var body = ChatResponseParser.BuildBody(model, messages, stream: false, maxTokens: 1);

        var url = EndpointResolver.BuildChatCompletionsUrl(baseUrl)
                  ?? "https://api.deepseek.com/v1/chat/completions";

        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var request = UnityWebRequest.Post(url, body, "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);
        request.timeout = 15;

        yield return request.SendWebRequest();
        sw.Stop();

        if (request.result == UnityWebRequest.Result.Success)
            onComplete(true, $"连接正常 ({sw.ElapsedMilliseconds}ms)", sw.ElapsedMilliseconds);
        else
            onComplete(false, $"连接失败 ({sw.ElapsedMilliseconds}ms): {request.error}", sw.ElapsedMilliseconds);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
#pragma warning restore IDE0008, IDE0032, RCS1085, IDE0063, CA1031, CA1305, RCS1181, IDE0011, CA1054, IDE0074, IDE0058, CA1308
