using System.Linq;
using FluentAssertions;
using Xunit;

namespace TaiwuBaixiaoceImporter.Tests;

public class MarkdownCleanerTest
{
    [Fact]
    public void CleanStripsSpanTagsAndKeepsText()
    {
        var source = "<span style=\"color:#6fb6ff\">纯文本</span>";
        var (cleaned, _) = MarkdownCleaner.Clean(source, "test.md");
        cleaned.Should().Be("纯文本");
    }

    [Fact]
    public void CleanReplacesLinkWithLookupMarker()
    {
        var source = "不会向<span style=\"color:#6fb6ff\">[名誉](../../../人物/人物信息/名誉/人物-人物信息-名誉.md)</span>过低之人传授";
        var (cleaned, links) = MarkdownCleaner.Clean(source, "门派/门派一览/少林派.md");
        cleaned.Should().Be("不会向[查:名誉]过低之人传授");
        links.Should().ContainSingle(l => l.Text == "名誉" && l.TargetPath == "../../../人物/人物信息/名誉/人物-人物信息-名誉.md");
    }

    [Fact]
    public void CleanHandlesAnchorLinks()
    {
        var source = "当前<span style=\"color:#6fb6ff\">[侵袭进度](../../../启程/相枢/相枢/启程-相枢-相枢.md#侵袭进度)</span>决定";
        var (cleaned, links) = MarkdownCleaner.Clean(source, "门派/门派概述/门派支持/门派-门派概述-门派支持.md");
        cleaned.Should().Be("当前[查:侵袭进度]决定");
        links.Should().ContainSingle(l => l.Text == "侵袭进度" && l.TargetPath == "../../../启程/相枢/相枢/启程-相枢-相枢.md");
    }

    [Fact]
    public void CleanHandlesBrokenSpanWrappingLink()
    {
        var source = "| 内功 | <span style=\"color:#9eb767\">[静禅功</span>](../../../词条/功法/静禅功.md) |";
        var (cleaned, links) = MarkdownCleaner.Clean(source, "修习/武学/功法/修习-武学-功法.md");
        cleaned.Should().Be("| 内功 | [查:静禅功] |");
        links.Should().ContainSingle(l => l.Text == "静禅功" && l.TargetPath == "../../../词条/功法/静禅功.md");
    }

    [Fact]
    public void CleanHandlesBareLinkWithoutSpan()
    {
        var source = "详见[伏龙坛](../伏龙坛/门派-门派一览-伏龙坛.md)";
        var (cleaned, links) = MarkdownCleaner.Clean(source, "门派/门派一览/少林派.md");
        cleaned.Should().Be("详见[查:伏龙坛]");
        links.Should().ContainSingle(l => l.Text == "伏龙坛" && l.TargetPath == "../伏龙坛/门派-门派一览-伏龙坛.md");
    }

    [Fact]
    public void CleanPreservesNonMdLinks()
    {
        var source = "详见[官网](https://example.com)";
        var (cleaned, links) = MarkdownCleaner.Clean(source, "test.md");
        cleaned.Should().Be("详见[官网](https://example.com)");
        links.Should().BeEmpty();
    }
}