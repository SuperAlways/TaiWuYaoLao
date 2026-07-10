using FluentAssertions;
using TaiwuEncyclopedia.Core.Http;
using TaiwuEncyclopedia.Core.Rag;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Rag;

public class RagResponseParserTest
{
    [Fact]
    public void Parse_ValidResponse_ExtractsContextAndReferences()
    {
        var json = @"{
            ""context"": ""太吾绘卷是一款武侠游戏"",
            ""references"": [
                {""full_doc_id"": ""doc1"", ""file_path"": ""/path/a.md"", ""source_url"": ""url1"",
                 ""source_type"": ""wiki"", ""knowledge_type"": ""mechanic"", ""author"": ""dev"",
                 ""game_version"": ""1.0"", ""snippet"": ""text..."", ""hit_count"": 5}
            ]
        }";
        var result = RagResponseParser.Parse(json);
        result.Context.Should().Be("太吾绘卷是一款武侠游戏");
        result.References.Should().HaveCount(1);
        result.References[0].FullDocId.Should().Be("doc1");
        result.References[0].FilePath.Should().Be("/path/a.md");
        result.References[0].SourceUrl.Should().Be("url1");
        result.References[0].SourceType.Should().Be("wiki");
        result.References[0].KnowledgeType.Should().Be("mechanic");
        result.References[0].Author.Should().Be("dev");
        result.References[0].GameVersion.Should().Be("1.0");
        result.References[0].Snippet.Should().Be("text...");
        result.References[0].HitCount.Should().Be(5);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyContext_ReturnsEmpty()
    {
        var json = @"{""context"":"""", ""references"":[]}";
        var result = RagResponseParser.Parse(json);
        result.Context.Should().Be("");
        result.References.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MissingFields_DefaultsSafely()
    {
        var json = @"{""context"":""text""}";
        var result = RagResponseParser.Parse(json);
        result.Context.Should().Be("text");
        result.References.Should().BeEmpty();
    }

    [Fact]
    public void Parse_InvalidJson_SetsParseError()
    {
        var json = "not valid json {{{";
        var result = RagResponseParser.Parse(json);
        result.Error.Should().Be("parse_failed");
        result.Context.Should().BeEmpty();
        result.References.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MultipleReferences_AllExtracted()
    {
        var json = @"{
            ""context"": ""result"",
            ""references"": [
                {""full_doc_id"": ""doc1"", ""file_path"": ""/a.md"", ""source_url"": """",
                 ""source_type"": ""wiki"", ""knowledge_type"": ""mechanic"", ""author"": """",
                 ""game_version"": """", ""snippet"": """", ""hit_count"": 3},
                {""full_doc_id"": ""doc2"", ""file_path"": ""/b.md"", ""source_url"": """",
                 ""source_type"": ""bbs"", ""knowledge_type"": ""guide"", ""author"": """",
                 ""game_version"": """", ""snippet"": """", ""hit_count"": 1}
            ]
        }";
        var result = RagResponseParser.Parse(json);
        result.References.Should().HaveCount(2);
        result.References[0].FullDocId.Should().Be("doc1");
        result.References[1].FullDocId.Should().Be("doc2");
        result.References[0].SourceType.Should().Be("wiki");
        result.References[1].SourceType.Should().Be("bbs");
    }

    [Fact]
    public void Parse_ReferenceWithMissingFields_DefaultsToEmptyString()
    {
        var json = @"{
            ""context"": ""text"",
            ""references"": [
                {""full_doc_id"": ""doc1""}
            ]
        }";
        var result = RagResponseParser.Parse(json);
        result.References.Should().HaveCount(1);
        result.References[0].FullDocId.Should().Be("doc1");
        result.References[0].FilePath.Should().Be("");
        result.References[0].HitCount.Should().Be(0);
    }
}
