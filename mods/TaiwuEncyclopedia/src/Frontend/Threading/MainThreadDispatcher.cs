#pragma warning disable CA1031, CA1812, IDE0011, IDE0022, IDE0051, IDE0058, IDE0008, IDE0090, RCS1213
using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace TaiwuEncyclopedia.Threading;

/// <summary>
/// 主线程调度器：允许后台线程将动作排入 Unity 主线程执行。
/// 用于 Task 6 AgentRunner 等后台逻辑向 UI 线程推送更新。
/// </summary>
public class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher? _instance;
    private readonly ConcurrentQueue<Action> _queue = new();

    public static MainThreadDispatcher Instance => _instance!;

    public static void Ensure()
    {
        if (_instance != null)
        {
            return;
        }
        GameObject go = new GameObject(nameof(MainThreadDispatcher));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<MainThreadDispatcher>();
    }

    public void Enqueue(Action action)
    {
        _queue.Enqueue(action);
    }

    private void Update()
    {
        while (_queue.TryDequeue(out Action action))
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogError($"[TaiwuEncyclopedia] MainThreadDispatcher action failed: {e}");
            }
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
#pragma warning restore CA1031, CA1812, IDE0011, IDE0022, IDE0051, IDE0058, IDE0008, IDE0090, RCS1213
