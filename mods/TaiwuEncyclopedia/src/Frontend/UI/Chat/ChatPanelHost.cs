#pragma warning disable CS8618, IDE0011, IDE0090, RCS1213, CA1305, RCS1181, RCS1146, CA1031, IDE0051
using System.Globalization;
using UnityEngine;
using TMPro;

using TaiwuEncyclopedia.Frontend.UI;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// 常驻宿主:监听 F8 热键打开 ChatPanel。仿 jianghu ConfigHost。DontDestroyOnLoad。
/// </summary>
public sealed class ChatPanelHost : MonoBehaviour
{
    public static ChatPanelHost? Instance { get; private set; }

    private TMP_FontAsset? _font;

    public static void Initialize()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("TaiwuEncyclopedia_ChatPanelHost");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<ChatPanelHost>();
    }

    private void Update()
    {
        // F8:打开/关闭 ChatPanel
        if (!Input.GetKeyDown(KeyCode.F8)) return;

        if (PanelStack.AnyOpen)
            PanelStack.Pop();
        else
            ChatPanel.Open(UiFactory.Font);
    }

    /// <summary>
    /// 从场景中任一 TextMeshProUGUI 借游戏中文字体；借不到则返回 null
    /// </summary>
}
#pragma warning restore CS8618, IDE0011, IDE0090, RCS1213, CA1305, RCS1181, RCS1146, CA1031, IDE0051
