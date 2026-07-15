using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaiwuEncyclopedia.Core.Context;
using TaiwuEncyclopedia.Core.Diagnostics;
using TaiwuEncyclopedia.Core.Rag;
using TaiwuEncyclopedia.Core.Llm;
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
        ILlmClient llmClient,
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
        IAgentTrace trace,
        System.Text.StringBuilder? thinkingBuilder = null,
        string? finalPrompt = null)
    {
        List<ToolCall>? prevToolCalls = null;
        var totalIterations = 0;

        for (int iteration = 0; iteration < maxIter; iteration++)
        {
            // 4.1 THINKING 调用（非流式，带 tools）
            LlmResponse response = null!;
            TokenUsage? thinkingUsage = null;
            ApiException? pendingError = null;
            try
            {
                var llmSw = System.Diagnostics.Stopwatch.StartNew();
                trace.LlmCall(iteration, "thinking", "thinking_normal", messages, toolsSchema);
                response = await llmClient.ChatAsync(
                    AgentLLMRole.Thinking, llmConfig, messages, tools: toolsSchema);
                llmSw.Stop();
                trace.LlmResponse(iteration, "thinking", response.Content, response.ToolCalls,
                    "", response.Usage, (int)llmSw.ElapsedMilliseconds);
                thinkingUsage = response.Usage;
            }
            catch (ApiException ex)
            {
                if (ex.ErrorType == ApiErrorType.Overload || ex.ErrorType == ApiErrorType.Timeout
                    || ex.ErrorType == ApiErrorType.ContextTooLong)
                {
                    // 可能 token 超限 -> force_compress 后重试一次。重试若仍失败会再次抛 ApiException 直接传播。
                    var llmSw2 = System.Diagnostics.Stopwatch.StartNew();
                    trace.LlmCall(iteration, "thinking", "thinking_force_compress_retry", messages, toolsSchema);
                    messages = ctx.ForceCompress(messages);
                    response = await llmClient.ChatAsync(
                        AgentLLMRole.Thinking, llmConfig, messages, tools: toolsSchema);
                    llmSw2.Stop();
                    trace.LlmResponse(iteration, "thinking", response.Content, response.ToolCalls,
                        "", response.Usage, (int)llmSw2.ElapsedMilliseconds);
                    thinkingUsage = response.Usage;
                }
                else
                {
                    // AuthError/Network/RateLimit/Client/Server：不压缩不重试，收集后出 catch 再 yield + 抛出
                    pendingError = ex;
                }
            }
            if (pendingError != null)
            {
                yield return new StatusEvent { Message = pendingError.Message, Level = pendingError.Level };
                throw pendingError;   // 传播到 AgentRunner.RunAsync（无 try/catch）-> AgentRunnerHost.IsFaulted
            }

            if (thinkingUsage != null)
            {
                yield return new UsageEvent
                {
                    Iteration = iteration,
                    Role = "thinking",
                    PromptTokens = thinkingUsage.PromptTokens,
                    CompletionTokens = thinkingUsage.CompletionTokens,
                    CacheHitTokens = thinkingUsage.CacheHitTokens,
                };
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
                    Content = finalPrompt != null
                        ? $"{finalPrompt}\n\n---\n请根据前面的检索结果回答玩家问题。"
                        : "请根据以上思考过程和检索到的资料，以选中 persona 的口吻给出最终回答。",
                });

                var ansSw = System.Diagnostics.Stopwatch.StartNew();
                trace.LlmCall(iteration, "answer", "answer_direct", messages, null);
                var answerContent = new System.Text.StringBuilder();
                TokenUsage? streamUsage = null;
                await foreach (var chunk in llmClient.StreamChatAsync(llmConfig, messages, System.Threading.CancellationToken.None))
                {
                    if (chunk.Content != null)
                    {
                        finalAnswerParts.Add(chunk.Content);
                        answerContent.Append(chunk.Content);
                        yield return new FinalChunkEvent { Content = chunk.Content, Iteration = iteration };
                    }
                    if (chunk.Usage != null) streamUsage = chunk.Usage;
                }
                ansSw.Stop();
                trace.LlmResponse(iteration, "answer", answerContent.ToString(), null, "stop",
                    streamUsage, (int)ansSw.ElapsedMilliseconds);
                if (streamUsage != null)
                {
                    yield return new UsageEvent
                    {
                        Iteration = iteration,
                        Role = "answer",
                        PromptTokens = streamUsage.PromptTokens,
                        CompletionTokens = streamUsage.CompletionTokens,
                        CacheHitTokens = streamUsage.CacheHitTokens,
                    };
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
                    Content = finalPrompt != null
                        ? $"{finalPrompt}\n\n---\n你似乎在重复检索相同的内容。请根据已检索到的资料直接回答。"
                        : "你似乎在重复检索相同的内容。请根据已检索到的资料，以选中 persona 的口吻给出最终回答。",
                });

                var ansSw = System.Diagnostics.Stopwatch.StartNew();
                trace.LlmCall(iteration, "answer", "answer_loop_detected", messages, null);
                var answerContent = new System.Text.StringBuilder();
                TokenUsage? streamUsage = null;
                await foreach (var chunk in llmClient.StreamChatAsync(llmConfig, messages, System.Threading.CancellationToken.None))
                {
                    if (chunk.Content != null)
                    {
                        finalAnswerParts.Add(chunk.Content);
                        answerContent.Append(chunk.Content);
                        yield return new FinalChunkEvent { Content = chunk.Content, Iteration = iteration };
                    }
                    if (chunk.Usage != null) streamUsage = chunk.Usage;
                }
                ansSw.Stop();
                trace.LlmResponse(iteration, "answer", answerContent.ToString(), null, "stop",
                    streamUsage, (int)ansSw.ElapsedMilliseconds);
                if (streamUsage != null)
                {
                    yield return new UsageEvent
                    {
                        Iteration = iteration,
                        Role = "answer",
                        PromptTokens = streamUsage.PromptTokens,
                        CompletionTokens = streamUsage.CompletionTokens,
                        CacheHitTokens = streamUsage.CacheHitTokens,
                    };
                }
                break;
            }

            // 4.3.5 检测 complete_retrieval 工具 → 快速通道进 final
            var completeTool = response.ToolCalls?.Find(tc => tc.Function.Name == "complete_retrieval");
            if (completeTool != null)
            {
                var args = ParseArgs(completeTool.Function.Arguments);
                var confirmed = args.TryGetValue("confirmed", out var c) && System.Convert.ToBoolean(c);
                if (!confirmed)
                {
                    // confirmed 为 false → 模型认为还需要检索，忽略此工具，继续循环
                    response.ToolCalls!.Remove(completeTool);
                    if (response.ToolCalls!.Count == 0) continue;
                }
                else
                {
                    // confirmed 为 true → 进 final
                    totalIterations = iteration;
                    var topics = args.TryGetValue("topics_found", out var t) ? t?.ToString() : "";
                    var missing = args.TryGetValue("missing", out var m) ? m?.ToString() : "";

                    yield return new StatusEvent { Message = "[检索完成] 正在生成答案 大约需要10S" };

                    // 构建交接消息（missing 是核心信息）
                    var bridgeMsg = finalPrompt != null
                        ? $"{finalPrompt}\n\n---\n"
                        : "";
                    if (!string.IsNullOrEmpty(topics))
                        bridgeMsg += $"【已检索到】{topics}\n";
                    if (!string.IsNullOrEmpty(missing) && missing != "无")
                        bridgeMsg += $"【未检索到】{missing}\n";
                    bridgeMsg += "\n**重要**：对于【未检索到】的内容，请诚实告知玩家信息不足，不要编造。";
                    bridgeMsg += "\n\n请根据以上信息回答玩家问题。";

                    messages.Add(new LlmMessage { Role = "user", Content = bridgeMsg });

                    // 流式 final 回答
                    var ansSw = System.Diagnostics.Stopwatch.StartNew();
                    trace.LlmCall(iteration, "answer", "answer_complete_tool", messages, null);
                    var answerContent = new System.Text.StringBuilder();
                    TokenUsage? streamUsage = null;
                    await foreach (var chunk in llmClient.StreamChatAsync(llmConfig, messages, System.Threading.CancellationToken.None))
                    {
                        if (chunk.Content != null)
                        {
                            finalAnswerParts.Add(chunk.Content);
                            answerContent.Append(chunk.Content);
                            yield return new FinalChunkEvent { Content = chunk.Content, Iteration = iteration };
                        }
                        if (chunk.Usage != null) streamUsage = chunk.Usage;
                    }
                    ansSw.Stop();
                    trace.LlmResponse(iteration, "answer", answerContent.ToString(), null, "stop",
                        streamUsage, (int)ansSw.ElapsedMilliseconds);
                    if (streamUsage != null)
                    {
                        yield return new UsageEvent
                        {
                            Iteration = iteration,
                            Role = "answer",
                            PromptTokens = streamUsage.PromptTokens,
                            CompletionTokens = streamUsage.CompletionTokens,
                            CacheHitTokens = streamUsage.CacheHitTokens,
                        };
                    }
                    break;
                }
            }

            // 4.4 有 tool_calls → yield tool_call → 执行 → yield tool_result → 回写
            foreach (var tc in response.ToolCalls)
            {
                var args = ParseArgs(tc.Function.Arguments);
                var displayText = BuildDisplayText(tc.Function.Name, args);
                thinkingBuilder?.AppendLine(displayText);
                trace.ToolCall(tc.Function.Name, tc.Id, args, iteration);
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
                trace.ToolResult(tc.Function.Name, tc.Id, resultItem.Content, iteration);
                yield return new ToolResultEvent { Name = tc.Function.Name, Iteration = iteration };

                // 探针降级提示(路2): 检查 tool_result 是否探针且非 ok, yield 思考面板提示
                if (ProbeStatusParser.TryGetProbeStatus(resultItem.Content, out var pStatus, out var pCode, out var pApi, out var pProbe)
                    && pStatus != "ok")
                {
                    var level = pStatus == "unavailable" ? "error" : "warn";
                    var msg = pStatus == "unavailable"
                        ? $"探针 {pProbe} 不可用（{pApi}），游戏可能已更新，请联系 mod 作者维护（报错码 {pCode}）"
                        : $"探针 {pProbe} 部分信息缺失（{pApi}），建议联系 mod 作者（报错码 {pCode}）";
                    yield return new StatusEvent { Level = level, Message = msg };
                }
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
                        AddOrMergeRef(collectedRefs, r);
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
                Content = finalPrompt != null
                    ? $"{finalPrompt}\n\n---\n你已用完所有工具调用轮次。请根据已检索到的资料直接回答。如果资料不足，诚实说明并给出已有的判断。"
                    : "你已用完所有工具调用轮次。请根据已检索到的资料和思考过程，以选中 persona 的口吻给出最终回答。如果资料不足，诚实说明并给出已有的判断。",
            });

            var ansSw = System.Diagnostics.Stopwatch.StartNew();
            trace.LlmCall(maxIter, "answer", "answer_fallback", messages, null);
            var answerContent = new System.Text.StringBuilder();
            TokenUsage? streamUsage = null;
            await foreach (var chunk in llmClient.StreamChatAsync(llmConfig, messages, System.Threading.CancellationToken.None))
            {
                if (chunk.Content != null)
                {
                    finalAnswerParts.Add(chunk.Content);
                    answerContent.Append(chunk.Content);
                    yield return new FinalChunkEvent { Content = chunk.Content, Iteration = maxIter };
                }
                if (chunk.Usage != null) streamUsage = chunk.Usage;
            }
            ansSw.Stop();
            trace.LlmResponse(maxIter, "answer", answerContent.ToString(), null, "stop",
                streamUsage, (int)ansSw.ElapsedMilliseconds);
            if (streamUsage != null)
            {
                yield return new UsageEvent
                {
                    Iteration = maxIter,
                    Role = "answer",
                    PromptTokens = streamUsage.PromptTokens,
                    CompletionTokens = streamUsage.CompletionTokens,
                    CacheHitTokens = streamUsage.CacheHitTokens,
                };
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

    /// <summary>
    /// 对 references 列表进行去重合并：FullDocId 相同时合并 HitCount；
    /// FullDocId 为空时以 FilePath 作为备用键，避免不同文档被误聚合。
    /// </summary>
    internal static void DedupReferences(List<Reference> collectedRefs)
    {
        // 原地去重：从后往前扫描，遇到重复则合并到前面并移除当前项
        for (int i = collectedRefs.Count - 1; i >= 0; i--)
        {
            var current = collectedRefs[i];
            var key = RefKey(current);
            if (string.IsNullOrEmpty(key)) continue; // 无键则不参与去重

            for (int j = 0; j < i; j++)
            {
                var earlier = collectedRefs[j];
                var earlierKey = RefKey(earlier);
                if (earlierKey == key)
                {
                    earlier.HitCount += current.HitCount;
                    collectedRefs.RemoveAt(i);
                    break;
                }
            }
        }
    }

    /// <summary>将单条 reference 添加到列表，如已存在则合并 HitCount。</summary>
    private static void AddOrMergeRef(List<Reference> collectedRefs, Reference r)
    {
        var key = RefKey(r);
        if (string.IsNullOrEmpty(key)) { collectedRefs.Add(r); return; }

        for (int i = 0; i < collectedRefs.Count; i++)
        {
            if (RefKey(collectedRefs[i]) == key)
            {
                collectedRefs[i].HitCount += r.HitCount;
                return;
            }
        }
        collectedRefs.Add(r);
    }

    /// <summary>计算引用去重键：FullDocId 优先，FilePath 兜底，SourceUrl 再次兜底。</summary>
    private static string RefKey(Reference r)
    {
        if (!string.IsNullOrEmpty(r.FullDocId)) return r.FullDocId;
        if (!string.IsNullOrEmpty(r.FilePath)) return r.FilePath;
        if (!string.IsNullOrEmpty(r.SourceUrl)) return r.SourceUrl;
        return "";
    }

    /// <summary>生成工具调用的显示文本(纯文本,无 emoji)。</summary>
    internal static string BuildDisplayText(string toolName, Dictionary<string, object> args)
    {
        return toolName switch
        {
            "retrieve_rag" => $"[检索] 查'{Short(args, "query", 40)}'相关",
            "load_background_skill" => args.TryGetValue("section", out var s) && s is string sec && !string.IsNullOrEmpty(sec)
                ? $"[百晓册] {sec}"
                : $"[百晓册] {Str(args, "chapter")}",
            "load_guidance_skill" => $"[引导] 加载{Str(args, "skill")}",
            "lookup_concept" => $"[查询] 查概念'{Str(args, "name")}'",
            "complete_retrieval" => "[检索完成] 正在生成答案 大约需要10S",
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
