using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TaiwuEncyclopedia;

/// <summary>探针 dump 落盘。追加写到 <TracesDir>/taiwuask-probe.log。写入失败静默吞(调试工具铁律)。</summary>
public sealed class FileProbeWriter
{
    private readonly string _path;
    public FileProbeWriter(string tracesDir)
    {
        _path = Path.Combine(tracesDir, "taiwuask-probe.log");
    }

    public Task WriteAsync(string probeName, object? dto)
    {
        var json = dto == null ? "null" : JsonConvert.SerializeObject(dto, Formatting.Indented);
        var line = $"[{Timestamp()}] {probeName}\n--- json: {json}\n\n";
        return AppendAsync(line);
    }

    public Task WriteSkipAsync(string reason)
    {
        var line = $"[{Timestamp()}] [skip] {reason}\n\n";
        return AppendAsync(line);
    }

    public Task WriteErrorAsync(string probeName, string error)
    {
        var line = $"[{Timestamp()}] [error] {probeName}: {error}\n\n";
        return AppendAsync(line);
    }

    private async Task AppendAsync(string line)
    {
        try { await File.AppendAllTextAsync(_path, line); }
        catch { /* 静默吞, 不崩游戏 */ }
    }

    private static string Timestamp()
    {
        try { return DateTime.Now.ToString("HH:mm:ss"); }
        catch { return "??:??:??"; }  // DateTime.Now 在某些 Unity 时序可能受限
    }
}
