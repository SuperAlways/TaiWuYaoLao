#pragma warning disable CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, IDE0010, IDE0022, IDE0048, RCS1123, CA1307, RCS1238, CA1852, CS0414, RCS1201, RCS1124, IDE0057, CA1031, RCS1001, IDE0370, RCS1085, IDE0028, RCS1161
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Frontend.Networking;
using TaiwuEncyclopedia.Frontend.UI;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// 设置面板编排层：组合 LlmConfigSection / RagSection / PersonaSection / HistorySection / DataSection。
/// 实现 IPanel，通过 PanelStack.Push 显示在 ChatPanel 之上。F9 快捷键独立打开。
/// </summary>
public class ConfigPanel : MonoBehaviour, IPanel
{
    private static ConfigPanel? _instance;

    /// <summary>打开设置面板（静态入口）。</summary>
    public static void Open(TMP_FontAsset? font = null)
    {
        if (_instance == null)
        {
            GameObject go = new GameObject("TaiwuEncyclopedia_ConfigPanel",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ConfigPanel>();
            _instance._font = font;
            _instance.Build();
        }
        PanelStack.Push(_instance);
    }

    // ========== 字段 ==========
    private TMP_FontAsset? _font;
    private ConfigPanelView? _view;
    private LlmConfigSection? _llmSection;
    private PersonaSection? _personaSection;
    private HistorySection? _historySection;
    private DataSection? _dataSection;
    private RagSection? _ragSection;

    // ========== 构建 ==========
    private void Build()
    {
        _view = gameObject.AddComponent<ConfigPanelView>();
        _view.Build(gameObject, _font);

        var content = _view.ContentTransform!;

        // 区域1: 大模型接口
        _llmSection = gameObject.AddComponent<LlmConfigSection>();
        _llmSection.Build(content, _font, (RectTransform)gameObject.transform);
        _llmSection.OnConfigChanged += () => _view.ValidationText!.text = "";
        _llmSection.OnTestConnection += (url, key) => RunTestConnection(url, key);

        // 区域1.5: RAG 远程检索
        _ragSection = gameObject.AddComponent<RagSection>();
        _ragSection.Build(content, _font);

        // 区域2: 对话风格
        _personaSection = gameObject.AddComponent<PersonaSection>();
        _personaSection.Build(content, _font, (RectTransform)gameObject.transform);

        // 区域3: 历史对话
        _historySection = gameObject.AddComponent<HistorySection>();
        _historySection.Build(content, _font);

        // 区域4: 数据与日志
        _dataSection = gameObject.AddComponent<DataSection>();
        _dataSection.Build(content, _font);
        _dataSection.OnOpenLog += () => PlayerLogViewer.Open(_font);

        _view.SaveBtn!.onClick.AddListener(OnSaveAndClose);
    }

    // ========== IPanel 实现 ==========
    public void Show()
    {
        _view?.Show();
        _llmSection?.Refresh();
        _ragSection?.Refresh();
        _personaSection?.Refresh();
        _historySection?.Refresh();
        _dataSection?.Refresh();
    }

    public void Hide() => _view?.Hide();

    // ========== 测试连接（协程转发） ==========
    private void RunTestConnection(string baseUrl, string apiKey)
    {
        StartCoroutine(TestConnectionCoroutine(baseUrl, apiKey));
    }

    private IEnumerator TestConnectionCoroutine(string baseUrl, string apiKey)
    {
        bool done = false;
        bool success = false;
        string message = "";
        long latencyMs = 0;

        StartCoroutine(LlmTransportHost.Instance.TestConnection(
            baseUrl, apiKey, _llmSection!.Model,
            (s, msg, ms) => { done = true; success = s; message = msg; latencyMs = ms; }));

        yield return new WaitUntil(() => done);
        _llmSection.SetTestResult(success, message);
    }

    // ========== 保存并关闭 ==========
    private async void OnSaveAndClose()
    {
        string baseUrl = _llmSection!.BaseUrl;
        string apiKey = _llmSection.ApiKey;
        string model = _llmSection.Model;
        string personaId = _personaSection!.SelectedPersonaId;
        bool ragEnabled = _ragSection?.RagEnabled ?? true;

        // 验证非空
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey)
            || string.IsNullOrWhiteSpace(model))
        {
            if (_view?.ValidationText != null)
                _view.ValidationText.text = "请填写完整的大模型配置";
            return;
        }

        // 测试 gate
        if (!_llmSection.TestPassed)
        {
            if (_view?.ValidationText != null)
                _view.ValidationText.text = "请先测试连接";
            return;
        }

        await FrontendServices.SaveLlmConfig(baseUrl, apiKey, model, personaId,
            ragEnabled: ragEnabled);
        PanelStack.Pop();
    }
}
#pragma warning restore CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, IDE0010, IDE0022, IDE0048, RCS1123, CA1307, RCS1238, CA1852, CS0414, RCS1201, RCS1124, IDE0057, CA1031, RCS1001, IDE0370, RCS1085, IDE0028, RCS1161
