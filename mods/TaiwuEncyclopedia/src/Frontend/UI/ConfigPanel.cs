#pragma warning disable CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, IDE0010, IDE0022, IDE0048, RCS1123, CA1307, RCS1238, CA1852, CS0414, RCS1201, RCS1124, IDE0057, CA1031, RCS1001, IDE0370, RCS1085, IDE0028, RCS1161
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using TaiwuEncyclopedia.Core.Session;
using TaiwuEncyclopedia.Core.Skills;
using TaiwuEncyclopedia.Core.Soul;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using TaiwuEncyclopedia.Frontend.UI;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// 设置面板：4个区域 - 大模型接口/对话风格/历史对话/数据与日志。
/// 实现 IPanel，通过 PanelStack.Push 显示在 ChatPanel 之上。
/// F9 快捷键独立打开。
/// </summary>
public class ConfigPanel : MonoBehaviour, IPanel
{
    private static ConfigPanel? _instance;

    /// <summary>
    /// 打开设置面板。
    /// </summary>
    public static void Open(TMP_FontAsset? font = null)
    {
        if (_instance == null)
        {
            GameObject go = new GameObject("TaiwuEncyclopedia_ConfigPanel", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ConfigPanel>();
            _instance._font = font;
            _instance.Build();
        }
        PanelStack.Push(_instance);
    }

    // ========== UGUI 字段 ==========
    private GameObject? _root;
    private Canvas? _canvas;
    private TMP_FontAsset? _font;

    // 区域1: 大模型接口
    private TMP_InputField? _baseUrlInput;
    private TMP_InputField? _apiKeyInput;
    private TMP_InputField? _modelInput;
    private TextMeshProUGUI? _testStatusText;
    private Button? _testBtn;
    private bool _testingConnection;

    // 区域2: 对话风格
    private Button? _personaButton;
    private TextMeshProUGUI? _personaBtnLabel;
    private int _currentPersonaIdx;
    private TextMeshProUGUI? _personaPreviewText;
    private List<string>? _personaIdList;

    // 区域3: 历史对话
    private RectTransform? _historyListContent;
    private List<GameObject>? _historyItems;

    // 区域4: 数据与日志
    private TextMeshProUGUI? _runtimePathText;
    private TextMeshProUGUI? _soulProfileText;
    private TextMeshProUGUI? _soulWorldText;
    private bool _soulProfileExpanded;
    private bool _soulWorldExpanded;

    // 底部按钮
    private Button? _saveBtn;
    private Button? _cancelBtn;
    private TextMeshProUGUI? _validationText;

    // 状态
    private bool _hasUnsavedChanges;

    // 测试验证状态
    private enum TestState { NotTested, Testing, Passed, Invalidated }
    private TestState _testState = TestState.NotTested;

    // 行内字段错误标签
    private TextMeshProUGUI? _baseUrlError;
    private TextMeshProUGUI? _apiKeyError;
    private TextMeshProUGUI? _modelError;

    // ========== IPanel 实现 ==========
    public void Show()
    {
        if (_root == null) return;
        _root.SetActive(true);
        RefreshAll();
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    // ========== 构建 (参照 jianghu ConfigWindow) ==========
    private void Build()
    {
        try
        {
            _root = gameObject;
        _canvas = _root.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 30010; // 高于 ChatPanel (30000)
        CanvasScaler sc = _root.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);

        // 主面板 (720x760, 稍高)
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
        Anchor(titleBar.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -56), new Vector2(0, 0));
        titleBar.GetComponent<Image>().color = UiTheme.TitleBarBg;

        TextMeshProUGUI titleText = NewText("Title", panel.transform, 26, TextAlignmentOptions.Center);
        Anchor(titleText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(48, -48), new Vector2(-96, -8));
        titleText.text = "设置";
        titleText.color = new Color(0.95f, 0.92f, 0.82f, 1f);

        // 关闭按钮
        GameObject closeGo = NewButton("CloseBtn", panel.transform, "X", 22, out Button closeBtn);
        RectTransform crt = closeGo.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1, 1);
        crt.anchoredPosition = new Vector2(-10, -10);
        crt.sizeDelta = new Vector2(36, 36);
        closeBtn.onClick.AddListener(PanelStack.Pop);

        // 滚动视图 (容纳所有区域)
        GameObject scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
        scrollGo.transform.SetParent(panel.transform, false);
        Anchor(scrollGo.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 1), new Vector2(12, 70), new Vector2(-12, -70));
        scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.12f);
        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 28f;

        // 内容容器
        GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.SetParent(scrollGo.transform, false);
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.sizeDelta = new Vector2(0, 0);
        VerticalLayoutGroup vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.spacing = 10f;
        vlg.padding = new RectOffset(8, 8, 12, 12);
        ContentSizeFitter csf = contentGo.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = contentRt;
        scroll.viewport = scrollGo.GetComponent<RectTransform>();

        // 区域1: 大模型接口
        BuildLlmSection(contentRt);

        // 区域2: 对话风格
        BuildPersonaSection(contentRt);

        // 区域3: 历史对话
        BuildHistorySection(contentRt);

        // 区域4: 数据与日志
        BuildDataSection(contentRt);

        // 验证文本
        _validationText = NewText("ValidationText", contentRt, 16, TextAlignmentOptions.Center);
        _validationText.color = UiTheme.ErrorText;
        _validationText.text = "";
        _validationText.enableWordWrapping = true;

        // 底部按钮栏
        BuildBottomBar(panel.transform);

            _root.SetActive(false);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[TaiwuEncyclopedia] ConfigPanel build failed: {e}");
            // Leave panel in non-broken state if possible
            if (_root != null) _root.SetActive(false);
        }
    }

    private void BuildLlmSection(Transform parent)
    {
        GameObject section = CreateSection(parent, "大模型接口");
        Transform content = section.transform.Find("Content")!;

        // Base URL
        _baseUrlInput = BuildLabeledInput(content, "Base URL", "https://api.deepseek.com", password: false, out _baseUrlError);
        _baseUrlInput.onValueChanged.AddListener(delegate { OnConfigChanged(); });

        // API Key
        _apiKeyInput = BuildLabeledInput(content, "API Key", "", password: true, out _apiKeyError);
        _apiKeyInput.onValueChanged.AddListener(delegate { OnConfigChanged(); });

        // Model
        _modelInput = BuildLabeledInput(content, "Model", "deepseek-chat", password: false, out _modelError);
        _modelInput.onValueChanged.AddListener(delegate { OnConfigChanged(); });

        // 测试按钮 + 状态
        GameObject btnRow = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow.transform.SetParent(content, false);
        HorizontalLayoutGroup hlg = btnRow.GetComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.spacing = 12f;
        hlg.padding = new RectOffset(0, 0, 6, 6);

        GameObject testGo = NewButton("TestBtn", btnRow.transform, "测试连接", 18, out _testBtn);
        LayoutElement tle = testGo.AddComponent<LayoutElement>();
        tle.preferredWidth = 120;
        tle.preferredHeight = 36;
        _testBtn.onClick.AddListener(OnTestConnection);

        _testStatusText = NewText("TestStatus", btnRow.transform, 16, TextAlignmentOptions.Left);
        _testStatusText.text = "";
        _testStatusText.enableWordWrapping = true;
        LayoutElement sle = _testStatusText.gameObject.AddComponent<LayoutElement>();
        sle.flexibleWidth = 1f;
        sle.preferredHeight = 36;
    }

    private void BuildPersonaSection(Transform parent)
    {
        GameObject section = CreateSection(parent, "对话风格 (Persona)");
        Transform content = section.transform.Find("Content")!;

        // 风格选择行 (循环按钮:点一下切到下一个 persona,规避程序化 TMP_Dropdown 模板的脆弱性)
        GameObject dropRow = new GameObject("DropRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        dropRow.transform.SetParent(content, false);
        HorizontalLayoutGroup hlg = dropRow.GetComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.spacing = 10f;
        hlg.padding = new RectOffset(0, 0, 4, 4);

        TextMeshProUGUI label = NewText("Label", dropRow.transform, 18, TextAlignmentOptions.Left);
        label.text = "选择风格：";
        label.color = new Color(0.80f, 0.78f, 0.70f, 1f);
        LayoutElement lle = label.gameObject.AddComponent<LayoutElement>();
        lle.preferredWidth = 100;
        lle.preferredHeight = 36;

        // 风格按钮(显示当前 persona 名 + ▶,点击循环)
        GameObject btnGo = NewButton("PersonaBtn", dropRow.transform, "", 18, out _personaButton);
        btnGo.GetComponent<Image>().color = new Color(1, 1, 1, 0.12f);
        LayoutElement dle = btnGo.AddComponent<LayoutElement>();
        dle.flexibleWidth = 1f;
        dle.preferredHeight = 36;
        _personaBtnLabel = btnGo.GetComponentInChildren<TextMeshProUGUI>();
        if (_personaButton != null) _personaButton.onClick.AddListener(OnPersonaCycle);

        // 预览区
        GameObject previewBox = new GameObject("PreviewBox", typeof(RectTransform), typeof(Image));
        previewBox.transform.SetParent(content, false);
        previewBox.GetComponent<Image>().color = new Color(0, 0, 0, 0.18f);
        LayoutElement pble = previewBox.AddComponent<LayoutElement>();
        pble.preferredHeight = 120;

        TextMeshProUGUI previewTitle = NewText("PreviewTitle", previewBox.transform, 16, TextAlignmentOptions.Left);
        previewTitle.text = "当前风格预览：";
        previewTitle.color = new Color(0.65f, 0.68f, 0.70f, 1f);
        Anchor(previewTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -26), new Vector2(-10, -6));

        _personaPreviewText = NewText("PreviewText", previewBox.transform, 17, TextAlignmentOptions.TopLeft);
        _personaPreviewText.text = "加载中...";
        _personaPreviewText.color = new Color(0.85f, 0.83f, 0.78f, 1f);
        _personaPreviewText.enableWordWrapping = true;
        // 溢出时省略号截断,防止文本超出 PreviewBox 与下方区域重叠。
        _personaPreviewText.overflowMode = TextOverflowModes.Ellipsis;
        Anchor(_personaPreviewText.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, 6), new Vector2(-10, -32));

        // 提示文本
        TextMeshProUGUI hint = NewText("Hint", content, 15, TextAlignmentOptions.Left);
        hint.text = "切换后下一条消息生效，历史消息不改写";
        hint.color = new Color(0.55f, 0.58f, 0.60f, 1f);
        hint.enableWordWrapping = true;
    }

    private void BuildHistorySection(Transform parent)
    {
        GameObject section = CreateSection(parent, "历史对话");
        Transform content = section.transform.Find("Content")!;

        // 历史列表 (可滚动)
        GameObject listBox = new GameObject("ListBox", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
        listBox.transform.SetParent(content, false);
        listBox.GetComponent<Image>().color = new Color(0, 0, 0, 0.18f);
        LayoutElement lle = listBox.AddComponent<LayoutElement>();
        lle.preferredHeight = 140;

        ScrollRect scroll = listBox.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 20f;

        GameObject listContentGo = new GameObject("ListContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        _historyListContent = listContentGo.GetComponent<RectTransform>();
        _historyListContent.SetParent(listBox.transform, false);
        _historyListContent.anchorMin = new Vector2(0, 1);
        _historyListContent.anchorMax = new Vector2(1, 1);
        _historyListContent.pivot = new Vector2(0.5f, 1);
        _historyListContent.sizeDelta = new Vector2(0, 0);
        VerticalLayoutGroup vlg = listContentGo.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.spacing = 6f;
        vlg.padding = new RectOffset(8, 8, 8, 8);
        ContentSizeFitter csf = listContentGo.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = _historyListContent;
        scroll.viewport = listBox.GetComponent<RectTransform>();

        _historyItems = new List<GameObject>();
    }

    private void BuildDataSection(Transform parent)
    {
        GameObject section = CreateSection(parent, "数据与日志");
        Transform content = section.transform.Find("Content")!;

        // 路径显示
        _runtimePathText = NewText("PathText", content, 15, TextAlignmentOptions.Left);
        _runtimePathText.text = "存档目录：加载中...";
        _runtimePathText.color = new Color(0.55f, 0.58f, 0.60f, 1f);
        _runtimePathText.enableWordWrapping = true;

        // 按钮行
        GameObject btnRow1 = new GameObject("BtnRow1", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow1.transform.SetParent(content, false);
        HorizontalLayoutGroup hlg1 = btnRow1.GetComponent<HorizontalLayoutGroup>();
        hlg1.childForceExpandWidth = false;
        hlg1.childForceExpandHeight = false;
        hlg1.childControlWidth = true;
        hlg1.childControlHeight = true;
        hlg1.spacing = 10f;
        hlg1.padding = new RectOffset(0, 0, 6, 6);

        GameObject openDirGo = NewButton("OpenDirBtn", btnRow1.transform, "打开存档目录", 17, out Button openDirBtn);
        LayoutElement odle = openDirGo.AddComponent<LayoutElement>();
        odle.preferredWidth = 150;
        odle.preferredHeight = 34;
        openDirBtn.onClick.AddListener(OnOpenRuntimeDir);

        GameObject openLogGo = NewButton("OpenLogBtn", btnRow1.transform, "打开日志", 17, out Button openLogBtn);
        LayoutElement olle = openLogGo.AddComponent<LayoutElement>();
        olle.preferredWidth = 110;
        olle.preferredHeight = 34;
        openLogBtn.onClick.AddListener(OnOpenLogsDir);

        // SoulProfile (可折叠)
        BuildSoulCollapsible(content, "SoulProfile (跨档全局)", isProfile: true);

        // SoulWorld (可折叠)
        BuildSoulCollapsible(content, "SoulWorld (当前档)", isProfile: false);

        // 底部按钮行
        GameObject btnRow2 = new GameObject("BtnRow2", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow2.transform.SetParent(content, false);
        HorizontalLayoutGroup hlg2 = btnRow2.GetComponent<HorizontalLayoutGroup>();
        hlg2.childForceExpandWidth = false;
        hlg2.childForceExpandHeight = false;
        hlg2.childControlWidth = true;
        hlg2.childControlHeight = true;
        hlg2.spacing = 10f;
        hlg2.padding = new RectOffset(0, 0, 6, 2);

        GameObject resetProfileGo = NewButton("ResetProfileBtn", btnRow2.transform, "重置 SoulProfile", 17, out Button resetProfileBtn);
        LayoutElement rple = resetProfileGo.AddComponent<LayoutElement>();
        rple.preferredWidth = 160;
        rple.preferredHeight = 34;
        resetProfileBtn.onClick.AddListener(OnResetSoulProfile);

        GameObject clearHistoryGo = NewButton("ClearHistoryBtn", btnRow2.transform, "清除当前对话历史", 17, out Button clearHistoryBtn);
        LayoutElement chle = clearHistoryGo.AddComponent<LayoutElement>();
        chle.preferredWidth = 170;
        chle.preferredHeight = 34;
        clearHistoryBtn.onClick.AddListener(OnClearCurrentHistory);
    }

    private void BuildSoulCollapsible(Transform parent, string title, bool isProfile)
    {
        GameObject box = new GameObject(isProfile ? "SoulProfileBox" : "SoulWorldBox", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        box.transform.SetParent(parent, false);
        box.GetComponent<Image>().color = new Color(0, 0, 0, 0.12f);
        VerticalLayoutGroup vlg = box.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(8, 8, 6, 8);

        // 标题 (可点击)
        GameObject headerGo = new GameObject("Header", typeof(RectTransform), typeof(Button));
        headerGo.transform.SetParent(box.transform, false);
        Button headerBtn = headerGo.GetComponent<Button>();
        LayoutElement hle = headerGo.AddComponent<LayoutElement>();
        hle.preferredHeight = 26;

        TextMeshProUGUI headerText = NewText("HeaderText", headerGo.transform, 17, TextAlignmentOptions.Left);
        headerText.text = $"{title} >>展开";
        headerText.color = new Color(0.70f, 0.72f, 0.75f, 1f);
        Anchor(headerText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // 内容 (初始隐藏)
        GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        contentGo.transform.SetParent(box.transform, false);
        contentGo.SetActive(false);

        TextMeshProUGUI bodyText = NewText("BodyText", contentGo.transform, 15, TextAlignmentOptions.TopLeft);
        bodyText.text = "加载中...";
        bodyText.color = new Color(0.75f, 0.78f, 0.82f, 1f);
        bodyText.enableWordWrapping = true;
        LayoutElement ble = bodyText.gameObject.AddComponent<LayoutElement>();
        ble.minHeight = 60;

        if (isProfile)
            _soulProfileText = bodyText;
        else
            _soulWorldText = bodyText;

        headerBtn.onClick.AddListener(delegate {
            if (isProfile)
            {
                _soulProfileExpanded = !_soulProfileExpanded;
                contentGo.SetActive(_soulProfileExpanded);
                headerText.text = title + (_soulProfileExpanded ? " >>收起" : " >>展开");
                if (_soulProfileExpanded) RefreshSoulProfile();
            }
            else
            {
                _soulWorldExpanded = !_soulWorldExpanded;
                contentGo.SetActive(_soulWorldExpanded);
                headerText.text = title + (_soulWorldExpanded ? " >>收起" : " >>展开");
                if (_soulWorldExpanded) RefreshSoulWorld();
            }
        });
    }

    private void BuildBottomBar(Transform parent)
    {
        GameObject bar = new GameObject("BottomBar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(parent, false);
        Anchor(bar.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 58));
        bar.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.10f, 0.95f);

        // 按钮靠右
        GameObject btnRow = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow.transform.SetParent(bar.transform, false);
        Anchor(btnRow.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 1), new Vector2(12, 8), new Vector2(-12, -8));
        HorizontalLayoutGroup hlg = btnRow.GetComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.spacing = 12f;
        hlg.childAlignment = TextAnchor.MiddleRight;

        GameObject cancelGo = NewButton("CancelBtn", btnRow.transform, "取消", 19, out _cancelBtn);
        LayoutElement cle = cancelGo.AddComponent<LayoutElement>();
        cle.preferredWidth = 110;
        cle.preferredHeight = 42;
        _cancelBtn.onClick.AddListener(PanelStack.Pop);

        GameObject saveGo = NewButton("SaveBtn", btnRow.transform, "保存并关闭", 19, out _saveBtn);
        LayoutElement sle = saveGo.AddComponent<LayoutElement>();
        sle.preferredWidth = 140;
        sle.preferredHeight = 42;
        _saveBtn.onClick.AddListener(OnSaveAndClose);
    }

    // ========== 辅助构建方法 ==========
    private GameObject CreateSection(Transform parent, string title)
    {
        GameObject section = new GameObject("Section_" + title.Replace(" ", "_"), typeof(RectTransform), typeof(VerticalLayoutGroup));
        section.transform.SetParent(parent, false);
        VerticalLayoutGroup vlg = section.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.spacing = 6f;

        // 标题
        TextMeshProUGUI titleText = NewText("SectionTitle", section.transform, 20, TextAlignmentOptions.Left);
        titleText.text = $"-- {title} --";
        titleText.color = new Color(0.75f, 0.78f, 0.82f, 1f);

        // 内容容器
        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(Image));
        content.transform.SetParent(section.transform, false);
        content.GetComponent<Image>().color = new Color(0, 0, 0, 0.16f);
        VerticalLayoutGroup clg = content.GetComponent<VerticalLayoutGroup>();
        clg.childForceExpandWidth = true;
        clg.childForceExpandHeight = false;
        clg.childControlWidth = true;
        clg.childControlHeight = true;
        clg.spacing = 8f;
        clg.padding = new RectOffset(12, 12, 12, 12);

        return section;
    }

    private TMP_InputField BuildLabeledInput(Transform parent, string labelText, string placeholder, bool password, out TextMeshProUGUI errorLabel)
    {
        // 标签
        TextMeshProUGUI label = NewText("Label_" + labelText, parent, 17, TextAlignmentOptions.Left);
        label.text = labelText;
        label.color = new Color(0.80f, 0.78f, 0.70f, 1f);

        // 输入框背景
        GameObject inputGo = new GameObject("Input_" + labelText, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputGo.transform.SetParent(parent, false);
        inputGo.GetComponent<Image>().color = new Color(1, 1, 1, 0.10f);
        LayoutElement ile = inputGo.AddComponent<LayoutElement>();
        ile.preferredHeight = 40;

        TMP_InputField input = inputGo.GetComponent<TMP_InputField>();
        TextMeshProUGUI textArea = NewText("Text", inputGo.transform, 18, TextAlignmentOptions.Left);
        Anchor(textArea.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 4), new Vector2(-10, -4));
        input.textViewport = textArea.rectTransform;
        input.textComponent = textArea;
        input.lineType = TMP_InputField.LineType.SingleLine;

        if (password)
        {
            input.contentType = TMP_InputField.ContentType.Password;
            input.asteriskChar = '*';
        }
        else
        {
            input.contentType = TMP_InputField.ContentType.Standard;
        }

        // Placeholder
        TextMeshProUGUI ph = NewText("Placeholder", inputGo.transform, 18, TextAlignmentOptions.Left);
        Anchor(ph.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 4), new Vector2(-10, -4));
        ph.text = placeholder;
        ph.color = new Color(0.5f, 0.52f, 0.55f, 0.7f);
        ph.raycastTarget = false;
        input.placeholder = ph;

        // 行内错误提示
        errorLabel = NewText("Error_" + labelText, parent, 14, TextAlignmentOptions.Left);
        errorLabel.color = UiTheme.ErrorText;
        errorLabel.text = "";

        return input;
    }

    // ========== 刷新所有 ==========
    private void RefreshAll()
    {
        // 从 FrontendServices 加载配置
        RefreshLlmInputs();
        RefreshPersonaList();
        RefreshHistoryList();
        RefreshRuntimePath();
        RefreshSoulProfile();
        RefreshSoulWorld();
        _hasUnsavedChanges = false;
        _testingConnection = false;
        _testState = TestState.NotTested;
        if (_testStatusText != null) _testStatusText.text = "";
        ValidateFieldsInline();
    }

    private void RefreshLlmInputs()
    {
        var config = FrontendServices.LoadedLlmConfig;
        if (_baseUrlInput != null) _baseUrlInput.text = config.BaseUrl;
        if (_apiKeyInput != null) _apiKeyInput.text = config.ApiKey;
        if (_modelInput != null) _modelInput.text = config.Model;
        if (_testStatusText != null) _testStatusText.text = "";
    }

    private void RefreshPersonaList()
    {
        if (_personaButton == null) return;

        _personaIdList = new List<string>();

        SkillManager? sm = FrontendServices.SkillManager;
        if (sm == null)
        {
            if (_personaBtnLabel != null) _personaBtnLabel.text = "(技能目录未就绪)";
            _personaButton.interactable = false;
            if (_personaPreviewText != null) _personaPreviewText.text = "进入游戏后可选择对话风格";
            return;
        }

        List<string> personaIds = sm.GetPersonaIds();
        if (personaIds.Count == 0)
        {
            if (_personaBtnLabel != null) _personaBtnLabel.text = "(无可用 Persona)";
            _personaButton.interactable = false;
            if (_personaPreviewText != null) _personaPreviewText.text = "请检查 Skills/registry.yaml 配置";
            return;
        }

        _personaButton.interactable = true;
        _personaIdList.AddRange(personaIds);

        // 选中当前保存的 persona，找不到时默认 sword-will
        string savedPersona = FrontendServices.SelectedPersonaId;
        int idx = _personaIdList.IndexOf(savedPersona);
        if (idx < 0) idx = _personaIdList.IndexOf("sword-will");
        if (idx < 0 && _personaIdList.Count > 0) idx = 0;
        _currentPersonaIdx = idx;
        UpdatePersonaButtonLabel();

        OnPersonaChanged(_currentPersonaIdx);
    }


    private void RefreshHistoryList()
    {
        if (_historyListContent == null || _historyItems == null) return;

        // 清空现有
        foreach (GameObject go in _historyItems)
            Destroy(go);
        _historyItems.Clear();

        // 异步加载历史列表
        StartCoroutine(LoadHistoryListCoroutine());
    }

    private IEnumerator LoadHistoryListCoroutine()
    {
        var task = FrontendServices.SessionManager.ListConversationsAsync();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted || task.IsCanceled || _historyListContent == null || _historyItems == null) yield break;

        List<ConversationMeta> metas = task.Result;
        if (metas.Count == 0)
        {
            // 显示空提示
            TextMeshProUGUI empty = NewText("EmptyHint", _historyListContent, 16, TextAlignmentOptions.Center);
            empty.text = "(暂无历史对话)";
            empty.color = new Color(0.45f, 0.48f, 0.50f, 1f);
            _historyItems.Add(empty.gameObject);
            yield break;
        }

        foreach (ConversationMeta meta in metas)
        {
            GameObject item = BuildHistoryItem(meta);
            _historyItems.Add(item);
        }
    }

    private GameObject BuildHistoryItem(ConversationMeta meta)
    {
        GameObject item = new GameObject($"HistoryItem_{meta.WorldId}", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        item.transform.SetParent(_historyListContent!, false);
        item.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.10f, 0.95f);
        LayoutElement ile = item.AddComponent<LayoutElement>();
        ile.preferredHeight = 38;

        HorizontalLayoutGroup hlg = item.GetComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.spacing = 10f;
        hlg.padding = new RectOffset(10, 10, 0, 0);

        // WorldId + 显示名
        string displayName = !string.IsNullOrEmpty(meta.Name) ? meta.Name
            : !string.IsNullOrEmpty(meta.AutoName) ? meta.AutoName
            : $"WorldId#{meta.WorldId}";

        string worldLabel = meta.WorldId == SessionManager.PregameWorldId ? "主界面" : $"WorldId#{meta.WorldId}";

        TextMeshProUGUI nameText = NewText("Name", item.transform, 16, TextAlignmentOptions.Left);
        nameText.text = $"{worldLabel} 「{displayName}」";
        nameText.color = new Color(0.85f, 0.83f, 0.78f, 1f);
        LayoutElement nle = nameText.gameObject.AddComponent<LayoutElement>();
        nle.flexibleWidth = 1f;

        // 条数
        TextMeshProUGUI countText = NewText("Count", item.transform, 15, TextAlignmentOptions.Right);
        countText.text = $"{meta.Count}条";
        countText.color = new Color(0.55f, 0.58f, 0.60f, 1f);
        LayoutElement cle = countText.gameObject.AddComponent<LayoutElement>();
        cle.preferredWidth = 50;

        // 重命名按钮
        GameObject renameGo = NewButton("RenameBtn", item.transform, "重命名", 15, out Button renameBtn);
        renameGo.GetComponent<Image>().color = new Color(0.25f, 0.30f, 0.32f, 0.95f);
        LayoutElement rle = renameGo.AddComponent<LayoutElement>();
        rle.preferredWidth = 70;
        rle.preferredHeight = 28;
        int capturedWorldId = meta.WorldId;
        string capturedName = displayName;
        renameBtn.onClick.AddListener(delegate { OnRenameConversation(capturedWorldId, capturedName); });

        return item;
    }

    private void RefreshRuntimePath()
    {
        if (_runtimePathText != null)
            _runtimePathText.text = $"存档目录：{Bootstrap.RuntimeRoot}";
    }

    private void RefreshSoulProfile()
    {
        if (_soulProfileText == null) return;

        StartCoroutine(LoadSoulProfileCoroutine());
    }

    private IEnumerator LoadSoulProfileCoroutine()
    {
        var store = FrontendServices.SoulStore;
        var task = store.LoadProfileAsync();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted || task.IsCanceled || _soulProfileText == null) yield break;

        SoulProfile profile = task.Result;
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("跨档全局 SoulProfile：");
        sb.AppendLine($"  玩法偏好：{(string.IsNullOrEmpty(profile.Playstyle) ? "(未设置)" : profile.Playstyle)}");
        sb.AppendLine($"  技术水平：{(string.IsNullOrEmpty(profile.TechnicalLevel) ? "(未设置)" : profile.TechnicalLevel)}");
        sb.AppendLine($"  提问习惯：{(string.IsNullOrEmpty(profile.QuestionHabits) ? "(未设置)" : profile.QuestionHabits)}");
        sb.AppendLine($"  保护字段：{(profile.ProtectedFields.Count == 0 ? "(无)" : string.Join(", ", profile.ProtectedFields))}");

        _soulProfileText.text = sb.ToString();
    }

    private void RefreshSoulWorld()
    {
        if (_soulWorldText == null) return;

        int worldId = WorldIdReader.CurrentWorldId();
        if (worldId == SessionManager.PregameWorldId)
        {
            _soulWorldText.text = "进入存档后可用";
            return;
        }

        StartCoroutine(LoadSoulWorldCoroutine(worldId));
    }

    private IEnumerator LoadSoulWorldCoroutine(int worldId)
    {
        var store = FrontendServices.SoulStore;
        var task = store.LoadWorldAsync(worldId);
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted || task.IsCanceled || _soulWorldText == null) yield break;

        SoulWorld world = task.Result;
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"当前档 SoulWorld (WorldId#{worldId})：");
        sb.AppendLine($"  门派：{(string.IsNullOrEmpty(world.Sect) ? "(未设置)" : world.Sect)}");
        sb.AppendLine($"  阶段：{(string.IsNullOrEmpty(world.Stage) ? "(未设置)" : world.Stage)}");
        sb.AppendLine($"  失败经历：{(string.IsNullOrEmpty(world.Failures) ? "(未设置)" : world.Failures)}");
        sb.AppendLine($"  历史摘要：{(string.IsNullOrEmpty(world.Summary) ? "(未设置)" : world.Summary)}");

        _soulWorldText.text = sb.ToString();
    }

    // ========== 事件处理 ==========
    private void OnConfigChanged(bool invalidateTest = true)
    {
        _hasUnsavedChanges = true;
        // 已通过测试 → 修改任一字段则失效
        if (invalidateTest && _testState == TestState.Passed)
        {
            _testState = TestState.Invalidated;
            if (_testStatusText != null)
            {
                _testStatusText.text = "[!] 配置已修改，请重新测试";
                _testStatusText.color = new Color(0.85f, 0.75f, 0.45f, 1f);
            }
        }
        // 清除旧的保存汇总提示
        if (_validationText != null) _validationText.text = "";
        // 实时校验所有字段
        ValidateFieldsInline();
    }

    private void OnPersonaCycle()
    {
        if (_personaIdList == null || _personaIdList.Count == 0) return;
        _currentPersonaIdx = (_currentPersonaIdx + 1) % _personaIdList.Count;
        UnityEngine.Debug.Log("[ConfigPanel] OnPersonaCycle: idx=" + _currentPersonaIdx + " id=" + _personaIdList[_currentPersonaIdx]);
        UpdatePersonaButtonLabel();
        OnPersonaChanged(_currentPersonaIdx);
        OnConfigChanged(invalidateTest: false);
    }

    private void UpdatePersonaButtonLabel()
    {
        if (_personaBtnLabel == null || _personaIdList == null) return;
        if (_currentPersonaIdx < 0 || _currentPersonaIdx >= _personaIdList.Count) return;
        SkillManager? sm = FrontendServices.SkillManager;
        string cn = sm != null ? sm.PersonaCnName(_personaIdList[_currentPersonaIdx]) : _personaIdList[_currentPersonaIdx];
        _personaBtnLabel.text = cn + "  >>";
    }

    private void OnPersonaChanged(int idx)
    {
        UnityEngine.Debug.Log("[ConfigPanel] OnPersonaChanged: idx=" + idx);
        if (_personaIdList == null || idx < 0 || idx >= _personaIdList.Count) return;

        string personaId = _personaIdList[idx];
        UnityEngine.Debug.Log("[ConfigPanel] OnPersonaChanged: personaId=" + personaId);
        SkillManager? sm = FrontendServices.SkillManager;

        if (_personaPreviewText != null)
        {
            if (sm == null)
            {
                _personaPreviewText.text = "(技能管理器未就绪)";
            }
            else
            {
                // 优先用 registry 的 description 字段(独立简介);缺省再从全文提取。
                string desc = sm.PersonaDescription(personaId);
                string preview = !string.IsNullOrWhiteSpace(desc)
                    ? desc
                    : ExtractPersonaSummary(sm.LoadPersona(personaId) ?? "");
                _personaPreviewText.text = preview;
            }
        }
    }

    /// <summary>从 persona markdown 提取简介:跳过标题行,取首段实际内容,限 120 字。</summary>
    private static string ExtractPersonaSummary(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var sb = new System.Text.StringBuilder();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) { if (sb.Length > 0) break; continue; }      // 空行:首段结束
            if (line.StartsWith('#')) continue;                                // 跳过标题
            if (line.StartsWith('-')) continue;                                // 跳过列表项
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(line);
            if (sb.Length >= 120) break;
        }
        string summary = sb.ToString().Trim();
        if (summary.Length > 120) summary = summary.Substring(0, 120) + "...";
        return string.IsNullOrEmpty(summary) ? "(无简介)" : summary;
    }

    private void OnTestConnection()
    {
        if (_testState == TestState.Testing) return;

        string baseUrl = (_baseUrlInput?.text ?? "").Trim();
        string apiKey = (_apiKeyInput?.text ?? "").Trim();
        string model = (_modelInput?.text ?? "").Trim();

        // 字段校验
        ValidateFieldsInline();
        var (bValid, _) = ValidateBaseUrl(baseUrl);
        var (aValid, _) = ValidateApiKey(apiKey);
        var (mValid, _) = ValidateModel(model);
        if (!bValid || !aValid || !mValid)
        {
            SetTestStatus("请先修正字段错误", isError: true);
            return;
        }

        _testState = TestState.Testing;
        _testingConnection = true;
        if (_testBtn != null) _testBtn.interactable = false;
        SetTestStatus("测试中...", isError: false);

        StartCoroutine(TestConnectionCoroutine(baseUrl, apiKey, model));
    }

    private IEnumerator TestConnectionCoroutine(string baseUrl, string apiKey, string model)
    {
        var sw = Stopwatch.StartNew();

        // 使用 OpenAiCompatibleClient 发送最小请求
        var client = new Core.Llm.OpenAiCompatibleClient();
        var config = new Core.Llm.LlmConfig
        {
            BaseUrl = baseUrl,
            ApiKey = apiKey,
            Model = model
        };
        var messages = new List<Core.Llm.LlmMessage>
        {
            new() { Role = "user", Content = "ping" }
        };

        Task<Core.Llm.LlmResponse>? task = null;
        Exception? fault = null;
        try
        {
            task = client.Chat(Core.Llm.AgentLLMRole.Testing, messages, config);
        }
        catch (Exception e)
        {
            fault = e;
        }

        if (task != null)
        {
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
                fault = task.Exception?.InnerException ?? task.Exception;
        }

        sw.Stop();

        if (fault != null)
        {
            _testState = TestState.NotTested;
            SetTestStatus($"X 连接失败 ({sw.ElapsedMilliseconds}ms): {fault.Message}", isError: true);
        }
        else if (task != null && task.Result != null)
        {
            _testState = TestState.Passed;
            SetTestStatus($"OK 连接正常 ({sw.ElapsedMilliseconds}ms)", isError: false);
        }
        else
        {
            _testState = TestState.NotTested;
            SetTestStatus("X 未知错误", isError: true);
        }

        _testingConnection = false;
        if (_testBtn != null) _testBtn.interactable = true;
    }

    private void SetTestStatus(string text, bool isError)
    {
        if (_testStatusText == null) return;
        _testStatusText.text = text;
        _testStatusText.color = isError ? UiTheme.ErrorText : new Color(0.70f, 0.84f, 0.66f, 1f);
    }

    private void OnOpenRuntimeDir()
    {
        try
        {
            string path = Bootstrap.RuntimeRoot;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Application.OpenURL(path);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[TaiwuEncyclopedia] Failed to open runtime dir: {e}");
        }
    }

    private void OnOpenLogsDir()
    {
        try
        {
            string path = Bootstrap.LogsDir;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Application.OpenURL(path);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[TaiwuEncyclopedia] Failed to open logs dir: {e}");
        }
    }

    private void OnResetSoulProfile()
    {
        StartCoroutine(ResetSoulProfileCoroutine());
    }

    private IEnumerator ResetSoulProfileCoroutine()
    {
        var store = FrontendServices.SoulStore;
        var task = store.SaveProfileAsync(new SoulProfile());
        yield return new WaitUntil(() => task.IsCompleted);

        if (_soulProfileExpanded) RefreshSoulProfile();
    }

    private void OnClearCurrentHistory()
    {
        int worldId = WorldIdReader.CurrentWorldId();
        StartCoroutine(ClearHistoryCoroutine(worldId));
    }

    private IEnumerator ClearHistoryCoroutine(int worldId)
    {
        var session = FrontendServices.SessionManager;
        var task = session.ClearAsync(worldId);
        yield return new WaitUntil(() => task.IsCompleted);

        // 同时清除 SoulWorld (仅档内)
        if (worldId != SessionManager.PregameWorldId)
        {
            var store = FrontendServices.SoulStore;
            var task2 = store.SaveWorldAsync(worldId, new SoulWorld());
            yield return new WaitUntil(() => task2.IsCompleted);
        }

        RefreshHistoryList();
        if (_soulWorldExpanded) RefreshSoulWorld();
    }

    private void OnRenameConversation(int worldId, string currentName)
    {
        ShowRenamePopup(worldId, currentName);
    }

    /// <summary>模态重命名弹窗:输入框预填当前名,确定→RenameConversationAsync,取消→关闭。</summary>
    private void ShowRenamePopup(int worldId, string currentName)
    {
        if (_root == null) return;
        Transform parent = _root.transform;

        // 遮罩层(半透明,拦截背景点击)
        GameObject overlay = new GameObject("RenameOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(parent, false);
        overlay.GetComponent<Image>().color = new Color(0, 0, 0, 0.55f);
        RectTransform ort = overlay.GetComponent<RectTransform>();
        ort.anchorMin = Vector2.zero;
        ort.anchorMax = Vector2.one;
        ort.sizeDelta = Vector2.zero;

        // 对话框
        GameObject dialog = new GameObject("RenameDialog", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        dialog.transform.SetParent(overlay.transform, false);
        dialog.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.20f, 1f);
        RectTransform drt = dialog.GetComponent<RectTransform>();
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
        drt.sizeDelta = new Vector2(440, 190);
        drt.pivot = new Vector2(0.5f, 0.5f);
        VerticalLayoutGroup dlg = dialog.GetComponent<VerticalLayoutGroup>();
        dlg.childForceExpandWidth = true;
        dlg.childForceExpandHeight = false;
        dlg.childControlWidth = true;
        dlg.childControlHeight = true;
        dlg.spacing = 12f;
        dlg.padding = new RectOffset(20, 20, 18, 18);

        TextMeshProUGUI title = NewText("Title", dialog.transform, 20, TextAlignmentOptions.Left);
        title.text = "重命名对话";
        title.color = new Color(0.92f, 0.90f, 0.82f, 1f);

        // 输入框(复用 BuildLabeledInput 的创建模式,但无标签/无错误行)
        GameObject inputGo = new GameObject("RenameInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputGo.transform.SetParent(dialog.transform, false);
        inputGo.GetComponent<Image>().color = new Color(1, 1, 1, 0.10f);
        LayoutElement ile = inputGo.AddComponent<LayoutElement>();
        ile.preferredHeight = 40;
        TMP_InputField input = inputGo.GetComponent<TMP_InputField>();
        TextMeshProUGUI textArea = NewText("Text", inputGo.transform, 18, TextAlignmentOptions.Left);
        Anchor(textArea.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 4), new Vector2(-10, -4));
        input.textViewport = textArea.rectTransform;
        input.textComponent = textArea;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.Standard;
        TextMeshProUGUI ph = NewText("Placeholder", inputGo.transform, 18, TextAlignmentOptions.Left);
        Anchor(ph.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 4), new Vector2(-10, -4));
        ph.text = "输入新名称";
        ph.color = new Color(0.5f, 0.52f, 0.55f, 0.7f);
        ph.raycastTarget = false;
        input.placeholder = ph;
        input.text = currentName ?? "";
        input.caretPosition = (currentName ?? "").Length;

        // 按钮行
        GameObject btnRow = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow.transform.SetParent(dialog.transform, false);
        HorizontalLayoutGroup bhlg = btnRow.GetComponent<HorizontalLayoutGroup>();
        bhlg.childForceExpandWidth = true;
        bhlg.childForceExpandHeight = false;
        bhlg.childControlWidth = true;
        bhlg.childControlHeight = true;
        bhlg.spacing = 12f;
        bhlg.childAlignment = TextAnchor.MiddleRight;

        GameObject cancelGo = NewButton("CancelBtn", btnRow.transform, "取消", 18, out Button cancelBtn);
        cancelGo.GetComponent<Image>().color = new Color(0.25f, 0.28f, 0.30f, 1f);
        LayoutElement cle = cancelGo.AddComponent<LayoutElement>();
        cle.preferredWidth = 90;
        cle.preferredHeight = 36;
        cancelBtn.onClick.AddListener(() => { input.DeactivateInputField(); Destroy(overlay); });

        GameObject okGo = NewButton("OkBtn", btnRow.transform, "确定", 18, out Button okBtn);
        okGo.GetComponent<Image>().color = new Color(0.20f, 0.40f, 0.30f, 1f);
        LayoutElement okle = okGo.AddComponent<LayoutElement>();
        okle.preferredWidth = 90;
        okle.preferredHeight = 36;
        okBtn.onClick.AddListener(() =>
        {
            string newName = (input.text ?? "").Trim();
            input.DeactivateInputField();
            Destroy(overlay);
            StartCoroutine(RenameCoroutine(worldId, newName));
        });

        // 输入框获焦
        input.ActivateInputField();
    }

    private IEnumerator RenameCoroutine(int worldId, string newName)
    {
        var task = FrontendServices.SessionManager.RenameConversationAsync(worldId, newName);
        yield return new WaitUntil(() => task.IsCompleted);
        RefreshHistoryList();
    }

    // ========== 保存与验证 ==========
    private static (bool Valid, string Error) ValidateBaseUrl(string v)
    {
        if (string.IsNullOrWhiteSpace(v)) return (false, "Base URL 不能为空");
        if (!v.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !v.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return (false, "Base URL 必须以 http:// 或 https:// 开头");
        return (true, "");
    }

    private static (bool Valid, string Error) ValidateApiKey(string v)
    {
        if (string.IsNullOrWhiteSpace(v)) return (false, "API Key 不能为空");
        return (true, "");
    }

    private static (bool Valid, string Error) ValidateModel(string v)
    {
        if (string.IsNullOrWhiteSpace(v)) return (false, "Model 不能为空");
        return (true, "");
    }

    private void ValidateFieldsInline()
    {
        string baseUrl = (_baseUrlInput?.text ?? "").Trim();
        string apiKey = (_apiKeyInput?.text ?? "").Trim();
        string model = (_modelInput?.text ?? "").Trim();

        var (bValid, bError) = ValidateBaseUrl(baseUrl);
        var (aValid, aError) = ValidateApiKey(apiKey);
        var (mValid, mError) = ValidateModel(model);

        if (_baseUrlError != null) _baseUrlError.text = bValid ? "" : bError;
        if (_apiKeyError != null) _apiKeyError.text = aValid ? "" : aError;
        if (_modelError != null) _modelError.text = mValid ? "" : mError;
    }


    private async void OnSaveAndClose()
    {
        string baseUrl = (_baseUrlInput?.text ?? "").Trim();
        string apiKey = (_apiKeyInput?.text ?? "").Trim();
        string model = (_modelInput?.text ?? "").Trim();
        string personaId = (_personaIdList != null && _currentPersonaIdx >= 0 && _currentPersonaIdx < _personaIdList.Count)
            ? _personaIdList[_currentPersonaIdx]
            : "";

        // 字段校验
        var errors = new List<string>();
        var (bValid, bError) = ValidateBaseUrl(baseUrl);
        if (!bValid) errors.Add(bError);
        var (aValid, aError) = ValidateApiKey(apiKey);
        if (!aValid) errors.Add(aError);
        var (mValid, mError) = ValidateModel(model);
        if (!mValid) errors.Add(mError);

        if (errors.Count > 0)
        {
            if (_validationText != null)
                _validationText.text = "请修正以下问题：" + string.Join("；", errors);
            ValidateFieldsInline();
            return;
        }

        // 测试 gate
        if (_testState != TestState.Passed)
        {
            if (_validationText != null) _validationText.text = "请先测试连接";
            return;
        }

        // persona 默认值兜底
        if (string.IsNullOrWhiteSpace(personaId) && _personaIdList != null && _personaIdList.Contains("sword-will"))
            personaId = "sword-will";

        // 保存配置
        await FrontendServices.SaveLlmConfig(baseUrl, apiKey, model, personaId);
        _hasUnsavedChanges = false;

        PanelStack.Pop();
    }

    // ========== UGUI 辅助方法 (仿照 jianghu) ==========
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
        go.GetComponent<Image>().color = UiTheme.Accent;
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
#pragma warning restore CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, IDE0010, IDE0022, IDE0048, RCS1123, CA1307, RCS1238, CA1852, CS0414, RCS1201, RCS1124, IDE0057, CA1031, RCS1001, IDE0370, RCS1085, IDE0028, RCS1161
