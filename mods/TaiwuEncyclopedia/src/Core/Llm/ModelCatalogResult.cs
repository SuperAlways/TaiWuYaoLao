using System.Collections.Generic;

namespace TaiwuEncyclopedia.Core.Llm;

public sealed class ModelCatalogResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public List<string> Models { get; init; } = [];
}
