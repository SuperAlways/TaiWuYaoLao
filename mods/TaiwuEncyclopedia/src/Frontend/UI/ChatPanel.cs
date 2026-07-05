#pragma warning disable CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, IDE0010, IDE0022, IDE0048, RCS1123, CA1307, RCS1238, CA1852, CA1031
using System;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TaiwuEncyclopedia.Core.Agent;
using TaiwuEncyclopedia.Core.Http;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Threading;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// 百晓问答主面板。MonoBehaviour + IPanel，UGUI 完全代码构建，
/// 流式事件通过 MainThreadDispatcher 回 UI，支持 pregame（WorldId=-1）。
/// </summary>
public class ChatPanel : MonoBehaviour, IPanel
{
    private static ChatPanel? _instance;

    /// <summary>
    /// 打开 ChatPanel（F8/入口按钮调用）。
    /// </summary>
    public static void Open(TMP_FontAsset? font = null)
    {
        if (_instance == null)
        {
            GameObject go = new GameObject("TaiwuEncyclopedia_ChatPanel", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ChatPanel>();
            _instance._font = font;
            _instance.Build();
        }
        PanelStack.Push(_instance);
    }

    // ========== UGUI 字段 ==========
    private GameObject? _root;
    private Canvas? _canvas;
    private TextMeshProUGUI? _title;
    private ScrollRect? _scroll;
    private RectTransform? _content;
    private TMP_InputField? _input;
    private Button? _sendBtn;
    private Button? _interruptBtn;
    private Button? _settingsBtn;
    private Button? _closeBtn;
    private TMP_FontAsset? _font;

    // ========== 状态字段 ==========
    private int _currentWorldId;
    private bool _busy;
    private bool _interrupted;
    private Coroutine? _runCoroutine;
    private ActiveRequest? _activeRequest;

    // 当前 Agent 消息的部件（思考区 + 主内容 + 参考文献 + 重试按钮）
    private ThinkingArea? _currentThinkingArea;
    private TextMeshProUGUI? _currentAgentText;
    private MarkdownBinder? _currentAgentBinder;
    private ReferenceArea? _currentRefArea;
    private StringBuilder? _answerBuffer;
    private float _lastRebindTime;
    private string? _pendingAutoName;
    private List<Reference>? _collectedRefs;

    // ========== UiTheme 配色 ==========
    private static readonly Color ColPanel = UiTheme.PanelBg;
    private static readonly Color ColPlayerBubble = UiTheme.PlayerBubble;
    private static readonly Color ColPlayerText = UiTheme.PlayerText;
    private static readonly Color ColAgentText = UiTheme.AgentText;
    private static readonly Color ColSysText = UiTheme.SysText;
    private static readonly Color ColError = UiTheme.ErrorText;
    private static readonly Color ColAccent = UiTheme.Accent;
    private static readonly Color ColTitleBar = UiTheme.TitleBarBg;

    // ========== IPanel 实现 ==========
    public void Show()
    {
        Debug.Log("[ChatPanel] Show called, busy=" + _busy);
        if (_root == null) return;
        _root.SetActive(true);

        // 更新标题（pregame-aware）
        _currentWorldId = WorldIdReader.CurrentWorldId();
        UpdateTitle();

        // 重连:检查 AgentRunnerHost 是否有正在跑的请求
        var running = AgentRunnerHost.Instance.GetRequest(_currentWorldId);
        if (running != null)
        {
            _activeRequest = running;
            // 重连到现有请求:渲染已有 AnswerBuilder 内容
            if (_currentAgentBinder != null && running.AnswerBuilder.Length > 0)
                _currentAgentBinder.Rebind(running.AnswerBuilder.ToString());
            // 注意:现有事件流通过 _activeRequest 的回调继续接收
        }
        else
        {
            StartCoroutine(LoadHistoryCoroutine());
        }
    }

    public void Hide()
    {
        Debug.Log("[ChatPanel] Hide called, busy=" + _busy);
        if (_root != null) _root.SetActive(false);
        // 不中断 Agent — Host 上继续跑
        // Interrupt();
    }

    private void UpdateTitle()
    {
        if (_title == null) return;
        if (_currentWorldId == Core.Session.SessionManager.PregameWorldId)
        {
            _title.text = "百晓问答 · 主界面";
            return;
        }
        // 先显示 WorldId 占位,再异步加载对话名(自定义名 > 自动名)替换。
        _title.text = string.Format(CultureInfo.InvariantCulture, "百晓问答 · 当前世界 WorldId#{0}", _currentWorldId);
        StartCoroutine(UpdateTitleFromMetaCoroutine());
    }

    private IEnumerator UpdateTitleFromMetaCoroutine()
    {
        var task = FrontendServices.SessionManager.ListConversationsAsync();
        yield return new WaitUntil(() => task.IsCompleted);
        if (task.IsFaulted || task.IsCanceled || task.Result == null) yield break;
        var meta = task.Result.Find(m => m.WorldId == _currentWorldId);
        if (meta == null || _title == null) yield break;
        string displayName = !string.IsNullOrEmpty(meta.Name) ? meta.Name
            : !string.IsNullOrEmpty(meta.AutoName) ? meta.AutoName
            : string.Format(CultureInfo.InvariantCulture, "WorldId#{0}", _currentWorldId);
        _title.text = "百晓问答 · " + displayName;
    }

    // ========== 构建（仿照 jianghu ChatWindow.Build） ==========
    private void Build()
    {
        try
        {
            _root = gameObject;
        _canvas = _root.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 30000;
        CanvasScaler sc = _root.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);

        // 主面板 (900x640)
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_root.transform, false);
        RectTransform prt = panel.GetComponent<RectTransform>();
        prt.sizeDelta = new Vector2(900, 640);
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
        _settingsBtn = sBtn;
        sBtn.onClick.AddListener(OnClickSettings);

        // 关闭按钮
        GameObject closeGo = NewButton("CloseBtn", panel.transform, "✕", 22, out Button cBtn);
        RectTransform crt = closeGo.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1, 1);
        crt.anchoredPosition = new Vector2(-10, -10);
        crt.sizeDelta = new Vector2(36, 36);
        _closeBtn = cBtn;
        cBtn.onClick.AddListener(PanelStack.Pop);

        // 滚动消息区
        GameObject scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
        scrollGo.transform.SetParent(panel.transform, false);
        Anchor(scrollGo.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 1), new Vector2(12, 70), new Vector2(-12, -70));
        scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.18f);
        _scroll = scrollGo.GetComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 24f;

        // 内容容器
        GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        _content = contentGo.GetComponent<RectTransform>();
        _content.SetParent(scrollGo.transform, false);
        _content.anchorMin = new Vector2(0, 1);
        _content.anchorMax = new Vector2(1, 1);
        _content.pivot = new Vector2(0.5f, 1);
        _content.anchoredPosition = Vector2.zero;
        _content.sizeDelta = new Vector2(0, 0);
        VerticalLayoutGroup vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.spacing = 8f;
        vlg.padding = new RectOffset(6, 6, 12, 12);
        ContentSizeFitter csf = contentGo.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _scroll.content = _content;

        // 输入框 + 按钮栏
        GameObject inputGo = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputGo.transform.SetParent(panel.transform, false);
        Anchor(inputGo.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 12), new Vector2(-180, 58));
        inputGo.GetComponent<Image>().color = new Color(1, 1, 1, 0.10f);
        _input = inputGo.GetComponent<TMP_InputField>();
        TextMeshProUGUI textArea = NewText("Text", inputGo.transform, 20, TextAlignmentOptions.TopLeft);
        Anchor(textArea.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-10, -6));
        _input.textViewport = textArea.rectTransform;
        _input.textComponent = textArea;
        _input.lineType = TMP_InputField.LineType.MultiLineSubmit;
        _input.customCaretColor = true;
        _input.caretColor = new Color(0.95f, 0.85f, 0.55f, 1f);
        _input.caretWidth = 3;
        _input.caretBlinkRate = 0f;
        _input.selectionColor = new Color(0.45f, 0.55f, 0.75f, 0.45f);
        TextMeshProUGUI ph = NewText("Placeholder", inputGo.transform, 20, TextAlignmentOptions.TopLeft);
        Anchor(ph.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-10, -6));
        ph.text = "在此提问…(回车发送)";
        ph.color = new Color(0.6f, 0.6f, 0.56f, 0.7f);
        ph.raycastTarget = false;
        _input.placeholder = ph;
        _input.onSubmit.AddListener(delegate { OnSend(); });

        // 中断按钮
        GameObject interruptGo = NewButton("InterruptBtn", panel.transform, "中断", 20, out Button iBtn);
        Anchor(interruptGo.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-170, 12), new Vector2(-90, 58));
        _interruptBtn = iBtn;
        iBtn.onClick.AddListener(Interrupt);
        iBtn.interactable = false;

        // 发送按钮
        GameObject sendGo = NewButton("SendBtn", panel.transform, "发送", 20, out Button btn);
        Anchor(sendGo.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-88, 12), new Vector2(-12, 58));
        _sendBtn = btn;
        btn.onClick.AddListener(OnSend);

            _root.SetActive(false);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[TaiwuEncyclopedia] ChatPanel build failed: {e}");
            // Leave panel in non-broken state if possible
            if (_root != null) _root.SetActive(false);
        }
    }

    // ========== 历史加载 ==========
    private IEnumerator LoadHistoryCoroutine()
    {
        int beforeClear = _content != null ? _content.childCount : -1;
        ClearLog();
        Debug.Log("[ChatPanel] LoadHistory: beforeClear=" + beforeClear + " worldId=" + _currentWorldId);
        var task = FrontendServices.SessionManager.LoadHistoryAsync(_currentWorldId, limit: 20);
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted || task.IsCanceled)
        {
            AddSysBubble("⚠ 加载历史失败");
            yield break;
        }

        List<LlmMessage> messages = task.Result;
        Debug.Log("[ChatPanel] LoadHistory: loadedCount=" + (messages?.Count ?? 0));
        if (messages == null || messages.Count == 0) yield break;

        // 从旧到新显示：user/assistant 交替
        for (int i = 0; i < messages.Count; i++)
        {
            LlmMessage msg = messages[i];
            if (msg.Role == "user")
            {
                AddPlayerBubble(msg.Content ?? "");
            }
            else if (msg.Role == "assistant")
            {
                AddAgentMessage(msg.Content ?? "");
            }
        }

        ScrollDown();
    }

    // ========== 发送/中断 ==========
    private void OnSend()
    {
        Debug.Log("[ChatPanel] OnSend entry: busy=" + _busy);
        if (_busy) return;
        string text = _input?.text?.Trim() ?? "";
        if (string.IsNullOrEmpty(text)) return;
        if (_input != null) _input.text = "";

        // 检查是否已配置 AgentRunner
        if (!FrontendServices.IsAgentReady)
        {
            AddSysBubble("请先在设置中配置大模型");
            ScrollDown();
            StartCoroutine(RefocusInputCoroutine());
            return;
        }

        // 添加玩家气泡
        AddPlayerBubble(text);
        ScrollDown();

        // 获取 autoName（callback-based）
        _pendingAutoName = null;
        _collectedRefs = null;
        if (_currentWorldId == Core.Session.SessionManager.PregameWorldId)
        {
            _pendingAutoName = "主界面对话";
        }
        else
        {
            TaiwuNameReader.CurrentTaiwuName(delegate(string name) {
                _pendingAutoName = string.IsNullOrEmpty(name) ? name : name + "的存档";
            });
        }

        // 即时持久化 user 提问
        var worldId = _currentWorldId;
        _ = FrontendServices.SessionManager.SaveUserQueryAsync(worldId, text);

        // 创建思考区 + 占位 Agent 消息
        _busy = true;
        _interrupted = false;
        _lastRebindTime = Time.realtimeSinceStartup;
        _currentThinkingArea = AddThinkingArea();
        _answerBuffer = new StringBuilder();
        var agentPair = AddAgentText();
        _currentAgentText = agentPair.Text;
        _currentAgentBinder = agentPair.Binder;
        _currentRefArea = null;

        // 显示思考动画
        _currentThinkingArea.SetThinking(true);
        ScrollDown();
        UpdateButtons();

        // 调度到 AgentRunnerHost
        var fullAnswer = new StringBuilder();
        _activeRequest = AgentRunnerHost.Instance.StartRequest(worldId, text, evt =>
        {
            MainThreadDispatcher.Instance.Enqueue(() =>
            {
                if (_interrupted) return;
                HandleAgentEvent(evt, fullAnswer);
                if (evt is EndEvent)
                {
                    // 最终保存
                    string finalAnswer = _answerBuffer?.ToString() ?? "";
                    string? autoName = _pendingAutoName;
                    List<Reference>? refs = _collectedRefs;
                    Debug.Log("[ChatPanel] SaveConversationAsync: worldId=" + worldId + " queryLen=" + text.Length + " answerLen=" + finalAnswer.Length);
                    _ = FrontendServices.SessionManager.SaveConversationAsync(
                        worldId, text, finalAnswer, refs, autoName);

                    _busy = false;
                    UpdateButtons();
                    _currentThinkingArea?.SetThinking(false);
                    _currentThinkingArea?.Collapse();
                }
            });
        });

        StartCoroutine(RefocusInputCoroutine());
    }

    private void Interrupt()
    {
        if (!_busy) return;
        _interrupted = true;
        // 取消 Host 上对应请求
        AgentRunnerHost.Instance.Cancel(_currentWorldId);
        if (_runCoroutine != null) StopCoroutine(_runCoroutine);
        _activeRequest = null;
        _busy = false;
        UpdateButtons();
        AddSysBubble("已中断");
        ScrollDown();
    }

    private void UpdateButtons()
    {
        if (_sendBtn != null) _sendBtn.interactable = !_busy;
        if (_interruptBtn != null) _interruptBtn.interactable = _busy;
    }

    private IEnumerator RefocusInputCoroutine()
    {
        yield return null;
        if (_input != null && _root != null && _root.activeSelf)
        {
            _input.ActivateInputField();
            _input.caretPosition = _input.text?.Length ?? 0;
        }
    }

    // ========== AgentRunner 流式协程(已迁移至 AgentRunnerHost) ==========

    private void HandleAgentEvent(AgentEvent evt, StringBuilder fullAnswer)
    {
        switch (evt)
        {
            case StartEvent _:
                // 已显示思考区
                break;

            case ToolCallEvent tc:
                _currentThinkingArea?.AddToolCall(tc.Name, tc.DisplayText, tc.Iteration);
                break;

            case ToolResultEvent tr:
                _currentThinkingArea?.AddToolResult(tr.Name, tr.Iteration);
                break;

            case FinalChunkEvent fc:
                // 首个内容到达 → 停止思考动画
                if (_answerBuffer?.Length == 0)
                {
                    _currentThinkingArea?.SetThinking(false);
                }
                _answerBuffer?.Append(fc.Content);
                fullAnswer.Append(fc.Content);
                Debug.Log("[ChatPanel] FinalChunk: chunkLen=" + (fc.Content?.Length ?? 0) + " bufferLen=" + (_answerBuffer?.Length ?? -1));
                // 节流重解析（100ms）
                if (Time.realtimeSinceStartup - _lastRebindTime > 0.1f)
                {
                    _currentAgentBinder?.Rebind(_answerBuffer?.ToString() ?? "");
                    _lastRebindTime = Time.realtimeSinceStartup;
                    ScrollDown();
                }
                break;

            case ReferencesEvent re:
                _collectedRefs = re.References;
                _currentRefArea = AddReferenceArea(re.References);
                ScrollDown();
                break;

            case EndEvent _:
                // 收尾：确保最后一次重解析
                if (_currentAgentBinder != null && _answerBuffer != null)
                {
                    _currentAgentBinder.Rebind(_answerBuffer.ToString());
                }
                _currentThinkingArea?.SetThinking(false);
                _currentThinkingArea?.Collapse();
                ScrollDown();
                break;
        }
    }

    // ========== UGUI 辅助方法（仿照 jianghu） ==========
    private void ClearLog()
    {
        if (_content == null) return;
        for (int i = _content.childCount - 1; i >= 0; i--)
            Destroy(_content.GetChild(i).gameObject);
    }

    private void ScrollDown()
    {
        Canvas.ForceUpdateCanvases();
        if (_scroll != null) _scroll.verticalNormalizedPosition = 0f;
    }

    private void AddPlayerBubble(string text)
    {
        Debug.Log("[ChatPanel] AddPlayerBubble: text='" + (text ?? "") + "'");
        if (_content == null) return;

        GameObject row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(_content, false);
        HorizontalLayoutGroup hl = row.GetComponent<HorizontalLayoutGroup>();
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.padding = new RectOffset(12, 12, 2, 3);
        hl.childAlignment = TextAnchor.MiddleRight;

        GameObject bubble = new GameObject("Bubble", typeof(RectTransform), typeof(Image),
            typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        bubble.transform.SetParent(row.transform, false);
        HorizontalLayoutGroup bl = bubble.GetComponent<HorizontalLayoutGroup>();
        bl.childForceExpandWidth = false;
        bl.childForceExpandHeight = false;
        bl.childControlWidth = true;
        bl.childControlHeight = true;
        bl.padding = new RectOffset(14, 14, 10, 10);
        ContentSizeFitter bfit = bubble.GetComponent<ContentSizeFitter>();
        bfit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        bfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        bubble.GetComponent<Image>().color = ColPlayerBubble;

        TextMeshProUGUI ptxt = NewText("T", bubble.transform, 20, TextAlignmentOptions.TopLeft);
        ptxt.enableWordWrapping = true;
        ptxt.raycastTarget = false;
        ptxt.extraPadding = true;
        ptxt.text = text ?? "";
        ptxt.color = ColPlayerText;
        LayoutElement ple = ptxt.gameObject.AddComponent<LayoutElement>();
        ple.preferredWidth = Mathf.Clamp(ptxt.GetPreferredValues(ptxt.text).x + 6f, 16f, 520f);
        ple.flexibleWidth = 0;
    }

    private (TextMeshProUGUI Text, MarkdownBinder Binder) AddAgentText(string? initialText = null)
    {
        Debug.Log("[ChatPanel] AddAgentText (new agent bubble)");
        TextMeshProUGUI t = NewText("AgentText", _content, 20, TextAlignmentOptions.TopLeft);
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        t.extraPadding = true;
        t.margin = new Vector4(16f, 2f, 16f, 2f);
        t.text = initialText ?? "";
        t.color = ColAgentText;

        MarkdownBinder binder = t.gameObject.AddComponent<MarkdownBinder>();
        MarkdownBinder.Bind(t, initialText ?? "");
        return (t, binder);
    }

    private void AddAgentMessage(string text)
    {
        AddAgentText(text);
    }

    private void AddSysBubble(string text)
    {
        if (_content == null) return;
        TextMeshProUGUI t = NewText("SysText", _content, 17, TextAlignmentOptions.Center);
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        t.extraPadding = true;
        t.margin = new Vector4(14f, 4f, 14f, 4f);
        t.text = text ?? "";
        t.color = ColSysText;
    }

    private void AddErrorBubble(string text)
    {
        if (_content == null) return;
        TextMeshProUGUI t = NewText("ErrorText", _content, 17, TextAlignmentOptions.Center);
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        t.extraPadding = true;
        t.margin = new Vector4(14f, 4f, 14f, 4f);
        t.text = text ?? "";
        t.color = ColError;
    }

    private ThinkingArea AddThinkingArea()
    {
        GameObject go = new GameObject("ThinkingArea", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(_content, false);
        VerticalLayoutGroup vlg = go.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.padding = new RectOffset(16, 16, 6, 6);
        vlg.spacing = 4f;
        ContentSizeFitter csf = go.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ThinkingArea area = go.AddComponent<ThinkingArea>();
        area.SetFont(_font);
        area.Build();
        return area;
    }

    private ReferenceArea AddReferenceArea(List<Reference> references)
    {
        GameObject go = new GameObject("ReferenceArea", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(_content, false);
        VerticalLayoutGroup vlg = go.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.padding = new RectOffset(16, 16, 6, 6);
        vlg.spacing = 6f;
        ContentSizeFitter csf = go.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ReferenceArea area = go.AddComponent<ReferenceArea>();
        area.SetFont(_font);
        area.Build(references);
        return area;
    }

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

    private void OnClickSettings()
    {
        ConfigPanel.Open(_font);
    }

    // ========== 子组件：ThinkingArea ==========
    private class ThinkingArea : MonoBehaviour
    {
        private TMP_FontAsset? _font;
        private TextMeshProUGUI? _headerText;
        private RectTransform? _content;
        private bool _collapsed;
        private Coroutine? _dotsCoroutine;
        private GameObject? _dotsText;

        public void SetFont(TMP_FontAsset? font) => _font = font;

        public void Build()
        {
            // 标题栏（可点击折叠）
            GameObject header = new GameObject("Header", typeof(RectTransform), typeof(Image), typeof(Button));
            header.transform.SetParent(transform, false);
            header.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.20f, 0.95f);
            RectTransform hrt = header.GetComponent<RectTransform>();
            hrt.sizeDelta = new Vector2(0, 32);
            Button hBtn = header.GetComponent<Button>();
            hBtn.onClick.AddListener(ToggleCollapse);

            _headerText = NewText("HeaderText", header.transform, 18, TextAlignmentOptions.Left);
            _headerText.text = "▾ 思考中…";
            _headerText.color = new Color(0.7f, 0.72f, 0.75f, 1f);
            Anchor(_headerText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-12, 0));

            // 内容区（工具步骤）
            GameObject contentGo = new GameObject("Steps", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(transform, false);
            _content = contentGo.GetComponent<RectTransform>();
            VerticalLayoutGroup vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(8, 8, 4, 4);
            vlg.spacing = 2f;
            ContentSizeFitter csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public void SetThinking(bool thinking)
        {
            if (thinking)
            {
                if (_dotsText == null && _content != null)
                {
                    _dotsText = new GameObject("Dots", typeof(RectTransform), typeof(TextMeshProUGUI));
                    _dotsText.transform.SetParent(_content, false);
                    TextMeshProUGUI t = _dotsText.GetComponent<TextMeshProUGUI>();
                    if (_font != null) t.font = _font;
                    t.fontSize = 18;
                    t.alignment = TextAlignmentOptions.Left;
                    t.color = new Color(0.65f, 0.68f, 0.7f, 1f);
                    t.text = "·";
                    Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, 0));
                }
                if (_dotsCoroutine == null)
                    _dotsCoroutine = StartCoroutine(DotsCoroutine());
            }
            else
            {
                if (_dotsCoroutine != null)
                {
                    StopCoroutine(_dotsCoroutine);
                    _dotsCoroutine = null;
                }
                if (_dotsText != null)
                {
                    Destroy(_dotsText);
                    _dotsText = null;
                }
            }
        }

        public void Collapse()
        {
            _collapsed = true;
            if (_content != null) _content.gameObject.SetActive(false);
            if (_headerText != null) _headerText.text = "▸ 思考过程";
        }

        public void Expand()
        {
            _collapsed = false;
            if (_content != null) _content.gameObject.SetActive(true);
            if (_headerText != null) _headerText.text = "▾ 思考过程";
        }

        private void ToggleCollapse()
        {
            if (_collapsed) Expand();
            else Collapse();
        }

        public void AddToolCall(string name, string displayText, int iteration)
        {
            if (_content == null) return;
            GameObject go = new GameObject(string.Format(CultureInfo.InvariantCulture, "ToolCall_{0}_{1}", name, iteration), typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(_content, false);
            go.name = string.Format(CultureInfo.InvariantCulture, "ToolCall_{0}_{1}", name, iteration);
            TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.fontSize = 16;
            t.alignment = TextAlignmentOptions.Left;
            t.color = new Color(0.75f, 0.78f, 0.82f, 1f);
            t.text = string.Format(CultureInfo.InvariantCulture, "⏳ {0}", displayText);
            Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, 0));
        }

        public void AddToolResult(string name, int iteration)
        {
            if (_content == null) return;
            // 查找对应的 ToolCall 并替换图标
            for (int i = 0; i < _content.childCount; i++)
            {
                Transform child = _content.GetChild(i);
                if (child.name == string.Format(CultureInfo.InvariantCulture, "ToolCall_{0}_{1}", name, iteration))
                {
                    TextMeshProUGUI? t = child.GetComponent<TextMeshProUGUI>();
                    if (t != null)
                        t.text = t.text.Replace("⏳", "✓");
                    break;
                }
            }
        }

        private IEnumerator DotsCoroutine()
        {
            int n = 1;
            while (true)
            {
                if (_dotsText != null)
                {
                    TextMeshProUGUI? t = _dotsText.GetComponent<TextMeshProUGUI>();
                    if (t != null) t.text = new string('·', n);
                }
                n = n % 3 + 1;
                yield return new WaitForSecondsRealtime(0.35f);
            }
        }

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

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }
    }

    // ========== 子组件：ReferenceArea ==========
    private class ReferenceArea : MonoBehaviour
    {
        private TMP_FontAsset? _font;

        public void SetFont(TMP_FontAsset? font) => _font = font;

        public void Build(List<Reference> references)
        {
            if (references == null || references.Count == 0) return;

            // 标题
            TextMeshProUGUI title = NewText("RefTitle", transform, 18, TextAlignmentOptions.Left);
            title.text = "——— 参考文献 ———";
            title.color = new Color(0.55f, 0.58f, 0.6f, 1f);
            Anchor(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(0, 2), new Vector2(0, -2));

            // 逐个文献卡片
            foreach (Reference r in references)
            {
                AddReferenceCard(r);
            }
        }

        private void AddReferenceCard(Reference r)
        {
            GameObject go = new GameObject("RefCard", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(transform, false);
            go.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.16f, 0.9f);
            HorizontalLayoutGroup hl = go.GetComponent<HorizontalLayoutGroup>();
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.padding = new RectOffset(10, 10, 8, 8);
            hl.spacing = 8f;

            // source_type 徽章
            var badgeInfo = GetSourceTypeBadge(r.SourceType);
            TextMeshProUGUI badge = NewText("Badge", go.transform, 16, TextAlignmentOptions.Center);
            badge.text = badgeInfo.Text;
            badge.color = badgeInfo.Color;
            LayoutElement ble = badge.gameObject.AddComponent<LayoutElement>();
            ble.preferredWidth = badgeInfo.Width;
            ble.preferredHeight = 24;

            // 文件名/链接
            string nameText = string.IsNullOrEmpty(r.FilePath)
                ? (string.IsNullOrEmpty(r.SourceUrl) ? r.FullDocId : r.SourceUrl)
                : System.IO.Path.GetFileNameWithoutExtension(r.FilePath);

            if (!string.IsNullOrEmpty(r.SourceUrl))
            {
                // 可点击链接
                GameObject linkGo = NewButton("LinkBtn", go.transform, nameText, 16, out Button btn);
                linkGo.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                btn.onClick.AddListener(delegate() { Application.OpenURL(r.SourceUrl); });
                TextMeshProUGUI? linkText = linkGo.GetComponentInChildren<TextMeshProUGUI>();
                if (linkText != null)
                {
                    linkText.color = UiTheme.Link;
                    linkText.fontStyle = FontStyles.Underline;
                    linkText.alignment = TextAlignmentOptions.Left;
                }
                LayoutElement lle = linkGo.AddComponent<LayoutElement>();
                lle.flexibleWidth = 1f;
                lle.preferredHeight = 24;

                // 外链图标
                TextMeshProUGUI arrow = NewText("Arrow", go.transform, 16, TextAlignmentOptions.Center);
                arrow.text = "↗";
                arrow.color = UiTheme.Link;
                LayoutElement ale = arrow.gameObject.AddComponent<LayoutElement>();
                ale.preferredWidth = 20;
            }
            else
            {
                // 纯文本
                TextMeshProUGUI txt = NewText("Name", go.transform, 16, TextAlignmentOptions.Left);
                txt.text = nameText;
                txt.color = new Color(0.75f, 0.78f, 0.82f, 1f);
                LayoutElement tle = txt.gameObject.AddComponent<LayoutElement>();
                tle.flexibleWidth = 1f;
                tle.preferredHeight = 24;
            }

            // game_version（可选，右对齐）
            if (!string.IsNullOrEmpty(r.GameVersion))
            {
                TextMeshProUGUI ver = NewText("Ver", go.transform, 14, TextAlignmentOptions.Right);
                ver.text = r.GameVersion;
                ver.color = new Color(0.5f, 0.52f, 0.55f, 1f);
                LayoutElement vle = ver.gameObject.AddComponent<LayoutElement>();
                vle.preferredWidth = 80;
                vle.preferredHeight = 24;
            }
        }

        private static (string Text, Color Color, float Width) GetSourceTypeBadge(string? sourceType)
        {
            return (sourceType?.ToUpperInvariant()) switch
            {
                "WIKI" => (" WIKI ", new Color(0.25f, 0.5f, 0.85f, 1f), 58f),
                "BBS" or "FORUM" => (" 论坛 ", new Color(0.85f, 0.55f, 0.25f, 1f), 52f),
                "VIDEO" => (" 视频 ", new Color(0.85f, 0.3f, 0.3f, 1f), 52f),
                "OFFICIAL" => (" 官方 ", new Color(0.3f, 0.75f, 0.4f, 1f), 52f),
                _ => (" 资料 ", new Color(0.5f, 0.5f, 0.52f, 1f), 52f),
            };
        }

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
            go.GetComponent<Image>().color = new Color(0.2f, 0.25f, 0.3f, 0.95f);
            btn = go.GetComponent<Button>();
            TextMeshProUGUI t = NewText("L", go.transform, size, TextAlignmentOptions.Left);
            Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, 0));
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
}
#pragma warning restore CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, IDE0010, IDE0022, IDE0048, RCS1123, CA1307, RCS1238, CA1852
