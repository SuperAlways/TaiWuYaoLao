using System.Collections.Generic;
using System.IO;
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
        var promptBuilder = new PromptBuilder(sm, "ring-elder");

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
  - id: ring-elder
    cn_name: 戒指老爷爷
    file: personas/ring-elder.md
");
        Directory.CreateDirectory(Path.Combine(dir, "background", "zhan-dou", "detail"));
        File.WriteAllText(Path.Combine(dir, "background", "zhan-dou", "overview.md"), "# 战斗\n概述");
        Directory.CreateDirectory(Path.Combine(dir, "personas"));
        File.WriteAllText(Path.Combine(dir, "personas", "ring-elder.md"), "# 戒指老爷爷\n你是隐士");
        return new SkillManager(dir);
    }

    private sealed class StubLlmHandler : HttpMessageHandler
    {
        private readonly string _thinking;
        private readonly string _stream;
        private int _callCount;

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
}
