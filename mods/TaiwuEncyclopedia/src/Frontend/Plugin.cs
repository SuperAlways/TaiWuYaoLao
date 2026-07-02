#pragma warning disable CA1001, CA1724, IDE0161
using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

namespace TaiwuEncyclopedia
{
    [PluginConfig("TaiwuEncyclopedia", "taiwu-encyclopedia", "1.0.0")]
    public class Plugin : TaiwuRemakePlugin
    {
        private Harmony? _harmony;

        public override void Initialize()
        {
            _harmony = new Harmony(GetGuid());
            _harmony.PatchAll(typeof(Plugin).Assembly);
            // TODO(Task 4): Bootstrap.Run() + MainThreadDispatcher.Ensure()
            // TODO(Task 7/8): ConfigHost/UiHost 启动
        }

        public override void Dispose()
        {
            _harmony?.UnpatchSelf();
        }

        public override void OnModSettingUpdate() { }

        public override void OnEnterNewWorld() { }
    }
}
#pragma warning restore CA1001, CA1724, IDE0161
