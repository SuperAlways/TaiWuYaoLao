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

    private string IndexPath => Path.Combine(_root, "Sessions", "Worlds", "index.json");

    private async Task<Dictionary<int, ConversationMeta>> LoadIndexAsync()
    {
        var idx = await AtomicFile.ReadJsonAsync<Dictionary<int, ConversationMeta>>(IndexPath);
        return idx ?? new Dictionary<int, ConversationMeta>();
    }

    private async Task SaveIndexAsync(Dictionary<int, ConversationMeta> idx)
    {
        await AtomicFile.WriteJsonAsync(IndexPath, idx);
    }

    private static ConversationMeta EnsureEntry(Dictionary<int, ConversationMeta> idx, int worldId)
    {
        if (!idx.TryGetValue(worldId, out var meta))
        {
            meta = new ConversationMeta { WorldId = worldId };
            idx[worldId] = meta;
        }
        return meta;
    }

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

        // 更新 index.json 的 count
        var idx = await LoadIndexAsync();
        var meta = EnsureEntry(idx, worldId);
        meta.Count = messages.Count;
        await SaveIndexAsync(idx);
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

    /// <summary>列出所有会话的元数据（扫描 world-*.json + index.json 合并）。</summary>
    public async Task<List<ConversationMeta>> ListConversationsAsync()
    {
        var idx = await LoadIndexAsync();
        var dir = Path.Combine(_root, "Sessions", "Worlds");
        var result = new List<ConversationMeta>();
        if (!Directory.Exists(dir)) return result;

        foreach (var file in Directory.GetFiles(dir, "world-*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!name.StartsWith("world-")) continue;
            if (!int.TryParse(name.Substring("world-".Length), out var worldId)) continue;

            var meta = idx.TryGetValue(worldId, out var m)
                ? m
                : new ConversationMeta { WorldId = worldId };
            // count 以实际文件为准（index 可能过时）
            var msgs = await LoadRecentAsync(worldId, int.MaxValue);
            meta.Count = msgs.Count;
            result.Add(meta);
        }
        result.Sort((a, b) => b.WorldId.CompareTo(a.WorldId));
        return result;
    }

    /// <summary>重命名指定 WorldId 的会话（只改 index.json 的 Name，不动对话流）。</summary>
    public async Task RenameConversationAsync(int worldId, string name)
    {
        var idx = await LoadIndexAsync();
        var meta = EnsureEntry(idx, worldId);
        meta.Name = name ?? "";
        await SaveIndexAsync(idx);
    }

    /// <summary>设置指定 WorldId 的 AutoName（仅当当前为空时写入，首次对话太吾名快照）。</summary>
    public async Task SetAutoNameAsync(int worldId, string autoName)
    {
        if (string.IsNullOrEmpty(autoName)) return;
        var idx = await LoadIndexAsync();
        var meta = EnsureEntry(idx, worldId);
        if (string.IsNullOrEmpty(meta.AutoName))
        {
            meta.AutoName = autoName;
            await SaveIndexAsync(idx);
        }
    }
}
