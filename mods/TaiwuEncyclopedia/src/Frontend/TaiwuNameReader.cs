using System;
using System.Collections;
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
    /// <summary>
    /// 获取当前太吾的显示名（带门派头衔）。
    /// 非阻塞异步方式：结果通过回调返回。
    /// </summary>
    /// <param name="callback">接收太吾显示名的回调函数</param>
    public static void CurrentTaiwuName(Action<string> callback)
    {
        if (callback == null)
        {
            return;
        }

        int worldId = WorldIdReader.CurrentWorldId();
        if (worldId == SessionManager.PregameWorldId)
        {
            callback("主界面");
            return;
        }

        try
        {
            // 获取太吾角色ID
            BasicGameData bgd = SingletonObject.getInstance<BasicGameData>();
            if (bgd == null)
            {
                callback("太吾");
                return;
            }

            int taiwuCharId = bgd.TaiwuCharId;
            if (taiwuCharId <= 0)
            {
                callback("太吾");
                return;
            }

            // 使用协程等待异步调用完成
#pragma warning disable IDE0058 // StartCoroutine 返回值不需要使用
            _ = NameReaderDriver.Instance.StartCoroutine(FetchNameCoroutine(taiwuCharId, callback));
#pragma warning restore IDE0058
        }
#pragma warning disable CA1031 // 我们需要捕获所有异常，因为在未进档时游戏API可能抛出任何异常
#pragma warning disable RCS1075 // 未进档时的异常是预期的
        catch (Exception)
#pragma warning restore CA1031, RCS1075
        {
            callback("太吾");
        }
    }

    /// <summary>
    /// 协程：异步获取太吾名字
    /// </summary>
    private static IEnumerator FetchNameCoroutine(int taiwuCharId, Action<string> callback)
    {
        CharacterDisplayData? dd = null;
        bool ddDone = false;

        CharacterDomainMethod.AsyncCall.GetCharacterDisplayData(null, taiwuCharId, (offset, pool) =>
        {
            try
            {
#pragma warning disable IDE0058 // 反序列化返回值不需要使用
                _ = Serializer.Deserialize(pool, offset, ref dd);
#pragma warning restore IDE0058
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

        // 使用 WaitUntil 而非 Thread.Sleep — 让主线程保持响应
        yield return new WaitUntil(() => ddDone);

        if (dd != null)
        {
            try
            {
                string name = NameCenter.GetMonasticTitleOrDisplayName(dd, false);
                if (!string.IsNullOrEmpty(name))
                {
                    callback(name);
                    yield break;
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
        callback($"太吾#{taiwuCharId}");
    }

    /// <summary>
    /// 驱动 MonoBehaviour：用于启动协程
    /// </summary>
#pragma warning disable CA1812 // 此类通过 Instance 属性实例化
#pragma warning disable CA1852 // 保留 unsealed 以便 Unity 正常工作
    private sealed class NameReaderDriver : MonoBehaviour
#pragma warning restore CA1812, CA1852
    {
        private static NameReaderDriver? _instance;

        public static NameReaderDriver Instance
        {
            get
            {
                if (_instance == null)
                {
                    Ensure();
                }
                return _instance!;
            }
        }

        public static void Ensure()
        {
            if (_instance != null)
            {
                return;
            }

            GameObject go = new("TaiwuEncyclopedia_NameReaderDriver");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<NameReaderDriver>();
        }

#pragma warning disable IDE0051 // Unity 生命周期方法由引擎调用
#pragma warning disable RCS1213 // Unity 生命周期方法由引擎调用
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
#pragma warning restore IDE0051, RCS1213
    }
}
