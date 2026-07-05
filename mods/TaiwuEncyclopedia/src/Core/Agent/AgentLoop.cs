using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaiwuEncyclopedia.Core.Context;
using TaiwuEncyclopedia.Core.Http;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Session;
using TaiwuEncyclopedia.Core.Tools;

namespace TaiwuEncyclopedia.Core.Agent;

/// <summary>
/// Agent 循环核心。Run 是静态方法，搬自 AgentRunner.RunAsync 的 ReAct for 循环。
/// 接收 AgentRunner 持有的客户端、执行器、工具 schema 等参数，迭代执行 Thinking → tool_calls → 答案，yield 事件。
/// AgentRunner 负责前后处理（构建 prompt → 调 Run → 保存 session → EndEvent）。
/// </summary>
public static class AgentLoop
{
    /// <summary>
    /// 运行 ReAct 循环。
    /// </summary>
    /// <param name="llmClient">LLM 客户端。</param>
    /// <param name="executor">工具执行器。</param>
    /// <param name="ctx">上下文管理器（用于 Chat 异常时的 force_compress 重试）。</param>
    /// <param name="toolsSchema">预构建的工具 schema。</param>
    /// <param name="messages">消息列表（可变，调用后追加了中间消息）。</param>
    /// <param name="llmConfig">LLM 配置。</param>
    /// <param name="worldId">世界 ID。</param>
    /// <param name="maxIter">最大迭代次数。</param>
    /// <param name="collectedRefs">跨轮累积的 references（引用，方法内修改）。</param>
    /// <param name="finalAnswerParts">最终答案分块列表（引用，方法内追加）。</param>
    /// <param name="totalIterations">总迭代次数（out，枚举完成后填充）。</param>
    /// <param name="trace">ReAct 追踪（可选，暂留占位，后续接入 IAgentTrace）。</param>
    /// <returns>Agent 事件异步枚举。</returns>
    public static async IAsyncEnumerable<AgentEvent> Run(
        OpenAiCompatibleClient llmClient,
        ToolExecutor executor,
        ContextManager ctx,
        List<Dictionary<string, object>>? toolsSchema,
        List<LlmMessage> messages,
        LlmConfig llmConfig,
        int worldId,
        int maxIter,
        List<Reference> collectedRefs,
        List<string> finalAnswerParts,
        AgentLoopResult result,
        ReactTrace? trace = null,
        System.Text.StringBuilder? thinkingBuilder = null)
    {
        List<ToolCall>? prevToolCalls = null;
        var totalIterations = 0;

        for (int iteration = 0; iteration < maxIter; iteration++)
        {
            // 4.1 THINKING 调用（非流式，带 tools）
            LlmResponse response;
            try
            {
                response = await llmClient.Chat(
                    AgentLLMRole.Thinking, messages, llmConfig, tools: toolsSchema);
            }
            catch
            {
                // force_compress 重试
                messages = ctx.ForceCompress(messages);
                response = await llmClient.Chat(
                    AgentLLMRole.Thinking, messages, llmConfig, tools: toolsSchema);
            }

            // 4.2 无 tool_calls → 最终答案
            if (response.ToolCalls == null || response.ToolCalls.Count == 0)
            {
                totalIterations = iteration;
                if (!string.IsNullOrEmpty(response.Content))
                {
                    messages.Add(new LlmMessage { Role = "assistant", Content = response.Content });
                }
                messages.Add(new LlmMessage
                {
                    Role = "user",
                    Content = "请根据以上思考过程和检索到的资料，以选中 persona 的口吻给出最终回答。",
                });

                await foreach (var chunk in llmClient.StreamChat(AgentLLMRole.Answer, messages, llmConfig))
                {
                    finalAnswerParts.Add(chunk);
                    yield return new FinalChunkEvent { Content = chunk, Iteration = iteration };
                }
                break;
            }

            // 4.3 Jaccard 循环检测
            if (LoopDetector.IsLoopSimilar(response.ToolCalls, prevToolCalls))
            {
                totalIterations = iteration;
                messages.Add(new LlmMessage { Role = "assistant", Content = response.Content ?? "" });
                messages.Add(new LlmMessage
                {
                    Role = "user",
                    Content = "你似乎在重复检索相同的内容。请根据已检索到的资料，以选中 persona 的口吻给出最终回答。",
                });

                await foreach (var chunk in llmClient.StreamChat(AgentLLMRole.Answer, messages, llmConfig))
                {
                    finalAnswerParts.Add(chunk);
                    yield return new FinalChunkEvent { Content = chunk, Iteration = iteration };
                }
                break;
            }

            // 4.4 有 tool_calls → yield tool_call → 执行 → yield tool_result → 回写
            foreach (var tc in response.ToolCalls)
            {
                var args = ParseArgs(tc.Function.Arguments);
                var displayText = BuildDisplayText(tc.Function.Name, args);
                thinkingBuilder?.AppendLine(displayText);
                yield return new ToolCallEvent
                {
                    Name = tc.Function.Name,
                    Args = args,
                    DisplayText = displayText,
                    Iteration = iteration,
                };
            }

            // 回写 assistant 消息（含 tool_calls）
            messages.Add(new LlmMessage
            {
                Role = "assistant",
                Content = response.Content ?? "",
                ToolCalls = response.ToolCalls,
            });

            // 并行执行工具
            var contextParams = new Dictionary<string, object> { ["world_id"] = worldId };
            var results = await executor.ExecuteParallelAsync(response.ToolCalls, contextParams);

            // 回写 tool results + yield tool_result 事件
            for (int i = 0; i < response.ToolCalls.Count; i++)
            {
                var tc = response.ToolCalls[i];
                var resultItem = results[i];
                messages.Add(new LlmMessage
                {
                    Role = "tool",
                    ToolCallId = tc.Id,
                    Content = resultItem.Content,
                });
                yield return new ToolResultEvent { Name = tc.Function.Name, Iteration = iteration };
            }

            // 累积 retrieve_rag 工具返回的 references（跨轮去重，按 full_doc_id 合并 hit_count）
            for (int i = 0; i < response.ToolCalls.Count; i++)
            {
                if (response.ToolCalls[i].Function.Name != "retrieve_rag") continue;
                try
                {
                    var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(results[i].Content);
                    if (parsed == null || !parsed.TryGetValue("references", out var refsObj)) continue;

                    // 处理 references 可能是 JArray 或已经是 List<Reference>
                    List<Reference>? refs = null;
                    if (refsObj is List<Reference> listRefs)
                    {
                        refs = listRefs;
                    }
                    else if (refsObj is JArray jArr)
                    {
                        refs = jArr.ToObject<List<Reference>>();
                    }

                    if (refs == null) continue;

                    foreach (var r in refs)
                    {
                        var fullDocId = r.FullDocId ?? "";
                        var existing = collectedRefs.Find(x => x.FullDocId == fullDocId);
                        if (existing != null)
                        {
                            existing.HitCount += r.HitCount;
                        }
                        else
                        {
                            collectedRefs.Add(r);
                        }
                    }
                }
                catch { /* 解析失败忽略，不影响主流程 */ }
            }

            prevToolCalls = response.ToolCalls;
            totalIterations = iteration + 1;
        }

        // 兜底轮（max_iter 耗尽且未收敛）
        if (finalAnswerParts.Count == 0)
        {
            messages.Add(new LlmMessage
            {
                Role = "user",
                Content = "你已用完所有工具调用轮次。请根据已检索到的资料和思考过程，以选中 persona 的口吻给出最终回答。如果资料不足，诚实说明并给出已有的判断。",
            });

            await foreach (var chunk in llmClient.StreamChat(AgentLLMRole.Answer, messages, llmConfig))
            {
                finalAnswerParts.Add(chunk);
                yield return new FinalChunkEvent { Content = chunk, Iteration = maxIter };
            }
        }

        // 通过 result 对象传出 totalIterations
        result.TotalIterations = totalIterations;
    }

    private static Dictionary<string, object> ParseArgs(string? arguments)
    {
        if (string.IsNullOrEmpty(arguments)) return new();
        try
        {
            return JObject.Parse(arguments).ToObject<Dictionary<string, object>>() ?? new();
        }
        catch { return new(); }
    }

    /// <summary>生成工具调用的显示文本(纯文本,无 emoji)。</summary>
    private static string BuildDisplayText(string toolName, Dictionary<string, object> args)
    {
        return toolName switch
        {
            "retrieve_rag" => $"[检索] 查'{Short(args, "query", 40)}'相关",
            "load_background_skill" => $"[百晓册] 加载{Str(args, "chapter")}",
            "load_guidance_skill" => $"[引导] 加载{Str(args, "skill")}",
            "lookup_concept" => $"[查询] 查概念'{Str(args, "name")}'",
            _ => $"[工具] {toolName}",
        };
    }

    private static string Str(Dictionary<string, object> args, string key)
        => args.TryGetValue(key, out var v) && v is string s ? s : "?";

    private static string Short(Dictionary<string, object> args, string key, int max)
    {
        var s = Str(args, key);
        return s.Length > max ? s[..max] : s;
    }
}

/// <summary>
/// AgentLoop.Run 的结果容器。因为 async IAsyncEnumerable 不支持 ref/out 参数，
/// 用此对象传出枚举完成后才能确定的值。
/// </summary>
public sealed class AgentLoopResult
{
    /// <summary>总迭代次数。</summary>
    public int TotalIterations { get; set; }
}
