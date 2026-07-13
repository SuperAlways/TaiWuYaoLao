using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Agent;
using TaiwuEncyclopedia.Core.Context;
using TaiwuEncyclopedia.Core.Diagnostics;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Rag;
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
        var client = new AuthErrorLlmClient();
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

    /// <summary>Overload 触发 force_compress 重试；重试也失败则传播，不 yield StatusEvent。</summary>
    [Fact]
    public async Task OverloadError_ForceCompressRetryAlsoFails_PropagatesWithoutStatusEvent()
    {
        // 一直抛 Overload：第 1 次 ChatAsync 抛 Overload -> catch 走 force_compress 重试 -> 第 2 次 ChatAsync 也抛 Overload -> 传播
        var client = new OverloadLlmClient();
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
    }

    /// <summary>Overload 后 force_compress 重试成功：AgentLoop 恢复，yield FinalChunkEvent。</summary>
    [Fact]
    public async Task OverloadError_ForceCompressRetrySucceeds_Recovers()
    {
        // 第 1 次 ChatAsync 抛 Overload，第 2 次（重试后）成功返回 thinking（无 tool_calls），流式答案
        var client = new OverloadThenRecoverLlmClient();
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

    // --- Stub ILlmClient implementations ---

    private sealed class AuthErrorLlmClient : ILlmClient
    {
        public Task<LlmResponse> ChatAsync(AgentLLMRole role, LlmConfig config,
            List<LlmMessage> messages, List<Dictionary<string, object>>? tools = null)
            => throw new ApiException(ApiErrorType.AuthError, "API Key 无效", "error");

        public async IAsyncEnumerable<StreamChunk> StreamChatAsync(LlmConfig config,
            List<LlmMessage> messages, [EnumeratorCancellation] CancellationToken ct)
        { yield break; }
    }

    private sealed class OverloadLlmClient : ILlmClient
    {
        public Task<LlmResponse> ChatAsync(AgentLLMRole role, LlmConfig config,
            List<LlmMessage> messages, List<Dictionary<string, object>>? tools = null)
            => throw new ApiException(ApiErrorType.Overload, "服务器过载", "warning");

        public async IAsyncEnumerable<StreamChunk> StreamChatAsync(LlmConfig config,
            List<LlmMessage> messages, [EnumeratorCancellation] CancellationToken ct)
        { yield break; }
    }

    /// <summary>ContextTooLong 触发 force_compress 重试；重试成功恢复，yield FinalChunkEvent。</summary>
    [Fact]
    public async Task ContextTooLongTriggersForceCompressAndRecovers()
    {
        var client = new ContextTooLongThenRecoverLlmClient();
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

    /// <summary>ContextTooLong 触发 force_compress 重试；重试也失败则传播 ApiException。</summary>
    [Fact]
    public async Task ContextTooLongForceCompressRetryAlsoFails_Propagates()
    {
        var client = new ContextTooLongLlmClient();
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
        thrown!.ErrorType.Should().Be(ApiErrorType.ContextTooLong);
    }

    /// <summary>第 1 次 ChatAsync 抛 Overload，第 2 次起成功（thinking 无 tool_calls），流式返回答案。</summary>
    private sealed class OverloadThenRecoverLlmClient : ILlmClient
    {
        private int _chatCallCount;

        public Task<LlmResponse> ChatAsync(AgentLLMRole role, LlmConfig config,
            List<LlmMessage> messages, List<Dictionary<string, object>>? tools = null)
        {
            _chatCallCount++;
            if (_chatCallCount == 1)
                throw new ApiException(ApiErrorType.Overload, "服务器过载", "warning");
            return Task.FromResult(new LlmResponse
            {
                Content = "ok",
                ToolCalls = null,
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5, CacheHitTokens = 0 },
            });
        }

        public async IAsyncEnumerable<StreamChunk> StreamChatAsync(LlmConfig config,
            List<LlmMessage> messages, [EnumeratorCancellation] CancellationToken ct)
        {
            yield return new StreamChunk { Content = "答案" };
            yield return new StreamChunk
            {
                FinishReason = "stop",
                Usage = new TokenUsage { PromptTokens = 20, CompletionTokens = 10, CacheHitTokens = 0 },
            };
        }
    }

    /// <summary>第 1 次 ChatAsync 抛 ContextTooLong，第 2 次起成功，流式返回答案。</summary>
    private sealed class ContextTooLongThenRecoverLlmClient : ILlmClient
    {
        private int _chatCallCount;

        public Task<LlmResponse> ChatAsync(AgentLLMRole role, LlmConfig config,
            List<LlmMessage> messages, List<Dictionary<string, object>>? tools = null)
        {
            _chatCallCount++;
            if (_chatCallCount == 1)
                throw new ApiException(ApiErrorType.ContextTooLong, "context length exceeded", "warn");
            return Task.FromResult(new LlmResponse
            {
                Content = "ok",
                ToolCalls = null,
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5, CacheHitTokens = 0 },
            });
        }

        public async IAsyncEnumerable<StreamChunk> StreamChatAsync(LlmConfig config,
            List<LlmMessage> messages, [EnumeratorCancellation] CancellationToken ct)
        {
            yield return new StreamChunk { Content = "答案" };
            yield return new StreamChunk
            {
                FinishReason = "stop",
                Usage = new TokenUsage { PromptTokens = 20, CompletionTokens = 10, CacheHitTokens = 0 },
            };
        }
    }

    // --- References dedup tests (Task 7: P1-7) ---

    [Fact]
    public void DedupReferences_EmptyFullDocId_DifferentFilePath_KeepsBoth()
    {
        // Empty FullDocId + different FilePath => should NOT merge
        var refs = new List<Reference>
        {
            new() { FullDocId = "", FilePath = "剑冢攻略.md", HitCount = 1 },
            new() { FullDocId = "", FilePath = "门派关系.md", HitCount = 1 },
        };
        AgentLoop.DedupReferences(refs);
        refs.Should().HaveCount(2);
    }

    [Fact]
    public void DedupReferences_SameFullDocId_MergesHitCount()
    {
        var refs = new List<Reference>
        {
            new() { FullDocId = "doc-1", FilePath = "剑冢攻略.md", HitCount = 2 },
            new() { FullDocId = "doc-1", FilePath = "剑冢攻略.md", HitCount = 3 },
        };
        AgentLoop.DedupReferences(refs);
        refs.Should().HaveCount(1);
        refs[0].HitCount.Should().Be(5);
    }

    [Fact]
    public void DedupReferences_EmptyFullDocId_SameFilePath_MergesHitCount()
    {
        var refs = new List<Reference>
        {
            new() { FullDocId = "", FilePath = "剑冢攻略.md", HitCount = 2 },
            new() { FullDocId = "", FilePath = "剑冢攻略.md", HitCount = 3 },
        };
        AgentLoop.DedupReferences(refs);
        refs.Should().HaveCount(1);
        refs[0].HitCount.Should().Be(5);
    }

    [Fact]
    public void DedupReferences_AllKeysEmpty_KeepsAll()
    {
        // Both FullDocId and FilePath are empty => no key to merge on, keep all
        var refs = new List<Reference>
        {
            new() { FullDocId = "", FilePath = "", HitCount = 1 },
            new() { FullDocId = "", FilePath = "", HitCount = 1 },
        };
        AgentLoop.DedupReferences(refs);
        refs.Should().HaveCount(2);
    }

    /// <summary>始终抛 ContextTooLong，用于测试重试也失败场景。</summary>
    private sealed class ContextTooLongLlmClient : ILlmClient
    {
        public Task<LlmResponse> ChatAsync(AgentLLMRole role, LlmConfig config,
            List<LlmMessage> messages, List<Dictionary<string, object>>? tools = null)
            => throw new ApiException(ApiErrorType.ContextTooLong, "context length exceeded", "warn");

        public async IAsyncEnumerable<StreamChunk> StreamChatAsync(LlmConfig config,
            List<LlmMessage> messages, [EnumeratorCancellation] CancellationToken ct)
        { yield break; }
    }
}
