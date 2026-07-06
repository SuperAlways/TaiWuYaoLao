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
    /// <param name="includeBoundaries">是否包含压缩边界消息（前端传 false 过滤）</param>
    System.Threading.Tasks.Task<System.Collections.Generic.List<Session.MessageRecord>> LoadRecentAsync(int worldId, int limit, bool includeBoundaries = true);

    /// <summary>加载 Agent 用的历史：找最后一条压缩边界，返回 (边界摘要, 边界之后的消息)。无边界返回 (null, 全部)。</summary>
    /// <param name="worldId">世界 ID</param>
    System.Threading.Tasks.Task<(string? oldSummary, System.Collections.Generic.List<Session.MessageRecord> newMessages)> LoadForAgentAsync(int worldId);

    /// <summary>在指定 WorldId 的对话流末尾追加压缩边界消息（Role=system, IsCompactBoundary=true）。</summary>
    /// <param name="worldId">世界 ID</param>
    /// <param name="summary">压缩摘要文本</param>
    System.Threading.Tasks.Task AppendBoundaryAsync(int worldId, string summary);

    /// <summary>清空指定 WorldId 的对话流（玩家重置用）。</summary>
    /// <param name="worldId">世界 ID</param>
    System.Threading.Tasks.Task ClearAsync(int worldId);

    /// <summary>列出所有会话的元数据（扫描 world-*.json + index.json 合并）。</summary>
    System.Threading.Tasks.Task<System.Collections.Generic.List<Session.ConversationMeta>> ListConversationsAsync();

    /// <summary>重命名指定 WorldId 的会话（只改 index.json 的 Name，不动对话流）。</summary>
    /// <param name="worldId">世界 ID</param>
    /// <param name="name">新名字（空串=清除自命名，回退到 AutoName）</param>
    System.Threading.Tasks.Task RenameConversationAsync(int worldId, string name);

    /// <summary>设置指定 WorldId 的 AutoName（仅当当前为空时写入，首次对话太吾名快照）。</summary>
    /// <param name="worldId">世界 ID</param>
    /// <param name="autoName">太吾名快照（非空才写入）</param>
    System.Threading.Tasks.Task SetAutoNameAsync(int worldId, string autoName);
}
