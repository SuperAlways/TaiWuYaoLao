#pragma warning disable CS8618
using UnityEngine;
using TMPro;

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
            ChatPanel.Open(ResolveFont());
    }

    // 从场景中任一 TextMeshProUGUI 借游戏中文字体；借不到则返回 null
    private TMP_FontAsset? ResolveFont()
    {
        if (_font != null) return _font;
        try
        {
            TextMeshProUGUI[] texts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
            foreach (TextMeshProUGUI t in texts)
            {
                // 跳过我们自己面板里的文本，优先借游戏自带文本的字体
                if (t != null && t.font != null && t.gameObject.scene.IsValid())
                {
                    _font = t.font;
                    Debug.Log(string.Format("[TaiwuEncyclopedia] Found font: {0}", _font.name));
                    break;
                }
            }
            // 退一步:连场景内的也借不到，就取任意一个非空字体
            if (_font == null)
            {
                foreach (TextMeshProUGUI t in texts)
                {
                    if (t != null && t.font != null)
                    {
                        _font = t.font;
                        Debug.Log(string.Format("[TaiwuEncyclopedia] Fallback font: {0}", _font.name));
                        break;
                    }
                }
            }
        }
        catch { _font = null; }

        // 不再回退到 defaultFontAsset（不同版本 TMP 可能没有）
        return _font;
    }
}
#pragma warning restore CS8618
