using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TaiwuEncyclopedia.Core.Storage;

/// <summary>
/// 原子文件读写工具：先写 .tmp 再 File.Replace（文件存在时）/ File.Move（新建时），单次 OS 原子操作，防止写一半崩溃损坏文件。
/// 参照 jianghu-youling NpcMemoryStore.Save 模式。
/// </summary>
public static class AtomicFile
{
    /// <summary>
    /// 原子写入 JSON 数据到文件。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="path">文件路径</param>
    /// <param name="data">要写入的数据</param>
    public static async Task WriteJsonAsync<T>(string path, T data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        await File.WriteAllTextAsync(tmp, json);
        try
        {
            if (File.Exists(path))
                File.Replace(tmp, path, null, ignoreMetadataErrors: true);
            else
                File.Move(tmp, path);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    /// <summary>
    /// 读 json 文件；损坏或不存在返回 default（调用方负责给默认值）。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="path">文件路径</param>
    public static async Task<T?> ReadJsonAsync<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }
        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch
        {
            // 损坏文件：备份后返回 default（spec 第 436 行：重建空）
            var backup = path + ".corrupt." + System.DateTime.UtcNow.Ticks;
            try { File.Move(path, backup); } catch { }
            return default;
        }
    }
}
