namespace TaiwuEncyclopedia.Core.Storage;

/// <summary>
/// 通用 KV 存储抽象（预留）。v1.0 主要给配置/缓存用，默认 JsonKeyValueStore。
/// 参照 lightrag BaseKVStorage 模式但简化（单玩家，无 namespace）。
/// </summary>
public interface IKeyValueStore
{
    /// <summary>获取指定键的值。</summary>
    /// <param name="key">键</param>
    System.Threading.Tasks.Task<string?> GetAsync(string key);

    /// <summary>设置指定键的值。</summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    System.Threading.Tasks.Task SetAsync(string key, string value);

    /// <summary>删除指定键。</summary>
    /// <param name="key">键</param>
    System.Threading.Tasks.Task DeleteAsync(string key);

    /// <summary>获取所有键值对。</summary>
    System.Threading.Tasks.Task<System.Collections.Generic.Dictionary<string, string>> GetAllAsync();
}
