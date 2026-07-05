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
}
