using System;
using System.Linq;
using FluentAssertions;
using TaiwuEncyclopedia.Core.Diagnostics;
using Xunit;

namespace TaiwuEncyclopedia.Core.Tests.Diagnostics;

public class ModLogTest
{
    public ModLogTest()
    {
        // ModLog 静态累积，每个测试前清空避免互相干扰
        ModLog.Clear();
    }

    [Fact]
    public void Write_AddsEntryToList()
    {
        ModLog.Write("TE.Agent", "info", "hello");

        ModLog.Entries.Should().ContainSingle()
            .Which.Message.Should().Be("hello");
    }

    [Fact]
    public void Write_StoresTagAndLevel()
    {
        ModLog.Write("TE.LLM", "warn", "slow");

        var e = ModLog.Entries.Last();
        e.Tag.Should().Be("TE.LLM");
        e.Level.Should().Be("warn");
    }

    [Fact]
    public void RingBuffer_EvictsOldestBeyond500()
    {
        for (int i = 0; i < 600; i++)
            ModLog.Write("TE.Agent", "info", i.ToString(System.Globalization.CultureInfo.InvariantCulture));

        ModLog.Entries.Should().HaveCount(500);
        ModLog.Entries.First().Message.Should().Be("100");
        ModLog.Entries.Last().Message.Should().Be("599");
    }

    [Fact]
    public void Sanitize_RedactsBearerToken()
    {
        ModLog.Write("TE.LLM", "error", "Bearer sk-abcdefghij1234567890abcdefghij1234567890 call failed");

        var msg = ModLog.Entries.Last().Message;
        msg.Should().NotContain("abcdefghij1234567890");
        msg.Should().Contain("sk-***REDACTED***");
    }

    [Fact]
    public void Sanitize_RedactsJsonApiKey()
    {
        ModLog.Write("TE.LLM", "error", "{\"api_key\":\"sk-abcdefghij1234567890abcdefghij1234567890\"}");

        var msg = ModLog.Entries.Last().Message;
        msg.Should().Contain("sk-***REDACTED***");
        msg.Should().NotContain("abcdefghij1234567890");
    }

    [Fact]
    public void Sanitize_RedactsKeyValueApiKey()
    {
        ModLog.Write("TE.LLM", "error", "config api_key=sk-abcdefghij1234567890abcdefghij1234567890 bad");

        var msg = ModLog.Entries.Last().Message;
        msg.Should().Contain("sk-***REDACTED***");
        msg.Should().NotContain("abcdefghij1234567890");
    }

    [Fact]
    public void Sanitize_RedactsBareSkKey()
    {
        // 不带 api_key 前缀，只靠 SkRegex 兜底（key= 而非 api_key=）
        ModLog.Write("TE.LLM", "error", "leaked key=sk-abcdefghijklmnopqrstuvwxyz123456 end");

        ModLog.Entries.Last().Message.Should().Contain("sk-***REDACTED***");
    }

    [Fact]
    public void Sanitize_DoesNotTouchNonKeyText()
    {
        ModLog.Write("TE.Agent", "info", "玩家问：少林怎么加点");

        ModLog.Entries.Last().Message.Should().Be("玩家问：少林怎么加点");
    }

    [Fact]
    public void OnEntry_FiresOnWrite()
    {
        LogEntry? received = null;
        Action<LogEntry> handler = e => received = e;
        ModLog.OnEntry += handler;
        try
        {
            ModLog.Write("TE.Agent", "info", "fire");
        }
        finally
        {
            ModLog.OnEntry -= handler;
        }

        received.Should().NotBeNull();
        received!.Message.Should().Be("fire");
    }
}
