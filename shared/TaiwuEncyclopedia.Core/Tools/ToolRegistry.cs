using System.Collections.Generic;
using TaiwuEncyclopedia.Core.Llm;

namespace TaiwuEncyclopedia.Core.Tools;

/// <summary>
/// 工具注册表 + OpenAI FC JSON Schema 生成。
/// 透传 enum/minimum/maximum/items 等约束字段（v0.5 _CONSTRAINT_KEYS）。
/// </summary>
public sealed class ToolRegistry
{
    private static readonly string[] _constraintKeys =
    {
        "enum", "minimum", "maximum",
        "min_length", "max_length",
        "min_items", "max_items",
        "pattern", "default", "items",
    };

    private readonly Dictionary<string, ToolBase> _tools = new();

    /// <summary>
    /// 注册一个工具。
    /// </summary>
    /// <param name="tool">要注册的工具实例。</param>
    public void Register(ToolBase tool) => _tools[tool.Metadata.Name] = tool;

    /// <summary>
    /// 获取已注册的工具。
    /// </summary>
    /// <param name="name">工具名称。</param>
    /// <returns>工具实例，如果未找到则返回 null。</returns>
    public ToolBase? GetTool(string name) => _tools.GetValueOrDefault(name);

    /// <summary>
    /// 构建 OpenAI Function Calling 格式的工具列表。
    /// </summary>
    /// <returns>OpenAI FC 格式的工具字典列表。</returns>
    public List<Dictionary<string, object>> BuildOpenaiTools()
    {
        var openaiTools = new List<Dictionary<string, object>>();
        foreach (var tool in _tools.Values)
        {
            var properties = new Dictionary<string, object>();
            var required = new List<string>();
            foreach (var (paramName, paramInfo) in tool.Metadata.Parameters)
            {
                var prop = new Dictionary<string, object>
                {
                    ["type"] = paramInfo.GetValueOrDefault("type", "string"),
                    ["description"] = paramInfo.GetValueOrDefault("description", paramName),
                };
                foreach (var key in _constraintKeys)
                {
                    if (paramInfo.TryGetValue(key, out var val))
                    {
                        prop[key] = val;
                    }
                }
                properties[paramName] = prop;
                if (paramInfo.GetValueOrDefault("required", false) is bool req && req)
                {
                    required.Add(paramName);
                }
            }
            openaiTools.Add(new Dictionary<string, object>
            {
                ["type"] = "function",
                ["function"] = new Dictionary<string, object>
                {
                    ["name"] = tool.Metadata.Name,
                    ["description"] = tool.Metadata.Description,
                    ["parameters"] = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = properties,
                        ["required"] = required,
                    },
                },
            });
        }
        return openaiTools;
    }
}
