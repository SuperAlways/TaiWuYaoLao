using System.Collections.Generic;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TaiwuBaixiaoceImporter.Tests;

public class ConceptIndexBuilderTest
{
    [Fact]
    public void AddGlossaryLinkResolvesToGlossaryPath()
    {
        var builder = new ConceptIndexBuilder();
        builder.Add(new LinkEntry("易筋经", "../../../词条/功法/易筋经.md"), "修习/武学/功法/修习-武学-功法.md");
        var json = builder.Build();
        var obj = JObject.Parse(json);
        obj["易筋经"]["path"].ToString().Should().Be("glossary/功法/易筋经.md");
        obj["易筋经"]["type"].ToString().Should().Be("glossary");
    }

    [Fact]
    public void AddSectionLinkResolvesToBackgroundPath()
    {
        var builder = new ConceptIndexBuilder();
        builder.Add(new LinkEntry("名誉", "../../../人物/人物信息/名誉/人物-人物信息-名誉.md"), "门派/门派一览/少林派.md");
        var json = builder.Build();
        var obj = JObject.Parse(json);
        obj["名誉"]["path"].ToString().Should().Be("background/renwu/detail/人物-人物信息-名誉.md");
        obj["名誉"]["type"].ToString().Should().Be("section");
    }

    [Fact]
    public void AddCrossChapterAnchorLinkResolvesToSectionPath()
    {
        var builder = new ConceptIndexBuilder();
        builder.Add(new LinkEntry("侵袭进度", "../../../启程/相枢/相枢/启程-相枢-相枢.md"), "门派/门派概述/门派支持/门派-门派概述-门派支持.md");
        var json = builder.Build();
        var obj = JObject.Parse(json);
        obj["侵袭进度"]["path"].ToString().Should().Be("background/qi-cheng/detail/启程-相枢-相枢.md");
        obj["侵袭进度"]["type"].ToString().Should().Be("section");
    }

    [Fact]
    public void AddSameLinkTwiceDoesNotDuplicate()
    {
        var builder = new ConceptIndexBuilder();
        var entry = new LinkEntry("易筋经", "../../../词条/功法/易筋经.md");
        builder.Add(entry, "修习/武学/功法/修习-武学-功法.md");
        builder.Add(entry, "修习/武学/突破/修习-武学-突破.md");
        var json = builder.Build();
        var obj = JObject.Parse(json);
        obj.Properties().Should().HaveCount(1);
    }

    [Fact]
    public void AddSameTextDifferentPathEmitsWarning()
    {
        var builder = new ConceptIndexBuilder();
        var warnings = new List<string>();
        builder.Warning = w => warnings.Add(w);
        builder.Add(new LinkEntry("狮子吼", "../../../词条/功法/狮子吼.md"), "file1.md");
        builder.Add(new LinkEntry("狮子吼", "../../../词条/绝技/狮子吼.md"), "file2.md");
        warnings.Should().NotBeEmpty();
        warnings[0].Should().Contain("狮子吼");
    }
}
