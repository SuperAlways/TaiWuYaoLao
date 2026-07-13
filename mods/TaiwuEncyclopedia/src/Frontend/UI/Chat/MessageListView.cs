#pragma warning disable CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, CA1031
using System;
using System.Globalization;
using System.Collections.Generic;
using TaiwuEncyclopedia.Core.Rag;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using TaiwuEncyclopedia.Frontend.UI;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// 消息列表：管理 ScrollRect 内容区，提供气泡、思考面板、参考文献面板的创建方法。
/// 对标 WorldTalk 的消息渲染层，与 ChatPanel 解耦。
/// </summary>
public sealed class MessageListView : MonoBehaviour
{
    private RectTransform? _content;
    private ScrollRect? _scroll;
    private TMP_FontAsset? _font;

    private static readonly Color ColPlayerBubble = UiTheme.PlayerBubble;
    private static readonly Color ColPlayerText = UiTheme.PlayerText;
    private static readonly Color ColAgentText = UiTheme.AgentText;
    private static readonly Color ColSysText = UiTheme.SysText;
    private static readonly Color ColError = UiTheme.ErrorText;
    private static readonly Color ColAccent = UiTheme.Accent;

    public void Initialize(RectTransform content, ScrollRect scroll, TMP_FontAsset? font)
    {
        _content = content;
        _scroll = scroll;
        _font = font;
    }

    public RectTransform? Content => _content;

    // ===== 消息渲染 =====

    public void AddPlayerBubble(string text)
    {
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

    public (TextMeshProUGUI Text, MarkdownBinder Binder) AddAgentText(string? initialText = null)
    {
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

    public void AddAgentMessage(string text)
    {
        AddAgentText(text);
    }

    public void AddSysBubble(string text)
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

    public void AddErrorBubble(string text)
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

    public ThinkingPanel AddThinkingPanel()
    {
        GameObject go = new GameObject("ThinkingPanel", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
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

        ThinkingPanel area = go.AddComponent<ThinkingPanel>();
        area.SetFont(_font);
        area.Build();
        return area;
    }

    public ReferencePanel AddReferencePanel(List<Reference> references)
    {
        GameObject go = new GameObject("ReferencePanel", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
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

        ReferencePanel area = go.AddComponent<ReferencePanel>();
        area.SetFont(_font);
        area.Build(references);
        return area;
    }

    // ===== 列表操作 =====

    public void ClearLog()
    {
        if (_content == null) return;
        for (int i = _content.childCount - 1; i >= 0; i--)
            Destroy(_content.GetChild(i).gameObject);
    }

    public void ScrollDown()
    {
        Canvas.ForceUpdateCanvases();
        if (_scroll != null) _scroll.verticalNormalizedPosition = 0f;
    }

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
}
#pragma warning restore CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, CA1031