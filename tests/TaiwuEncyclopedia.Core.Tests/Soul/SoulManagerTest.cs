using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
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
        // 用 null llmClient 模拟失败（实际调用会 NPE → catch → 返回空）
        var llmClient = new OpenAiCompatibleClient(new FailingHandler());
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

        // LLM 返回试图覆盖 Playstyle 的响应（OpenAI chat completion 格式）
        var extractionJson = @"{""summary"":""对话摘要"",""profile_fields"":{""Playstyle"":""苟道流"",""TechnicalLevel"":""老手""},""world_fields"":{""Sect"":""少林""}}";
        var responseJson = Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            choices = new[] { new { message = new { role = "assistant", content = extractionJson } } },
            usage = new { prompt_tokens = 10, completion_tokens = 5 }
        });
        var handler = new StubHandler(responseJson);
        var llmClient = new OpenAiCompatibleClient(handler);
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
        var responseJson = Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            choices = new[] { new { message = new { role = "assistant", content = extractionJson } } },
            usage = new { prompt_tokens = 10, completion_tokens = 5 }
        });
        var handler = new StubHandler(responseJson);
        var llmClient = new OpenAiCompatibleClient(handler);
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
        var handler = new StubLlmHandlerForSoul(
            responseJson: "{\"summary\":\"新摘要\",\"profile_fields\":{},\"world_fields\":{}}",
            capturePrompt: p => capturedPrompt = p);
        var llmClient = new OpenAiCompatibleClient(handler);
        var config = new LlmConfig { ApiKey = "k", Model = "m", BaseUrl = "http://test" };

        await sm.UpdateFromCompressAsync(worldId: 1, earlyHistoryText: "user: 新问题", llmClient, config, oldSummary: "旧摘要内容");

        capturedPrompt.Should().Contain("旧摘要内容");
        capturedPrompt.Should().Contain("新问题");
    }

    private sealed class FailingHandler : System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken ct)
            => throw new System.Net.Http.HttpRequestException("connection failed");
    }

    private sealed class StubHandler : System.Net.Http.HttpMessageHandler
    {
        private readonly string _response;
        public StubHandler(string response) { _response = response; }

        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken ct)
        {
            return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(_response, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StubLlmHandlerForSoul : System.Net.Http.HttpMessageHandler
    {
        private readonly string _responseJson;
        private readonly System.Action<string>? _capturePrompt;
        public StubLlmHandlerForSoul(string responseJson, System.Action<string>? capturePrompt = null)
        { _responseJson = responseJson; _capturePrompt = capturePrompt; }
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken ct)
        {
            var body = request.Content?.ReadAsStringAsync(ct).Result ?? "";
            // 从请求 body 解出 messages[0].content 作为 prompt
            try
            {
                var obj = Newtonsoft.Json.Linq.JObject.Parse(body);
                _capturePrompt?.Invoke(obj["messages"]?[0]?["content"]?.ToString() ?? "");
            }
            catch { }
            return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new System.Net.Http.StringContent($"{{\"choices\":[{{\"message\":{{\"role\":\"assistant\",\"content\":{_responseJson}}}}}],\"usage\":{{\"prompt_tokens\":10,\"completion_tokens\":5}}}}", System.Text.Encoding.UTF8, "application/json") });
        }
    }
}
