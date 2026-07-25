using UnityEngine;
using TaiwuEncyclopedia.Core.Session;

namespace TaiwuEncyclopedia;

/// <summary>NPC 面板药老入口注入器。F9: 从UI获取当前NPC charId并注入供探针测试用。
/// 参照 worldtalk VariableExtractor 从 EventModel.DisplayingEventData.TargetCharacter.CharacterId 拿 charId。</summary>
internal sealed class NpcEntryInjector : MonoBehaviour
{
    private static NpcEntryInjector? _instance;
    private int _injectedCharId = 0;

    public static int InjectedCharId => _instance?._injectedCharId ?? 0;

    public static void Ensure()
    {
        if (_instance != null) return;
        var go = new GameObject("TaiwuEncyclopedia_NpcEntryInjector");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<NpcEntryInjector>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    private void Update()
    {
        // F9: 从当前UI获取NPC charId并注入(测试用)
        // 正式版应该在NPC面板加按钮, 这里用F9简化
        if (!Input.GetKeyDown(KeyCode.F9)) return;
        TryInjectNpcCharId();
    }

    private void TryInjectNpcCharId()
    {
        try
        {
            // 参照 worldtalk: EventModel.DisplayingEventData.TargetCharacter.CharacterId
            var eventModel = SingletonObject.getInstance<EventModel>();
            if (eventModel?.DisplayingEventData?.TargetCharacter != null)
            {
                _injectedCharId = eventModel.DisplayingEventData.TargetCharacter.CharacterId;
                Debug.Log($"[TaiwuEncyclopedia] NPC charId injected: {_injectedCharId}");
                return;
            }
        }
        catch { }
        _injectedCharId = 0;
        Debug.Log("[TaiwuEncyclopedia] No NPC charId to inject");
    }

    public static void ClearInjection()
    {
        if (_instance != null) _instance._injectedCharId = 0;
    }
}
