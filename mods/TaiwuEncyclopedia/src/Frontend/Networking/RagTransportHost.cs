#pragma warning disable IDE0008, IDE0032, RCS1085, IDE0063, CA1031, CA1305, RCS1181, IDE0011, CA1054, IDE0074, IDE0058, CA1308
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TaiwuEncyclopedia.Core.Rag;
using UnityEngine;
using UnityEngine.Networking;

namespace TaiwuEncyclopedia.Frontend.Networking;

/// <summary>
/// UnityWebRequest 实现的 RAG 传输层。实现 IRagClient 接口。
/// 通过协程桥接 UnityWebRequest 到 async/await。
/// </summary>
public sealed class RagTransportHost : IRagClient
{
    private readonly string _baseUrl;

    public RagTransportHost(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
    }

    /// <summary>RAG 检索调用。通过协程桥接 UnityWebRequest 到 async。</summary>
    public async Task<RagRetrieveResult> RetrieveAsync(RagRetrieveRequest request, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<RagRetrieveResult>();

        // RagTransportHost 不是 MonoBehaviour,借用 LlmTransportHost 的协程调度
        LlmTransportHost.Instance.StartCoroutine(RetrieveCoroutine(request, tcs, ct));

        // 如果外部 token 取消，也取消 tcs
        using var reg = ct.Register(() => tcs.TrySetCanceled());
        return await tcs.Task;
    }

    private IEnumerator RetrieveCoroutine(RagRetrieveRequest request, TaskCompletionSource<RagRetrieveResult> tcs, CancellationToken ct)
    {
        var body = JsonConvert.SerializeObject(request);
        var url = _baseUrl + "/api/retrieve";

        using var req = UnityWebRequest.Post(url, body, "application/json");
        req.timeout = 60;

        var op = req.SendWebRequest();

        while (!op.isDone)
        {
            if (ct.IsCancellationRequested)
            {
                req.Abort();
                tcs.SetResult(new RagRetrieveResult { Error = "请求已取消" });
                yield break;
            }
            yield return null;
        }

        if (req.result != UnityWebRequest.Result.Success)
        {
            var error = req.error ?? "网络错误";
            Core.Diagnostics.CoreLog.Write("TE.RAG", $"RAG HTTP failed: {error}");
            tcs.SetResult(new RagRetrieveResult { Error = "unreachable" });
            yield break;
        }

        var json = req.downloadHandler.text;
        var result = RagResponseParser.Parse(json);
        tcs.SetResult(result);
    }
}
#pragma warning restore IDE0008, IDE0032, RCS1085, IDE0063, CA1031, CA1305, RCS1181, IDE0011, CA1054, IDE0074, IDE0058, CA1308
