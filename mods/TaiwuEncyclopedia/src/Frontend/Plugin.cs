#pragma warning disable CA1001, CA1724, IDE0161
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

        // Task 4: Bootstrap + MainThreadDispatcher
        Bootstrap.Run();
        Threading.MainThreadDispatcher.Ensure();

        // Task 6: ChatPanelHost (F8 热键打开 ChatPanel)
        ChatPanelHost.Initialize();

        // TODO(Task 7): ConfigHost.Initialize() - F9 打开 ConfigPanel
        // TODO(Task 8): EntryButtonInjector/UiHost.Initialize() - 轮询注入「百晓问答」按钮

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
