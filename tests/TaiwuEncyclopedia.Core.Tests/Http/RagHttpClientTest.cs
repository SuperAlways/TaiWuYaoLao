using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Http;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Http;

public class RagHttpClientTest
{
    [Fact]
    public async Task SuccessReturnsContextText()
    {
        var handler = new StubHandler("{\"context\":\"RAG 检索结果\",\"chunks\":[]}", HttpStatusCode.OK);
        var client = new RagHttpClient(handler, "http://taiwuasker");

        var result = await client.RetrieveAsync(new RagRetrieveRequest { Query = "太吾" });

        result.Should().Be("RAG 检索结果");
    }

    [Fact]
    public async Task HttpErrorReturnsEmptyContext()
    {
        var handler = new StubHandler("error", HttpStatusCode.InternalServerError);
        var client = new RagHttpClient(handler, "http://taiwuasker");

        var result = await client.RetrieveAsync(new RagRetrieveRequest { Query = "太吾" });

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task TimeoutReturnsEmptyContext()
    {
        var handler = new SlowHandler();
        var client = new RagHttpClient(handler, "http://taiwuasker", timeoutSeconds: 1);

        var result = await client.RetrieveAsync(new RagRetrieveRequest { Query = "太吾" });

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task NullContextFieldReturnsEmpty()
    {
        var handler = new StubHandler("{\"chunks\":[]}", HttpStatusCode.OK);
        var client = new RagHttpClient(handler, "http://taiwuasker");

        var result = await client.RetrieveAsync(new RagRetrieveRequest { Query = "太吾" });

        result.Should().BeEmpty();
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

    private sealed class SlowHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            await Task.Delay(2000, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}