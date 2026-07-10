using System.Collections.Generic;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Llm;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Llm;

public class ModelCatalogParserTest
{
    [Fact]
    public void Parse_OpenAiFormat_ReturnsSortedModels()
    {
        var json = @"{""data"":[{""id"":""gpt-4""},{""id"":""gpt-3.5""}]}";
        var result = ModelCatalogParser.Parse(json);
        result.Should().Equal("gpt-3.5", "gpt-4");
    }

    [Fact]
    public void Parse_ModelsField_ReturnsModels()
    {
        var json = @"{""models"":[{""id"":""deepseek-chat""}]}";
        var result = ModelCatalogParser.Parse(json);
        result.Should().Equal("deepseek-chat");
    }

    [Fact]
    public void Parse_StringElements_AcceptsPlainStrings()
    {
        var json = @"{""data"":[""model-a"",""model-b""]}";
        var result = ModelCatalogParser.Parse(json);
        result.Should().Equal("model-a", "model-b");
    }

    [Fact]
    public void Parse_NameField_FallsBackToName()
    {
        var json = @"{""data"":[{""name"":""legacy-model""}]}";
        var result = ModelCatalogParser.Parse(json);
        result.Should().Equal("legacy-model");
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsEmpty()
    {
        var result = ModelCatalogParser.Parse("not json");
        result.Should().BeEmpty();
    }

    [Fact]
    public void ClassifyError_401_ReturnsKeyInvalid()
    {
        ModelCatalogParser.ClassifyError(401)
            .Should().Contain("Key 无效");
    }

    [Fact]
    public void ClassifyError_404_ReturnsNotProvided()
    {
        ModelCatalogParser.ClassifyError(404)
            .Should().Contain("未提供模型列表接口");
    }
}
