using System.IO;
using System.Threading.Tasks;
using TaiwuEncyclopedia.Core.Soul;

namespace TaiwuEncyclopedia.Core.Storage;

/// <summary>
/// JsonSoulStore：SoulProfile 存 {root}/Soul/Profile.json，SoulWorld 存 {root}/Soul/World-{worldId}.json。
/// </summary>
public sealed class JsonSoulStore : ISoulStore
{
    private readonly string _root;

    /// <summary>
    /// 初始化 JsonSoulStore。
    /// </summary>
    /// <param name="root">根目录路径</param>
    public JsonSoulStore(string root)
    {
        _root = root;
    }

    private string ProfilePath => Path.Combine(_root, "Soul", "Profile.json");
    private string WorldPath(int worldId) => Path.Combine(_root, "Soul", $"World-{worldId}.json");

    /// <summary>
    /// 读取跨档全局 SoulProfile（损坏返回空 Profile）。
    /// </summary>
    public async Task<SoulProfile> LoadProfileAsync()
    {
        var profile = await AtomicFile.ReadJsonAsync<SoulProfile>(ProfilePath);
        return profile ?? new SoulProfile();
    }

    /// <summary>
    /// 保存跨档全局 SoulProfile（原子写）。
    /// </summary>
    /// <param name="profile">Soul 配置文件</param>
    public Task SaveProfileAsync(SoulProfile profile)
        => AtomicFile.WriteJsonAsync(ProfilePath, profile);

    /// <summary>
    /// 读取指定 WorldId 的 SoulWorld（损坏返回空 World）。
    /// </summary>
    /// <param name="worldId">世界 ID</param>
    public async Task<SoulWorld> LoadWorldAsync(int worldId)
    {
        var world = await AtomicFile.ReadJsonAsync<SoulWorld>(WorldPath(worldId));
        return world ?? new SoulWorld();
    }

    /// <summary>
    /// 保存指定 WorldId 的 SoulWorld（原子写）。
    /// </summary>
    /// <param name="worldId">世界 ID</param>
    /// <param name="world">Soul 世界数据</param>
    public Task SaveWorldAsync(int worldId, SoulWorld world)
        => AtomicFile.WriteJsonAsync(WorldPath(worldId), world);
}
