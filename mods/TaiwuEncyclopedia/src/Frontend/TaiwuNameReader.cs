using System;
using GameData.Domains.Character;
using GameData.Domains.Character.Display;
using GameData.Serializer;
using TaiwuEncyclopedia.Core.Session;
using UnityEngine;

namespace TaiwuEncyclopedia;

/// <summary>
/// 太吾名字读取器：获取当前太吾角色的显示名（带门派头衔）。
/// 未进档时返回"主界面"。
/// </summary>
public static class TaiwuNameReader
{
    private const float TIMEOUT = 10f;

    /// <summary>
    /// 获取当前太吾的显示名（带门派头衔）。
    /// </summary>
    /// <returns>太吾显示名，未进档时返回"主界面"</returns>
    public static string CurrentTaiwuName()
    {
        int worldId = WorldIdReader.CurrentWorldId();
        if (worldId == SessionManager.PregameWorldId)
        {
            return "主界面";
        }

        try
        {
            // 获取太吾角色ID
            BasicGameData bgd = SingletonObject.getInstance<BasicGameData>();
            if (bgd == null)
            {
                return "太吾";
            }

            int taiwuCharId = bgd.TaiwuCharId;
            if (taiwuCharId <= 0)
            {
                return "太吾";
            }

            // TODO(Task0 实测): 异步等待方式需在游戏中确认
            // jianghu-youling 在协程中使用 yield return WaitDone(() => ddDone)
            // 由于我们在同步方法中，使用自旋等待作为临时方案
            CharacterDisplayData? dd = null;
            bool ddDone = false;

            CharacterDomainMethod.AsyncCall.GetCharacterDisplayData(null, taiwuCharId, (offset, pool) =>
            {
                try
                {
                    _ = Serializer.Deserialize(pool, offset, ref dd);
                }
#pragma warning disable CA1031 // 反序列化可能抛出各种异常
#pragma warning disable RCS1075 // 反序列化异常是预期的
                catch (Exception)
#pragma warning restore CA1031, RCS1075
                {
                    // 反序列化失败时保持 dd 为 null
                }
                finally
                {
                    ddDone = true;
                }
            });

            // 自旋等待异步调用完成（带超时保护）
            float startTime = Time.realtimeSinceStartup;
            while (!ddDone && Time.realtimeSinceStartup < startTime + TIMEOUT)
            {
                System.Threading.Thread.Sleep(1);
            }

            if (dd != null)
            {
                try
                {
                    string name = NameCenter.GetMonasticTitleOrDisplayName(dd, false);
                    if (!string.IsNullOrEmpty(name))
                    {
                        return name;
                    }
                }
#pragma warning disable CA1031 // NameCenter 调用可能抛出各种异常
#pragma warning disable RCS1075 // NameCenter 异常是预期的
                catch (Exception)
#pragma warning restore CA1031, RCS1075
                {
                    // NameCenter 调用失败时回退
                }
            }

            // 回退方案
            return $"太吾#{taiwuCharId}";
        }
#pragma warning disable CA1031 // 我们需要捕获所有异常，因为在未进档时游戏API可能抛出任何异常
#pragma warning disable RCS1075 // 未进档时的异常是预期的
        catch (Exception)
#pragma warning restore CA1031, RCS1075
        {
            // 异常时回退
        }

        // 如果获取失败，回退到"太吾"
        return "太吾";
    }
}
