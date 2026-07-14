namespace TaiwuEncyclopedia.Core.Probe;

/// <summary>探针统一返回结构。status: ok/degraded/unavailable。</summary>
public sealed class ProbeResult
{
    public string Probe { get; set; } = "";
    public string Status { get; set; } = "ok";   // ok / degraded / unavailable
    public string? FailedApi { get; set; }        // 对应 L0 契约条目
    public string[]? MissingFields { get; set; }  // 降级时缺失字段名
    public string? ErrorCode { get; set; }         // 用户给作者的报错码, 如 "P-CS-003"
    public string? Error { get; set; }            // 异常摘要
    public object? Partial { get; set; }            // 降级时已拿到的部分字段(路1 给 agent)
}