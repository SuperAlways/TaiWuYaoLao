using System.Collections.Generic;

namespace TaiwuEncyclopedia.Core.Tools;

/// <summary>工具元数据：name / description / parameters（JSON Schema dict）/ timeout。</summary>
public sealed class ToolMetadata
{
    /// <summary>工具名称。</summary>
    public string Name { get; set; } = "";

    /// <summary>工具描述。</summary>
    public string Description { get; set; } = "";

    /// <summary>工具参数的 JSON Schema 字典。</summary>
    public Dictionary<string, Dictionary<string, object>> Parameters { get; set; } = new();

    /// <summary>工具超时时间（秒）。</summary>
    public int Timeout { get; set; } = 30;
}
