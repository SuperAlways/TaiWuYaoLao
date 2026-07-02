using System;
using TaiwuEncyclopedia.Core.Session;

namespace TaiwuEncyclopedia;

/// <summary>
/// WorldId读取器：获取当前存档的稳定标识（会话隔离键）。
/// 未进档时返回 SessionManager.PregameWorldId（-1）。
/// </summary>
public static class WorldIdReader
{
    /// <summary>
    /// 获取当前存档的WorldId（会话隔离键）。
    /// </summary>
    /// <returns>当前存档的WorldId，未进档时返回 SessionManager.PregameWorldId（-1）</returns>
    public static int CurrentWorldId()
    {
        try
        {
            // TODO(Task0 实测): 确认 TaiwuCharId 是否为"档"隔离键(spec §D3 候选 DomainManager.World)
            // 按 jianghu-youling 成熟模式：使用 BasicGameData.TaiwuCharId 作为隔离键
            // 理由：每个存档的太吾角色ID是唯一且稳定的，换档时变化，满足会话隔离需求
            BasicGameData bgd = SingletonObject.getInstance<BasicGameData>();
            if (bgd != null)
            {
                int taiwuCharId = bgd.TaiwuCharId;
                if (taiwuCharId > 0)
                {
                    return taiwuCharId;
                }
            }
        }
#pragma warning disable CA1031 // 我们需要捕获所有异常，因为在未进档时游戏API可能抛出任何异常
#pragma warning disable RCS1075 // 未进档时的异常是预期的，不需要处理
        catch (Exception)
#pragma warning restore CA1031, RCS1075
        {
            // 未进档或单例未就绪时，返回PregameWorldId
        }

        // 未进档或异常时返回 PregameWorldId
        return SessionManager.PregameWorldId;
    }
}
