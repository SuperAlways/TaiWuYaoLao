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

    [Fact]
    public void ParseBoldWrapsInBTag()
    {
        MarkdownParser.Parse("这是 **加粗** 文本").Should().Be("这是 <b>加粗</b> 文本");
    }

    [Fact]
    public void ParseItalicWrapsInITag()
    {
        MarkdownParser.Parse("这是 *斜体* 文本").Should().Be("这是 <i>斜体</i> 文本");
    }

    [Fact]
    public void ParseInlineCodeWrapsInMarkTag()
    {
        MarkdownParser.Parse("用 `Console.WriteLine` 输出").Should().Contain("<mark>").And.Contain("Console.WriteLine").And.Contain("</mark>");
    }

    [Fact]
    public void ParseLinkGeneratesLinkTag()
    {
        var result = MarkdownParser.Parse("[文本](https://example.com)");
        result.Should().Contain("<link=\"https://example.com\">").And.Contain("文本").And.Contain("</link>");
    }

    [Fact]
    public void ParseBoldItalicNested()
    {
        MarkdownParser.Parse("***加粗斜体***").Should().Contain("<b>").And.Contain("<i>");
    }
}
