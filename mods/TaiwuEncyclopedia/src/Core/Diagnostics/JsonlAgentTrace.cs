using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using TaiwuEncyclopedia.Core.Llm;

namespace TaiwuEncyclopedia.Core.Diagnostics;

/// <summary>
/// JSONL 文件 trace 实现。按 worldId 聚合，每事件追加一行。
/// 写入失败静默吞 + CoreLog 警告，不影响 Agent 主流程。
/// </summary>
public sealed class JsonlAgentTrace : IAgentTrace
{
    private readonly string _dir;
    private string? _sessionId;
    private int _worldId;

    public JsonlAgentTrace(string dir)
    {
        _dir = dir;
    }

    public void BeginSession(string query, int worldId, string? personaId)
    {
        _sessionId = Guid.NewGuid().ToString("N");
        _worldId = worldId;
        EnsureDir();
        WriteLine(new Dictionary<string, object?>
        {
            ["type"] = "session_start",
            ["ts"] = IsoNow(),
            ["sessionId"] = _sessionId,
            ["worldId"] = worldId,
            ["query"] = query,
            ["personaId"] = personaId,
        });
    }

    public void ContextStep(string step, int durationMs, Dictionary<string, object> detail)
    {
        if (_sessionId == null) return;
        WriteLine(new Dictionary<string, object?>
        {
            ["type"] = "context_step",
            ["ts"] = IsoNow(),
            ["sessionId"] = _sessionId,
            ["phase"] = "pre_react",
            ["step"] = step,
            ["durationMs"] = durationMs,
            ["detail"] = detail,
        });
    }

    public void LlmCall(int iteration, string role, string trigger,
        List<LlmMessage> messages, List<Dictionary<string, object>>? tools)
    {
        if (_sessionId == null) return;
        var msgChars = 0;
        foreach (var m in messages) msgChars += (m.Content ?? "").Length;
        WriteLine(new Dictionary<string, object?>
        {
            ["type"] = "llm_call",
            ["ts"] = IsoNow(),
            ["sessionId"] = _sessionId,
            ["phase"] = "react",
            ["iteration"] = iteration,
            ["role"] = role,
            ["trigger"] = trigger,
            ["messagesCount"] = messages.Count,
            ["messagesTotalChars"] = msgChars,
            ["toolsCount"] = tools?.Count ?? 0,
            ["tools"] = tools,
            ["messages"] = messages,
        });
    }

    public void LlmResponse(int iteration, string role, string? content,
        List<ToolCall>? toolCalls, string finishReason, TokenUsage? usage, int durationMs)
    {
        if (_sessionId == null) return;
        WriteLine(new Dictionary<string, object?>
        {
            ["type"] = "llm_response",
            ["ts"] = IsoNow(),
            ["sessionId"] = _sessionId,
            ["phase"] = "react",
            ["iteration"] = iteration,
            ["role"] = role,
            ["content"] = content,
            ["toolCalls"] = toolCalls,
            ["finishReason"] = finishReason,
            ["usage"] = usage,
            ["durationMs"] = durationMs,
        });
    }

    public void ToolCall(string name, string callId, Dictionary<string, object> args, int iteration)
    {
        if (_sessionId == null) return;
        WriteLine(new Dictionary<string, object?>
        {
            ["type"] = "tool_call",
            ["ts"] = IsoNow(),
            ["sessionId"] = _sessionId,
            ["phase"] = "react",
            ["iteration"] = iteration,
            ["name"] = name,
            ["callId"] = callId,
            ["args"] = args,
        });
    }

    public void ToolResult(string name, string callId, string content, int iteration)
    {
        if (_sessionId == null) return;
        WriteLine(new Dictionary<string, object?>
        {
            ["type"] = "tool_result",
            ["ts"] = IsoNow(),
            ["sessionId"] = _sessionId,
            ["phase"] = "react",
            ["iteration"] = iteration,
            ["name"] = name,
            ["callId"] = callId,
            ["contentChars"] = content.Length,
            ["content"] = content,
        });
    }

    public void EndSession(int thinkingTimeMs, int totalIterations, int finalAnswerChars, TokenUsage totalUsage)
    {
        if (_sessionId == null) return;
        WriteLine(new Dictionary<string, object?>
        {
            ["type"] = "session_end",
            ["ts"] = IsoNow(),
            ["sessionId"] = _sessionId,
            ["thinkingTimeMs"] = thinkingTimeMs,
            ["totalIterations"] = totalIterations,
            ["finalAnswerChars"] = finalAnswerChars,
            ["tokenUsage"] = new
            {
                prompt = totalUsage.PromptTokens,
                completion = totalUsage.CompletionTokens,
                cacheHit = totalUsage.CacheHitTokens,
            },
        });
    }

    private void EnsureDir()
    {
        try { Directory.CreateDirectory(_dir); }
        catch (Exception ex) { CoreLog.Write("TE.Trace", $"create dir failed: {ex.Message}"); }
    }

    private void WriteLine(object payload)
    {
        try
        {
            var line = JsonConvert.SerializeObject(payload, Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            File.AppendAllText(Path.Combine(_dir, $"trace_world_{_worldId}.jsonl"),
                line + "\n", Encoding.UTF8);
        }
        catch (Exception ex) { CoreLog.Write("TE.Trace", $"write failed: {ex.Message}"); }
    }

    private static string IsoNow() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff");
}
