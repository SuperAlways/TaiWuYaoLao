using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Diagnostics;
using TaiwuEncyclopedia.Core.Llm;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Diagnostics;

public class JsonlAgentTraceTest
{
    [Fact]
    public async Task BeginSession_WritesSessionStartWithSessionId()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-trace-" + System.Guid.NewGuid().ToString("N"));
        var trace = new JsonlAgentTrace(dir);

        trace.BeginSession("少林怎么加点", worldId: 5, personaId: "sword-will");

        var file = Path.Combine(dir, "trace_world_5.jsonl");
        File.Exists(file).Should().BeTrue();
        var lines = await File.ReadAllLinesAsync(file);
        lines.Should().HaveCount(1);
        var evt = Newtonsoft.Json.Linq.JObject.Parse(lines[0]);
        evt["type"]!.ToString().Should().Be("session_start");
        evt["sessionId"]!.ToString().Should().NotBeNullOrEmpty();
        evt["worldId"]!.ToObject<int>().Should().Be(5);
        evt["query"]!.ToString().Should().Be("少林怎么加点");
        evt["personaId"]!.ToString().Should().Be("sword-will");
    }

    [Fact]
    public async Task LlmResponse_RecordsUsageFromLlmResponseObject()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-trace-" + System.Guid.NewGuid().ToString("N"));
        var trace = new JsonlAgentTrace(dir);
        trace.BeginSession("q", 5, null);

        trace.LlmResponse(0, "thinking", "content", null, "stop",
            new TokenUsage { PromptTokens = 100, CompletionTokens = 20, CacheHitTokens = 80 }, 850);

        var lines = await File.ReadAllLinesAsync(Path.Combine(dir, "trace_world_5.jsonl"));
        var resp = Newtonsoft.Json.Linq.JObject.Parse(lines[1]);
        resp["type"]!.ToString().Should().Be("llm_response");
        resp["usage"]!["promptTokens"]!.ToObject<int>().Should().Be(100);
        resp["usage"]!["completionTokens"]!.ToObject<int>().Should().Be(20);
        resp["usage"]!["cacheHitTokens"]!.ToObject<int>().Should().Be(80);
        // sessionId 应与 session_start 一致
        var start = Newtonsoft.Json.Linq.JObject.Parse(lines[0]);
        resp["sessionId"]!.ToString().Should().Be(start["sessionId"]!.ToString());
    }

    [Fact]
    public async Task SameWorldId_AppendsToSameFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-trace-" + System.Guid.NewGuid().ToString("N"));
        var trace = new JsonlAgentTrace(dir);

        trace.BeginSession("q1", 5, null);
        trace.EndSession(1000, 1, 50, new TokenUsage { PromptTokens = 10, CompletionTokens = 5, CacheHitTokens = 0 });
        trace.BeginSession("q2", 5, null);
        trace.EndSession(2000, 1, 60, new TokenUsage { PromptTokens = 20, CompletionTokens = 8, CacheHitTokens = 0 });

        var lines = await File.ReadAllLinesAsync(Path.Combine(dir, "trace_world_5.jsonl"));
        lines.Should().HaveCount(4); // session_start, session_end, session_start, session_end
        // 两次 session_start 的 sessionId 不同
        Newtonsoft.Json.Linq.JObject.Parse(lines[0])["sessionId"]!.ToString()
            .Should().NotBe(Newtonsoft.Json.Linq.JObject.Parse(lines[2])["sessionId"]!.ToString());
    }

    [Fact]
    public async Task WriteFailure_DoesNotThrow()
    {
        // 用一个非法路径触发写入失败
        var trace = new JsonlAgentTrace("Z:/nonexistent-root-" + System.Guid.NewGuid().ToString("N") + "/traces");
        var act = () =>
        {
            trace.BeginSession("q", 5, null);
            trace.LlmResponse(0, "thinking", "c", null, "stop", null, 100);
        };
        act.Should().NotThrow();
        await Task.CompletedTask;
    }
}
