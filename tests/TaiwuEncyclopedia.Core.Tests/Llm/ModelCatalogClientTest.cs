using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Llm;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Llm;

public class ModelCatalogClientTest
{
    // 复用 OpenAiCompatibleClientTest 的 ScriptedHandler（同一 test project 内的 public class）
    // 如果 ScriptedHandler 不是 public: 在此测试文件内部定义一份相同的

    [Fact]
    public async Task FetchModels_Success_ReturnsModels()
    {
        string json = @"{""data"":[{""id"":""deepseek-chat""},{""id"":""deepseek-reasoner""}]}";
        var handler = new ModelCatalogScriptedHandler(HttpStatusCode.OK, json);

        var result = await ModelCatalogClient.FetchModelsAsync(
            "http://test", "sk-test", handler);

        result.Success.Should().BeTrue();
        result.Models.Should().BeEquivalentTo(["deepseek-chat", "deepseek-reasoner"]);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task FetchModels_ParsesModelsArray()
    {
        string json = @"{""object"":""list"",""models"":[{""id"":""gpt-4""},{""id"":""gpt-3.5-turbo""}]}";
        var handler = new ModelCatalogScriptedHandler(HttpStatusCode.OK, json);

        var result = await ModelCatalogClient.FetchModelsAsync(
            "http://test", "sk-test", handler);

        result.Success.Should().BeTrue();
        result.Models.Should().BeEquivalentTo(["gpt-3.5-turbo", "gpt-4"]); // 字母序
    }

    [Fact]
    public async Task FetchModels_ParsesItemsWithNameField()
    {
        string json = @"{""data"":[{""name"":""claude-3.5-sonnet""}]}";
        var handler = new ModelCatalogScriptedHandler(HttpStatusCode.OK, json);

        var result = await ModelCatalogClient.FetchModelsAsync(
            "http://test", "sk-test", handler);

        result.Success.Should().BeTrue();
        result.Models.Should().BeEquivalentTo(["claude-3.5-sonnet"]);
    }

    [Fact]
    public async Task FetchModels_ParsesStringArrayItems()
    {
        string json = @"{""data"":[""model-a"",""model-b"",""model-a""]}"; // 含重复
        var handler = new ModelCatalogScriptedHandler(HttpStatusCode.OK, json);

        var result = await ModelCatalogClient.FetchModelsAsync(
            "http://test", "sk-test", handler);

        result.Success.Should().BeTrue();
        result.Models.Should().BeEquivalentTo(["model-a", "model-b"]); // 去重
    }

    [Fact]
    public async Task FetchModels_401_ReturnsErrorNotSuccess()
    {
        var handler = new ModelCatalogScriptedHandler(HttpStatusCode.Unauthorized, "{}");

        var result = await ModelCatalogClient.FetchModelsAsync(
            "http://test", "sk-test", handler);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Key 无效");
        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchModels_404_ReturnsErrorNotSuccess()
    {
        var handler = new ModelCatalogScriptedHandler(HttpStatusCode.NotFound, "{}");

        var result = await ModelCatalogClient.FetchModelsAsync(
            "http://test", "sk-test", handler);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("服务商未提供模型列表接口");
        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchModels_5xx_ReturnsErrorNotSuccess()
    {
        var handler = new ModelCatalogScriptedHandler(HttpStatusCode.ServiceUnavailable, "{}");

        var result = await ModelCatalogClient.FetchModelsAsync(
            "http://test", "sk-test", handler);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("暂时不可用");
        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchModels_SendsApiKeyHeader()
    {
        var handler = new ModelCatalogScriptedHandler(HttpStatusCode.OK,
            @"{""data"":[{""id"":""test-model""}]}");

        await ModelCatalogClient.FetchModelsAsync("http://test", "sk-abc123", handler);

        handler.LastAuthHeader.Should().Be("Bearer sk-abc123");
    }
}

// 专用 HttpMessageHandler（与 OpenAiCompatibleClientTest.ScriptedHandler 同模式）
public sealed class ModelCatalogScriptedHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _responseBody;
    public int CallCount { get; private set; }
    public string? LastAuthHeader { get; private set; }

    public ModelCatalogScriptedHandler(HttpStatusCode status, string responseBody)
    {
        _status = status;
        _responseBody = responseBody;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
    {
        CallCount++;
        LastAuthHeader = request.Headers.Authorization?.ToString();
        return Task.FromResult(new HttpResponseMessage(_status)
        {
            Content = new StringContent(_responseBody)
        });
    }
}
