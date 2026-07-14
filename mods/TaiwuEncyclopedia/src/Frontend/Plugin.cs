#pragma warning disable CA1001, CA1724, IDE0161, IDE0011, IDE0090, CA1822
using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;
using TaiwuEncyclopedia.UI;
using UnityEngine;

namespace TaiwuEncyclopedia;

[PluginConfig("TaiwuEncyclopedia", "taiwu-encyclopedia", "1.0.0")]
public class Plugin : TaiwuRemakePlugin
{
    private Harmony? _harmony;

    public override void Initialize()
    {
        _harmony = new Harmony(GetGuid());
        _harmony.PatchAll(typeof(Plugin).Assembly);

        // Bootstrap + MainThreadDispatcher
        Bootstrap.Run();
        Threading.MainThreadDispatcher.Ensure();

        // F8 快捷键（唯一入口）
        ChatPanelHost.Initialize();

        // FrontendServices: 尝试从已有配置初始化 AgentRunner
        FrontendServices.TryInitializeAgentRunner();

        // 探针调试采集器 (step1: F10 dump)
        ProbeDriver.Ensure();
        ProbeDebuggerHost.Ensure();

        Debug.Log("[TaiwuEncyclopedia] plugin initialized");
    }

    public override void Dispose()
    {
        _harmony?.UnpatchSelf();
    }

    public override void OnModSettingUpdate() { }

    public override void OnEnterNewWorld() { }
}
#pragma warning restore CA1001, CA1724, IDE0161
