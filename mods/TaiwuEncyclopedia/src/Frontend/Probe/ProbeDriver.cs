using System.Collections;
using UnityEngine;

namespace TaiwuEncyclopedia;

/// <summary>探针协程驱动单例。DontDestroyOnLoad。照 TaiwuNameReader.NameReaderDriver 模式。</summary>
#pragma warning disable CA1812, CA1852
internal sealed class ProbeDriver : MonoBehaviour
#pragma warning restore CA1812, CA1852
{
    private static ProbeDriver? _instance;
    public static ProbeDriver Instance
    {
        get { if (_instance == null) Ensure(); return _instance!; }
    }

    public static void Ensure()
    {
        if (_instance != null) return;
        var go = new GameObject("TaiwuEncyclopedia_ProbeDriver");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ProbeDriver>();
    }

#pragma warning disable IDE0051, RCS1213
    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }
#pragma warning restore IDE0051, RCS1213
}
