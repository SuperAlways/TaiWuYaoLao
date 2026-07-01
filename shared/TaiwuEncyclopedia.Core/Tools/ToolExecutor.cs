using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TaiwuEncyclopedia.Core.Llm;

namespace TaiwuEncyclopedia.Core.Tools;

/// <summary>
/// 并行执行 tool_calls，永不抛异常。搬 v0.5 ToolExecutor。
/// Task.WhenAll 并行；每个工具独立 try/catch；超时用 CancellationTokenSource。
/// 错误以 JSON 返回给 LLM（spec 第 441 行）。
/// </summary>
public sealed class ToolExecutor
{
    private readonly ToolRegistry _registry;
    // v0.5 自动注入的上下文参数 key（v1.0 单玩家无 user_id，保留 conversation_id 预留）
    private static readonly string[] _ctxKeys = { "conversation_id", "world_id" };

    /// <summary>
    /// 初始化 ToolExecutor 实例。
    /// </summary>
    /// <param name="registry">工具注册表。</param>
    public ToolExecutor(ToolRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// 并行执行多个工具调用。
    /// </summary>
    /// <param name="toolCalls">工具调用列表。</param>
    /// <param name="contextParams">上下文参数（自动注入 conversation_id、world_id 等）。</param>
    /// <returns>工具执行结果列表，顺序与输入一致。</returns>
    public async Task<List<ToolResult>> ExecuteAsync(
        List<ToolCall> toolCalls,
        Dictionary<string, object>? contextParams = null)
    {
        var tasks = new List<Task<ToolResult>>();
        foreach (var tc in toolCalls)
        {
            tasks.Add(ExecOne(tc, contextParams ?? new()));
        }
        var results = await Task.WhenAll(tasks);
        return new List<ToolResult>(results);
    }

    private async Task<ToolResult> ExecOne(ToolCall tc, Dictionary<string, object> contextParams)
    {
        var args = new Dictionary<string, object>();
        var toolName = tc.Function.Name;
        var tool = _registry.GetTool(toolName);
        try
        {
            args = string.IsNullOrEmpty(tc.Function.Arguments)
                ? new()
                : JsonConvert.DeserializeObject<Dictionary<string, object>>(tc.Function.Arguments) ?? new();

            // 自动注入上下文参数
            foreach (var key in _ctxKeys)
            {
                if (!args.ContainsKey(key) && contextParams.ContainsKey(key))
                {
                    args[key] = contextParams[key];
                }
            }

            if (tool == null)
            {
                return new ToolResult
                {
                    CallId = tc.Id,
                    Content = JsonConvert.SerializeObject(new { error = $"工具 {toolName} 不存在" }),
                };
            }

            // netstandard2.1 没有 Task.WaitAsync，用 Task.WhenAny + Task.Delay 实现超时
            var task = tool.ExecuteAsync(args);
            var timeoutTask = Task.Delay(System.TimeSpan.FromSeconds(tool.Metadata.Timeout));
            if (await Task.WhenAny(task, timeoutTask) != task)
            {
                var timeout = tool.Metadata.Timeout;
                return new ToolResult
                {
                    CallId = tc.Id,
                    Content = JsonConvert.SerializeObject(new { error = $"工具 {toolName} 执行超时({timeout}s)" }),
                };
            }
            var result = await task;
            return new ToolResult
            {
                CallId = tc.Id,
                Content = JsonConvert.SerializeObject(result),
            };
        }
        catch (JsonException)
        {
            return new ToolResult
            {
                CallId = tc.Id,
                Content = JsonConvert.SerializeObject(new { error = $"参数解析失败: {tc.Function.Arguments}" }),
            };
        }
        catch (System.Exception exc)
        {
            return new ToolResult
            {
                CallId = tc.Id,
                Content = JsonConvert.SerializeObject(new
                {
                    error = exc.Message,
                    exc_type = exc.GetType().Name,
                    tool = toolName,
                    hint = "检查参数类型和完整性，或尝试其他工具",
                }),
            };
        }
    }
}
