#pragma warning disable CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, CA1031
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using TaiwuEncyclopedia.Frontend.UI;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// ConfigPanel 视图层：纯 UGUI 构建，对标 ChatPanelView。
/// 只负责 UI 骨架创建，不含业务逻辑。
/// </summary>
public sealed class ConfigPanelView : MonoBehaviour
{
    private GameObject? _root;
    private TMP_FontAsset? _font;

    public RectTransform? ContentTransform { get; private set; }
    public TextMeshProUGUI? ValidationText { get; private set; }
    public Button? SaveBtn { get; private set; }
    public Button? CancelBtn { get; private set; }
    public TMP_FontAsset? Font => _font;

    public void Build(GameObject root, TMP_FontAsset? font)
    {
        _root = root;
        _font = font;

        Canvas canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30010;
        CanvasScaler sc = _root.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);

        // 主面板
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_root.transform, false);
        RectTransform prt = panel.GetComponent<RectTransform>();
        prt.sizeDelta = new Vector2(1100, 750);
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = UiTheme.PanelBg;

        // 标题栏
        GameObject titleBar = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
        titleBar.transform.SetParent(panel.transform, false);
        UiFactory.Anchor(titleBar.GetComponent<RectTransform>(),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -56), new Vector2(0, 0));
        titleBar.GetComponent<Image>().color = UiTheme.TitleBarBg;

        TextMeshProUGUI titleText = NewText("Title", panel.transform, 26, TextAlignmentOptions.Center);
        UiFactory.Anchor(titleText.rectTransform,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(48, -48), new Vector2(-96, -8));
        titleText.text = "设置";
        titleText.color = new Color(0.95f, 0.92f, 0.82f, 1f);

        // 关闭按钮
        Button closeBtn = UiFactory.CreateButton(panel.transform, "CloseBtn", "X", 22, UiTheme.Accent, out _);
        RectTransform crt = closeBtn.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1, 1);
        crt.anchoredPosition = new Vector2(-10, -10);
        crt.sizeDelta = new Vector2(36, 36);
        closeBtn.onClick.AddListener(PanelStack.Pop);

        // ScrollRect
        GameObject scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image),
            typeof(ScrollRect), typeof(RectMask2D));
        scrollGo.transform.SetParent(panel.transform, false);
        UiFactory.Anchor(scrollGo.GetComponent<RectTransform>(),
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(12, 70), new Vector2(-12, -70));
        scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.12f);
        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 28f;

        // Content 容器
        GameObject contentGo = new GameObject("Content", typeof(RectTransform),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.SetParent(scrollGo.transform, false);
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.sizeDelta = new Vector2(0, 0);
        VerticalLayoutGroup vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.spacing = 10f; vlg.padding = new RectOffset(8, 8, 12, 12);
        ContentSizeFitter csf = contentGo.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = contentRt;
        scroll.viewport = scrollGo.GetComponent<RectTransform>();
        ContentTransform = contentRt;

        // 验证文本
        ValidationText = NewText("ValidationText", contentRt, 16, TextAlignmentOptions.Center);
        ValidationText.color = UiTheme.ErrorText;
        ValidationText.text = "";
        ValidationText.enableWordWrapping = true;

        // 底部栏
        BuildBottomBar(panel.transform);

        _root.SetActive(false);
    }

    private void BuildBottomBar(Transform parent)
    {
        GameObject bar = new GameObject("BottomBar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(parent, false);
        UiFactory.Anchor(bar.GetComponent<RectTransform>(),
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 58));
        bar.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.10f, 0.95f);

        GameObject btnRow = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow.transform.SetParent(bar.transform, false);
        UiFactory.Anchor(btnRow.GetComponent<RectTransform>(),
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(12, 8), new Vector2(-12, -8));
        HorizontalLayoutGroup hlg = btnRow.GetComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.spacing = 12f; hlg.childAlignment = TextAnchor.MiddleRight;

        CancelBtn = UiFactory.CreateButton(btnRow.transform, "CancelBtn", "取消", 19, UiTheme.Accent, out _);
        CancelBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 19;
        CancelBtn.onClick.AddListener(PanelStack.Pop);
        LayoutElement cle = CancelBtn.gameObject.AddComponent<LayoutElement>();
        cle.preferredWidth = 110; cle.preferredHeight = 42;

        SaveBtn = UiFactory.CreateButton(btnRow.transform, "SaveBtn", "保存并关闭", 19, UiTheme.Accent, out _);
        SaveBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 19;
        LayoutElement sle = SaveBtn.gameObject.AddComponent<LayoutElement>();
        sle.preferredWidth = 140; sle.preferredHeight = 42;
    }

    public void Show() { if (_root != null) _root.SetActive(true); }
    public void Hide() { if (_root != null) _root.SetActive(false); }

    private TextMeshProUGUI NewText(string name, Transform parent, float size, TextAlignmentOptions align)
    {
        return UiFactory.CreateText(parent, name, "", size,
            new Color(0.92f, 0.90f, 0.82f, 1f), align);
    }
}
#pragma warning restore CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, CA1031
