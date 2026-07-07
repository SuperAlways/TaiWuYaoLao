using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Agent;
using TaiwuEncyclopedia.Core.Context;
using TaiwuEncyclopedia.Core.Http;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Session;
using TaiwuEncyclopedia.Core.Skills;
using TaiwuEncyclopedia.Core.Soul;
using TaiwuEncyclopedia.Core.Storage;
using TaiwuEncyclopedia.Core.Tools;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Agent;

/// <summary>
/// AgentRunner 测试。
/// </summary>
public class AgentRunnerTest
{
    /// <summary>
    /// 不调用工具直接回答的场景。
    /// </summary>
    [Fact]
    public async Task RunAsyncDirectAnswerWithoutTools()
    {
        var root = PathRoot();
        var llmHandler = new StubLlmHandler(
            thinkingResponse: "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"我直接回答\"}}],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5}}",
            streamResponse: "data: {\"choices\":[{\"delta\":{\"content\":\"最终答案\"}}]}\n\ndata: {\"choices\":[],\"usage\":{\"prompt_tokens\":20,\"completion_tokens\":10}}\n\ndata: [DONE]\n\n");
        var sm = MakeSkillManager();
        var llmClient = new OpenAiCompatibleClient(llmHandler);
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };
        var ragClient = new RagHttpClient(new StubRagHandler(), "http://taiwuasker");
        var soulStore = new JsonSoulStore(root);
        var sessionStore = new JsonSessionStore(root);

        var registry = new ToolRegistry();
        registry.Register(new RetrieveRagTool(ragClient));
        registry.Register(new LoadBackgroundSkillTool(sm));
        registry.Register(new LoadGuidanceSkillTool(sm));
        var executor = new ToolExecutor(registry);
        var soulManager = new SoulManager(soulStore);
        var contextManager = new ContextManager(soulManager, llmClient, config);
        var sessionManager = new SessionManager(sessionStore);
        var promptBuilder = new PromptBuilder(sm, "sword-will");

        var runner = new AgentRunner(llmClient, config, registry, executor,
            contextManager, soulManager, sessionManager, promptBuilder);

        var events = new List<AgentEvent>();
        await foreach (var ev in runner.RunAsync("你好", worldId: 1))
        {
            events.Add(ev);
        }

        events.Should().Contain(e => e is StartEvent);
        events.Should().Contain(e => e is FinalChunkEvent);
        events.Should().Contain(e => e is EndEvent);
        var endEvent = events.Find(e => e is EndEvent) as EndEvent;
        endEvent.Should().NotBeNull();
        endEvent!.TotalIterations.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// ReAct 循环调 RetrieveRagTool 后，yield ReferencesEvent 且 references 被持久化到 SessionStore。
    /// </summary>
    [Fact]
    public async Task RunAsyncYieldsReferencesEventAndPersistsReferences()
    {
        var root = PathRoot();
        // LLM: 第 1 轮 THINKING 返回 tool_call(retrieve_rag)，第 2 轮 THINKING 直答（无 tool_calls），第 3 次是 ANSWER 流式
        var llmHandler = new StubLlmHandlerWithToolCall(
            thinking1: "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"id\":\"call_1\",\"type\":\"function\",\"function\":{\"name\":\"retrieve_rag\",\"arguments\":\"{\\\"query\\\":\\\"太吾\\\"}\"}}]}}],\"usage\":{\"prompt_tokens\":100,\"completion_tokens\":20}}",
            thinking2: "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"根据资料回答\"}}],\"usage\":{\"prompt_tokens\":200,\"completion_tokens\":10}}",
            stream: "data: {\"choices\":[{\"delta\":{\"content\":\"最终答案\"}}]}\n\ndata: {\"choices\":[],\"usage\":{\"prompt_tokens\":50,\"completion_tokens\":10}}\n\ndata: [DONE]\n\n");
        var sm = MakeSkillManager();
        var llmClient = new OpenAiCompatibleClient(llmHandler);
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };
        // RAG stub 返回带 references 的响应
        var ragHandler = new StubRagHandlerWithRefs();
        var ragClient = new RagHttpClient(ragHandler, "http://taiwuasker");
        var soulStore = new JsonSoulStore(root);
        var sessionStore = new JsonSessionStore(root);

        var registry = new ToolRegistry();
        registry.Register(new RetrieveRagTool(ragClient));
        registry.Register(new LoadBackgroundSkillTool(sm));
        registry.Register(new LoadGuidanceSkillTool(sm));
        var executor = new ToolExecutor(registry);
        var soulManager = new SoulManager(soulStore);
        var contextManager = new ContextManager(soulManager, llmClient, config);
        var sessionManager = new SessionManager(sessionStore);
        var promptBuilder = new PromptBuilder(sm, "sword-will");

        var runner = new AgentRunner(llmClient, config, registry, executor,
            contextManager, soulManager, sessionManager, promptBuilder);

        var events = new List<AgentEvent>();
        await foreach (var ev in runner.RunAsync("太吾怎么玩", worldId: 1))
        {
            events.Add(ev);
        }

        // 应 yield ReferencesEvent
        events.Should().Contain(e => e is ReferencesEvent);
        var refsEvent = events.OfType<ReferencesEvent>().Single();
        refsEvent.References.Should().HaveCount(1);
        refsEvent.References[0].SourceUrl.Should().Be("https://wiki.example.com/a");

        // references 应被持久化到 session store
        var history = await sessionStore.LoadRecentAsync(1, 10);
        history.Should().HaveCount(2); // user + assistant
        history[1].Role.Should().Be("assistant");
        history[1].References.Should().NotBeNull();
        history[1].References!.Should().HaveCount(1);
        history[1].References![0].SourceUrl.Should().Be("https://wiki.example.com/a");
    }

    private static string PathRoot() =>
        Path.Combine(Path.GetTempPath(), "yaolao-runner-" + System.Guid.NewGuid().ToString("N"));

    private static SkillManager MakeSkillManager()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-runner-sm-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "registry.yaml"), @"
background:
  - id: taiwu-wiki-zhan-dou
    cn_name: 战斗
    overview_file: background/zhan-dou/overview.md
    detail_dir: background/zhan-dou/detail
personas:
  - id: sword-will
    cn_name: 剑中虚影
    file: personas/sword-will.md
");
        Directory.CreateDirectory(Path.Combine(dir, "background", "zhan-dou", "detail"));
        File.WriteAllText(Path.Combine(dir, "background", "zhan-dou", "overview.md"), "# 战斗\n概述");
        Directory.CreateDirectory(Path.Combine(dir, "personas"));
        File.WriteAllText(Path.Combine(dir, "personas", "sword-will.md"), "# 剑中虚影\n你是隐士");
        return new SkillManager(dir);
    }

    private sealed class StubLlmHandler : HttpMessageHandler
    {
        private readonly string _thinking;
        private readonly string _stream;
        private int _callCount;

        public string? LastThinkingRequestBody;

        public StubLlmHandler(string thinkingResponse, string streamResponse)
        {
            _thinking = thinkingResponse;
            _stream = streamResponse;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            _callCount++;
            // 第 1 次是 THINKING（非流式），第 2 次是 ANSWER（流式）
            var body = _callCount == 1 ? _thinking : _stream;
            var isStream = _callCount > 1;
            var content = new StringContent(body, Encoding.UTF8,
                isStream ? "text/event-stream" : "application/json");

            if (!isStream)
            {
                LastThinkingRequestBody = req.Content!.ReadAsStringAsync(ct).Result;
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class StubRagHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            var content = new StringContent("{\"context\":\"\",\"chunks\":[]}", Encoding.UTF8, "application/json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    /// <summary>支持 3 次调用的 LLM handler：thinking1(有 tool_calls) → thinking2(直答) → stream。</summary>
    private sealed class StubLlmHandlerWithToolCall : HttpMessageHandler
    {
        private readonly string _thinking1;
        private readonly string _thinking2;
        private readonly string _stream;
        private int _thinkingCount;

        public StubLlmHandlerWithToolCall(string thinking1, string thinking2, string stream)
        {
            _thinking1 = thinking1;
            _thinking2 = thinking2;
            _stream = stream;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            var reqBody = req.Content!.ReadAsStringAsync(ct).Result;
            var isStream = reqBody.Contains("\"stream\":true");
            string body;
            if (isStream)
            {
                body = _stream;
            }
            else
            {
                body = _thinkingCount == 0 ? _thinking1 : _thinking2;
                _thinkingCount++;
            }
            var content = new StringContent(body, Encoding.UTF8,
                isStream ? "text/event-stream" : "application/json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class StubRagHandlerWithRefs : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            var body = @"{""context"":""RAG 结果"",
""references"":[
  {""full_doc_id"":""doc-A"",""file_path"":""wiki/a.md"",
   ""source_url"":""https://wiki.example.com/a"",""source_type"":""wiki"",
   ""knowledge_type"":""机制"",""author"":""灰机"",
   ""game_version"":""1.0"",""snippet"":""片段"",""hit_count"":1}
]}";
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task RunAsync_LoadsHistoryFromSession_WhenHistoryParamNull()
    {
        var root = PathRoot();
        var llmHandler = new StubLlmHandler(
            thinkingResponse: "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"回答\"}}],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5}}",
            streamResponse: "data: {\"choices\":[{\"delta\":{\"content\":\"最终答案\"}}]}\n\ndata: {\"choices\":[],\"usage\":{\"prompt_tokens\":20,\"completion_tokens\":10}}\n\ndata: [DONE]\n\n");
        var sm = MakeSkillManager();
        var llmClient = new OpenAiCompatibleClient(llmHandler);
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };
        var ragClient = new RagHttpClient(new StubRagHandler(), "http://taiwuasker");
        var soulStore = new JsonSoulStore(root);
        var sessionStore = new JsonSessionStore(root);
        var sessionManager = new SessionManager(sessionStore);

        // 预存一轮历史
        await sessionManager.SaveConversationAsync(worldId: 1, userQuery: "上一轮问题", assistantAnswer: "上一轮回答");

        var registry = new ToolRegistry();
        var executor = new ToolExecutor(registry);
        var ctx = new ContextManager();
        var soulManager = new SoulManager(soulStore);
        var prompts = new PromptBuilder(sm);
        var runner = new AgentRunner(llmClient, config, registry, executor, ctx, soulManager, sessionManager, prompts);

        await foreach (var _ in runner.RunAsync(query: "这轮问题", worldId: 1)) { }

        // 验证 LLM 收到的 messages 含上一轮历史（通过 handler 捕获）
        llmHandler.LastThinkingRequestBody.Should().Contain("上一轮问题");
        llmHandler.LastThinkingRequestBody.Should().Contain("这轮问题");
    }

    [Fact]
    public async System.Threading.Tasks.Task RunAsync_CompressesHistory_WhenTokenExceedsThreshold()
    {
        var root = PathRoot();
        // 使用支持多次调用的 handler
        var llmHandler = new StubLlmHandlerWithToolCall(
            thinking1: "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"summary\\\":\\\"玩家问了历史问题\\\",\\\"profile_fields\\\":{},\\\"world_fields\\\":{}}\"}}],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5}}",
            thinking2: "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"我直接回答\"}}],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5}}",
            stream: "data: {\"choices\":[{\"delta\":{\"content\":\"最终答案\"}}]}\n\ndata: {\"choices\":[],\"usage\":{\"prompt_tokens\":20,\"completion_tokens\":10}}\n\ndata: [DONE]\n\n");
        var sm = MakeSkillManager();
        var llmClient = new OpenAiCompatibleClient(llmHandler);
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };
        var soulStore = new JsonSoulStore(root);
        var sessionStore = new JsonSessionStore(root);
        var sessionManager = new SessionManager(sessionStore);

        // 预存历史
        await sessionManager.SaveConversationAsync(1, "历史问题", "历史回答");

        var registry = new ToolRegistry();
        var executor = new ToolExecutor(registry);
        var soulManager = new SoulManager(soulStore);
        var ctx = new ContextManager(soulManager, llmClient, config, collapseThresholdTokens: 1);
        var prompts = new PromptBuilder(sm);
        var runner = new AgentRunner(llmClient, config, registry, executor, ctx, soulManager, sessionManager, prompts, maxIter: 6);

        var events = new List<AgentEvent>();
        await foreach (var evt in runner.RunAsync(query: "新问题", worldId: 1)) events.Add(evt);

        // 验证 yield 了 StatusEvent（压缩提示）
        events.Should().Contain(e => e.GetType() == typeof(StatusEvent) && ((StatusEvent)e).Message.Contains("压缩"));
        // 验证边界已追加到存储
        var (oldSummary, newMessages) = await sessionManager.LoadForAgentAsync(1);
        oldSummary.Should().Contain("历史问题");
        newMessages.Should().BeEmpty(); // 压缩后边界在末尾，之后无消息
    }
}
