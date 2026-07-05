namespace TaiwuEncyclopedia.Core.Storage;

/// <summary>
/// Soul 持久化抽象。SoulProfile 跨档全局，SoulWorld 按 WorldId 隔离。
/// 损坏文件由实现负责重建空对象（spec 第 436 行）。
/// </summary>
public interface ISoulStore
{
    /// <summary>读取跨档全局 SoulProfile（损坏返回空 Profile）。</summary>
    System.Threading.Tasks.Task<Soul.SoulProfile> LoadProfileAsync();

    /// <summary>保存跨档全局 SoulProfile（原子写）。</summary>
    /// <param name="profile">Soul 配置文件</param>
    System.Threading.Tasks.Task SaveProfileAsync(Soul.SoulProfile profile);

    /// <summary>读取指定 WorldId 的 SoulWorld（损坏返回空 World）。</summary>
    /// <param name="worldId">世界 ID</param>
    System.Threading.Tasks.Task<Soul.SoulWorld> LoadWorldAsync(int worldId);

    /// <summary>保存指定 WorldId 的 SoulWorld（原子写）。</summary>
    /// <param name="worldId">世界 ID</param>
    /// <param name="world">Soul 世界数据</param>
    System.Threading.Tasks.Task SaveWorldAsync(int worldId, Soul.SoulWorld world);
}
