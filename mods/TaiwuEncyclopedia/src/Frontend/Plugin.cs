#pragma warning disable CA1001, CA1724, IDE0161, IDE0011, IDE0090, CA1822
using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;
using TaiwuEncyclopedia.UI;
using TaiwuEncyclopedia.Hooks;
using UnityEngine;

namespace TaiwuEncyclopedia;

[PluginConfig("TaiwuEncyclopedia", "taiwu-encyclopedia", "1.0.0")]
public class Plugin : TaiwuRemakePlugin
{
    private Harmony? _harmony;
    private static EntryButtonInjector? s_entryInjector;

    public override void Initialize()
    {
        _harmony = new Harmony(GetGuid());
        _harmony.PatchAll(typeof(Plugin).Assembly);

        // Bootstrap + MainThreadDispatcher
        Bootstrap.Run();
        Threading.MainThreadDispatcher.Ensure();

        // ChatPanelHost (F8)
        ChatPanelHost.Initialize();

        // ConfigPanelHost (F9)
        ConfigPanelHost.Initialize();

        // EntryButtonInjector (事件窗口按钮注入)
        InitializeEntryInjector();

        // FrontendServices: 尝试从已有配置初始化 AgentRunner
        FrontendServices.TryInitializeAgentRunner();

        Debug.Log("[TaiwuEncyclopedia] plugin initialized");
    }

    /// <summary>
    /// 初始化 EntryButtonInjector，创建 GameObject，DontDestroyOnLoad
    /// </summary>
    private void InitializeEntryInjector()
    {
        if (s_entryInjector != null)
        {
            return;
        }

        GameObject go = new GameObject("TaiwuEncyclopedia_EntryButtonInjector");
        UnityEngine.Object.DontDestroyOnLoad(go);
        s_entryInjector = go.AddComponent<EntryButtonInjector>();

        // 字体暂时传 null，EntryButtonInjector 会在运行时从事件窗口借字体
        s_entryInjector.StartPolling(font: null);
    }

    public override void Dispose()
    {
        _harmony?.UnpatchSelf();
    }

    public override void OnModSettingUpdate() { }

    public override void OnEnterNewWorld() { }
}
#pragma warning restore CA1001, CA1724, IDE0161
