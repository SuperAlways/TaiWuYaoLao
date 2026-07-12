using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Tools;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Tools;

public class CompleteRetrievalToolTest
{
    [Fact]
    public void Metadata_HasCorrectName()
    {
        var tool = new CompleteRetrievalTool();
        tool.Metadata.Name.Should().Be("complete_retrieval");
    }

    [Fact]
    public void Metadata_HasThreeParameters()
    {
        var tool = new CompleteRetrievalTool();
        tool.Metadata.Parameters.Should().ContainKeys("confirmed", "topics_found", "missing");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsArgsUnchanged()
    {
        var tool = new CompleteRetrievalTool();
        var args = new Dictionary<string, object>
        {
            ["confirmed"] = true,
            ["topics_found"] = "毒术体系",
            ["missing"] = "具体数值",
        };
        var result = await tool.ExecuteAsync(args);
        result["confirmed"].Should().Be(true);
        result["topics_found"].Should().Be("毒术体系");
        result["missing"].Should().Be("具体数值");
    }

    [Fact]
    public void RequiresSaveGame_IsFalse()
    {
        var tool = new CompleteRetrievalTool();
        tool.RequiresSaveGame.Should().BeFalse();
    }
}
