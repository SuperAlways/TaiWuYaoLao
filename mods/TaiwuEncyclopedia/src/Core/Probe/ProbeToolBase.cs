using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaiwuEncyclopedia.Core.Tools;

namespace TaiwuEncyclopedia.Core.Probe;

/// <summary>探针 tool 通用骨架。封装: 调接口 -> 据 collector 组 ProbeResult -> 序列化。
/// 子类只实现 ProbeReadAsync(读什么), 不操心 try/catch/序列化/进档拦截。</summary>
public abstract class ProbeToolBase : ToolBase
{
    protected readonly IGameStateProvider _gameState;
    protected readonly string _errorCodePrefix;

    protected ProbeToolBase(string name, string description, int timeout,
                           IGameStateProvider gameState, string errorCodePrefix)
        : base(name, description, timeout)
    {
        _gameState = gameState;
        _errorCodePrefix = errorCodePrefix;
        SetParameters(new Dictionary<string, Dictionary<string, object>>());  // 默认无参; 有参子类在构造里再调 SetParameters
    }

    public override bool RequiresSaveGame => true;  // 所有探针进档才能用(主界面拦截已就绪)

    /// <summary>子类只写"读什么返回什么 snapshot"。collector 由基座传入。</summary>
    protected abstract Task<object> ProbeReadAsync(
        IGameStateProvider gs, IProbeErrorCollector collector,
        Dictionary<string, object> args, CancellationToken ct);

    public override async Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> args, CancellationToken ct = default)
    {
        try
        {
            var collector = new ProbeErrorCollector();  // 每次调用新建, 独立无串台
            var snapshot = await ProbeReadAsync(_gameState, collector, args, ct);
            return ToDict(BuildResult(snapshot, collector));
        }
        catch (Exception e)  // 探针级异常逃逸 -> unavailable
        {
            Core.Diagnostics.CoreLog.Write("probe", $"{Metadata.Name} unavailable: {e.Message}");  // 路3
            return ToDict(new ProbeResult
            {
                Probe = Metadata.Name,
                Status = "unavailable",
                ErrorCode = $"{_errorCodePrefix}-000",
                Error = e.Message,
            });
        }
    }

    private static Dictionary<string, object> ToDict(ProbeResult r) => new()
    {
        ["probe"] = r.Probe,
        ["status"] = r.Status,
        ["failed_api"] = r.FailedApi ?? "",
        ["missing_fields"] = r.MissingFields ?? Array.Empty<string>(),
        ["error_code"] = r.ErrorCode ?? "",
        ["error"] = r.Error ?? "",
        ["snapshot"] = r.Partial!,
    };

    private ProbeResult BuildResult(object? snapshot, IProbeErrorCollector collector)
    {
        if (collector.Failures.Count == 0)
            return new ProbeResult { Probe = Metadata.Name, Status = "ok", Partial = snapshot };
        var f = collector.Failures[0];
        return new ProbeResult
        {
            Probe = Metadata.Name, Status = "degraded", Partial = snapshot,
            FailedApi = f.ApiName, ErrorCode = f.ErrorCode, Error = f.ErrorMessage,
            MissingFields = collector.Failures.Select(x => x.ApiName).ToArray(),
        };
    }
}
