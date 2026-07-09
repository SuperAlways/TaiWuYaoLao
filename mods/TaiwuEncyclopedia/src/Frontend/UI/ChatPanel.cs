#pragma warning disable CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, IDE0010, IDE0022, IDE0048, RCS1123, CA1307, RCS1238, CA1852, CA1031
using System;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TaiwuEncyclopedia.Core.Agent;
using TaiwuEncyclopedia.Core.Http;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Session;
using TaiwuEncyclopedia.Threading;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// ChatPanel 编排层：持有 ChatPanelView（UI）+ 状态机，委托消息渲染给 MessageListView、输入给 ChatInputBar。
/// </summary>
public class ChatPanel : MonoBehaviour, IPanel
{
    private static ChatPanel? _instance;

    public static void Open(TMP_FontAsset? font = null)
    {
        if (_instance == null)
        {
            GameObject go = new GameObject("TaiwuEncyclopedia_ChatPanel", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ChatPanel>();
            _instance._font = font;
            _instance._view = go.AddComponent<ChatPanelView>();
            _instance._view.Build(go, font);
            _instance._view.InputBar!.OnSubmit += _instance.OnSend;
            _instance._view.InputBar!.OnInterrupt += _instance.Interrupt;
            _instance._view.Hide();
        }
        PanelStack.Push(_instance);
    }

    // ========== 组件引用 ==========
    private ChatPanelView? _view;
    private TMP_FontAsset? _font;

    // ========== 状态字段 ==========
    private int _currentWorldId;
    private bool _busy;
    private bool _interrupted;
    private Coroutine? _runCoroutine;
    private ActiveRequest? _activeRequest;

    // 当前 Agent 消息的部件
    private ThinkingPanel? _currentThinkingPanel;
    private TextMeshProUGUI? _currentAgentText;
    private MarkdownBinder? _currentAgentBinder;
    private ReferencePanel? _currentRefArea;
    private StringBuilder? _answerBuffer;
    private float _lastRebindTime;
    private string? _pendingAutoName;
    private List<Reference>? _collectedRefs;

    // ========== IPanel 实现 ==========
    public void Show()
    {
        Debug.Log("[ChatPanel] Show called, busy=" + _busy);
        if (_view == null) return;
        _view.Show();

        _currentWorldId = WorldIdReader.CurrentWorldId();
        UpdateTitle();

        var running = AgentRunnerHost.Instance.GetRequest(_currentWorldId);
        if (running != null)
        {
            _activeRequest = running;
            if (_currentAgentBinder != null && running.AnswerBuilder.Length > 0)
                _currentAgentBinder.Rebind(running.AnswerBuilder.ToString());
            _currentThinkingPanel?.SetActiveRequest(running);
            _currentThinkingPanel?.ResumeThinkingAnimation();
        }
        else
        {
            StartCoroutine(LoadHistoryCoroutine());
        }
    }

    public void Hide()
    {
        Debug.Log("[ChatPanel] Hide called, busy=" + _busy);
        _view?.Hide();
    }

    private void UpdateTitle()
    {
        var title = _view?.Title;
        if (title == null) return;
        if (_currentWorldId == Core.Session.SessionManager.PregameWorldId)
        {
            title.text = "百晓问答 · 主界面";
            return;
        }
        title.text = string.Format(CultureInfo.InvariantCulture, "百晓问答 · 当前世界 WorldId#{0}", _currentWorldId);
        StartCoroutine(UpdateTitleFromMetaCoroutine());
    }

    private IEnumerator UpdateTitleFromMetaCoroutine()
    {
        var task = FrontendServices.SessionManager.ListConversationsAsync();
        yield return new WaitUntil(() => task.IsCompleted);
        if (task.IsFaulted || task.IsCanceled || task.Result == null) yield break;
        var meta = task.Result.Find(m => m.WorldId == _currentWorldId);
        var title = _view?.Title;
        if (meta == null || title == null) yield break;
        string displayName = !string.IsNullOrEmpty(meta.Name) ? meta.Name
            : !string.IsNullOrEmpty(meta.AutoName) ? meta.AutoName
            : string.Format(CultureInfo.InvariantCulture, "WorldId#{0}", _currentWorldId);
        title.text = "百晓问答 · " + displayName;
    }

    // ========== 历史加载 ==========
    private IEnumerator LoadHistoryCoroutine()
    {
        var msgList = _view?.MsgList;
        if (msgList == null) yield break;
        msgList.ClearLog();
        var task = FrontendServices.SessionManager.LoadHistoryAsync(_currentWorldId, limit: 20, includeBoundaries: false);
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted || task.IsCanceled) { msgList.AddSysBubble("[!] 加载历史失败"); yield break; }

        List<MessageRecord> messages = task.Result;
        if (messages == null || messages.Count == 0) yield break;

        for (int i = 0; i < messages.Count; i++)
        {
            var msg = messages[i];
            if (msg.Role == "user")
            {
                msgList.AddPlayerBubble(msg.Content ?? "");
            }
            else if (msg.Role == "assistant")
            {
                if (!string.IsNullOrEmpty(msg.ThinkingContent))
                {
                    var area = msgList.AddThinkingPanel();
                    var lines = msg.ThinkingContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    for (int j = 0; j < lines.Length; j++)
                        area.AddToolCall("", lines[j].Trim(), j);
                    area.Collapse();
                }
                msgList.AddAgentMessage(msg.Content ?? "");
                if (msg.References is { Count: > 0 })
                    msgList.AddReferencePanel(msg.References);
            }
        }
        msgList.ScrollDown();
    }

    // ========== 发送/中断 ==========
    private void OnSend(string text)
    {
        if (_busy) return;
        if (string.IsNullOrEmpty(text)) return;
        _view?.InputBar?.ClearInput();

        if (!FrontendServices.IsAgentReady)
        {
            _view?.MsgList?.AddSysBubble("请先在设置中配置大模型");
            _view?.MsgList?.ScrollDown();
            StartCoroutine(_view?.InputBar?.RefocusCoroutine() ?? (IEnumerator)EmptyCoroutine());
            return;
        }

        _view?.MsgList?.AddPlayerBubble(text);
        _view?.MsgList?.ScrollDown();

        _pendingAutoName = null;
        _collectedRefs = null;
        if (_currentWorldId == Core.Session.SessionManager.PregameWorldId)
            _pendingAutoName = "主界面对话";
        else
            TaiwuNameReader.CurrentTaiwuName(delegate(string name) {
                _pendingAutoName = string.IsNullOrEmpty(name) ? name : name + "的存档";
            });

        var worldId = _currentWorldId;

        _busy = true;
        _interrupted = false;
        _lastRebindTime = Time.realtimeSinceStartup;
        _currentThinkingPanel = _view?.MsgList?.AddThinkingPanel();
        _answerBuffer = new StringBuilder();
        var fullAnswer = new StringBuilder();
        var agentPair = _view?.MsgList?.AddAgentText();
        if (agentPair.HasValue)
        {
            _currentAgentText = agentPair.Value.Text;
            _currentAgentBinder = agentPair.Value.Binder;
        }
        _currentRefArea = null;

        _activeRequest = AgentRunnerHost.Instance.StartRequest(worldId, text, evt =>
        {
            MainThreadDispatcher.Instance.Enqueue(() =>
            {
                if (_interrupted) return;
                HandleAgentEvent(evt, fullAnswer);
                if (evt is EndEvent)
                {
                    _busy = false;
                    _view?.InputBar?.SetBusy(false);
                    _currentThinkingPanel?.SetThinking(false);
                    _currentThinkingPanel?.Collapse();
                }
            });
        });

        _currentThinkingPanel?.SetActiveRequest(_activeRequest);
        _currentThinkingPanel?.SetThinking(true);
        _view?.MsgList?.ScrollDown();
        _view?.InputBar?.SetBusy(true);

        StartCoroutine(_view?.InputBar?.RefocusCoroutine() ?? (IEnumerator)EmptyCoroutine());
    }

    private void Interrupt()
    {
        if (!_busy) return;
        _interrupted = true;
        AgentRunnerHost.Instance.Cancel(_currentWorldId);
        if (_runCoroutine != null) StopCoroutine(_runCoroutine);
        _activeRequest = null;
        _busy = false;
        _view?.InputBar?.SetBusy(false);
        _view?.MsgList?.AddSysBubble("已中断");
        _view?.MsgList?.ScrollDown();
    }

    private static IEnumerator EmptyCoroutine() { yield break; }

    // ========== AgentRunner 流式事件处理 ==========
    private void HandleAgentEvent(AgentEvent evt, StringBuilder fullAnswer)
    {
        switch (evt)
        {
            case StartEvent _: break;
            case UsageEvent _:
                break;
            case StatusEvent se:
                _currentThinkingPanel?.SetHint(se.Message, se.Level);
                break;
            case ToolCallEvent tc:
                _currentThinkingPanel?.AddToolCall(tc.Name, tc.DisplayText, tc.Iteration);
                break;
            case ToolResultEvent tr:
                _currentThinkingPanel?.AddToolResult(tr.Name, tr.Iteration);
                break;
            case FinalChunkEvent fc:
                if (_answerBuffer?.Length == 0)
                    _currentThinkingPanel?.SetThinking(false);
                _answerBuffer?.Append(fc.Content);
                fullAnswer.Append(fc.Content);
                if (Time.realtimeSinceStartup - _lastRebindTime > 0.1f)
                {
                    _currentAgentBinder?.Rebind(_answerBuffer?.ToString() ?? "");
                    _lastRebindTime = Time.realtimeSinceStartup;
                    _view?.MsgList?.ScrollDown();
                }
                break;
            case ReferencesEvent re:
                _collectedRefs = re.References;
                _currentRefArea = _view?.MsgList?.AddReferencePanel(re.References);
                _view?.MsgList?.ScrollDown();
                break;
            case EndEvent _:
                if (_currentAgentBinder != null && _answerBuffer != null)
                    _currentAgentBinder.Rebind(_answerBuffer.ToString());
                _currentThinkingPanel?.SetThinking(false);
                _currentThinkingPanel?.Collapse();
                _view?.MsgList?.ScrollDown();
                break;
        }
    }
}
#pragma warning restore CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, IDE0010, IDE0022, IDE0048, RCS1123, CA1307, RCS1238, CA1852, CA1031