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

    public static void Run()
    {
        // RuntimeRoot: persistentDataPath 下的 TaiwuEncyclopedia 目录
        RuntimeRoot = Path.Combine(Application.persistentDataPath, "TaiwuEncyclopedia");

        // SkillsRoot: 尝试定位 mod 自带的 Skills 目录
        // TODO(Task0 实测): 确认 TaiwuModdingLib 提供的 mod 目录访问 API
        // 目前策略: 先尝试基于 AppDomain.BaseDirectory 的路径，后续根据实测调整
        SkillsRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods", "TaiwuEncyclopedia", "Skills");

        // 如果上述路径不存在，回退到一个标记路径并记录警告
        if (!Directory.Exists(SkillsRoot))
        {
            // 也可以尝试其他可能的位置，这里先按最可能的结构写
            Debug.LogWarning($"[TaiwuEncyclopedia] SkillsRoot not found at: {SkillsRoot}");
        }

        // 创建所需目录
        foreach (string d in new[] { RuntimeRoot, WorldsDir, SoulDir, LogsDir })
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
