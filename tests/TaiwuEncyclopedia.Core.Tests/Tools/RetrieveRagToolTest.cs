using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Http;
using TaiwuEncyclopedia.Core.Tools;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Tools;

public class RetrieveRagToolTest
{
    [Fact]
    public async Task ReturnsContextOnSuccess()
    {
        var handler = new StubHandler("{\"context\":\"攻略内容\",\"chunks\":[]}", HttpStatusCode.OK);
        var ragClient = new RagHttpClient(handler, "http://taiwuasker");
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
        var handler = new StubHandler("{\"context\":\"\",\"chunks\":[]}", HttpStatusCode.OK);
        var ragClient = new RagHttpClient(handler, "http://taiwuasker");
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
    public async Task RagFailureReturnsEmptyContext()
    {
        var handler = new StubHandler("error", HttpStatusCode.InternalServerError);
        var ragClient = new RagHttpClient(handler, "http://taiwuasker");
        var tool = new RetrieveRagTool(ragClient);

        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["query"] = "太吾",
        });

        result["context"].ToString().Should().BeEmpty();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _code;
        public StubHandler(string body, HttpStatusCode code) { _body = body; _code = code; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(_code)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
    }
}
