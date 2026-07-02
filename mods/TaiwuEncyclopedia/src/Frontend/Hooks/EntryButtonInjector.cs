// Reflection + Unity UGUI patterns: nullable flow-analysis + instantiation via AddComponent + no crash guarantee + non-static members for Unity pattern.
#pragma warning disable CS8604, CS8618, CA1812, CA1031, CA1822
// IDE/RCS style preferences (per-file, matches Tasks 2-5 convention).
#pragma warning disable IDE0008, IDE0011, IDE0090, IDE0031, IDE0032, IDE0040, IDE0051, IDE0052, IDE0055, IDE0058, IDE0074, RCS1048, RCS1124, RCS1146, RCS1181, RCS1213, RCS1222
using System;
using System.Collections;
using System.Reflection;
using System.Globalization;
using TaiwuEncyclopedia.UI;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Views.EventWindow;
using EwChar = Game.Components.EventWindow.EventWindowCharacter;

namespace TaiwuEncyclopedia.Hooks;

/// <summary>
/// 反射访问游戏 ViewEventWindow 的私有字段。仿照 jianghu EwReflect。
/// </summary>
internal static class EwReflect
{
    const BindingFlags NP = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    // Private fields - keep using reflection (matches jianghu pattern)
    private static readonly FieldInfo? s_rightCharField = typeof(ViewEventWindow).GetField("rightCharacter", NP);
    private static readonly FieldInfo? s_mainRootField = typeof(ViewEventWindow).GetField("mainWindowRoot", NP);
    private static readonly FieldInfo? s_curCharIdField = typeof(EwChar).GetField("_curCharacterId", NP);

    /// <summary>
    /// 获取 ViewEventWindow 的 rightCharacter（如果有）
    /// </summary>
    public static EwChar? RightCharacter(ViewEventWindow? window)
    {
        if (window == null || s_rightCharField == null) return null;
        try { return s_rightCharField.GetValue(window) as EwChar; }
        catch { return null; }
    }

    /// <summary>
    /// 获取 ViewEventWindow 的 mainWindowRoot（注入按钮的父对象）
    /// </summary>
    public static RectTransform? MainRoot(ViewEventWindow? window)
    {
        if (window == null || s_mainRootField == null) return null;
        try { return s_mainRootField.GetValue(window) as RectTransform; }
        catch { return null; }
    }

    /// <summary>
    /// 从 rightCharacter 获取 nameLabel（用于借字体）
    /// </summary>
    public static TextMeshProUGUI? NameLabel(EwChar? ewChar)
    {
        if (ewChar == null) return null;
        try { return ewChar.nameLabel; }
        catch { return null; }
    }

    /// <summary>
    /// 从 rightCharacter 获取当前 NPC ID
    /// </summary>
    public static int CharacterId(EwChar? ewChar)
    {
        if (ewChar == null || s_curCharIdField == null) return -1;
        try
        {
            var val = s_curCharIdField.GetValue(ewChar);
            if (val is int id) return id;
        }
        catch
        {
            // ignored
        }
        return -1;
    }

    /// <summary>
    /// 检查当前是否在正常交互事件中（EventModel.IsOnNormalInteractEvent）
    /// </summary>
    public static bool IsOnNormalInteractEvent()
    {
        try
        {
            var eventModel = SingletonObject.getInstance<EventModel>();
            if (eventModel == null) return false;
            return eventModel.IsOnNormalInteractEvent;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取当前显示的 ViewEventWindow（通过 UIElement.EventWindow）
    /// </summary>
    public static ViewEventWindow? GetCurrentEventWindow()
    {
        try
        {
            if (UIElement.EventWindow == null) return null;
            return UIElement.EventWindow.UiBase as ViewEventWindow;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 检查 EventWindow 是否存在/显示
    /// </summary>
    public static bool EventWindowExists()
    {
        try
        {
            if (UIElement.EventWindow == null) return false;
            return UIElement.EventWindow.Exist;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// EntryButtonInjector: 协程每 0.25s 检查事件窗口，注入「百晓问答」按钮。
/// 仿照 jianghu TalkEntryHost + TalkEntryInjector。
/// </summary>
public class EntryButtonInjector : MonoBehaviour
{
    private const string ButtonName = "TaiwuEncyclopedia_EntryButton";
    private WaitForSeconds? _wait250ms;
    private TMP_FontAsset? _cachedFont;
    private GameObject? _injectedButton;

    /// <summary>
    /// 启动注入器（带字体参数）
    /// </summary>
    public void StartPolling(TMP_FontAsset? font = null)
    {
        _cachedFont = font;
        _wait250ms = new WaitForSeconds(0.25f);
        StartCoroutine(PollCoroutine());
        Debug.Log("[TaiwuEncyclopedia] EntryButtonInjector started polling");
    }

    /// <summary>
    /// 主轮询协程
    /// </summary>
    private IEnumerator PollCoroutine()
    {
        while (true)
        {
            yield return _wait250ms;
            try
            {
                TryRefresh();
            }
            catch (Exception ex)
            {
                Debug.LogError(string.Format(CultureInfo.InvariantCulture, "[TaiwuEncyclopedia] EntryButtonInjector poll error: {0}", ex.Message));
            }
        }
    }

    /// <summary>
    /// 检查状态并刷新按钮
    /// </summary>
    private void TryRefresh()
    {
        var window = EwReflect.GetCurrentEventWindow();
        bool show = ShouldShowButton(window);

        if (!show)
        {
            HideButton();
            return;
        }

        ShowOrCreateButton(window);
    }

    /// <summary>
    /// 判断是否应该显示按钮
    /// </summary>
    private bool ShouldShowButton(ViewEventWindow? window)
    {
        if (window == null || !EwReflect.EventWindowExists()) return false;
        if (!EwReflect.IsOnNormalInteractEvent()) return false;

        var ewChar = EwReflect.RightCharacter(window);
        if (ewChar == null) return false;

        var charId = EwReflect.CharacterId(ewChar);
        if (charId <= 0) return false;

        // 检查是不是太吾本人（可选，但为了安全）
        try
        {
            BasicGameData bgd = SingletonObject.getInstance<BasicGameData>();
            if (bgd != null && charId == bgd.TaiwuCharId)
            {
                return false;
            }
        }
        catch
        {
            // ignored
        }

        return true;
    }

    /// <summary>
    /// 隐藏/销毁已注入的按钮
    /// </summary>
    private void HideButton()
    {
        if (_injectedButton != null)
        {
            _injectedButton.SetActive(false);
            // 不立即销毁，避免反复创建；下次需要时直接 SetActive(true)
        }
    }

    /// <summary>
    /// 显示已有的按钮，或创建新按钮
    /// </summary>
    private void ShowOrCreateButton(ViewEventWindow? window)
    {
        if (window == null) return;
        var mainRoot = EwReflect.MainRoot(window);
        if (mainRoot == null) return;

        // 检查按钮是否已存在（可能是我们之前创建的，或是场景里残留的）
        var existing = mainRoot.Find(ButtonName);
        if (existing != null)
        {
            _injectedButton = existing.gameObject;
            _injectedButton.SetActive(true);
            _injectedButton.transform.SetAsLastSibling(); // 确保在最上层
            return;
        }

        // 创建新按钮
        var ewChar = EwReflect.RightCharacter(window);
        var nameLabel = EwReflect.NameLabel(ewChar);

        // 如果还没有字体，从 nameLabel 借；如果 nameLabel 也没有，就用 _cachedFont
        var font = nameLabel?.font ?? _cachedFont;

        _injectedButton = CreateButton(mainRoot, font);

        if (_injectedButton != null)
        {
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "[TaiwuEncyclopedia] Injected entry button, font available: {0}", font != null));
        }
    }

    /// <summary>
    /// 创建「百晓问答」按钮，仿照 jianghu TalkEntryInjector.Create
    /// </summary>
    private GameObject? CreateButton(RectTransform parent, TMP_FontAsset? font)
    {
        try
        {
            GameObject go = new GameObject(ButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 32f);
            rt.sizeDelta = new Vector2(196f, 40f);

            // 颜色：用 UiTheme.Accent（青绿色），和 ChatPanel 按钮一致
            go.GetComponent<Image>().color = UiTheme.Accent;

            // 创建文本标签
            TextMeshProUGUI lbl = CreateLabel(go.transform, font);
            lbl.text = "百晓问答";

            // 绑定点击事件
            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            btn.onClick.AddListener(() => OnButtonClick(font));

            return go;
        }
        catch (Exception ex)
        {
            Debug.LogError(string.Format(CultureInfo.InvariantCulture, "[TaiwuEncyclopedia] Failed to create entry button: {0}", ex.Message));
            return null;
        }
    }

    /// <summary>
    /// 创建按钮文本
    /// </summary>
    private TextMeshProUGUI CreateLabel(Transform parent, TMP_FontAsset? font)
    {
        GameObject lblGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        lblGo.transform.SetParent(parent, false);

        TextMeshProUGUI lbl = lblGo.GetComponent<TextMeshProUGUI>();
        if (font != null) lbl.font = font;
        lbl.fontSize = 20;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.enableAutoSizing = true;
        lbl.fontSizeMin = 14;
        lbl.fontSizeMax = 22;
        lbl.color = new Color(0.92f, 0.92f, 0.82f, 1f);
        lbl.raycastTarget = false;

        RectTransform rt = lbl.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(6f, 1f);
        rt.offsetMax = new Vector2(-6f, -1f);

        return lbl;
    }

    /// <summary>
    /// 按钮点击：打开 ChatPanel
    /// </summary>
    private void OnButtonClick(TMP_FontAsset? font)
    {
        try
        {
            Debug.Log("[TaiwuEncyclopedia] Entry button clicked, opening ChatPanel");
            ChatPanel.Open(font);
        }
        catch (Exception ex)
        {
            Debug.LogError(string.Format(CultureInfo.InvariantCulture, "[TaiwuEncyclopedia] Button click error: {0}", ex.Message));
        }
    }
}
#pragma warning restore CS8604, CS8618, CA1812, CA1031, CA1822
#pragma warning restore IDE0008, IDE0011, IDE0090, IDE0031, IDE0032, IDE0040, IDE0051, IDE0052, IDE0055, IDE0058, IDE0074, RCS1048, RCS1124, RCS1146, RCS1181, RCS1213, RCS1222
