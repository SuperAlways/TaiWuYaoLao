using System;
using System.Collections.Generic;
using TaiwuEncyclopedia.Core.Agent;
using TaiwuEncyclopedia.Core.Llm;
using UnityEngine;

namespace TaiwuEncyclopedia.Core.Agent;

/// <summary>Agent 持久宿主(DontDestroyOnLoad)。协程跑在它身上,不随 ChatPanel 关停。</summary>
public sealed class AgentRunnerHost : MonoBehaviour
{
    private static AgentRunnerHost? _instance;
    private readonly Dictionary<int, ActiveRequest> _active = new();
    private int _nextGeneration = 1;

    public static AgentRunnerHost Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("TaiwuEncyclopedia_AgentRunnerHost");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<AgentRunnerHost>();
            }
            return _instance;
        }
    }

    public ActiveRequest StartRequest(int worldId, string query, Action<AgentEvent> onEvent,
        List<LlmMessage>? history = null)
    {
        var generation = _nextGeneration++;
        var req = new ActiveRequest { WorldId = worldId, Query = query, Generation = generation };
        if (history != null) req.History = history;

        if (_active.TryGetValue(worldId, out var old)) { old.Cts.Cancel(); _active.Remove(worldId); }
        _active[worldId] = req;

        StartCoroutine(RunRequest(req, onEvent));
        return req;
    }

    public void Cancel(int worldId)
    {
        if (_active.TryGetValue(worldId, out var req)) { req.Cts.Cancel(); _active.Remove(worldId); }
    }

    public ActiveRequest? GetRequest(int worldId)
        => _active.TryGetValue(worldId, out var req) ? req : null;

    private System.Collections.IEnumerator RunRequest(ActiveRequest req, Action<AgentEvent> onEvent)
    {
        var runner = FrontendServices.AgentRunner;
        if (runner == null) { onEvent(new StatusEvent { Message = "AgentRunner 未配置" }); yield break; }

        var enumerator = runner.RunAsync(req.Query, req.WorldId, null, req.History)
            .GetAsyncEnumerator(req.CancellationToken);
        var receivedEnd = false;

        while (true)
        {
            var moveNextTask = enumerator.MoveNextAsync();
            yield return new WaitUntil(() => moveNextTask.IsCompleted);
            if (moveNextTask.IsFaulted || moveNextTask.IsCanceled || !moveNextTask.Result) break;

            var evt = enumerator.Current;
            req.CompletedEvents.Add(evt);
            if (evt is StartEvent)
            {
                req.StartTime = UnityEngine.Time.realtimeSinceStartup;
                req.IsThinking = true;
            }
            else if (evt is FinalChunkEvent fc)
            {
                if (fc.Content != null) req.AnswerBuilder.Append(fc.Content);
                // IsThinking 在 EndEvent 时置 false（流式输出完才算结束）
            }
            else if (evt is UsageEvent ue)
            {
                req.TotalPromptTokens += ue.PromptTokens;
                req.TotalCompletionTokens += ue.CompletionTokens;
                req.TotalCacheHitTokens += ue.CacheHitTokens;
            }
            else if (evt is EndEvent)
            {
                req.IsThinking = false;
            }
            onEvent(evt);
            if (evt is EndEvent) { receivedEnd = true; break; }
        }

        if (!receivedEnd)
            try { onEvent(new EndEvent()); } catch { }

        if (_active.TryGetValue(req.WorldId, out var current) && current == req)
            _active.Remove(req.WorldId);
    }

    private void OnDestroy()
    {
        foreach (var req in _active.Values) { req.Cts.Cancel(); req.Cts.Dispose(); }
        _active.Clear();
    }
}
