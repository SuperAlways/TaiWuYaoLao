using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TaiwuEncyclopedia.Core.Session;

namespace TaiwuEncyclopedia.Core.Storage;

/// <summary>
/// JsonSessionStore：每个 WorldId 一个 json 文件，存消息列表。
/// 文件路径：{root}/Sessions/Worlds/world-{worldId}.json
/// </summary>
public sealed class JsonSessionStore : ISessionStore
{
    private readonly string _root;

    /// <summary>
    /// 初始化 JsonSessionStore。
    /// </summary>
    /// <param name="root">根目录路径</param>
    public JsonSessionStore(string root)
    {
        _root = root;
    }

    private string PathFor(int worldId) => Path.Combine(_root, "Sessions", "Worlds", $"world-{worldId}.json");

    /// <summary>
    /// 追加一条消息到指定 WorldId 的对话流。
    /// </summary>
    /// <param name="worldId">世界 ID</param>
    /// <param name="message">消息记录</param>
    public async Task AppendMessageAsync(int worldId, MessageRecord message)
    {
        var path = PathFor(worldId);
        var messages = await LoadRecentAsync(worldId, int.MaxValue);
        messages.Add(message);
        await AtomicFile.WriteJsonAsync(path, messages);
    }

    /// <summary>
    /// 读取指定 WorldId 的最近 N 条消息（按时间正序返回）。
    /// </summary>
    /// <param name="worldId">世界 ID</param>
    /// <param name="limit">最大消息数</param>
    public async Task<List<MessageRecord>> LoadRecentAsync(int worldId, int limit)
    {
        var path = PathFor(worldId);
        var messages = await AtomicFile.ReadJsonAsync<List<MessageRecord>>(path);
        if (messages == null || messages.Count == 0)
        {
            return new List<MessageRecord>();
        }
        if (limit >= messages.Count)
        {
            return messages;
        }
        return messages.GetRange(messages.Count - limit, limit);
    }

    /// <summary>
    /// 清空指定 WorldId 的对话流（玩家重置用）。
    /// </summary>
    /// <param name="worldId">世界 ID</param>
    public Task ClearAsync(int worldId)
    {
        var path = PathFor(worldId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }
}
