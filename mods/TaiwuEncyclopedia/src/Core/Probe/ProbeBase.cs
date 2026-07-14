using System;
using System.Threading.Tasks;
using TaiwuEncyclopedia.Core.Diagnostics;

namespace TaiwuEncyclopedia.Core.Probe;

/// <summary>读取韧性 helper。TryRead 包单步游戏调用: 成功返值, 失败记 CoreLog(路3)+收集到 collector, 返 default。</summary>
public static class ProbeBase
{
    public static async Task<T?> TryRead<T>(
        string probeName, string apiName, string errorCode,
        IProbeErrorCollector collector, Func<Task<T>> read)
    {
        try
        {
            return await read();
        }
        catch (Exception e)
        {
            CoreLog.Write("probe", $"{probeName} degraded: failed_api={apiName} err={e.Message}");  // 路3
            collector.AddFailed(apiName, errorCode, e);
            return default;
        }
    }
}