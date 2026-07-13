using FluentAssertions;
using TaiwuEncyclopedia.Core.Llm;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Llm;

public class EndpointResolverTest
{
    [Theory]
    [InlineData("https://api.deepseek.com/v1", "https://api.deepseek.com/v1")]
    [InlineData("https://api.deepseek.com", "https://api.deepseek.com")]
    [InlineData("https://api.deepseek.com/v1/chat/completions", "https://api.deepseek.com/v1")]
    [InlineData("https://api.deepseek.com/chat/completions", "https://api.deepseek.com")]
    [InlineData("https://api.openai.com/v1/models", "https://api.openai.com/v1")]
    [InlineData("https://api.openai.com/v1", "https://api.openai.com/v1")]
    public void NormalizeApiBase_StripsKnownSuffixes(string input, string expected)
    {
        EndpointResolver.NormalizeApiBase(input).Should().Be(expected);
    }

    [Fact]
    public void NormalizeApiBase_EmptyOrNull_ReturnsDeepSeekDefault()
    {
        EndpointResolver.NormalizeApiBase("").Should().Be("https://api.deepseek.com");
        EndpointResolver.NormalizeApiBase(null!).Should().Be("https://api.deepseek.com");
    }

    [Theory]
    [InlineData("https://api.deepseek.com", "/chat/completions", "https://api.deepseek.com/v1/chat/completions")]
    [InlineData("https://api.openai.com/v1", "/chat/completions", "https://api.openai.com/v1/chat/completions")]
    [InlineData("https://api.openai.com/v1", "/models", "https://api.openai.com/v1/models")]
    [InlineData("https://generativelanguage.googleapis.com/v1beta/openai", "/chat/completions", "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions")]
    public void BuildResourceUrl_JoinsCorrectly(string apiBase, string path, string expected)
    {
        EndpointResolver.BuildResourceUrl(apiBase, path).Should().Be(expected);
    }

    [Fact]
    public void BuildResourceUrl_NonHttpScheme_ReturnsNull()
    {
        EndpointResolver.BuildResourceUrl("ftp://evil.com", "/models").Should().BeNull();
    }

    [Fact]
    public void BuildChatCompletionsUrl_ReturnsExpectedUrl()
    {
        EndpointResolver.BuildChatCompletionsUrl("https://api.deepseek.com").Should().Be("https://api.deepseek.com/v1/chat/completions");
    }

    [Fact]
    public void BuildModelsUrl_ReturnsExpectedUrl()
    {
        EndpointResolver.BuildModelsUrl("https://api.deepseek.com").Should().Be("https://api.deepseek.com/v1/models");
    }
}
