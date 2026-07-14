using UnityEngine;
using TaiwuEncyclopedia.Core.Session;

namespace TaiwuEncyclopedia;

/// <summary>F10 触发探针 dump。step1 骨架: 验证热键+文件落盘链路。Task5 接真实读取。</summary>
internal sealed class ProbeDebuggerHost : MonoBehaviour
{
    private static ProbeDebuggerHost? _instance;
    private FileProbeWriter _writer = null!;

    public static void Ensure()
    {
        if (_instance != null) return;
        var go = new GameObject("TaiwuEncyclopedia_ProbeDebuggerHost");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ProbeDebuggerHost>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        _writer = new FileProbeWriter(Bootstrap.TracesDir);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.F10)) return;
        // 骨架: 先只写一行, 验证热键+文件落盘
        if (WorldIdReader.CurrentWorldId() == SessionManager.PregameWorldId)
        {
            _ = _writer.WriteSkipAsync("未载入存档");
            return;
        }
        _ = _writer.WriteAsync("probe_combat_skills", new { skeleton = "F10 triggered, read not wired yet" });
    }
}
