using FluentAssertions;
using TaiwuEncyclopedia.Core.Agent;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Agent;

public class StatusEventTest
{
    [Fact]
    public void Level_DefaultsToInfo()
    {
        var se = new StatusEvent { Message = "正在压缩历史对话..." };
        se.Level.Should().Be("info");
    }

    [Fact]
    public void Level_CanBeSetToWarn()
    {
        var se = new StatusEvent { Message = "模型响应超时", Level = "warn" };
        se.Level.Should().Be("warn");
    }

    [Fact]
    public void Level_CanBeSetToError()
    {
        var se = new StatusEvent { Message = "API Key 无效", Level = "error" };
        se.Level.Should().Be("error");
    }
}
