#pragma warning disable CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, CA1031
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// ChatPanel 的视图层：构建完整 UGUI 面板（Canvas/标题栏/ScrollRect/MessageListView/ChatInputBar）。
/// 对标 WorldTalk 的 Hub 宿主模式。
/// </summary>
public sealed class ChatPanelView : MonoBehaviour
{
    private GameObject? _root;
    private TextMeshProUGUI? _title;
    private TMP_FontAsset? _font;

    // 子组件（外部访问）
    public MessageListView? MsgList { get; private set; }
    public ChatInputBar? InputBar { get; private set; }
    public GameObject? Root => _root;
    public TextMeshProUGUI? Title => _title;
    public TMP_FontAsset? Font => _font;

    private static readonly Color ColPanel = UiTheme.PanelBg;
    private static readonly Color ColAccent = UiTheme.Accent;
    private static readonly Color ColTitleBar = UiTheme.TitleBarBg;

    public void Build(GameObject root, TMP_FontAsset? font)
    {
        _root = root;
        _font = font;

        Canvas canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        CanvasScaler sc = _root.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);

        // 主面板 (1100x750)
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_root.transform, false);
        RectTransform prt = panel.GetComponent<RectTransform>();
        prt.sizeDelta = new Vector2(1100, 750);
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = ColPanel;

        // 标题栏
        GameObject titleBar = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
        titleBar.transform.SetParent(panel.transform, false);
        Anchor(titleBar.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -56), new Vector2(0, 0));
        titleBar.GetComponent<Image>().color = ColTitleBar;

        _title = NewText("Title", panel.transform, 26, TextAlignmentOptions.Center);
        Anchor(_title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(48, -48), new Vector2(-140, -8));
        _title.color = new Color(0.95f, 0.92f, 0.82f, 1f);

        // 设置按钮
        GameObject settingsGo = NewButton("SettingsBtn", panel.transform, "⚙ 设置", 20, out Button sBtn);
        RectTransform srt = settingsGo.GetComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(1, 1);
        srt.anchoredPosition = new Vector2(-54, -10);
        srt.sizeDelta = new Vector2(80, 36);
        sBtn.onClick.AddListener(() => ConfigPanel.Open(_font));

        // 关闭按钮
        GameObject closeGo = NewButton("CloseBtn", panel.transform, "✕", 22, out Button cBtn);
        RectTransform crt = closeGo.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1, 1);
        crt.anchoredPosition = new Vector2(-10, -10);
        crt.sizeDelta = new Vector2(36, 36);
        cBtn.onClick.AddListener(PanelStack.Pop);

        // 滚动消息区
        GameObject scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
        scrollGo.transform.SetParent(panel.transform, false);
        Anchor(scrollGo.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 1), new Vector2(12, 70), new Vector2(-12, -70));
        scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.18f);

        // 垂直滚动条
        GameObject scrollbarGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarGo.transform.SetParent(scrollGo.transform, false);
        RectTransform sbrt = scrollbarGo.GetComponent<RectTransform>();
        sbrt.anchorMin = new Vector2(1, 0);
        sbrt.anchorMax = new Vector2(1, 1);
        sbrt.pivot = new Vector2(1, 1);
        sbrt.anchoredPosition = Vector2.zero;
        sbrt.sizeDelta = new Vector2(10, 0);
        Scrollbar sb = scrollbarGo.GetComponent<Scrollbar>();
        sb.direction = Scrollbar.Direction.BottomToTop;
        sb.handleRect = CreateScrollbarHandle(scrollbarGo.transform);
        sb.targetGraphic = sb.handleRect.GetComponent<Image>();

        scrollGo.GetComponent<ScrollRect>().verticalScrollbar = sb;
        scrollGo.GetComponent<ScrollRect>().verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollGo.GetComponent<ScrollRect>().scrollSensitivity = 24f;
        scrollGo.GetComponent<ScrollRect>().movementType = ScrollRect.MovementType.Clamped;

        // 内容容器
        GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.SetParent(scrollGo.transform, false);
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0, 0);
        VerticalLayoutGroup vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.spacing = 8f;
        vlg.padding = new RectOffset(6, 6, 12, 12);
        ContentSizeFitter csf = contentGo.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollGo.GetComponent<ScrollRect>().content = contentRt;

        MsgList = gameObject.AddComponent<MessageListView>();
        MsgList.Initialize(contentRt, scrollGo.GetComponent<ScrollRect>(), _font);

        // 输入栏
        InputBar = gameObject.AddComponent<ChatInputBar>();
        InputBar.Build(panel.transform, _font);
    }

    private RectTransform CreateScrollbarHandle(Transform parent)
    {
        // track 背景设为透明
        parent.GetComponent<Image>().color = new Color(0, 0, 0, 0);

        GameObject handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.transform.SetParent(parent, false);
        handleGo.GetComponent<Image>().color = UiTheme.Accent;
        RectTransform hrt = handleGo.GetComponent<RectTransform>();
        hrt.anchorMin = Vector2.zero;
        hrt.anchorMax = Vector2.one;
        hrt.pivot = new Vector2(0.5f, 0.5f);
        hrt.anchoredPosition = Vector2.zero;
        hrt.sizeDelta = new Vector2(-2, -4);
        return hrt;
    }

    public void Show() { if (_root != null) _root.SetActive(true); }
    public void Hide() { if (_root != null) _root.SetActive(false); }

    // ===== 内部 UGUI 工具 =====
    private TextMeshProUGUI NewText(string name, Transform parent, float size, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.fontSize = size;
        t.alignment = align;
        t.richText = true;
        t.color = new Color(0.92f, 0.90f, 0.82f, 1f);
        return t;
    }

    private GameObject NewButton(string name, Transform parent, string label, float size, out Button btn)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = ColAccent;
        btn = go.GetComponent<Button>();
        TextMeshProUGUI t = NewText("L", go.transform, size, TextAlignmentOptions.Center);
        Anchor(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        t.text = label;
        t.raycastTarget = false;
        return go;
    }

    private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
    }
}
#pragma warning restore CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, CA1031