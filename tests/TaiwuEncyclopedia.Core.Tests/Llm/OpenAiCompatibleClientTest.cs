using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Llm;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Llm;

public class OpenAiCompatibleClientTest
{
    [Fact]
    public async Task ChatNonStreamReturnsContentAndToolCalls()
    {
        var handler = new StubHandler(@"{
            ""choices"": [{
                ""message"": {
                    ""role"": ""assistant"",
                    ""content"": ""thinking..."",
                    ""tool_calls"": [{
                        ""id"": ""call_1"",
                        ""type"": ""function"",
                        ""function"": {""name"": ""retrieve_rag"", ""arguments"": ""{\""query\"":\""太吾\""}""}
                    }]
                }
            }],
            ""usage"": {""prompt_tokens"": 100, ""completion_tokens"": 20}
        }");
        var client = new OpenAiCompatibleClient(handler);
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        var resp = await client.Chat(AgentLLMRole.Thinking, new List<LlmMessage>(), config, tools: null);

        resp.Content.Should().Be("thinking...");
        resp.ToolCalls.Should().NotBeNull();
        resp.ToolCalls![0].Function.Name.Should().Be("retrieve_rag");
        client.Tracker.PromptTokens.Should().Be(100);
        client.Tracker.CompletionTokens.Should().Be(20);
    }

    [Fact]
    public async Task StreamChatYieldsContentChunks()
    {
        var handler = new StubHandler(
            "data: {\"choices\":[{\"delta\":{\"content\":\"hello\"}}]}\n\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\" world\"}}]}\n\n" +
            "data: {\"choices\":[],\"usage\":{\"prompt_tokens\":50,\"completion_tokens\":10}}\n\n" +
            "data: [DONE]\n\n",
            isStream: true);
        var client = new OpenAiCompatibleClient(handler);
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        var chunks = new List<string>();
        await foreach (var chunk in client.StreamChat(AgentLLMRole.Answer, new List<LlmMessage>(), config))
        {
            chunks.Add(chunk);
        }
        string.Join("", chunks).Should().Be("hello world");
        client.Tracker.PromptTokens.Should().Be(50);
    }

    [Fact]
    public async Task ChatRetriesOnceOnTransientError()
    {
        var handler = new StubHandler(@"{
            ""choices"": [{
                ""message"": {
                    ""role"": ""assistant"",
                    ""content"": ""ok""
                }
            }],
            ""usage"": {""prompt_tokens"": 10, ""completion_tokens"": 2}
        }", statusCode: HttpStatusCode.InternalServerError, failFirstCall: true);
        var client = new OpenAiCompatibleClient(handler);
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        var resp = await client.Chat(AgentLLMRole.Thinking, new List<LlmMessage>(), config, tools: null);

        resp.Content.Should().Be("ok");
        handler.CallCount.Should().Be(2); // 第1次500，第2次200
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _response;
        private readonly HttpStatusCode _statusCode;
        private readonly bool _isStream;
        private readonly bool _failFirstCall;
        public int CallCount;

        public StubHandler(string response, HttpStatusCode statusCode = HttpStatusCode.OK,
                           bool isStream = false, bool failFirstCall = false)
        {
            _response = response; _statusCode = statusCode; _isStream = isStream; _failFirstCall = failFirstCall;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CallCount++;
            var sc = _failFirstCall && CallCount == 1 ? HttpStatusCode.InternalServerError : _statusCode;
            var body = _failFirstCall && CallCount == 1 ? "error" : _response;
            var content = new StringContent(body, Encoding.UTF8, _isStream ? "text/event-stream" : "application/json");
            return Task.FromResult(new HttpResponseMessage(sc) { Content = content });
        }
    }
}
