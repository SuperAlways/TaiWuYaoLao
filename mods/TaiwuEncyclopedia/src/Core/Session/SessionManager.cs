using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaiwuEncyclopedia.Core.Rag;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Storage;

namespace TaiwuEncyclopedia.Core.Session;

/// <summary>
/// 会话管理：保存最终 user/assistant 消息，加载历史。
/// 搬 v0.5 ConversationManager，但 PostgreSQL → ISessionStore（默认 JsonSessionStore）。
/// user_id/conversation_id → worldId（按 WorldId 绑定）。
/// FC 中间态（tool_call/tool_result）不入库，循环结束一次性保存。
/// </summary>
public sealed class SessionManager
{
    private readonly ISessionStore _store;

    /// <summary>主界面（建档前）对话的 WorldId。未进档时用这个值走完整 session 链路。</summary>
    public const int PregameWorldId = -1;

    /// <summary>
    /// 初始化会话管理器
    /// </summary>
    /// <param name="store">会话存储实现</param>
    public SessionManager(ISessionStore store)
    {
        _store = store;
    }

    /// <summary>即时持久化用户提问（不等循环结束），支持面板关后重连恢复上下文。</summary>
    /// <param name="worldId">世界 ID</param>
    /// <param name="userQuery">用户查询内容</param>
    public async Task SaveUserQueryAsync(int worldId, string userQuery)
    {
        await _store.AppendMessageAsync(worldId, new MessageRecord { Role = "user", Content = userQuery });
    }

    /// <summary>循环结束后一次性保存 user_query + assistant_answer（含可选 references）。</summary>
    /// <param name="worldId">世界ID</param>
    /// <param name="userQuery">用户查询内容</param>
    /// <param name="assistantAnswer">助手回答内容</param>
    /// <param name="references">参考文献列表（可选，仅 assistant 消息携带）</param>
    /// <param name="autoName">太吾名快照（可选，首次对话写入，之后不覆盖）</param>
    public async Task SaveConversationAsync(
        int worldId,
        string userQuery,
        string assistantAnswer,
        List<Reference>? references = null,
        string? autoName = null,
        string? thinkingContent = null)
    {
        await _store.AppendMessageAsync(worldId, new MessageRecord { Role = "user", Content = userQuery });
        await _store.AppendMessageAsync(worldId, new MessageRecord
        {
            Role = "assistant",
            Content = assistantAnswer,
            References = references,
            ThinkingContent = thinkingContent,
        });

        // 首次对话写入太吾名快照（SetAutoNameAsync 自身是幂等 guard，仅当 AutoName 为空时写入）
        if (!string.IsNullOrEmpty(autoName))
        {
            await _store.SetAutoNameAsync(worldId, autoName);
        }
    }

    /// <summary>加载会话历史，格式可直接用于 build_initial_messages。</summary>
    /// <param name="worldId">世界ID</param>
    /// <param name="limit">返回的最大消息数量，默认10</param>
    /// <returns>按时间顺序排列的LLM消息列表</returns>
    public async Task<List<MessageRecord>> LoadHistoryAsync(int worldId, int limit = 10)
    {
        return await _store.LoadRecentAsync(worldId, limit);
    }

    /// <summary>列出所有会话的元数据（透传 ISessionStore.ListConversationsAsync）。</summary>
    public Task<List<ConversationMeta>> ListConversationsAsync() => _store.ListConversationsAsync();

    /// <summary>重命名指定 WorldId 的会话（透传 ISessionStore.RenameConversationAsync）。</summary>
    /// <param name="worldId">世界 ID</param>
    /// <param name="name">新名字（空串=清除自命名，回退到 AutoName）</param>
    public Task RenameConversationAsync(int worldId, string name) => _store.RenameConversationAsync(worldId, name);

    /// <summary>清空指定 WorldId 的对话流（透传 ISessionStore.ClearAsync）。</summary>
    public Task ClearAsync(int worldId) => _store.ClearAsync(worldId);

    /// <summary>加载 Agent 用的历史：找最后一条压缩边界，返回 (边界摘要, 边界之后的消息)。</summary>
    public async Task<(string? oldSummary, List<MessageRecord> newMessages)> LoadForAgentAsync(int worldId)
    {
        return await _store.LoadForAgentAsync(worldId);
    }

    /// <summary>加载 Agent 用的历史并转换为 LlmMessage（只取 user/assistant，跳过 system/tool）。</summary>
    public async Task<(string? oldSummary, List<LlmMessage> newMessages)> LoadForAgentAsMessagesAsync(int worldId)
    {
        var (oldSummary, records) = await _store.LoadForAgentAsync(worldId);
        var messages = records
            .Where(m => m.Role == "user" || m.Role == "assistant")
            .Select(m => new LlmMessage { Role = m.Role, Content = m.Content })
            .ToList();
        return (oldSummary, messages);
    }

    /// <summary>末尾追加压缩边界消息（L2 压缩完成后调用）。</summary>
    public Task AppendBoundaryAsync(int worldId, string summary) => _store.AppendBoundaryAsync(worldId, summary);

    /// <summary>加载会话历史，格式可直接用于 build_initial_messages。</summary>
    /// <param name="worldId">世界ID</param>
    /// <param name="limit">返回的最大消息数量，默认10</param>
    /// <param name="includeBoundaries">是否包含压缩边界消息</param>
    /// <returns>按时间顺序排列的LLM消息列表</returns>
    public async Task<List<MessageRecord>> LoadHistoryAsync(int worldId, int limit = 10, bool includeBoundaries = true)
    {
        return await _store.LoadRecentAsync(worldId, limit, includeBoundaries);
    }
}