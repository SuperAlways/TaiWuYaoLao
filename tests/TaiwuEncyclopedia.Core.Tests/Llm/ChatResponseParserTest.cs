using System.Collections.Generic;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Llm;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Llm;

public class ChatResponseParserTest
{
    [Fact]
    public void BuildBody_NonStream_ReturnsCorrectJson()
    {
        var messages = new List<LlmMessage>
        {
            new() { Role = "user", Content = "hello" }
        };
        var json = ChatResponseParser.BuildBody("test-model", messages, stream: false);
        json.Should().Contain("\"model\":\"test-model\"");
        json.Should().Contain("\"stream\":false");
        json.Should().Contain("\"messages\":");
        json.Should().NotContain("stream_options");
    }

    [Fact]
    public void BuildBody_Stream_IncludesStreamOptions()
    {
        var messages = new List<LlmMessage>();
        var json = ChatResponseParser.BuildBody("m", messages, stream: true);
        json.Should().Contain("\"stream\":true");
        json.Should().Contain("\"stream_options\"");
        json.Should().Contain("\"include_usage\":true");
    }

    [Fact]
    public void BuildBody_WithTools_IncludesToolChoice()
    {
        var messages = new List<LlmMessage>();
        var tools = new List<Dictionary<string, object>>
        {
            new() { ["type"] = "function", ["function"] = new Dictionary<string, object>() }
        };
        var json = ChatResponseParser.BuildBody("m", messages, false, tools);
        json.Should().Contain("\"tools\":");
        json.Should().Contain("\"tool_choice\":\"auto\"");
    }

    [Fact]
    public void ParseResponse_NormalResponse_ExtractsContent()
    {
        var json = @"{
            ""choices"": [{""message"": {""role"": ""assistant"", ""content"": ""Hello world""}}],
            ""usage"": {""prompt_tokens"": 10, ""completion_tokens"": 5}
        }";
        var result = ChatResponseParser.ParseResponse(json, AgentLLMRole.Answer);
        result.Content.Should().Be("Hello world");
        result.Usage.Should().NotBeNull();
        result.Usage!.PromptTokens.Should().Be(10);
        result.Usage!.CompletionTokens.Should().Be(5);
    }

    [Fact]
    public void ParseResponse_WithToolCalls_ExtractsToolCalls()
    {
        var json = @"{
            ""choices"": [{""message"": {""role"": ""assistant"", ""tool_calls"": [
                {""id"": ""call_1"", ""type"": ""function"",
                 ""function"": {""name"": ""lookup"", ""arguments"": ""{\""key\"":\""taiji\""}""}}
            ]}}]
        }";
        var result = ChatResponseParser.ParseResponse(json, AgentLLMRole.Thinking);
        result.ToolCalls.Should().NotBeNull();
        result.ToolCalls!.Count.Should().Be(1);
        result.ToolCalls[0].Id.Should().Be("call_1");
        result.ToolCalls[0].Function.Name.Should().Be("lookup");
    }

    [Fact]
    public void ParseChunk_ContentChunk_ReturnsContent()
    {
        var json = @"{""choices"":[{""delta"":{""content"":""partial""}}]}";
        var chunk = ChatResponseParser.ParseChunk(json);
        chunk.Should().NotBeNull();
        chunk!.Content.Should().Be("partial");
        chunk.Usage.Should().BeNull();
    }

    [Fact]
    public void ParseChunk_UsageOnlyChunk_ReturnsUsage()
    {
        var json = @"{""choices"":[],""usage"":{""prompt_tokens"":20,""completion_tokens"":10}}";
        var chunk = ChatResponseParser.ParseChunk(json);
        chunk.Should().NotBeNull();
        chunk!.Usage.Should().NotBeNull();
        chunk.Usage!.PromptTokens.Should().Be(20);
    }

    [Fact]
    public void ParseChunk_EmptyChoices_ReturnsNull()
    {
        var json = @"{""choices"":[]}";
        var chunk = ChatResponseParser.ParseChunk(json);
        chunk.Should().BeNull();
    }
}
