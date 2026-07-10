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
        var llmClient = new DirectAnswerLlmClient();
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };
        var ragClient = new StubRagClient(new RagRetrieveResult { Context = "" });
        var soulStore = new JsonSoulStore(root);
        var sessionStore = new JsonSessionStore(root);

        var registry = new ToolRegistry();
        registry.Register(new RetrieveRagTool(ragClient));
        var sm = MakeSkillManager();
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
        var llmClient = new ToolCallThenAnswerLlmClient();
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };
        var ragClient = new StubRagClient(new RagRetrieveResult
        {
            Context = "RAG 结果",
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
        var soulStore = new JsonSoulStore(root);
        var sessionStore = new JsonSessionStore(root);

        var registry = new ToolRegistry();
        registry.Register(new RetrieveRagTool(ragClient));
        var sm = MakeSkillManager();
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

    [Fact]
    public async System.Threading.Tasks.Task RunAsync_LoadsHistoryFromSession_WhenHistoryParamNull()
    {
        var root = PathRoot();
        var llmClient = new HistoryCapturingLlmClient();
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };
        var ragClient = new StubRagClient(new RagRetrieveResult { Context = "" });
        var soulStore = new JsonSoulStore(root);
        var sessionStore = new JsonSessionStore(root);
        var sessionManager = new SessionManager(sessionStore);

        // 预存一轮历史
        await sessionManager.SaveConversationAsync(worldId: 1, userQuery: "上一轮问题", assistantAnswer: "上一轮回答");

        var registry = new ToolRegistry();
        var executor = new ToolExecutor(registry);
        var ctx = new ContextManager();
        var soulManager = new SoulManager(soulStore);
        var sm = MakeSkillManager();
        var prompts = new PromptBuilder(sm);
        var runner = new AgentRunner(llmClient, config, registry, executor, ctx, soulManager, sessionManager, prompts);

        await foreach (var _ in runner.RunAsync(query: "这轮问题", worldId: 1)) { }

        // 验证 LLM 收到的 messages 含上一轮历史（通过 client 捕获）
        llmClient.LastThinkingRequestBody.Should().Contain("上一轮问题");
        llmClient.LastThinkingRequestBody.Should().Contain("这轮问题");
    }

    [Fact]
    public async System.Threading.Tasks.Task RunAsync_CompressesHistory_WhenTokenExceedsThreshold()
    {
        var root = PathRoot();
        // 使用 compress 场景的 LLM client：第一次调用（compress）返回摘要，第二次（thinking）直答，流式答案
        var llmClient = new CompressThenAnswerLlmClient();
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };
        var soulStore = new JsonSoulStore(root);
        var sessionStore = new JsonSessionStore(root);
        var sessionManager = new SessionManager(sessionStore);

        // 预存历史
        await sessionManager.SaveConversationAsync(1, "历史问题", "历史回答");

        var registry = new ToolRegistry();
        var executor = new ToolExecutor(registry);
        var soulManager = new SoulManager(soulStore);
        var sm = MakeSkillManager();
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

    // --- Stub ILlmClient implementations ---

    /// <summary>直答：ChatAsync 返回无 tool_calls 的 thinking，StreamChatAsync 返回答案。</summary>
    private sealed class DirectAnswerLlmClient : ILlmClient
    {
        public Task<LlmResponse> ChatAsync(AgentLLMRole role, LlmConfig config,
            List<LlmMessage> messages, List<Dictionary<string, object>>? tools = null)
            => Task.FromResult(new LlmResponse
            {
                Content = "我直接回答",
                ToolCalls = null,
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5, CacheHitTokens = 0 },
            });

        public async IAsyncEnumerable<StreamChunk> StreamChatAsync(LlmConfig config,
            List<LlmMessage> messages, [EnumeratorCancellation] CancellationToken ct)
        {
            yield return new StreamChunk { Content = "最终答案" };
            yield return new StreamChunk
            {
                FinishReason = "stop",
                Usage = new TokenUsage { PromptTokens = 20, CompletionTokens = 10, CacheHitTokens = 0 },
            };
        }
    }

    /// <summary>先返回 tool_call(retrieve_rag)，再返回直答，然后流式答案。</summary>
    private sealed class ToolCallThenAnswerLlmClient : ILlmClient
    {
        private int _chatCallCount;

        public Task<LlmResponse> ChatAsync(AgentLLMRole role, LlmConfig config,
            List<LlmMessage> messages, List<Dictionary<string, object>>? tools = null)
        {
            _chatCallCount++;
            if (_chatCallCount == 1)
            {
                return Task.FromResult(new LlmResponse
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
                });
            }
            return Task.FromResult(new LlmResponse
            {
                Content = "根据资料回答",
                ToolCalls = null,
                Usage = new TokenUsage { PromptTokens = 200, CompletionTokens = 10, CacheHitTokens = 0 },
            });
        }

        public async IAsyncEnumerable<StreamChunk> StreamChatAsync(LlmConfig config,
            List<LlmMessage> messages, [EnumeratorCancellation] CancellationToken ct)
        {
            yield return new StreamChunk { Content = "最终答案" };
            yield return new StreamChunk
            {
                FinishReason = "stop",
                Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 10, CacheHitTokens = 0 },
            };
        }
    }

    /// <summary>捕获 ChatAsync 收到的 messages 文本，用于验证历史加载。</summary>
    private sealed class HistoryCapturingLlmClient : ILlmClient
    {
        public string? LastThinkingRequestBody { get; private set; }

        public Task<LlmResponse> ChatAsync(AgentLLMRole role, LlmConfig config,
            List<LlmMessage> messages, List<Dictionary<string, object>>? tools = null)
        {
            // 将所有 messages 拼接成文本以验证内容
            LastThinkingRequestBody = string.Join("|", messages.Select(m => m.Content ?? ""));
            return Task.FromResult(new LlmResponse
            {
                Content = "回答",
                ToolCalls = null,
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5, CacheHitTokens = 0 },
            });
        }

        public async IAsyncEnumerable<StreamChunk> StreamChatAsync(LlmConfig config,
            List<LlmMessage> messages, [EnumeratorCancellation] CancellationToken ct)
        {
            yield return new StreamChunk { Content = "最终答案" };
            yield return new StreamChunk
            {
                FinishReason = "stop",
                Usage = new TokenUsage { PromptTokens = 20, CompletionTokens = 10, CacheHitTokens = 0 },
            };
        }
    }

    /// <summary>压缩场景：所有 ChatAsync 调用都返回摘要/直答，流式返回最终答案。</summary>
    private sealed class CompressThenAnswerLlmClient : ILlmClient
    {
        public Task<LlmResponse> ChatAsync(AgentLLMRole role, LlmConfig config,
            List<LlmMessage> messages, List<Dictionary<string, object>>? tools = null)
            => Task.FromResult(new LlmResponse
            {
                Content = @"{""summary"":""玩家问了历史问题"",""profile_fields"":{},""world_fields"":{}}",
                ToolCalls = null,
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5, CacheHitTokens = 0 },
            });

        public async IAsyncEnumerable<StreamChunk> StreamChatAsync(LlmConfig config,
            List<LlmMessage> messages, [EnumeratorCancellation] CancellationToken ct)
        {
            yield return new StreamChunk { Content = "最终答案" };
            yield return new StreamChunk
            {
                FinishReason = "stop",
                Usage = new TokenUsage { PromptTokens = 20, CompletionTokens = 10, CacheHitTokens = 0 },
            };
        }
    }

    // --- Stub IRagClient ---

    private sealed class StubRagClient : IRagClient
    {
        private readonly RagRetrieveResult _result;
        public StubRagClient(RagRetrieveResult result) { _result = result; }
        public Task<RagRetrieveResult> RetrieveAsync(RagRetrieveRequest request)
            => Task.FromResult(_result);
    }
}
