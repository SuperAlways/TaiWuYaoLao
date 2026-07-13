// shared/TaiWuEncyclopedia.Core/Diagnostics/CoreLog.cs
// 极简桥接:Core(shared)无 UnityEngine,通过静态回调把日志交给 Frontend 输出到 Player.log。
using System;

namespace TaiwuEncyclopedia.Core.Diagnostics;

/// <summary>
/// Core 层日志桥接。Frontend 订阅 <see cref="OnLog"/> 后,
/// Core 层任何模块调 <see cref="Write"/> 即可输出到 Player.log。
/// 替代 spec3 的 AgentDebugLog(文件写,有 field 关键字 bug)。
/// </summary>
public static class CoreLog
{
    /// <summary>订阅此回调以将 Core 日志转发到 Unity Debug.Log。</summary>
    public static event Action<string>? OnLog;

    /// <summary>写一条日志。tag 和 message 以 "[tag] message" 格式拼接。</summary>
    public static void Write(string tag, string message)
    {
        OnLog?.Invoke($"[{tag}] {message}");    // -> Unity Debug.Log
        ModLog.Write(tag, "info", message);      // -> 内存环形缓冲（脱敏后）
    }
}
