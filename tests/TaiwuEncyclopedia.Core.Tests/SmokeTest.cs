using FluentAssertions;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests;

public class SmokeTest
{
    [Fact]
    public void Smoke_Test_Infrastructure_Works()
    {
        const int expected = 42;
        const int actual = 6 * 7;
        actual.Should().Be(expected);
    }
}
