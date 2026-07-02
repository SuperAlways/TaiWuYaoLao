#pragma warning disable CS8618, IDE0011, IDE0090, RCS1213, CA1305, RCS1181, RCS1146, CA1031, IDE0051
using System.Globalization;
using UnityEngine;
using TMPro;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// 常驻宿主: 监听 F9 热键打开 ConfigPanel。
/// 注：ChatPanelHost 监听 F8，这里单独处理 F9。
/// </summary>
public sealed class ConfigPanelHost : MonoBehaviour
{
    public static ConfigPanelHost? Instance { get; private set; }

    private TMP_FontAsset? _font;

    public static void Initialize()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("TaiwuEncyclopedia_ConfigPanelHost");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<ConfigPanelHost>();
    }

    private void Update()
    {
        // F9: 打开/关闭 ConfigPanel
        if (!Input.GetKeyDown(KeyCode.F9)) return;

        if (PanelStack.AnyOpen)
        {
            // 如果有面板打开，先判断是不是 ConfigPanel 在最上层
            PanelStack.Pop();
        }
        else
        {
            ConfigPanel.Open(ResolveFont());
        }
    }

    /// <summary>
    /// 从场景中任一 TextMeshProUGUI 借游戏中文字体；借不到则返回 null。
    /// (与 ChatPanelHost.ResolveFont 相同逻辑，共享缓存)
    /// </summary>
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
                    Debug.Log(string.Format(CultureInfo.InvariantCulture, "[TaiwuEncyclopedia] Found font: {0}", _font.name));
                    break;
                }
            }
            // 退一步: 连场景内的也借不到，就取任意一个非空字体
            if (_font == null)
            {
                foreach (TextMeshProUGUI t in texts)
                {
                    if (t != null && t.font != null)
                    {
                        _font = t.font;
                        Debug.Log(string.Format(CultureInfo.InvariantCulture, "[TaiwuEncyclopedia] Fallback font: {0}", _font.name));
                        break;
                    }
                }
            }
        }
        catch { _font = null; }

        return _font;
    }
}
#pragma warning restore CS8618, IDE0011, IDE0090, RCS1213, CA1305, RCS1181, RCS1146, CA1031, IDE0051
