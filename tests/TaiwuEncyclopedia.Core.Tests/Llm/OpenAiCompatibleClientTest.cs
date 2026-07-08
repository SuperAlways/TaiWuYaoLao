using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Llm;
using Xunit;
using static TaiwuEncyclopedia.Core.Llm.ApiRetryPolicy;

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
        }", statusCode: HttpStatusCode.OK, failFirstCall: true);
        var client = new OpenAiCompatibleClient(handler);
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        var resp = await client.Chat(AgentLLMRole.Thinking, new List<LlmMessage>(), config, tools: null);

        resp.Content.Should().Be("ok");
        handler.CallCount.Should().Be(2); // 第1次500，第2次200
    }

    [Fact]
    public async Task ChatRetriesOnNetworkException()
    {
        var handler = new ThrowingStubHandler();
        var client = new OpenAiCompatibleClient(handler);
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        var resp = await client.Chat(AgentLLMRole.Thinking, new List<LlmMessage>(), config, tools: null);

        resp.Content.Should().Be("ok");
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Chat_FillsLlmResponseUsage()
    {
        var handler = new StubHandler(
            "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"hi\"}}],\"usage\":{\"prompt_tokens\":100,\"completion_tokens\":20,\"prompt_cache_hit_tokens\":80}}");
        var client = new OpenAiCompatibleClient(handler);
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        var resp = await client.Chat(AgentLLMRole.Thinking, new List<LlmMessage> { new() { Role = "user", Content = "q" } }, config, tools: null);

        resp.Usage.Should().NotBeNull();
        resp.Usage!.PromptTokens.Should().Be(100);
        resp.Usage.CompletionTokens.Should().Be(20);
        resp.Usage.CacheHitTokens.Should().Be(80);
    }

    [Fact]
    public async Task StreamChat_FillsLastStreamUsage()
    {
        var handler = new StubHandler(
            "data: {\"choices\":[{\"delta\":{\"content\":\"a\"}}]}\n\ndata: {\"usage\":{\"prompt_tokens\":50,\"completion_tokens\":10,\"prompt_cache_hit_tokens\":40},\"choices\":[]}\n\ndata: [DONE]\n\n",
            isStream: true);
        var client = new OpenAiCompatibleClient(handler);
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        var chunks = new List<string>();
        await foreach (var c in client.StreamChat(AgentLLMRole.Answer, new List<LlmMessage> { new() { Role = "user", Content = "q" } }, config))
            chunks.Add(c);

        client.LastStreamUsage.Should().NotBeNull();
        client.LastStreamUsage!.PromptTokens.Should().Be(50);
        client.LastStreamUsage.CompletionTokens.Should().Be(10);
        client.LastStreamUsage.CacheHitTokens.Should().Be(40);
    }

    [Fact]
    public async Task SendWithRetry_SuccessReturnsImmediately()
    {
        var handler = new ScriptedHandler(HttpStatusCode.OK, "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"ok\"}}]}");
        var client = new OpenAiCompatibleClient(handler) { RetryDelayOverride = _ => System.TimeSpan.FromMilliseconds(1) };
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        var resp = await client.Chat(AgentLLMRole.Thinking, new List<LlmMessage>(), config);

        resp.Content.Should().Be("ok");
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task SendWithRetry_429ThenSuccess_RetriesOnce()
    {
        var handler = new ScriptedHandler(HttpStatusCode.TooManyRequests, HttpStatusCode.OK,
            okBody: "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"ok\"}}]}");
        var client = new OpenAiCompatibleClient(handler) { RetryDelayOverride = _ => System.TimeSpan.FromMilliseconds(1) };
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        var resp = await client.Chat(AgentLLMRole.Thinking, new List<LlmMessage>(), config);

        resp.Content.Should().Be("ok");
        handler.CallCount.Should().Be(2); // 429 -> 重试 -> 200
    }

    [Fact]
    public async Task SendWithRetry_401_AuthError_ImmediateThrowNoRetry()
    {
        var handler = new ScriptedHandler(HttpStatusCode.Unauthorized);
        var client = new OpenAiCompatibleClient(handler) { RetryDelayOverride = _ => System.TimeSpan.FromMilliseconds(1) };
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        var act = async () => await client.Chat(AgentLLMRole.Thinking, new List<LlmMessage>(), config);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Subject.Single();
        ex.ErrorType.Should().Be(ApiErrorType.AuthError);
        ex.Level.Should().Be("error");
        handler.CallCount.Should().Be(1); // 不重试
    }

    [Fact]
    public async Task SendWithRetry_529x3_Overload_TellPlayerAfter3()
    {
        var handler = new ScriptedHandler((HttpStatusCode)529);
        var client = new OpenAiCompatibleClient(handler) { RetryDelayOverride = _ => System.TimeSpan.FromMilliseconds(1) };
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        var act = async () => await client.Chat(AgentLLMRole.Thinking, new List<LlmMessage>(), config);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Subject.Single();
        ex.ErrorType.Should().Be(ApiErrorType.Overload);
        ex.Level.Should().Be("error"); // TellPlayer
        handler.CallCount.Should().Be(3); // 连续 3 次 529 -> 熔断
    }

    [Fact]
    public async Task SendWithRetry_NetworkError_ExhaustedFailsAfterMaxRetries()
    {
        var handler = new ScriptedHandler(throwNetwork: true);
        var client = new OpenAiCompatibleClient(handler) { RetryDelayOverride = _ => System.TimeSpan.FromMilliseconds(1) };
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        var act = async () => await client.Chat(AgentLLMRole.Thinking, new List<LlmMessage>(), config);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Subject.Single();
        ex.ErrorType.Should().Be(ApiErrorType.NetworkError);
        ex.Level.Should().Be("error");
        handler.CallCount.Should().Be(ApiRetryPolicy.MAX_RETRIES + 1); // attempt 0..4 = 5 次
    }

    [Fact]
    public async Task SendWithRetry_BackgroundRole_NoRetry_ImmediateFail()
    {
        var handler = new ScriptedHandler(HttpStatusCode.TooManyRequests);
        var client = new OpenAiCompatibleClient(handler) { RetryDelayOverride = _ => System.TimeSpan.FromMilliseconds(1) };
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        var act = async () => await client.Chat(AgentLLMRole.Intent, new List<LlmMessage>(), config);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Subject.Single();
        ex.ErrorType.Should().Be(ApiErrorType.RateLimit);
        handler.CallCount.Should().Be(1); // 背景不重试
    }

    /// <summary>按脚本返回状态码序列（最后一个重复）；可抛网络异常。供 SendWithRetry 重试测试。</summary>
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode[] _codes;
        private readonly bool _throwNetwork;
        private readonly string _okBody;
        public int CallCount;

        public ScriptedHandler(params HttpStatusCode[] codes) : this(codes, okBody: "{}", throwNetwork: false) { }
        public ScriptedHandler(bool throwNetwork) : this(System.Array.Empty<HttpStatusCode>(), okBody: "{}", throwNetwork: true) { }
        public ScriptedHandler(HttpStatusCode single, string okBody = "{}") : this(new[] { single }, okBody, false) { }
        public ScriptedHandler(HttpStatusCode first, HttpStatusCode second, string okBody = "{}")
            : this(new[] { first, second }, okBody, false) { }

        private ScriptedHandler(HttpStatusCode[] codes, string okBody, bool throwNetwork)
        { _codes = codes; _okBody = okBody; _throwNetwork = throwNetwork; }

        protected override System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, System.Threading.CancellationToken ct)
        {
            CallCount++;
            if (_throwNetwork) throw new HttpRequestException("simulated network error");
            var sc = CallCount <= _codes.Length ? _codes[CallCount - 1] : _codes[_codes.Length - 1];
            var body = sc == HttpStatusCode.OK ? _okBody : "err";
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            return System.Threading.Tasks.Task.FromResult(new HttpResponseMessage(sc) { Content = content });
        }
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

    private sealed class ThrowingStubHandler : HttpMessageHandler
    {
        public int CallCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CallCount++;
            if (CallCount == 1)
            {
                throw new HttpRequestException("Simulated network error");
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{
                    ""choices"": [{
                        ""message"": {
                            ""role"": ""assistant"",
                            ""content"": ""ok""
                        }
                    }],
                    ""usage"": {""prompt_tokens"": 10, ""completion_tokens"": 2}
                }", Encoding.UTF8, "application/json")
            });
        }
    }
}
