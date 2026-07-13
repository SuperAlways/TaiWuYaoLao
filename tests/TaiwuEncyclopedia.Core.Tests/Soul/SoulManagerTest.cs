using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Agent;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Session;
using TaiwuEncyclopedia.Core.Soul;
using TaiwuEncyclopedia.Core.Storage;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Soul;

public class SoulManagerTest
{
    [Fact]
    public async Task GetSoulSummaryEmptyReturnsEmptyString()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-soul-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSoulStore(root);
        var sm = new SoulManager(store);

        var summary = await sm.GetSoulSummaryAsync(1);
        summary.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSoulSummaryWithDataReturnsFormatted()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-soul-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSoulStore(root);
        await store.SaveProfileAsync(new SoulProfile { Playstyle = "苟道流", TechnicalLevel = "老手" });
        await store.SaveWorldAsync(1, new SoulWorld { Sect = "少林", Stage = "中期" });

        var sm = new SoulManager(store);
        var summary = await sm.GetSoulSummaryAsync(1);
        summary.Should().Contain("玩法偏好: 苟道流");
        summary.Should().Contain("门派: 少林");
    }

    [Fact]
    public async Task SetPlayerFieldsMarksAsProtected()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-soul-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSoulStore(root);
        var sm = new SoulManager(store);

        await sm.SetPlayerFieldsAsync(new Dictionary<string, string> { ["Playstyle"] = "速通" });

        var profile = await store.LoadProfileAsync();
        profile.Playstyle.Should().Be("速通");
        profile.ProtectedFields.Should().Contain("Playstyle");
    }

    [Fact]
    public async Task UpdateFromCompressLlmFailureReturnsEmptySummary()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-soul-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSoulStore(root);
        var sm = new SoulManager(store);
        // 用抛异常的 llmClient 模拟失败
        var llmClient = new ThrowingLlmClient();
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        var summary = await sm.UpdateFromCompressAsync(1, "some history", llmClient, config);
        summary.Should().BeEmpty();
    }

    [Fact]
    public async Task ProtectedFieldsNotOverwrittenByUpdateFromCompress()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-soul-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSoulStore(root);
        var sm = new SoulManager(store);

        // 玩家主动设置 Playstyle 为 protected
        await sm.SetPlayerFieldsAsync(new Dictionary<string, string> { ["Playstyle"] = "速通" });

        // LLM 返回试图覆盖 Playstyle 的响应
        var extractionJson = @"{""summary"":""对话摘要"",""profile_fields"":{""Playstyle"":""苟道流"",""TechnicalLevel"":""老手""},""world_fields"":{""Sect"":""少林""}}";
        var llmClient = new StubLlmClient(new LlmResponse { Content = extractionJson, Usage = new TokenUsage() });
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        await sm.UpdateFromCompressAsync(1, "some history", llmClient, config);

        var profile = await store.LoadProfileAsync();
        profile.Playstyle.Should().Be("速通"); // protected，不被覆盖
        profile.TechnicalLevel.Should().Be("老手"); // 非 protected，被 LLM 更新
    }

    [Fact]
    public async Task UpdateFromCompressPregameSkipsSoulWorld()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-pregame-soul-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSoulStore(root);
        var sm = new SoulManager(store);

        // LLM 返回 profile + world 字段
        var extractionJson = @"{""summary"":""主界面对话摘要"",""profile_fields"":{""Playstyle"":""苟道流""},""world_fields"":{""Sect"":""少林""}}";
        var llmClient = new StubLlmClient(new LlmResponse { Content = extractionJson, Usage = new TokenUsage() });
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        await sm.UpdateFromCompressAsync(SessionManager.PregameWorldId, "主界面对话历史", llmClient, config);

        // Profile 应更新（主界面也写 Profile）
        var profile = await store.LoadProfileAsync();
        profile.Playstyle.Should().Be("苟道流");

        // SoulWorld 不应被写入（World--1.json 不创建，LoadWorldAsync 返回默认空值）
        var world = await store.LoadWorldAsync(SessionManager.PregameWorldId);
        world.Sect.Should().BeNullOrEmpty();
        world.Summary.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateFromCompressAsync_WithOldSummary_PassesOldSummaryToLlm()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-soul-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSoulStore(root);
        var sm = new SoulManager(store);
        string? capturedPrompt = null;
        var llmClient = new PromptCapturingLlmClient(
            response: new LlmResponse
            {
                Content = @"{""summary"":""新摘要"",""profile_fields"":{},""world_fields"":{}}",
                Usage = new TokenUsage(),
            },
            capturePrompt: p => capturedPrompt = p);
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        await sm.UpdateFromCompressAsync(worldId: 1, earlyHistoryText: "user: 新问题", llmClient, config, oldSummary: "旧摘要内容");

        capturedPrompt.Should().Contain("旧摘要内容");
        capturedPrompt.Should().Contain("新问题");
    }

    [Fact]
    public async Task UpdateFromCompressTruncatesLongHistoryBeforeFeedingLlm()
    {
        var root = Path.Combine(Path.GetTempPath(), "yaolao-soul-" + System.Guid.NewGuid().ToString("N"));
        var store = new JsonSoulStore(root);
        var sm = new SoulManager(store);
        string? capturedPrompt = null;
        var llmClient = new PromptCapturingLlmClient(
            response: new LlmResponse
            {
                Content = @"{""summary"":""ok"",""profile_fields"":{},""world_fields"":{}}",
                Usage = new TokenUsage(),
            },
            capturePrompt: p => capturedPrompt = p);
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        // 构造 15000 字符的历史（远超合理 Soul 提取所需）
        var longHistory = new string('x', 15000);

        await sm.UpdateFromCompressAsync(worldId: 1, earlyHistoryText: longHistory,
            llmClient, config, oldSummary: null);

        // prompt 中的 history 部分应被截断到 ≤ 4500 字符（4000 + oldSummary 开销）
        capturedPrompt.Should().NotBeNull();
        capturedPrompt!.Length.Should().BeLessThan(5000);
    }

    // --- Stub implementations ---

    private sealed class ThrowingLlmClient : ILlmClient
    {
        public Task<LlmResponse> ChatAsync(AgentLLMRole role, LlmConfig config,
            List<LlmMessage> messages, List<Dictionary<string, object>>? tools = null)
            => throw new System.Exception("LLM call failed");

        public async IAsyncEnumerable<StreamChunk> StreamChatAsync(LlmConfig config,
            List<LlmMessage> messages, [EnumeratorCancellation] CancellationToken ct)
        { yield break; }
    }

    private sealed class StubLlmClient : ILlmClient
    {
        private readonly LlmResponse _response;

        public StubLlmClient(LlmResponse response) { _response = response; }

        public Task<LlmResponse> ChatAsync(AgentLLMRole role, LlmConfig config,
            List<LlmMessage> messages, List<Dictionary<string, object>>? tools = null)
            => Task.FromResult(_response);

        public async IAsyncEnumerable<StreamChunk> StreamChatAsync(LlmConfig config,
            List<LlmMessage> messages, [EnumeratorCancellation] CancellationToken ct)
        { yield break; }
    }

    private sealed class PromptCapturingLlmClient : ILlmClient
    {
        private readonly LlmResponse _response;
        private readonly System.Action<string>? _capturePrompt;

        public PromptCapturingLlmClient(LlmResponse response, System.Action<string>? capturePrompt = null)
        { _response = response; _capturePrompt = capturePrompt; }

        public Task<LlmResponse> ChatAsync(AgentLLMRole role, LlmConfig config,
            List<LlmMessage> messages, List<Dictionary<string, object>>? tools = null)
        {
            if (messages.Count > 0 && messages[0].Content != null)
                _capturePrompt?.Invoke(messages[0].Content);
            return Task.FromResult(_response);
        }

        public async IAsyncEnumerable<StreamChunk> StreamChatAsync(LlmConfig config,
            List<LlmMessage> messages, [EnumeratorCancellation] CancellationToken ct)
        { yield break; }
    }
}
