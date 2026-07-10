#pragma warning disable CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, IDE0010, IDE0022, IDE0048, RCS1123, CA1307, RCS1238, CA1852, CS0414, RCS1201, RCS1124, IDE0057, CA1031, RCS1001, IDE0370, RCS1085, IDE0028, RCS1161
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Frontend.UI;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// 大模型接口配置区：厂商预设下拉、Base URL / API Key / Model 输入、模型拉取、测试连接。
/// 由 ConfigPanel 组合使用，通过事件回调通知父面板。
/// </summary>
public sealed class LlmConfigSection : MonoBehaviour
{
    private TMP_InputField? _baseUrlInput, _apiKeyInput, _modelInput;
    private TextMeshProUGUI? _statusText, _providerBtnLabel, _modelBtnLabel;
    private TextMeshProUGUI? _baseUrlError, _apiKeyError, _modelError;
    private Button? _fetchBtn, _testBtn;
    private TMP_FontAsset? _font;
    private RectTransform? _canvasRt;

    private int _currentProviderIndex = -1;
    private int _fetchGeneration;
    private bool _testPassed;
    private List<string> _fetchedModels = [];
    private OverlayDropdown? _providerDropdown;
    private OverlayDropdown? _modelDropdown;

    public bool TestPassed => _testPassed;
    public string BaseUrl => _baseUrlInput?.text?.Trim() ?? "";
    public string ApiKey => _apiKeyInput?.text?.Trim() ?? "";
    public string Model => _modelInput?.text?.Trim() ?? "";

    public event Action? OnConfigChanged;
    public event Action<string, string>? OnFetchModels;
    public event Action<string, string>? OnTestConnection;

    public void Build(Transform content, TMP_FontAsset? font, RectTransform canvasRt)
    {
        _font = font;
        _canvasRt = canvasRt;
        var section = CreateSection(content, "大模型接口");
        var inner = section.transform.Find("Content")!;

        // 厂商预设按钮
        _providerBtnLabel = CreateProviderRow(inner);

        // Base URL
        _baseUrlInput = BuildInput(inner, "Base URL", "https://api.deepseek.com", false, out _baseUrlError);
        _baseUrlInput.onValueChanged.AddListener(_ => NotifyChanged());

        // API Key
        _apiKeyInput = BuildInput(inner, "API Key", "", true, out _apiKeyError);
        _apiKeyInput.onValueChanged.AddListener(_ => NotifyChanged());
        _apiKeyInput.asteriskChar = '*';

        // Model 按钮
        _modelBtnLabel = CreateModelRow(inner);

        // 手动输入 Model（作为下拉的备选）
        _modelInput = BuildInput(inner, "Model（手动）", "deepseek-chat", false, out _modelError);
        _modelInput.onValueChanged.AddListener(_ => NotifyChanged());

        // 按钮行
        var btnRow = CreateButtonRow(inner);
        _fetchBtn = CreateSmallButton(btnRow, "拉取模型");
        _fetchBtn.onClick.AddListener(FetchModels);
        _testBtn = CreateSmallButton(btnRow, "测试连接");
        _testBtn.onClick.AddListener(TestConnection);

        _statusText = UiFactory.CreateText(inner, "Status", "", 16,
            new Color(0.55f, 0.58f, 0.60f, 1f), TextAlignmentOptions.Left);
        LayoutElement sle = _statusText.gameObject.AddComponent<LayoutElement>();
        sle.flexibleWidth = 1f; sle.preferredHeight = 36;
    }

    public void Refresh()
    {
        var config = FrontendServices.LoadedLlmConfig;
        if (_baseUrlInput != null) _baseUrlInput.text = config.BaseUrl;
        if (_apiKeyInput != null) _apiKeyInput.text = config.ApiKey;
        if (_modelInput != null) _modelInput.text = config.Model;
        RefreshProviderButton();
        _testPassed = false;
        if (_statusText != null) _statusText.text = "";
    }

    private void NotifyChanged()
    {
        _testPassed = false;
        OnConfigChanged?.Invoke();
    }

    // ========== Provider 下拉 ==========

    private TextMeshProUGUI CreateProviderRow(Transform parent)
    {
        var row = new GameObject("ProviderRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandWidth = false;
        hlg.childControlWidth = true;
        hlg.spacing = 10f;

        UiFactory.CreateText(row.transform, "Label", "厂商：", 17,
            new Color(0.80f, 0.78f, 0.70f, 1f), TextAlignmentOptions.Left);

        var btn = UiFactory.CreateButton(row.transform, "ProviderBtn", "选择厂商...", 17,
            new Color(1, 1, 1, 0.12f), out var label);
        var labelTmp = label;
        LayoutElement le = btn.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f; le.preferredHeight = 36;

        btn.onClick.AddListener(() =>
        {
            if (_providerDropdown != null && _providerDropdown.IsOpen)
            {
                _providerDropdown.Hide();
                return;
            }
            if (_providerDropdown == null)
                _providerDropdown = btn.gameObject.AddComponent<OverlayDropdown>();
            var presets = ApiProviderPresets.All;
            var names = new List<string>();
            foreach (var p in presets) names.Add(p.DisplayName);
            _providerDropdown.Show(_canvasRt!, btn.GetComponent<RectTransform>(), names,
                _currentProviderIndex, idx =>
            {
                _currentProviderIndex = idx;
                labelTmp.text = names[idx];
                if (_baseUrlInput != null && presets[idx].BaseUrl.Length > 0)
                    _baseUrlInput.text = presets[idx].BaseUrl;
                NotifyChanged();
            }, _font);
        });

        ((RectTransform)row.transform).sizeDelta = new Vector2(0, 40);
        return labelTmp;
    }

    // ========== Model 下拉 ==========

    private TextMeshProUGUI CreateModelRow(Transform parent)
    {
        var row = new GameObject("ModelRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandWidth = false;
        hlg.childControlWidth = true;
        hlg.spacing = 10f;

        UiFactory.CreateText(row.transform, "Label", "Model：", 17,
            new Color(0.80f, 0.78f, 0.70f, 1f), TextAlignmentOptions.Left);

        var btn = UiFactory.CreateButton(row.transform, "ModelBtn", "选择模型...", 17,
            new Color(1, 1, 1, 0.12f), out var label);
        var labelTmp = label;
        LayoutElement le = btn.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f; le.preferredHeight = 36;

        btn.onClick.AddListener(() =>
        {
            if (_fetchedModels.Count == 0)
            {
                if (_statusText != null) _statusText.text = "请先拉取模型列表";
                return;
            }
            if (_modelDropdown != null && _modelDropdown.IsOpen)
            {
                _modelDropdown.Hide();
                return;
            }
            if (_modelDropdown == null)
                _modelDropdown = btn.gameObject.AddComponent<OverlayDropdown>();
            _modelDropdown.Show(_canvasRt!, btn.GetComponent<RectTransform>(), _fetchedModels,
                _fetchedModels.IndexOf(Model), idx =>
            {
                if (_modelInput != null) _modelInput.text = _fetchedModels[idx];
                labelTmp.text = _fetchedModels[idx];
                NotifyChanged();
            }, _font);
        });

        ((RectTransform)row.transform).sizeDelta = new Vector2(0, 40);
        return labelTmp;
    }

    // ========== 拉取模型 ==========

    private void FetchModels()
    {
        int generation = ++_fetchGeneration;
        if (_statusText != null) _statusText.text = "正在拉取模型列表...";
        OnFetchModels?.Invoke(BaseUrl, ApiKey);
        _ = FetchModelsAsync(generation);
    }

    private async System.Threading.Tasks.Task FetchModelsAsync(int generation)
    {
        try
        {
            var result = await FrontendServices.FetchModelsAsync(BaseUrl, ApiKey);
            if (generation != _fetchGeneration) return;
            if (result.Success)
            {
                _fetchedModels = result.Models;
                if (_modelBtnLabel != null && _fetchedModels.Count > 0)
                    _modelBtnLabel.text = _fetchedModels[0];
                if (_statusText != null)
                    _statusText.text = $"OK 已拉取 {_fetchedModels.Count} 个模型";
            }
            else
            {
                if (_statusText != null) _statusText.text = $"X {result.Error}";
            }
        }
        catch
        {
            if (generation == _fetchGeneration && _statusText != null)
                _statusText.text = "X 拉取模型列表失败";
        }
    }

    // ========== 测试连接 ==========

    private void TestConnection()
    {
        if (_statusText != null) _statusText.text = "测试中...";
        OnTestConnection?.Invoke(BaseUrl, ApiKey);
    }

    public void SetTestResult(bool passed, string message)
    {
        _testPassed = passed;
        if (_statusText != null)
        {
            _statusText.text = passed ? $"OK {message}" : $"X {message}";
            _statusText.color = passed
                ? new Color(0.70f, 0.84f, 0.66f, 1f)
                : UiTheme.ErrorText;
        }
    }

    // ========== Provider 按钮 refresh ==========

    private void RefreshProviderButton()
    {
        var match = ApiProviderPresets.Match(BaseUrl);
        if (match != null)
        {
            var presets = ApiProviderPresets.All;
            for (int i = 0; i < presets.Count; i++)
            {
                if (presets[i].Id == match.Id)
                {
                    _currentProviderIndex = i;
                    if (_providerBtnLabel != null) _providerBtnLabel.text = match.DisplayName;
                    return;
                }
            }
        }
        _currentProviderIndex = -1;
        if (_providerBtnLabel != null) _providerBtnLabel.text = "选择厂商...";
    }

    // ========== Helpers (migrated from ConfigPanel + UiFactory) ==========

    private static GameObject CreateSection(Transform parent, string title)
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
        TextMeshProUGUI titleText = UiFactory.CreateText(section.transform, "SectionTitle",
            $"-- {title} --", 20,
            new Color(0.75f, 0.78f, 0.82f, 1f), TextAlignmentOptions.Left);

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

    private static TMP_InputField BuildInput(Transform parent, string labelText, string placeholder, bool password, out TextMeshProUGUI errorLabel)
    {
        // 标签
        UiFactory.CreateText(parent, "Label_" + labelText, labelText, 17,
            new Color(0.80f, 0.78f, 0.70f, 1f), TextAlignmentOptions.Left);

        // 输入框背景
        GameObject inputGo = new GameObject("Input_" + labelText, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputGo.transform.SetParent(parent, false);
        inputGo.GetComponent<Image>().color = new Color(1, 1, 1, 0.10f);
        LayoutElement ile = inputGo.AddComponent<LayoutElement>();
        ile.preferredHeight = 40;

        TMP_InputField input = inputGo.GetComponent<TMP_InputField>();
        TextMeshProUGUI textArea = UiFactory.CreateText(inputGo.transform, "Text", "", 18,
            new Color(0.92f, 0.90f, 0.82f, 1f), TextAlignmentOptions.Left);
        UiFactory.Anchor(textArea.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 4), new Vector2(-10, -4));
        input.textViewport = textArea.rectTransform;
        input.textComponent = textArea;
        input.lineType = TMP_InputField.LineType.SingleLine;

        if (password)
        {
            input.contentType = TMP_InputField.ContentType.Password;
        }
        else
        {
            input.contentType = TMP_InputField.ContentType.Standard;
        }

        // Placeholder
        TextMeshProUGUI ph = UiFactory.CreateText(inputGo.transform, "Placeholder", placeholder, 18,
            new Color(0.5f, 0.52f, 0.55f, 0.7f), TextAlignmentOptions.Left);
        UiFactory.Anchor(ph.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 4), new Vector2(-10, -4));
        ph.raycastTarget = false;
        input.placeholder = ph;

        // 行内错误提示
        errorLabel = UiFactory.CreateText(parent, "Error_" + labelText, "", 14,
            UiTheme.ErrorText, TextAlignmentOptions.Left);

        return input;
    }

    private static Transform CreateButtonRow(Transform parent)
    {
        GameObject btnRow = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow.transform.SetParent(parent, false);
        HorizontalLayoutGroup hlg = btnRow.GetComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.spacing = 12f;
        hlg.padding = new RectOffset(0, 0, 6, 6);
        return btnRow.transform;
    }

    private static Button CreateSmallButton(Transform parent, string label)
    {
        var btn = UiFactory.CreateButton(parent, label + "Btn", label, 17,
            UiTheme.Accent, out _);
        LayoutElement le = btn.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 120;
        le.preferredHeight = 36;
        return btn;
    }
}
#pragma warning restore CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, IDE0010, IDE0022, IDE0048, RCS1123, CA1307, RCS1238, CA1852, CS0414, RCS1201, RCS1124, IDE0057, CA1031, RCS1001, IDE0370, RCS1085, IDE0028, RCS1161
