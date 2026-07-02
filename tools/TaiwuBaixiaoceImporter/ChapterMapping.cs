using System.Collections.Generic;

namespace TaiwuBaixiaoceImporter;

/// <summary>百晓册 10 章源目录→章节 ID 映射（spec 3.1 节）。</summary>
internal sealed record ChapterMapping(string Id, string CnName, string SourceDir)
{
    /// <summary>全部 10 章映射。</summary>
    public static readonly IReadOnlyList<ChapterMapping> All = new[]
    {
        new ChapterMapping("qi-cheng", "启程", "启程"),
        new ChapterMapping("shijie", "世界", "世界"),
        new ChapterMapping("menpai", "门派", "门派"),
        new ChapterMapping("renwu", "人物", "人物"),
        new ChapterMapping("xiuxi", "修习", "修习"),
        new ChapterMapping("zhandou", "战斗", "战斗"),
        new ChapterMapping("jiaohu", "交互", "交互"),
        new ChapterMapping("chanye", "产业", "产业"),
        new ChapterMapping("wupin", "物品", "物品"),
        new ChapterMapping("youli", "游历", "游历"),
    };

    /// <summary>按源目录名查章节映射。</summary>
    /// <param name="sourceDir">源目录名（如 "启程"）。</param>
    /// <returns>映射条目，未找到返回 null。</returns>
    public static ChapterMapping? ResolveBySourceDir(string sourceDir)
    {
        foreach (ChapterMapping m in All)
        {
            if (m.SourceDir == sourceDir)
            {
                return m;
            }
        }
        return null;
    }
}