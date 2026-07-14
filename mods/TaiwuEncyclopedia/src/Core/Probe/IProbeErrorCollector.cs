using System;
using System.Collections.Generic;

namespace TaiwuEncyclopedia.Core.Probe;

/// <summary>字段级降级信息收集器。每次探针调用新建实例, 避免并发串台。</summary>
public interface IProbeErrorCollector
{
    void AddFailed(string apiName, string errorCode, Exception error);
    IReadOnlyList<ProbeFailure> Failures { get; }
}

public sealed class ProbeFailure
{
    public string ApiName { get; init; } = "";
    public string ErrorCode { get; init; } = "";
    public string ErrorMessage { get; init; } = "";
}

public sealed class ProbeErrorCollector : IProbeErrorCollector
{
    private readonly List<ProbeFailure> _failures = new();
    public IReadOnlyList<ProbeFailure> Failures => _failures;
    public void AddFailed(string apiName, string errorCode, Exception error) =>
        _failures.Add(new ProbeFailure { ApiName = apiName, ErrorCode = errorCode, ErrorMessage = error.Message });
}