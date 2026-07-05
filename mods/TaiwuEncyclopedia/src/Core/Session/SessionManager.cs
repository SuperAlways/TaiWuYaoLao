using System.Collections.Generic;
using System.Threading.Tasks;
using TaiwuEncyclopedia.Core.Http;
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
        string? autoName = null)
    {
        await _store.AppendMessageAsync(worldId, new MessageRecord { Role = "user", Content = userQuery });
        await _store.AppendMessageAsync(worldId, new MessageRecord
        {
            Role = "assistant",
            Content = assistantAnswer,
            References = references,
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
    public async Task<List<LlmMessage>> LoadHistoryAsync(int worldId, int limit = 10)
    {
        var records = await _store.LoadRecentAsync(worldId, limit);
        var messages = new List<LlmMessage>();
        foreach (var r in records)
        {
            messages.Add(new LlmMessage { Role = r.Role, Content = r.Content });
        }
        return messages;
    }

    /// <summary>列出所有会话的元数据（透传 ISessionStore.ListConversationsAsync）。</summary>
    public Task<List<ConversationMeta>> ListConversationsAsync() => _store.ListConversationsAsync();

    /// <summary>重命名指定 WorldId 的会话（透传 ISessionStore.RenameConversationAsync）。</summary>
    /// <param name="worldId">世界 ID</param>
    /// <param name="name">新名字（空串=清除自命名，回退到 AutoName）</param>
    public Task RenameConversationAsync(int worldId, string name) => _store.RenameConversationAsync(worldId, name);

    /// <summary>清空指定 WorldId 的对话流（透传 ISessionStore.ClearAsync）。</summary>
    public Task ClearAsync(int worldId) => _store.ClearAsync(worldId);
}