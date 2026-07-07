using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TaiwuEncyclopedia.Core.Llm;

namespace TaiwuEncyclopedia.Core.Agent;

public sealed class ActiveRequest
{
    public int WorldId { get; init; }
    public string Query { get; init; } = "";
    public int Generation { get; init; }
    public CancellationTokenSource Cts { get; init; } = new();
    public CancellationToken CancellationToken => Cts.Token;
    public List<AgentEvent> CompletedEvents { get; init; } = new();
    public StringBuilder AnswerBuilder { get; init; } = new();
    public List<LlmMessage>? History { get; set; }

    /// <summary>请求开始时间戳（Time.realtimeSinceStartup）。</summary>
    public float StartTime { get; set; }

    /// <summary>累积 prompt tokens。</summary>
    public int TotalPromptTokens { get; set; }

    /// <summary>累积 completion tokens。</summary>
    public int TotalCompletionTokens { get; set; }

    /// <summary>累积 cache 命中 tokens。</summary>
    public int TotalCacheHitTokens { get; set; }

    /// <summary>是否还在思考（StartEvent→true，首个 FinalChunkEvent→false）。</summary>
    public bool IsThinking { get; set; } = true;
}
