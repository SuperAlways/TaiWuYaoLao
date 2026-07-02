using System.Collections.Generic;
using System.Threading.Tasks;

namespace TaiwuEncyclopedia.Core.Tools;

/// <summary>工具基类。子类实现 ExecuteAsync()，构造时调 SetParameters()。</summary>
public abstract class ToolBase
{
    /// <summary>工具元数据。</summary>
    public ToolMetadata Metadata { get; }

    /// <summary>初始化 ToolBase 实例。</summary>
    /// <param name="name">工具名称。</param>
    /// <param name="description">工具描述。</param>
    /// <param name="timeout">工具超时时间（秒）。</param>
    protected ToolBase(string name, string description, int timeout = 30)
    {
        Metadata = new ToolMetadata { Name = name, Description = description, Timeout = timeout };
    }

    /// <summary>设置工具参数的 JSON Schema。</summary>
    /// <param name="parameters">参数字典。</param>
    protected void SetParameters(Dictionary<string, Dictionary<string, object>> parameters)
    {
        Metadata.Parameters = parameters;
    }

    /// <summary>执行工具。</summary>
    /// <param name="args">工具调用参数。</param>
    /// <returns>工具执行结果字典。</returns>
    public abstract Task<Dictionary<string, object>> ExecuteAsync(Dictionary<string, object> args);
}
