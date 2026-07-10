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
using TaiwuEncyclopedia.Core.Rag;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Session;
using TaiwuEncyclopedia.Core.Skills;
using TaiwuEncyclopedia.Core.Soul;
using TaiwuEncyclopedia.Core.Storage;
using TaiwuEncyclopedia.Core.Tools;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Integration;

/// <summary>
/// 端到端集成测试，验证 Core 层全链路：AgentRunner + 3 工具 + ContextManager + SoulManager + SessionManager 协作。
/// </summary>
public class EndToEndTest
{
    private static readonly string[] ReActLoopResponses = new[]
    {
        // 轮 1 THINKING: 返回 tool_call
        "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"id\":\"call_1\",\"type\":\"function\",\"function\":{\"name\":\"retrieve_rag\",\"arguments\":\"{\\\"query\\\":\\\"太吾\\\"}\"}}]}}],\"usage\":{\"prompt_tokens\":100,\"completion_tokens\":20}}",
        // 轮 2 THINKING: 返回直答（无 tool_calls）
        "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"根据资料...\"}}],\"usage\":{\"prompt_tokens\":200,\"completion_tokens\":10}}",
    };

    private static readonly string[] ExhaustionResponses = new[]
    {
        "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"id\":\"call_0\",\"type\":\"function\",\"function\":{\"name\":\"retrieve_rag\",\"arguments\":\"{\\\"query\\\":\\\"太吾绘卷战斗系统详解\\\"}\"}}]}}],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5}}",
        "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"id\":\"call_1\",\"type\":\"function\",\"function\":{\"name\":\"retrieve_rag\",\"arguments\":\"{\\\"query\\\":\\\"门派武功秘籍收集攻略\\\"}\"}}]}}],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5}}",
        "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"id\":\"call_2\",\"type\":\"function\",\"function\":{\"name\":\"retrieve_rag\",\"arguments\":\"{\\\"query\\\":\\\"村民好感度提升方法大全\\\"}\"}}]}}],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5}}",
    };

    private static readonly string[] LoopDetectionResponses = new[]
    {
        "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"id\":\"call_1\",\"type\":\"function\",\"function\":{\"name\":\"retrieve_rag\",\"arguments\":\"{\\\"query\\\":\\\"相同查询\\\"}\"}}]}}],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5}}",
        "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"id\":\"call_1\",\"type\":\"function\",\"function\":{\"name\":\"retrieve_rag\",\"arguments\":\"{\\\"query\\\":\\\"相同查询\\\"}\"}}]}}],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5}}",
    };

    private const string StreamResponse = "data: {\"choices\":[{\"delta\":{\"content\":\"最终答案\"}}]}\n\ndata: {\"choices\":[],\"usage\":{\"prompt_tokens\":50,\"completion_tokens\":10}}\n\ndata: [DONE]\n\n";
    private const string FallbackStreamResponse = "data: {\"choices\":[{\"delta\":{\"content\":\"兜底答案\"}}]}\n\ndata: {\"choices\":[],\"usage\":{\"prompt_tokens\":50,\"completion_tokens\":10}}\n\ndata: [DONE]\n\n";
    private const string LoopStreamResponse = "data: {\"choices\":[{\"delta\":{\"content\":\"循环检测答案\"}}]}\n\ndata: {\"choices\":[],\"usage\":{\"prompt_tokens\":50,\"completion_tokens\":10}}\n\ndata: [DONE]\n\n";
    /// <summary>
    /// 验证 ReAct 循环：先调用工具，然后收敛到最终答案。
    /// </summary>
    [Fact]
    public async Task ReActLoopWithToolCallThenConverge()
    {
        var root = PathRoot();
        var sm = MakeSkillManager();
        // LLM: 第1轮返回 tool_call(retrieve_rag)，第2轮返回直答
        var llmHandler = new SequenceLlmHandler(ReActLoopResponses, StreamResponse);

        var runner = BuildRunner(llmHandler, sm, root);

        var events = new List<AgentEvent>();
        await foreach (var ev in runner.RunAsync("太吾怎么玩", worldId: 1))
        {
            events.Add(ev);
        }

        // 事件序列: Start → ToolCall → ToolResult → FinalChunk → End
        events.Should().Contain(e => e is StartEvent);
        events.Should().Contain(e => e is ToolCallEvent);
        events.Should().Contain(e => e is ToolResultEvent);
        events.OfType<FinalChunkEvent>().Should().NotBeEmpty();
        events.Should().Contain(e => e is EndEvent);

        // 会话持久化
        var sessionStore = new JsonSessionStore(root);
        var history = await sessionStore.LoadRecentAsync(1, 10);
        history.Should().HaveCount(2); // user + assistant
        history[0].Content.Should().Be("太吾怎么玩");
        history[1].Content.Should().Be("最终答案");

        // 应 yield ReferencesEvent 且持久化 references
        events.Should().Contain(e => e is ReferencesEvent);
        var refsEvent = events.OfType<ReferencesEvent>().Single();
        refsEvent.References.Should().HaveCount(1);
        refsEvent.References[0].SourceUrl.Should().Be("https://wiki.example.com/a");

        // session 持久化
        history[1].References.Should().NotBeNull();
        history[1].References!.Should().HaveCount(1);
        history[1].References![0].SourceUrl.Should().Be("https://wiki.example.com/a");
    }

    /// <summary>
    /// 验证最大迭代次数耗尽时，强制给出兜底答案。
    /// </summary>
    [Fact]
    public async Task SixRoundExhaustionForcesAnswer()
    {
        var root = PathRoot();
        var sm = MakeSkillManager();
        // LLM: 每轮都返回 tool_call，直到 max_iter 耗尽
        // 使用完全不同的查询参数，确保 Jaccard 相似度 < 0.8，避免触发循环检测
        var llmHandler = new SequenceLlmHandler(ExhaustionResponses, FallbackStreamResponse);

        var runner = BuildRunner(llmHandler, sm, root, maxIter: 3);

        var events = new List<AgentEvent>();
        await foreach (var ev in runner.RunAsync("test", worldId: 1))
        {
            events.Add(ev);
        }

        // 3 轮 tool_call + 兜底 answer
        events.OfType<ToolCallEvent>().Should().HaveCount(3);
        events.OfType<FinalChunkEvent>().Should().NotBeEmpty();
        var endEvent = events.OfType<EndEvent>().Single();
        endEvent.TotalIterations.Should().BeGreaterThanOrEqualTo(3);
    }

    /// <summary>
    /// 验证循环检测：连续相似的工具调用会触发早期答案。
    /// </summary>
    [Fact]
    public async Task LoopDetectionForcesEarlyAnswer()
    {
        var root = PathRoot();
        var sm = MakeSkillManager();
        // LLM: 连续两轮返回相同的 tool_call
        var llmHandler = new SequenceLlmHandler(LoopDetectionResponses, LoopStreamResponse);

        var runner = BuildRunner(llmHandler, sm, root);

        var events = new List<AgentEvent>();
        await foreach (var ev in runner.RunAsync("test", worldId: 1))
        {
            events.Add(ev);
        }

        // 第2轮检测到循环 → 强制 answer
        events.OfType<ToolCallEvent>().Should().HaveCount(1); // 只第1轮执行了工具
        events.OfType<FinalChunkEvent>().Should().NotBeEmpty();
    }

    /// <summary>
    /// 验证魂（Soul）数据跨运行持久化。
    /// </summary>
    [Fact]
    public async Task SoulPersistsAcrossRuns()
    {
        var root = PathRoot();
        var sm = MakeSkillManager();
        var soulStore = new JsonSoulStore(root);
        var soulManager = new SoulManager(soulStore);
        await soulManager.SetPlayerFieldsAsync(new Dictionary<string, string> { ["Playstyle"] = "苟道流" });

        // 重新加载
        var soulStore2 = new JsonSoulStore(root);
        var soulManager2 = new SoulManager(soulStore2);
        var summary = await soulManager2.GetSoulSummaryAsync(1);
        summary.Should().Contain("苟道流");
    }

    // --- helpers ---

    private static AgentRunner BuildRunner(HttpMessageHandler llmHandler, SkillManager sm, string root, int maxIter = 6)
    {
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
        return new AgentRunner(llmClient, config, registry, executor,
            contextManager, soulManager, sessionManager, promptBuilder, maxIter);
    }

    private static string PathRoot() =>
        Path.Combine(Path.GetTempPath(), "yaolao-e2e-" + System.Guid.NewGuid().ToString("N"));

    private static SkillManager MakeSkillManager()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-e2e-sm-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "registry.yaml"), @"
background:
  - id: 战斗
    overview_file: background/战斗/战斗概述.md
    detail_dir: background/战斗/detail
personas:
  - id: sword-will
    cn_name: 剑中虚影
    file: personas/sword-will.md
");
        Directory.CreateDirectory(Path.Combine(dir, "background", "战斗", "detail"));
        File.WriteAllText(Path.Combine(dir, "background", "战斗", "战斗概述.md"), "# 战斗\n概述");
        Directory.CreateDirectory(Path.Combine(dir, "personas"));
        File.WriteAllText(Path.Combine(dir, "personas", "sword-will.md"), "# 剑中虚影\n你是隐士");
        return new SkillManager(dir);
    }

    /// <summary>按顺序返回 THINKING 响应，ANSWER 流式响应固定。</summary>
    private sealed class SequenceLlmHandler : HttpMessageHandler
    {
        private readonly string[] _thinkingResponses;
        private readonly string _streamResponse;
        private int _thinkingCount;
        private int _streamCount;

        public SequenceLlmHandler(string[] thinkingResponses, string streamResponse)
        {
            _thinkingResponses = thinkingResponses;
            _streamResponse = streamResponse;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            // 判断是流式还是非流式：流式请求的 body 含 "stream":true
            var body = req.Content!.ReadAsStringAsync(ct).Result;
            var isStream = body.Contains("\"stream\":true");

            string respBody;
            bool isStreamContent;
            if (isStream)
            {
                respBody = _streamResponse;
                isStreamContent = true;
                _streamCount++;
            }
            else
            {
                var idx = System.Math.Min(_thinkingCount, _thinkingResponses.Length - 1);
                respBody = _thinkingResponses[idx];
                isStreamContent = false;
                _thinkingCount++;
            }

            var content = new StringContent(respBody, Encoding.UTF8,
                isStreamContent ? "text/event-stream" : "application/json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class StubRagHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            var content = new StringContent("{\"context\":\"RAG结果\",\"references\":[{\"full_doc_id\":\"doc-A\",\"file_path\":\"wiki/a.md\",\"source_url\":\"https://wiki.example.com/a\",\"source_type\":\"wiki\",\"knowledge_type\":\"机制\",\"author\":\"灰机\",\"game_version\":\"1.0\",\"snippet\":\"片段\",\"hit_count\":1}]}", Encoding.UTF8, "application/json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
