using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TaiwuEncyclopedia.Core.Diagnostics;

/// <summary>
/// 内存环形缓冲日志（500 条）。供开发者定位 mod 问题 + 后续前端 PlayerLogViewer 消费。
/// API Key 在写入前脱敏（修正 WorldTalk 回读时脱敏的缺陷）。不写文件。
/// </summary>
public sealed class LogEntry
{
    public DateTime Timestamp { get; init; }
    public string Tag { get; init; } = "";
    public string Level { get; init; } = "info";   // info / warn / error
    public string Message { get; init; } = "";      // 已脱敏
}

public static class ModLog
{
    private const int MaxEntries = 500;
    private static readonly List<LogEntry> _entries = new();
    private static readonly object _lock = new();

    // 4 正则脱敏 API Key（DeepSeek/OpenAI 等均 sk- 前缀）
    private static readonly Regex _bearerRegex = new(@"Bearer\s+sk-[A-Za-z0-9_-]+", RegexOptions.Compiled);
    private static readonly Regex _jsonSecretRegex = new(@"(?i)""(api_key|api-key|apikey|secret|token)""\s*:\s*""sk-[^""]+""", RegexOptions.Compiled);
    private static readonly Regex _keyValueRegex = new(@"(?i)(api_key|api-key|apikey|secret|token)\s*=\s*sk-[A-Za-z0-9_-]+", RegexOptions.Compiled);
    private static readonly Regex _skRegex = new(@"sk-[A-Za-z0-9]{32,}", RegexOptions.Compiled);

    /// <summary>当前缓冲快照（拷贝，遍历安全）。</summary>
    public static IReadOnlyList<LogEntry> Entries
    {
        get { lock (_lock) { return _entries.ToArray(); } }
    }

    /// <summary>每条写入实时触发（供 PlayerLogViewer 订阅）。</summary>
    public static event Action<LogEntry>? OnEntry;

    /// <summary>写一条日志（内部先脱敏再入缓冲）。</summary>
    public static void Write(string tag, string level, string message)
    {
        var sanitized = Sanitize(message);
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Tag = tag,
            Level = level,
            Message = sanitized,
        };
        lock (_lock)
        {
            _entries.Add(entry);
            while (_entries.Count > MaxEntries) _entries.RemoveAt(0);
        }
        OnEntry?.Invoke(entry);
    }

    /// <summary>清空缓冲（测试隔离 / 重置用）。</summary>
    public static void Clear()
    {
        lock (_lock) { _entries.Clear(); }
    }

    private static string Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        text = _bearerRegex.Replace(text, "Bearer sk-***REDACTED***");
        text = _jsonSecretRegex.Replace(text, m => $"\"{m.Groups[1].Value}\":\"sk-***REDACTED***\"");
        text = _keyValueRegex.Replace(text, m => $"{m.Groups[1].Value}=sk-***REDACTED***");
        text = _skRegex.Replace(text, "sk-***REDACTED***");
        return text;
    }
}
