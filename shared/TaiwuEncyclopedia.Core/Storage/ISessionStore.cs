namespace TaiwuEncyclopedia.Core.Storage;

/// <summary>
/// 对话流持久化抽象。按 WorldId 隔离（同一存档不同世界会话独立）。
/// v1.0 默认实现 JsonSessionStore；未来可换 SqliteSessionStore。
/// </summary>
public interface ISessionStore
{
    /// <summary>追加一条消息到指定 WorldId 的对话流。</summary>
    /// <param name="worldId">世界 ID</param>
    /// <param name="message">消息记录</param>
    System.Threading.Tasks.Task AppendMessageAsync(int worldId, Session.MessageRecord message);

    /// <summary>读取指定 WorldId 的最近 N 条消息（按时间正序返回）。</summary>
    /// <param name="worldId">世界 ID</param>
    /// <param name="limit">最大消息数</param>
    System.Threading.Tasks.Task<System.Collections.Generic.List<Session.MessageRecord>> LoadRecentAsync(int worldId, int limit);

    /// <summary>清空指定 WorldId 的对话流（玩家重置用）。</summary>
    /// <param name="worldId">世界 ID</param>
    System.Threading.Tasks.Task ClearAsync(int worldId);

    /// <summary>列出所有会话的元数据（扫描 world-*.json + index.json 合并）。</summary>
    System.Threading.Tasks.Task<System.Collections.Generic.List<Session.ConversationMeta>> ListConversationsAsync();

    /// <summary>重命名指定 WorldId 的会话（只改 index.json 的 Name，不动对话流）。</summary>
    /// <param name="worldId">世界 ID</param>
    /// <param name="name">新名字（空串=清除自命名，回退到 AutoName）</param>
    System.Threading.Tasks.Task RenameConversationAsync(int worldId, string name);
}
