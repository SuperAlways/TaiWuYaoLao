#pragma warning disable CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, IDE0010, IDE0022, IDE0048, RCS1123, CA1307, RCS1238, CA1852, CS0414, RCS1201, RCS1124, IDE0057, CA1031, RCS1001, IDE0370, RCS1085, IDE0028, RCS1161
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TaiwuEncyclopedia.Core.Diagnostics;
using TaiwuEncyclopedia.Frontend.UI;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// 日志查看面板：消费 ModLog.Entries + OnEntry，按 tag/level 过滤，一键复制。
/// </summary>
public sealed class PlayerLogViewer : MonoBehaviour, IPanel
{
    private TMP_FontAsset? _font;
    private GameObject? _root;
    private TextMeshProUGUI? _logText;
    private ScrollRect? _scrollRect;
    private readonly List<LogEntry> _allEntries = new();
    private string _tagFilter = "";   // "" = 全部
    private string _levelFilter = ""; // "" = 全部

    // Active filter button references (for highlight)
    private Button? _activeTagBtn;
    private Button? _activeLevelBtn;
    private readonly Color _filterBtnNormal = new(0.18f, 0.20f, 0.22f, 1f);
    private readonly Color _filterBtnActive = new(0.90f, 0.76f, 0.40f, 1f); // Accent

    // Level colors (same as ThinkingPanel.SetHint)
    private static readonly Color InfoColor = new(0.55f, 0.58f, 0.60f, 1f);
    private static readonly Color WarnColor = new(0.85f, 0.75f, 0.30f, 1f);
    private static readonly Color ErrorColor = new(0.90f, 0.30f, 0.30f, 1f);

    public static void Open(TMP_FontAsset? font)
    {
        var go = new GameObject("PlayerLogViewer", typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        DontDestroyOnLoad(go);
        var viewer = go.AddComponent<PlayerLogViewer>();
        viewer._font = font;
        viewer.Build(go);
        PanelStack.Push(viewer);
    }

    private void Build(GameObject root)
    {
        _root = root;
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30020;
        var sc = root.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);

        // 面板
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.sizeDelta = new Vector2(1100, 700);
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = UiTheme.PanelBg;

        // 标题栏
        BuildTitleBar(panel.transform);

        // 过滤栏
        BuildFilterBar(panel.transform);

        // 日志文本（ScrollRect）
        BuildLogArea(panel.transform);

        // 底部栏
        BuildBottomBar(panel.transform);

        // 订阅 ModLog — 快照当前缓冲 + 实时追加
        _allEntries.AddRange(ModLog.Entries);
        ModLog.OnEntry += OnNewEntry;
        RefreshDisplay();
    }

    // ========== 标题栏 ==========

    private void BuildTitleBar(Transform parent)
    {
        var titleBar = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
        titleBar.transform.SetParent(parent, false);
        UiFactory.Anchor(titleBar.GetComponent<RectTransform>(),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -48), new Vector2(0, 0));
        titleBar.GetComponent<Image>().color = UiTheme.TitleBarBg;

        var titleText = UiFactory.CreateText(titleBar.transform, "Title",
            "运行日志", 24, new Color(0.95f, 0.92f, 0.82f, 1f), TextAlignmentOptions.Center);
        UiFactory.Anchor(titleText.rectTransform,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(48, -42), new Vector2(-96, -6));

        // 关闭按钮
        var closeBtn = UiFactory.CreateButton(parent, "CloseBtn", "X", 22, UiTheme.Accent, out _);
        var crt = closeBtn.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1, 1);
        crt.anchoredPosition = new Vector2(-10, -10);
        crt.sizeDelta = new Vector2(36, 36);
        closeBtn.onClick.AddListener(PanelStack.Pop);
    }

    // ========== 过滤栏 ==========

    private void BuildFilterBar(Transform parent)
    {
        var filterBar = new GameObject("FilterBar", typeof(RectTransform), typeof(Image));
        filterBar.transform.SetParent(parent, false);
        UiFactory.Anchor(filterBar.GetComponent<RectTransform>(),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -90), new Vector2(0, -48));
        filterBar.GetComponent<Image>().color = new Color(0.10f, 0.12f, 0.12f, 0.95f);

        // 两行水平按钮
        // Row 1: tag 过滤
        var tagRow = new GameObject("TagRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        tagRow.transform.SetParent(filterBar.transform, false);
        var tagHlg = tagRow.GetComponent<HorizontalLayoutGroup>();
        tagHlg.childForceExpandWidth = false;
        tagHlg.childForceExpandHeight = false;
        tagHlg.childControlWidth = true;
        tagHlg.childControlHeight = true;
        tagHlg.spacing = 6f;
        tagHlg.padding = new RectOffset(12, 12, 4, 2);
        UiFactory.Anchor(tagRow.GetComponent<RectTransform>(),
            new Vector2(0, 1), new Vector2(1, 0.5f), Vector2.zero, Vector2.zero);

        // Tag 过滤按钮
        string[] tags = { "", "Agent", "LLM", "RAG", "Session" };
        string[] tagLabels = { "全部", "Agent", "LLM", "RAG", "Session" };
        for (int i = 0; i < tags.Length; i++)
        {
            string tag = tags[i];
            string label = tagLabels[i];
            var btn = UiFactory.CreateButton(tagRow.transform, $"Tag_{label}", label, 16,
                i == 0 ? _filterBtnActive : _filterBtnNormal, out var btnLabel);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 70;
            le.preferredHeight = 24;
            if (i == 0)
            {
                _activeTagBtn = btn;
                btnLabel.color = new Color(0.12f, 0.12f, 0.14f, 1f); // dark text on accent
            }
            btn.onClick.AddListener(() => SetTagFilter(tag, btn));
        }

        // Row 2: level 过滤
        var levelRow = new GameObject("LevelRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        levelRow.transform.SetParent(filterBar.transform, false);
        var levelHlg = levelRow.GetComponent<HorizontalLayoutGroup>();
        levelHlg.childForceExpandWidth = false;
        levelHlg.childForceExpandHeight = false;
        levelHlg.childControlWidth = true;
        levelHlg.childControlHeight = true;
        levelHlg.spacing = 6f;
        levelHlg.padding = new RectOffset(12, 12, 2, 4);
        UiFactory.Anchor(levelRow.GetComponent<RectTransform>(),
            new Vector2(0, 0.5f), new Vector2(1, 0), Vector2.zero, Vector2.zero);

        // Level 过滤按钮
        string[] levels = { "", "info", "warn", "error" };
        string[] levelLabels = { "全部", "info", "warn", "error" };
        for (int i = 0; i < levels.Length; i++)
        {
            string level = levels[i];
            string label = levelLabels[i];
            var btn = UiFactory.CreateButton(levelRow.transform, $"Level_{label}", label, 16,
                i == 0 ? _filterBtnActive : _filterBtnNormal, out var btnLabel);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 70;
            le.preferredHeight = 24;
            if (i == 0)
            {
                _activeLevelBtn = btn;
                btnLabel.color = new Color(0.12f, 0.12f, 0.14f, 1f); // dark text on accent
            }
            btn.onClick.AddListener(() => SetLevelFilter(level, btn));
        }
    }

    // ========== 日志文本区域 ==========

    private void BuildLogArea(Transform parent)
    {
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image),
            typeof(ScrollRect), typeof(RectMask2D));
        scrollGo.transform.SetParent(parent, false);
        UiFactory.Anchor(scrollGo.GetComponent<RectTransform>(),
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(12, 60), new Vector2(-12, -94));
        scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.18f);
        var sr = scrollGo.GetComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 28f;

        _logText = UiFactory.CreateText(scrollGo.transform, "LogText", "", 15,
            new Color(0.85f, 0.83f, 0.78f, 1f), TextAlignmentOptions.TopLeft);
        UiFactory.Anchor(_logText.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(10, 6), new Vector2(-10, -6));
        _logText.enableWordWrapping = true;
        sr.content = _logText.rectTransform;
        sr.viewport = scrollGo.GetComponent<RectTransform>();

        _scrollRect = sr;
    }

    // ========== 底部栏 ==========

    private void BuildBottomBar(Transform parent)
    {
        var bar = new GameObject("BottomBar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(parent, false);
        UiFactory.Anchor(bar.GetComponent<RectTransform>(),
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 50));
        bar.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.10f, 0.95f);

        var btnRow = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow.transform.SetParent(bar.transform, false);
        UiFactory.Anchor(btnRow.GetComponent<RectTransform>(),
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(12, 8), new Vector2(-12, -8));
        var hlg = btnRow.GetComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.spacing = 12f;
        hlg.childAlignment = TextAnchor.MiddleRight;

        var copyBtn = UiFactory.CreateButton(btnRow.transform, "CopyBtn", "复制", 18,
            UiTheme.Accent, out _);
        copyBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 18;
        var cle = copyBtn.gameObject.AddComponent<LayoutElement>();
        cle.preferredWidth = 100;
        cle.preferredHeight = 36;
        copyBtn.onClick.AddListener(CopyAll);

        var backBtn = UiFactory.CreateButton(btnRow.transform, "BackBtn", "返回", 18,
            UiTheme.Accent, out _);
        backBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 18;
        var ble = backBtn.gameObject.AddComponent<LayoutElement>();
        ble.preferredWidth = 100;
        ble.preferredHeight = 36;
        backBtn.onClick.AddListener(PanelStack.Pop);
    }

    // ========== 数据更新 ==========

    private void OnNewEntry(LogEntry entry)
    {
        _allEntries.Add(entry);
        RefreshDisplay();
    }

    private void SetTagFilter(string tag, Button btn)
    {
        _tagFilter = tag;
        HighlightFilterButton(ref _activeTagBtn, btn);
        RefreshDisplay();
    }

    private void SetLevelFilter(string level, Button btn)
    {
        _levelFilter = level;
        HighlightFilterButton(ref _activeLevelBtn, btn);
        RefreshDisplay();
    }

    private void HighlightFilterButton(ref Button? activeBtn, Button newBtn)
    {
        if (activeBtn != null)
        {
            activeBtn.GetComponent<Image>().color = _filterBtnNormal;
            var lbl = activeBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) lbl.color = new Color(0.92f, 0.90f, 0.82f, 1f);
        }
        newBtn.GetComponent<Image>().color = _filterBtnActive;
        var newLbl = newBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (newLbl != null) newLbl.color = new Color(0.12f, 0.12f, 0.14f, 1f);
        activeBtn = newBtn;
    }

    private void RefreshDisplay()
    {
        if (_logText == null) return;

        var filtered = _allEntries
            .Where(e => string.IsNullOrEmpty(_tagFilter) || e.Tag == _tagFilter)
            .Where(e => string.IsNullOrEmpty(_levelFilter) || e.Level == _levelFilter);

        // Build rich text with per-level coloring
        _logText.text = string.Join("\n", filtered.Select(FormatEntry));

        // Auto-scroll to bottom
        if (_scrollRect != null)
            _scrollRect.normalizedPosition = new Vector2(0, 0);
    }

    private static string FormatEntry(LogEntry e)
    {
        string colorHex = e.Level switch
        {
            "warn" => ColorUtility.ToHtmlStringRGBA(WarnColor),
            "error" => ColorUtility.ToHtmlStringRGBA(ErrorColor),
            _ => ColorUtility.ToHtmlStringRGBA(InfoColor),
        };
        string time = e.Timestamp.ToString("HH:mm", CultureInfo.InvariantCulture);
        return $"<color=#{colorHex}>[{e.Level}]</color> {time}  {e.Message}";
    }

    private void CopyAll()
    {
        // Copy plain text (no rich-text tags) to clipboard
        var filtered = _allEntries
            .Where(e => string.IsNullOrEmpty(_tagFilter) || e.Tag == _tagFilter)
            .Where(e => string.IsNullOrEmpty(_levelFilter) || e.Level == _levelFilter);
        GUIUtility.systemCopyBuffer = string.Join("\n", filtered.Select(FormatEntryPlain));
    }

    private static string FormatEntryPlain(LogEntry e)
    {
        string time = e.Timestamp.ToString("HH:mm", CultureInfo.InvariantCulture);
        return string.Format(CultureInfo.InvariantCulture, "[{0}] {1}  {2}", e.Level, time, e.Message);
    }

    // ========== IPanel ==========

    public void Show() => _root?.SetActive(true);
    public void Hide() => _root?.SetActive(false);

    private void OnDestroy()
    {
        ModLog.OnEntry -= OnNewEntry;
    }
}
#pragma warning restore CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, IDE0010, IDE0022, IDE0048, RCS1123, CA1307, RCS1238, CA1852, CS0414, RCS1201, RCS1124, IDE0057, CA1031, RCS1001, IDE0370, RCS1085, IDE0028, RCS1161
