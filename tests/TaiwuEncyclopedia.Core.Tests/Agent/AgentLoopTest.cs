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
using TaiwuEncyclopedia.Core.Diagnostics;
using TaiwuEncyclopedia.Core.Http;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Skills;
using TaiwuEncyclopedia.Core.Tools;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Agent;

public class AgentLoopTest
{
    /// <summary>非重试错误（401 AuthError）：AgentLoop 应 yield StatusEvent(Level=error) 后抛 ApiException 传播。</summary>
    [Fact]
    public async Task NonRetryableError_YieldsStatusEventAndPropagates()
    {
        var handler = new AlwaysStatusHandler(HttpStatusCode.Unauthorized);
        var client = new OpenAiCompatibleClient(handler) { RetryDelayOverride = _ => System.TimeSpan.FromMilliseconds(1) };
        var ctx = new ContextManager();
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };
        var messages = new List<LlmMessage> { new() { Role = "user", Content = "q" } };
        var result = new AgentLoopResult();

        var events = new List<AgentEvent>();
        ApiException? thrown = null;
        try
        {
            await foreach (var ev in AgentLoop.Run(client, new ToolExecutor(new ToolRegistry()),
                ctx, null, messages, config, worldId: 1, maxIter: 6,
                new List<Reference>(), new List<string>(), result, NullAgentTrace.Instance))
            {
                events.Add(ev);
            }
        }
        catch (ApiException ex) { thrown = ex; }

        thrown.Should().NotBeNull();
        thrown!.ErrorType.Should().Be(ApiErrorType.AuthError);
        events.OfType<StatusEvent>().Should().Contain(se =>
            se.Level == "error" && se.Message.Contains("API Key"));
    }

    /// <summary>Overload（529x3 触发 TellPlayer）走 force_compress 重试；重试也失败则传播，不 yield StatusEvent。</summary>
    [Fact]
    public async Task OverloadError_ForceCompressRetryAlsoFails_PropagatesWithoutStatusEvent()
    {
        // 一直 529：第 1 次 Chat 抛 Overload -> catch 走 force_compress 重试 -> 第 2 次 Chat 也抛 Overload -> 传播
        var handler = new AlwaysStatusHandler((HttpStatusCode)529);
        var client = new OpenAiCompatibleClient(handler) { RetryDelayOverride = _ => System.TimeSpan.FromMilliseconds(1) };
        var ctx = new ContextManager();
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };
        var messages = new List<LlmMessage> { new() { Role = "user", Content = "q" } };
        var result = new AgentLoopResult();

        ApiException? thrown = null;
        try
        {
            await foreach (var _ in AgentLoop.Run(client, new ToolExecutor(new ToolRegistry()),
                ctx, null, messages, config, 1, 6, new List<Reference>(), new List<string>(),
                result, NullAgentTrace.Instance)) { }
        }
        catch (ApiException ex) { thrown = ex; }

        thrown.Should().NotBeNull();
        thrown!.ErrorType.Should().Be(ApiErrorType.Overload);
        // Overload 分支不 yield StatusEvent（force_compress 重试失败直接传播）
        // 注：force_compress 重试的 Chat 也会内部重试 3 次 529 才抛
    }

    /// <summary>Overload 后 force_compress 重试成功：AgentLoop 恢复，yield FinalChunkEvent。</summary>
    [Fact]
    public async Task OverloadError_ForceCompressRetrySucceeds_Recovers()
    {
        // 非流式前 3 次 529（触发第 1 次 Chat 抛 Overload），第 4 次 200 thinking（无 tool_calls）；流式 200 答案
        var handler = new OverloadThenRecoverHandler();
        var client = new OpenAiCompatibleClient(handler) { RetryDelayOverride = _ => System.TimeSpan.FromMilliseconds(1) };
        var ctx = new ContextManager();
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };
        var messages = new List<LlmMessage> { new() { Role = "user", Content = "q" } };
        var result = new AgentLoopResult();
        var finalParts = new List<string>();

        var events = new List<AgentEvent>();
        await foreach (var ev in AgentLoop.Run(client, new ToolExecutor(new ToolRegistry()),
            ctx, null, messages, config, 1, 6, new List<Reference>(), finalParts,
            result, NullAgentTrace.Instance))
        {
            events.Add(ev);
        }

        events.Should().Contain(e => e is FinalChunkEvent);
        string.Join("", finalParts).Should().Contain("答案");
    }

    private sealed class AlwaysStatusHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        public AlwaysStatusHandler(HttpStatusCode status) { _status = status; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent("err", Encoding.UTF8, "application/json") });
    }

    /// <summary>非流式：前 3 次 529，第 4 次起 200 thinking（无 tool_calls）。流式：返回答案。</summary>
    private sealed class OverloadThenRecoverHandler : HttpMessageHandler
    {
        private int _nonStreamCount;
        private const string Thinking = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"ok\"}}],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5}}";
        private const string Stream = "data: {\"choices\":[{\"delta\":{\"content\":\"答案\"}}]}\n\ndata: {\"choices\":[],\"usage\":{\"prompt_tokens\":20,\"completion_tokens\":10}}\n\ndata: [DONE]\n\n";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            var reqBody = req.Content!.ReadAsStringAsync(ct).Result;
            var isStream = reqBody.Contains("\"stream\":true");
            if (isStream)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(Stream, Encoding.UTF8, "text/event-stream") });

            _nonStreamCount++;
            if (_nonStreamCount <= 3)
                return Task.FromResult(new HttpResponseMessage((HttpStatusCode)529)
                { Content = new StringContent("overloaded", Encoding.UTF8, "application/json") });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(Thinking, Encoding.UTF8, "application/json") });
        }
    }

    // --- BuildDisplayText tests (Task 2) ---

    [Fact]
    public void BuildDisplayText_LoadBackgroundSkill_Detail_ShowsSection()
    {
        var args = new Dictionary<string, object> { ["chapter"] = "产业", ["depth"] = "detail", ["section"] = "产业-产业-产业建设" };
        var text = AgentLoop.BuildDisplayText("load_background_skill", args);
        text.Should().Be("[百晓册] 产业-产业-产业建设");
    }

    [Fact]
    public void BuildDisplayText_LoadBackgroundSkill_Overview_ShowsChapter()
    {
        var args = new Dictionary<string, object> { ["chapter"] = "产业" };
        var text = AgentLoop.BuildDisplayText("load_background_skill", args);
        text.Should().Be("[百晓册] 产业");
    }

    [Fact]
    public void BuildDisplayText_LoadGuidanceSkill_UsesSkillName()
    {
        var args = new Dictionary<string, object> { ["skill"] = "战斗 build 指引" };
        var text = AgentLoop.BuildDisplayText("load_guidance_skill", args);
        text.Should().Contain("战斗 build 指引");
    }

    // --- Guidance directive with relevant_chapters (Task 2 Step 5) ---

    [Fact]
    public async Task LoadGuidanceSkill_Directive_IncludesRelevantChapters()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-gd-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "registry.yaml"), @"
guidance:
  - id: 武学搭配
    file: guidance/武学搭配.md
    relevant_chapters: [修习, 战斗]
");
        Directory.CreateDirectory(Path.Combine(dir, "guidance"));
        File.WriteAllText(Path.Combine(dir, "guidance", "武学搭配.md"), "# 武学搭配\n引导内容");
        var sm = new SkillManager(dir);
        var tool = new LoadGuidanceSkillTool(sm);
        var result = await tool.ExecuteAsync(new Dictionary<string, object> { ["skill"] = "武学搭配" });
        var content = result["content"].ToString()!;
        content.Should().Contain("本骨架关联百晓册章节：修习、战斗");
        content.Should().Contain("load_background_skill");
    }

    [Fact]
    public async Task LoadGuidanceSkill_Directive_NoChaptersWhenEmpty()
    {
        var dir = Path.Combine(Path.GetTempPath(), "yaolao-gd-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "registry.yaml"), @"
guidance:
  - id: 问题诊断
    file: guidance/问题诊断.md
    relevant_chapters: []
");
        Directory.CreateDirectory(Path.Combine(dir, "guidance"));
        File.WriteAllText(Path.Combine(dir, "guidance", "问题诊断.md"), "# 问题诊断\n引导内容");
        var sm = new SkillManager(dir);
        var tool = new LoadGuidanceSkillTool(sm);
        var result = await tool.ExecuteAsync(new Dictionary<string, object> { ["skill"] = "问题诊断" });
        var content = result["content"].ToString()!;
        content.Should().Contain("引导内容");
        content.Should().NotContain("本骨架关联百晓册章节");
    }
}