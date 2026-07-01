using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Tools;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Tools;

public class ToolExecutorTest
{
    [Fact]
    public async Task ExecuteReturnsResultsInOrder()
    {
        var registry = new ToolRegistry();
        registry.Register(new EchoTool("a"));
        registry.Register(new EchoTool("b"));
        var executor = new ToolExecutor(registry);

        var toolCalls = new List<ToolCall>
        {
            new() { Id = "1", Function = new ToolCallFunction { Name = "a", Arguments = "{\"x\":\"1\"}" } },
            new() { Id = "2", Function = new ToolCallFunction { Name = "b", Arguments = "{\"x\":\"2\"}" } },
        };

        var results = await executor.ExecuteAsync(toolCalls);
        results.Should().HaveCount(2);
        results[0].CallId.Should().Be("1");
        results[1].CallId.Should().Be("2");
    }

    [Fact]
    public async Task UnknownToolReturnsErrorJson()
    {
        var registry = new ToolRegistry();
        var executor = new ToolExecutor(registry);

        var results = await executor.ExecuteAsync(new List<ToolCall>
        {
            new() { Id = "1", Function = new ToolCallFunction { Name = "nonexistent", Arguments = "{}" } },
        });

        var obj = JObject.Parse(results[0].Content);
        obj["error"]!.ToString().Should().Contain("不存在");
    }

    [Fact]
    public async Task ToolExceptionReturnsErrorJsonNotThrow()
    {
        var registry = new ToolRegistry();
        registry.Register(new ThrowingTool());
        var executor = new ToolExecutor(registry);

        var results = await executor.ExecuteAsync(new List<ToolCall>
        {
            new() { Id = "1", Function = new ToolCallFunction { Name = "throwing", Arguments = "{}" } },
        });

        var obj = JObject.Parse(results[0].Content);
        obj["error"]!.ToString().Should().NotBeEmpty();
        obj["exc_type"]!.ToString().Should().Be("InvalidOperationException");
    }

    [Fact]
    public async Task InvalidJsonArgumentsReturnsError()
    {
        var registry = new ToolRegistry();
        registry.Register(new EchoTool("echo"));
        var executor = new ToolExecutor(registry);

        var results = await executor.ExecuteAsync(new List<ToolCall>
        {
            new() { Id = "1", Function = new ToolCallFunction { Name = "echo", Arguments = "{INVALID" } },
        });

        var obj = JObject.Parse(results[0].Content);
        obj["error"]!.ToString().Should().Contain("参数解析失败");
    }

    private sealed class EchoTool : ToolBase
    {
        public EchoTool(string name) : base(name, "echo") { }
        public override Task<Dictionary<string, object>> ExecuteAsync(Dictionary<string, object> args)
            => Task.FromResult(new Dictionary<string, object> { ["echo"] = args });
    }

    private sealed class ThrowingTool : ToolBase
    {
        public ThrowingTool() : base("throwing", "throws") { }
        public override Task<Dictionary<string, object>> ExecuteAsync(Dictionary<string, object> args)
            => throw new System.InvalidOperationException("boom");
    }
}
