using System.Collections.Generic;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Agent;
using TaiwuEncyclopedia.Core.Llm;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Agent;

/// <summary>
/// LoopDetector 测试。
/// </summary>
public class LoopDetectorTest
{
    /// <summary>
    /// 上一轮为空时返回 false。
    /// </summary>
    [Fact]
    public void NullPreviousReturnsFalse()
    {
        var current = new List<ToolCall> { MakeToolCall("retrieve_rag", "{\"query\":\"太吾\"}") };
        LoopDetector.IsLoopSimilar(current, null).Should().BeFalse();
    }

    /// <summary>
    /// 完全相同的调用返回 true。
    /// </summary>
    [Fact]
    public void IdenticalCallsReturnsTrue()
    {
        var current = new List<ToolCall> { MakeToolCall("retrieve_rag", "{\"query\":\"太吾\"}") };
        var previous = new List<ToolCall> { MakeToolCall("retrieve_rag", "{\"query\":\"太吾\"}") };
        LoopDetector.IsLoopSimilar(current, previous).Should().BeTrue();
    }

    /// <summary>
    /// 不同的调用返回 false。
    /// </summary>
    [Fact]
    public void DifferentCallsReturnsFalse()
    {
        var current = new List<ToolCall> { MakeToolCall("retrieve_rag", "{\"query\":\"战斗\"}") };
        var previous = new List<ToolCall> { MakeToolCall("load_background_skill", "{\"chapter\":\"zhan-dou\"}") };
        LoopDetector.IsLoopSimilar(current, previous).Should().BeFalse();
    }

    /// <summary>
    /// 轻微不同的调用（低于阈值）返回 false。
    /// </summary>
    [Fact]
    public void SlightlyDifferentCallsBelowThresholdReturnsFalse()
    {
        var current = new List<ToolCall> { MakeToolCall("retrieve_rag", "{\"query\":\"太吾绘卷战斗系统功法推荐攻略详解\"}") };
        var previous = new List<ToolCall> { MakeToolCall("retrieve_rag", "{\"query\":\"门派关系NPC交互社交\"}") };
        LoopDetector.IsLoopSimilar(current, previous, threshold: 0.8).Should().BeFalse();
    }

    private static ToolCall MakeToolCall(string name, string arguments)
    {
        return new ToolCall
        {
            Id = "call_1",
            Function = new ToolCallFunction { Name = name, Arguments = arguments },
        };
    }
}
