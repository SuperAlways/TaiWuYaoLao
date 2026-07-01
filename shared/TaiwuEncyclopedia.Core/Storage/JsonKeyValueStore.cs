using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace TaiwuEncyclopedia.Core.Storage;

/// <summary>
/// JsonKeyValueStore：单个 json 文件存 Dictionary&lt;string,string&gt;。
/// v1.0 主要给配置/缓存用。
/// </summary>
public sealed class JsonKeyValueStore : IKeyValueStore
{
    private readonly string _path;
    private Dictionary<string, string> _cache;

    /// <summary>
    /// 初始化 JsonKeyValueStore。
    /// </summary>
    /// <param name="path">文件路径</param>
    public JsonKeyValueStore(string path)
    {
        _path = path;
        _cache = AtomicFile.ReadJsonAsync<Dictionary<string, string>>(path).GetAwaiter().GetResult()
                 ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// 获取指定键的值。
    /// </summary>
    /// <param name="key">键</param>
    public Task<string?> GetAsync(string key)
    {
        _cache.TryGetValue(key, out string? value);
        return Task.FromResult<string?>(value);
    }

    /// <summary>
    /// 设置指定键的值。
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    public async Task SetAsync(string key, string value)
    {
        _cache[key] = value;
        await AtomicFile.WriteJsonAsync(_path, _cache);
    }

    /// <summary>
    /// 删除指定键。
    /// </summary>
    /// <param name="key">键</param>
    public async Task DeleteAsync(string key)
    {
        _cache.Remove(key);
        await AtomicFile.WriteJsonAsync(_path, _cache);
    }

    /// <summary>
    /// 获取所有键值对。
    /// </summary>
    public Task<Dictionary<string, string>> GetAllAsync()
        => Task.FromResult(new Dictionary<string, string>(_cache));
}
