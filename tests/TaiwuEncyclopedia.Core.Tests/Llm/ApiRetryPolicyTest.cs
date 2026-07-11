using FluentAssertions;
using TaiwuEncyclopedia.Core.Llm;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Llm;

public class ApiRetryPolicyTest
{
    [Theory]
    [InlineData(200, ApiErrorType.Success)]
    [InlineData(408, ApiErrorType.Timeout)]
    [InlineData(429, ApiErrorType.RateLimit)]
    [InlineData(529, ApiErrorType.Overload)]
    [InlineData(401, ApiErrorType.AuthError)]
    [InlineData(403, ApiErrorType.AuthError)]
    [InlineData(404, ApiErrorType.ClientError)]
    [InlineData(500, ApiErrorType.ServerError)]
    [InlineData(502, ApiErrorType.ServerError)]
    [InlineData(999, ApiErrorType.ServerError)]
    public void ClassifyStatus_MapsStatusCode(int code, ApiErrorType expected)
    {
        ApiRetryPolicy.ClassifyStatus(code).Should().Be(expected);
    }

    [Theory]
    [InlineData(AgentLLMRole.Thinking, true)]
    [InlineData(AgentLLMRole.Answer, true)]
    [InlineData(AgentLLMRole.Intent, false)]
    [InlineData(AgentLLMRole.Testing, false)]
    public void IsForeground_SplitsForegroundVsBackground(AgentLLMRole role, bool expected)
    {
        ApiRetryPolicy.IsForeground(role).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 375, 500)]   // 500 * 2^0 * [0.75,1.0]
    [InlineData(1, 750, 1000)]
    [InlineData(2, 1500, 2000)]
    [InlineData(3, 3000, 4000)]
    public void GetDelay_StaysWithinExponentialJitterBounds(int attempt, int minMs, int maxMs)
    {
        for (int i = 0; i < 50; i++)   // 抖动随机，多次抽样确认始终在区间内
        {
            var ms = ApiRetryPolicy.GetDelay(attempt).TotalMilliseconds;
            ms.Should().BeInRange(minMs, maxMs);
        }
    }

    [Fact]
    public void Evaluate_BackgroundAlwaysFailsImmediately()
    {
        int c529 = 0;
        var (decision, msg, level) = ApiRetryPolicy.Evaluate(
            ApiErrorType.RateLimit, attempt: 0, ref c529, isForeground: false);
        decision.Should().Be(RetryDecision.Fail);
        level.Should().Be("info");
        msg.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_OverloadRetriesTwiceThenTellPlayerOnThird()
    {
        int c529 = 0;
        var r1 = ApiRetryPolicy.Evaluate(ApiErrorType.Overload, 0, ref c529, true);
        r1.Decision.Should().Be(RetryDecision.Retry);
        r1.Level.Should().Be("warn");
        c529.Should().Be(1);

        var r2 = ApiRetryPolicy.Evaluate(ApiErrorType.Overload, 1, ref c529, true);
        r2.Decision.Should().Be(RetryDecision.Retry);
        c529.Should().Be(2);

        var r3 = ApiRetryPolicy.Evaluate(ApiErrorType.Overload, 2, ref c529, true);
        r3.Decision.Should().Be(RetryDecision.TellPlayer);
        r3.Level.Should().Be("error");
        r3.Message.Should().Contain("持续过载");
        c529.Should().Be(3);
    }

    [Fact]
    public void Evaluate_NonOverloadResets529Counter()
    {
        int c529 = 2;
        // 一个 Timeout（非 Overload）应清零计数
        ApiRetryPolicy.Evaluate(ApiErrorType.Timeout, 0, ref c529, true);
        c529.Should().Be(0);
    }

    [Fact]
    public void Evaluate_TimeoutRetriesUntilMaxThenFails()
    {
        int c = 0;
        var r0 = ApiRetryPolicy.Evaluate(ApiErrorType.Timeout, 0, ref c, true);
        r0.Decision.Should().Be(RetryDecision.Retry);
        r0.Level.Should().Be("warn");

        var rMax = ApiRetryPolicy.Evaluate(ApiErrorType.Timeout, ApiRetryPolicy.MAX_RETRIES, ref c, true);
        rMax.Decision.Should().Be(RetryDecision.Fail);
        rMax.Level.Should().Be("error");
        rMax.Message.Should().Contain("超时");
    }

    [Fact]
    public void Evaluate_AuthErrorFailsImmediatelyWithoutRetry()
    {
        int c = 0;
        var r = ApiRetryPolicy.Evaluate(ApiErrorType.AuthError, 0, ref c, true);
        r.Decision.Should().Be(RetryDecision.Fail);
        r.Level.Should().Be("error");
        r.Message.Should().Contain("API Key");
    }

    [Fact]
    public void Evaluate_ClientErrorFailsImmediately()
    {
        int c = 0;
        var r = ApiRetryPolicy.Evaluate(ApiErrorType.ClientError, 0, ref c, true);
        r.Decision.Should().Be(RetryDecision.Fail);
        r.Level.Should().Be("error");
    }

    [Fact]
    public void Evaluate_NetworkAndServerAndRateLimitRetry()
    {
        int c = 0;
        foreach (var e in new[] { ApiErrorType.NetworkError, ApiErrorType.ServerError, ApiErrorType.RateLimit })
        {
            var r = ApiRetryPolicy.Evaluate(e, 0, ref c, true);
            r.Decision.Should().Be(RetryDecision.Retry);
            r.Level.Should().Be("warn");
        }
    }

    [Fact]
    public void GetFailMessage_ReturnsPerErrorChineseMessage()
    {
        ApiRetryPolicy.GetFailMessage(ApiErrorType.AuthError).Should().Contain("API Key");
        ApiRetryPolicy.GetFailMessage(ApiErrorType.NetworkError).Should().Contain("网络");
        ApiRetryPolicy.GetFailMessage(ApiErrorType.Unknown).Should().Contain("未知");
    }

    [Fact]
    public void GetRetryMessage_ReturnsPerErrorChineseMessage()
    {
        ApiRetryPolicy.GetRetryMessage(ApiErrorType.Overload).Should().Contain("过载");
        ApiRetryPolicy.GetRetryMessage(ApiErrorType.Timeout).Should().Contain("超时");
    }

    // --- ContextTooLong tests (Task 1: P0-2 ForceCompress trigger) ---

    [Fact]
    public void Evaluate_ContextTooLongRetriesOnceWithForceCompress()
    {
        int c = 0;
        var r = ApiRetryPolicy.Evaluate(ApiErrorType.ContextTooLong, 0, ref c, true);
        r.Decision.Should().Be(RetryDecision.Retry);
        r.Level.Should().Be("warn");
        r.Message.Should().Contain("过长");
    }

    [Fact]
    public void GetFailMessage_ContextTooLong_ReturnsContextMessage()
    {
        ApiRetryPolicy.GetFailMessage(ApiErrorType.ContextTooLong).Should().Contain("上下文");
    }

    [Fact]
    public void GetRetryMessage_ContextTooLong_ReturnsRetryMessage()
    {
        ApiRetryPolicy.GetRetryMessage(ApiErrorType.ContextTooLong).Should().Contain("过长");
    }
}
