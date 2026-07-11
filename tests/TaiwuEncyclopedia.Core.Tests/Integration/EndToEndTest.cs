using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
    /// <summary>
    /// 验证 ReAct 循环：先调用工具，然后收敛到最终答案。
    /// </summary>
    [Fact]
    public async Task ReActLoopWithToolCallThenConverge()
    {
        var root = PathRoot();
        var sm = MakeSkillManager();
        var llmClient = new SequenceLlmClient(
            thinkingResponses: new[]
            {
                // 轮 1 THINKING: 返回 tool_call(retrieve_rag)
                new LlmResponse
                {
                    Content = "",
                    ToolCalls = new List<ToolCall>
                    {
                        new()
                        {
                            Id = "call_1",
                            Type = "function",
                            Function = new ToolCallFunction
                            {
                                Name = "retrieve_rag",
                                Arguments = "{\"query\":\"太吾\"}",
                            },
                        },
                    },
                    Usage = new TokenUsage { PromptTokens = 100, CompletionTokens = 20, CacheHitTokens = 0 },
                },
                // 轮 2 THINKING: 返回直答（无 tool_calls）
                new LlmResponse
                {
                    Content = "根据资料...",
                    ToolCalls = null,
                    Usage = new TokenUsage { PromptTokens = 200, CompletionTokens = 10, CacheHitTokens = 0 },
                },
            },
            streamChunks: new[]
            {
                new StreamChunk { Content = "最终答案" },
                new StreamChunk { FinishReason = "stop", Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 10, CacheHitTokens = 0 } },
            });
        var ragClient = new StubRagClient(new RagRetrieveResult
        {
            Context = "RAG结果",
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
                    HitCount = 1,
                },
            },
        });

        var runner = BuildRunner(llmClient, ragClient, sm, root);

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
        var llmClient = new SequenceLlmClient(
            thinkingResponses: new[]
            {
                new LlmResponse
                {
                    Content = "",
                    ToolCalls = new List<ToolCall>
                    {
                        new() { Id = "call_0", Type = "function", Function = new ToolCallFunction { Name = "retrieve_rag", Arguments = "{\"query\":\"太吾绘卷战斗系统详解\"}" } },
                    },
                    Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5, CacheHitTokens = 0 },
                },
                new LlmResponse
                {
                    Content = "",
                    ToolCalls = new List<ToolCall>
                    {
                        new() { Id = "call_1", Type = "function", Function = new ToolCallFunction { Name = "retrieve_rag", Arguments = "{\"query\":\"门派武功秘籍收集攻略\"}" } },
                    },
                    Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5, CacheHitTokens = 0 },
                },
                new LlmResponse
                {
                    Content = "",
                    ToolCalls = new List<ToolCall>
                    {
                        new() { Id = "call_2", Type = "function", Function = new ToolCallFunction { Name = "retrieve_rag", Arguments = "{\"query\":\"村民好感度提升方法大全\"}" } },
                    },
                    Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5, CacheHitTokens = 0 },
                },
            },
            streamChunks: new[]
            {
                new StreamChunk { Content = "兜底答案" },
                new StreamChunk { FinishReason = "stop", Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 10, CacheHitTokens = 0 } },
            });

        var ragClient = new StubRagClient(new RagRetrieveResult { Context = "RAG结果" });
        var runner = BuildRunner(llmClient, ragClient, sm, root, maxIter: 3);

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
        var llmClient = new SequenceLlmClient(
            thinkingResponses: new[]
            {
                new LlmResponse
                {
                    Content = "",
                    ToolCalls = new List<ToolCall>
                    {
                        new() { Id = "call_1", Type = "function", Function = new ToolCallFunction { Name = "retrieve_rag", Arguments = "{\"query\":\"相同查询\"}" } },
                    },
                    Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5, CacheHitTokens = 0 },
                },
                new LlmResponse
                {
                    Content = "",
                    ToolCalls = new List<ToolCall>
                    {
                        new() { Id = "call_1", Type = "function", Function = new ToolCallFunction { Name = "retrieve_rag", Arguments = "{\"query\":\"相同查询\"}" } },
                    },
                    Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5, CacheHitTokens = 0 },
                },
            },
            streamChunks: new[]
            {
                new StreamChunk { Content = "循环检测答案" },
                new StreamChunk { FinishReason = "stop", Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 10, CacheHitTokens = 0 } },
            });

        var ragClient = new StubRagClient(new RagRetrieveResult { Context = "RAG结果" });
        var runner = BuildRunner(llmClient, ragClient, sm, root);

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

    private static AgentRunner BuildRunner(ILlmClient llmClient, IRagClient ragClient, SkillManager sm, string root, int maxIter = 6)
    {
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };
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
    private sealed class SequenceLlmClient : ILlmClient
    {
        private readonly LlmResponse[] _thinkingResponses;
        private readonly StreamChunk[] _streamChunks;
        private int _thinkingCount;

        public SequenceLlmClient(LlmResponse[] thinkingResponses, StreamChunk[] streamChunks)
        {
            _thinkingResponses = thinkingResponses;
            _streamChunks = streamChunks;
        }

        public Task<LlmResponse> ChatAsync(AgentLLMRole role, LlmConfig config,
            List<LlmMessage> messages, List<Dictionary<string, object>>? tools = null)
        {
            var idx = System.Math.Min(_thinkingCount, _thinkingResponses.Length - 1);
            _thinkingCount++;
            return Task.FromResult(_thinkingResponses[idx]);
        }

        public async IAsyncEnumerable<StreamChunk> StreamChatAsync(LlmConfig config,
            List<LlmMessage> messages, [EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var chunk in _streamChunks)
                yield return chunk;
        }
    }

    // --- Stub IRagClient ---

    private sealed class StubRagClient : IRagClient
    {
        private readonly RagRetrieveResult _result;
        public StubRagClient(RagRetrieveResult result) { _result = result; }
        public Task<RagRetrieveResult> RetrieveAsync(RagRetrieveRequest request, CancellationToken ct = default)
            => Task.FromResult(_result);
    }
}
