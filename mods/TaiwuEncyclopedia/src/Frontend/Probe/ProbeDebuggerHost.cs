using System;
using System.Threading.Tasks;
using UnityEngine;
using TaiwuEncyclopedia.Core.Probe;
using TaiwuEncyclopedia.Core.Session;

namespace TaiwuEncyclopedia;

/// <summary>F10/F11/F12 触发探针 dump。Task9: 扩展支持4个探针 + NpcEntryInjector charId注入。</summary>
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
        if (WorldIdReader.CurrentWorldId() == SessionManager.PregameWorldId)
        {
            if (Input.GetKeyDown(KeyCode.F10) || Input.GetKeyDown(KeyCode.F11) || Input.GetKeyDown(KeyCode.F12))
            {
                _ = _writer.WriteSkipAsync("未载入存档");
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.F10))
        {
            _ = DumpTaiwuAsync();
            // 如果有注入的 charId，同时 dump probe_npc
            var injectedCharId = NpcEntryInjector.InjectedCharId;
            if (injectedCharId > 0)
            {
                _ = DumpNpcAsync(injectedCharId);
            }
        }
        else if (Input.GetKeyDown(KeyCode.F11))
        {
            _ = DumpCombatSkillsAsync();
        }
        else if (Input.GetKeyDown(KeyCode.F12))
        {
            var injectedCharId = NpcEntryInjector.InjectedCharId;
            if (injectedCharId > 0)
            {
                _ = DumpInventoryAsync(injectedCharId);
            }
            else
            {
                _ = _writer.WriteSkipAsync("未注入 NPC charId (按 F9 注入)");
            }
        }
    }

    private async Task DumpTaiwuAsync()
    {
        try
        {
            var provider = new GameStateProvider();
            var collector = new Core.Probe.ProbeErrorCollector();
            var snap = await provider.GetTaiwu(collector);
            await _writer.WriteAsync("probe_taiwu", new
            {
                snapshot = snap,
                failures = collector.Failures,
            });
        }
        catch (Exception e)
        {
            await _writer.WriteErrorAsync("probe_taiwu", e.Message);
        }
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
                failures = collector.Failures,
            });
        }
        catch (Exception e)
        {
            await _writer.WriteErrorAsync("probe_combat_skills", e.Message);
        }
    }

    private async Task DumpNpcAsync(int charId)
    {
        try
        {
            var provider = new GameStateProvider();
            var collector = new Core.Probe.ProbeErrorCollector();
            var snap = await provider.GetNpcDetail(charId, collector);
            await _writer.WriteAsync("probe_npc", new
            {
                charId = charId,
                snapshot = snap,
                failures = collector.Failures,
            });
        }
        catch (Exception e)
        {
            await _writer.WriteErrorAsync("probe_npc", e.Message);
        }
    }

    private async Task DumpInventoryAsync(int charId)
    {
        try
        {
            var provider = new GameStateProvider();
            var collector = new Core.Probe.ProbeErrorCollector();
            var snap = await provider.GetInventory(charId, collector);
            await _writer.WriteAsync("probe_inventory", new
            {
                charId = charId,
                snapshot = snap,
                failures = collector.Failures,
            });
        }
        catch (Exception e)
        {
            await _writer.WriteErrorAsync("probe_inventory", e.Message);
        }
    }
}
