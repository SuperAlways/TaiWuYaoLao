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
    public async Task SuccessReturnsContextAndReferences()
    {
        var body = @"{""context"":""RAG 结果"",
""references"":[
  {""full_doc_id"":""doc-A"",""file_path"":""wiki/a.md"",
   ""source_url"":""https://wiki.example.com/a"",""source_type"":""wiki"",
   ""knowledge_type"":""机制"",""author"":""灰机"",
   ""game_version"":""1.0"",""snippet"":""片段"",""hit_count"":2}
]}";
        var handler = new StubHandler(body, HttpStatusCode.OK);
        var client = new RagHttpClient(handler, "http://taiwuasker");

        var result = await client.RetrieveAsync(new RagRetrieveRequest { Query = "太吾" });

        result.Context.Should().Be("RAG 结果");
        result.Error.Should().BeNull();
        result.References.Should().HaveCount(1);
        result.References[0].FullDocId.Should().Be("doc-A");
        result.References[0].SourceUrl.Should().Be("https://wiki.example.com/a");
        result.References[0].HitCount.Should().Be(2);
    }

    [Fact]
    public async Task HttpErrorReturnsErrorUnreachable()
    {
        var handler = new StubHandler("error", HttpStatusCode.InternalServerError);
        var client = new RagHttpClient(handler, "http://taiwuasker");

        var result = await client.RetrieveAsync(new RagRetrieveRequest { Query = "太吾" });

        result.Context.Should().BeEmpty();
        result.References.Should().BeEmpty();
        result.Error.Should().Be("unreachable");
    }

    [Fact]
    public async Task TimeoutReturnsErrorTimeout()
    {
        var handler = new SlowHandler();
        var client = new RagHttpClient(handler, "http://taiwuasker", timeoutSeconds: 1);

        var result = await client.RetrieveAsync(new RagRetrieveRequest { Query = "太吾" });

        result.Context.Should().BeEmpty();
        result.References.Should().BeEmpty();
        result.Error.Should().Be("timeout");
    }

    [Fact]
    public async Task NullContextFieldReturnsEmptyContext()
    {
        var handler = new StubHandler("{\"references\":[]}", HttpStatusCode.OK);
        var client = new RagHttpClient(handler, "http://taiwuasker");

        var result = await client.RetrieveAsync(new RagRetrieveRequest { Query = "太吾" });

        result.Context.Should().BeEmpty();
        result.References.Should().BeEmpty();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task EmptyReferencesArrayReturnsEmptyList()
    {
        var handler = new StubHandler("{\"context\":\"有结果但无引用\",\"references\":[]}", HttpStatusCode.OK);
        var client = new RagHttpClient(handler, "http://taiwuasker");

        var result = await client.RetrieveAsync(new RagRetrieveRequest { Query = "太吾" });

        result.Context.Should().Be("有结果但无引用");
        result.References.Should().BeEmpty();
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
