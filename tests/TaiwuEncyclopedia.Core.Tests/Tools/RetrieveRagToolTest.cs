using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Rag;
using TaiwuEncyclopedia.Core.Tools;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Tools;

public class RetrieveRagToolTest
{
    [Fact]
    public async Task ReturnsContextOnSuccess()
    {
        var ragClient = new StubRagClient(new RagRetrieveResult { Context = "攻略内容" });
        var tool = new RetrieveRagTool(ragClient);

        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["query"] = "太吾",
            ["mode"] = "hybrid",
        });

        result["context"].ToString().Should().Be("攻略内容");
        result.Should().ContainKey("retrieval_info");
    }

    [Fact]
    public async Task ClampsTopKToBounds()
    {
        var ragClient = new StubRagClient(new RagRetrieveResult { Context = "" });
        var tool = new RetrieveRagTool(ragClient);

        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["query"] = "太吾",
            ["top_k"] = 999,   // 超上限
            ["chunk_top_k"] = 1, // 低于下限
        });

        var info = (result["retrieval_info"] as Dictionary<string, object>)!;
        info["top_k"].Should().Be(40);
        info["chunk_top_k"].Should().Be(3);
    }

    [Fact]
    public async Task RagFailureReturnsErrorContext()
    {
        var ragClient = new StubRagClient(new RagRetrieveResult { Error = "server error" });
        var tool = new RetrieveRagTool(ragClient);

        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["query"] = "太吾",
        });

        result["error"].ToString().Should().Be("server error");
    }

    [Fact]
    public async Task ReturnsReferencesFromRagResult()
    {
        var ragClient = new StubRagClient(new RagRetrieveResult
        {
            Context = "攻略内容",
            References = new List<Reference>
            {
                new()
                {
                    FullDocId = "doc-A",
                    FilePath = "wiki/a.md",
                    SourceUrl = "https://wiki.example.com/a",
                    SourceType = "wiki",
                    KnowledgeType = "机制",
                    Author = "灰机",
                    GameVersion = "1.0",
                    Snippet = "片段",
                    HitCount = 2,
                },
            },
        });
        var tool = new RetrieveRagTool(ragClient);

        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["query"] = "太吾",
        });

        result.Should().ContainKey("references");
        var refs = result["references"] as List<Reference>;
        refs.Should().NotBeNull();
        refs!.Should().HaveCount(1);
        refs![0].SourceUrl.Should().Be("https://wiki.example.com/a");
    }

    [Fact]
    public async Task RagEnabled_False_ReturnsDisabled()
    {
        var ragClient = new StubRagClient(new RagRetrieveResult { Context = "should not reach" });
        var tool = new RetrieveRagTool(ragClient);
        tool.RagEnabled = false;

        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["query"] = "太吾",
        });

        result["error"].ToString().Should().Be("rag_disabled");
        result["context"].ToString().Should().BeEmpty();
        result.Should().ContainKey("retrieval_info");
        var info = result["retrieval_info"] as Dictionary<string, object>;
        info.Should().NotBeNull();
        info!["source"].ToString().Should().Be("rag_disabled");
    }

    [Fact]
    public async Task RagEnabled_True_Default_NormalPath()
    {
        // 默认构造后 RagEnabled 应为 true，走正常 RAG 路径
        var ragClient = new StubRagClient(new RagRetrieveResult { Context = "正常结果" });
        var tool = new RetrieveRagTool(ragClient);

        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["query"] = "太吾",
        });

        result["context"].ToString().Should().Be("正常结果");
        result["error"].ToString().Should().BeEmpty();  // 成功时 error 为空串，非 null
    }

    private sealed class StubRagClient : IRagClient
    {
        private readonly RagRetrieveResult _result;

        public StubRagClient(RagRetrieveResult result) { _result = result; }

        public Task<RagRetrieveResult> RetrieveAsync(RagRetrieveRequest request, CancellationToken ct = default)
            => Task.FromResult(_result);
    }
}
