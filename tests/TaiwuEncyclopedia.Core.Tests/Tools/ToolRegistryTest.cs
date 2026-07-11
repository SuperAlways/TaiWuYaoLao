using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Tools;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Tools;

public class ToolRegistryTest
{
    [Fact]
    public void RegisterAndGetTool()
    {
        var registry = new ToolRegistry();
        registry.Register(new StubTool("my_tool", "test"));

        var tool = registry.GetTool("my_tool");
        tool.Should().NotBeNull();
        tool!.Metadata.Name.Should().Be("my_tool");
    }

    [Fact]
    public void GetNonexistentToolReturnsNull()
    {
        var registry = new ToolRegistry();
        registry.GetTool("nope").Should().BeNull();
    }

    [Fact]
    public void BuildOpenaiToolsPassesEnumConstraint()
    {
        var registry = new ToolRegistry();
        var tool = new StubTool("t", "d");
        tool.SetParams(new Dictionary<string, Dictionary<string, object>>
        {
            ["mode"] = new()
            {
                ["type"] = "string",
                ["enum"] = new List<string> { "local", "hybrid" },
                ["description"] = "mode",
                ["required"] = true,
            },
        });
        registry.Register(tool);

        var openaiTools = registry.BuildOpenaiTools();
        openaiTools.Should().HaveCount(1);
        var func = openaiTools[0]["function"] as Dictionary<string, object>;
        var parameters = func!["parameters"] as Dictionary<string, object>;
        var props = parameters!["properties"] as Dictionary<string, object>;
        var modeProp = props!["mode"] as Dictionary<string, object>;
        modeProp!["enum"].Should().NotBeNull();
        var required = parameters!["required"] as List<string>;
        required.Should().Contain("mode");
    }

    private sealed class StubTool : ToolBase
    {
        public StubTool(string name, string desc) : base(name, desc) { }
        public void SetParams(Dictionary<string, Dictionary<string, object>> p) => SetParameters(p);
        public override Task<Dictionary<string, object>> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
            => Task.FromResult(new Dictionary<string, object>());
    }
}
