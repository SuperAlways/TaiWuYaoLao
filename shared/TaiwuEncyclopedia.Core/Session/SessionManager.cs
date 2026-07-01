using System.Collections.Generic;
using System.Threading.Tasks;
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

    /// <summary>
    /// 初始化会话管理器
    /// </summary>
    /// <param name="store">会话存储实现</param>
    public SessionManager(ISessionStore store)
    {
        _store = store;
    }

    /// <summary>循环结束后一次性保存 user_query + assistant_answer。</summary>
    /// <param name="worldId">世界ID</param>
    /// <param name="userQuery">用户查询内容</param>
    /// <param name="assistantAnswer">助手回答内容</param>
    public async Task SaveConversationAsync(
        int worldId,
        string userQuery,
        string assistantAnswer)
    {
        await _store.AppendMessageAsync(worldId, new MessageRecord { Role = "user", Content = userQuery });
        await _store.AppendMessageAsync(worldId, new MessageRecord { Role = "assistant", Content = assistantAnswer });
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
}