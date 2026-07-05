namespace TaiwuEncyclopedia.Core.Session;

/// <summary>会话元数据（index.json 一条记录）。</summary>
public sealed class ConversationMeta
{
    /// <summary>世界 ID（会话隔离键）。</summary>
    public int WorldId { get; set; }

    /// <summary>玩家自命名，空串=未自命名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>首次对话时存入的太吾名快照，空串=当时没读到。之后不覆盖（除非手动改 Name）。</summary>
    public string AutoName { get; set; } = string.Empty;

    /// <summary>对话条数（user+assistant 消息总数）。</summary>
    public int Count { get; set; }
}
