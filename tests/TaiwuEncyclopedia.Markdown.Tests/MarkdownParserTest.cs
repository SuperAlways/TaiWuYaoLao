using FluentAssertions;
using TaiwuEncyclopedia.Markdown;
using Xunit;

namespace TaiwuEncyclopedia.Markdown.Tests;

public class MarkdownParserTest
{
    [Fact]
    public void ParseHeading1WrapsInSizeTag()
    {
        var result = MarkdownParser.Parse("# 标题一");
        result.Should().Contain("<size=").And.Contain("标题一").And.Contain("</size>");
    }

    [Fact]
    public void ParseHeading2UsesSmallerSizeThanHeading1()
    {
        var h1 = MarkdownParser.Parse("# H1");
        var h2 = MarkdownParser.Parse("## H2");
        h1.Should().NotBe(h2);
    }

    [Fact]
    public void ParsePlainTextReturnedAsIs()
    {
        var result = MarkdownParser.Parse("只是普通文本");
        result.Should().Be("只是普通文本");
    }

    [Fact]
    public void ParseEmptyStringReturnsEmpty()
    {
        MarkdownParser.Parse("").Should().Be("");
    }
}
