#pragma warning disable CA1031, IDE0058, IDE0008
using System;
using System.IO;
using UnityEngine;

namespace TaiwuEncyclopedia;

/// <summary>
/// 初始化路径并创建所需目录。
/// RuntimeRoot: Application.persistentDataPath/TaiwuEncyclopedia (可写数据)
/// SkillsRoot: mod 自带的 Skills 目录 (待实测确认准确路径)
/// </summary>
public static class Bootstrap
{
    public static string RuntimeRoot { get; private set; } = "";
    public static string SkillsRoot { get; private set; } = "";
    public static string SessionsDir => Path.Combine(RuntimeRoot, "Sessions");
    public static string WorldsDir => Path.Combine(SessionsDir, "Worlds");
    public static string SoulDir => Path.Combine(RuntimeRoot, "Soul");
    public static string LogsDir => Path.Combine(RuntimeRoot, "Logs");
    public static string TracesDir => Path.Combine(RuntimeRoot, "Traces");

    public static void Run()
    {
        // RuntimeRoot: persistentDataPath 下的 TaiwuEncyclopedia 目录
        RuntimeRoot = Path.Combine(Application.persistentDataPath, "TaiwuEncyclopedia");

        // SkillsRoot: 基于 Bootstrap 程序集自身的部署位置定位 mod 根目录。
        // dll 部署在 <mod根>/Plugins/TaiwuEncyclopedia.Frontend.dll,反推两级得 mod 根,
        // 再拼 Skills。这样随游戏实际 mod 目录名(Mod/Mods 等)自适应,不硬编码。
        // 见 docs/mod-build-deploy-runbook.md「已知坑」关于 Mod vs Mods 的说明。
        var dllPath = typeof(Bootstrap).Assembly.Location;
        string modRoot;
        if (!string.IsNullOrEmpty(dllPath))
        {
            // <mod根>/Plugins/X.dll → <mod根>/Plugins → <mod根>
            var pluginsDir = Path.GetDirectoryName(dllPath);
            modRoot = Path.GetDirectoryName(pluginsDir) ?? "";
        }
        else
        {
            // 回退: 游戏可执行目录下的 Mod/TaiwuEncyclopedia (游戏实际用单数 Mod)
            modRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mod", "TaiwuEncyclopedia");
        }
        SkillsRoot = Path.Combine(modRoot, "Skills");

        // 如果上述路径不存在，回退到一个标记路径并记录警告
        if (!Directory.Exists(SkillsRoot))
        {
            // 也可以尝试其他可能的位置，这里先按最可能的结构写
            Debug.LogWarning($"[TaiwuEncyclopedia] SkillsRoot not found at: {SkillsRoot}");
        }

        // 创建所需目录
        foreach (string d in new[] { RuntimeRoot, WorldsDir, SoulDir, LogsDir, TracesDir })
        {
            try
            {
                Directory.CreateDirectory(d);
            }
            catch (Exception e)
            {
                Debug.LogError($"[TaiwuEncyclopedia] Failed to create directory {d}: {e}");
            }
        }

        Debug.Log($"[TaiwuEncyclopedia] Bootstrap complete: RuntimeRoot={RuntimeRoot}, SkillsRoot={SkillsRoot}");
    }
}
#pragma warning restore CA1031, IDE0058, IDE0008
