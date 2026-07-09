#pragma warning disable CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, IDE0010, IDE0022, IDE0048, RCS1123, CA1307, RCS1238, CA1852, CS0414, RCS1201, RCS1124, IDE0057, CA1031, RCS1001, IDE0370, RCS1085, IDE0028, RCS1161
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using TaiwuEncyclopedia.Core.Skills;
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
    private PersonaSection? _personaSection;

    // 区域3: 历史对话
    private HistorySection? _historySection;

    // 区域4: 数据与日志
    private DataSection? _dataSection;

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
        _personaSection = gameObject.AddComponent<PersonaSection>();
        _personaSection.Build(contentRt, _font, _root.GetComponent<RectTransform>());
        _personaSection.OnConfigChanged += delegate { OnConfigChanged(invalidateTest: false); };

        // 区域3: 历史对话
        _historySection = gameObject.AddComponent<HistorySection>();
        _historySection.Build(contentRt, _font);

        // 区域4: 数据与日志
        _dataSection = gameObject.AddComponent<DataSection>();
        _dataSection.Build(contentRt, _font);
        _dataSection.OnOpenLog += delegate { OpenLogsDir(); };

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
        if (_personaSection != null) _personaSection.Refresh();
        if (_historySection != null) _historySection.Refresh();
        if (_dataSection != null) _dataSection.Refresh();
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

    /// <summary>打开日志目录 (Task 8 将改为调用 PlayerLogViewer)。</summary>
    private void OpenLogsDir()
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
        string personaId = _personaSection?.SelectedPersonaId ?? "";

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
        if (string.IsNullOrWhiteSpace(personaId) && _personaSection != null)
        {
            var ids = FrontendServices.SkillManager?.GetPersonaIds();
            if (ids != null && ids.Contains("sword-will"))
                personaId = "sword-will";
        }

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
