using System;
using System.Threading.Tasks;
using UnityEngine;
using TaiwuEncyclopedia.Core.Probe;
using TaiwuEncyclopedia.Core.Session;

namespace TaiwuEncyclopedia;

/// <summary>F10 触发探针 dump。Task5: 接真实 GameStateProvider 读取。</summary>
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
        if (WorldIdReader.CurrentWorldId() == SessionManager.PregameWorldId)
        {
            _ = _writer.WriteSkipAsync("未载入存档");
            return;
        }
        _ = DumpCombatSkillsAsync();
    }

    private async Task DumpCombatSkillsAsync()
    {
        try
        {
            var provider = new GameStateProvider();
            var collector = new Core.Probe.ProbeErrorCollector();
            var snap = await provider.GetCombatSkills(collector);
            await _writer.WriteAsync("probe_combat_skills", new
            {
                snapshot = snap,
                failures = collector.Failures,  // 字段级降级记录, 对照用
            });
        }
        catch (Exception e)
        {
            await _writer.WriteErrorAsync("probe_combat_skills", e.Message);
        }
    }
}
