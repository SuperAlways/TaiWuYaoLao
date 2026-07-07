using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using TaiwuEncyclopedia.Core.Agent;
using TaiwuEncyclopedia.Frontend.UI;

namespace TaiwuEncyclopedia.UI;

public sealed class ThinkingPanel : MonoBehaviour
{
    public TMP_FontAsset? _font;
    public TextMeshProUGUI? _headerText;
    public RectTransform? _content;
    public bool _collapsed;
    public Coroutine? _dotsCoroutine;
    public GameObject? _dotsText;
    public GameObject? _hintText;
    public TextMeshProUGUI? _timerText;
    public float _startTime;

    private ActiveRequest? _activeRequest;

    public void SetFont(TMP_FontAsset? font) => _font = font;

    /// <summary>设置当前请求（用于 Update 读取计时器+token）。null 清除。</summary>
    public void SetActiveRequest(ActiveRequest? req) => _activeRequest = req;

    public void Build()
    {
        _startTime = Time.realtimeSinceStartup;

        GameObject headerRow = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        headerRow.transform.SetParent(transform, false);
        HorizontalLayoutGroup hlg = headerRow.GetComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.spacing = 6f;
        hlg.padding = new RectOffset(16, 16, 4, 4);

        TextMeshProUGUI icon = new GameObject("Icon", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        icon.transform.SetParent(headerRow.transform, false);
        if (_font != null) icon.font = _font;
        icon.fontSize = 18;
        icon.text = "▾";
        icon.color = new Color(0.65f, 0.68f, 0.70f, 1f);
        icon.alignment = TextAlignmentOptions.Left;
        icon.raycastTarget = false;
        LayoutElement ile = icon.gameObject.AddComponent<LayoutElement>();
        ile.preferredWidth = 20;

        _headerText = new GameObject("HeaderText", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        _headerText.transform.SetParent(headerRow.transform, false);
        if (_font != null) _headerText.font = _font;
        _headerText.fontSize = 17;
        _headerText.text = "▾ 思考中…";
        _headerText.color = new Color(0.65f, 0.68f, 0.70f, 1f);
        _headerText.alignment = TextAlignmentOptions.Left;
        _headerText.raycastTarget = false;

        _timerText = new GameObject("Timer", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        _timerText.transform.SetParent(headerRow.transform, false);
        if (_font != null) _timerText.font = _font;
        _timerText.fontSize = 15;
        _timerText.color = new Color(0.45f, 0.48f, 0.50f, 1f);
        _timerText.alignment = TextAlignmentOptions.Right;
        _timerText.raycastTarget = false;
        LayoutElement tle = _timerText.gameObject.AddComponent<LayoutElement>();
        tle.flexibleWidth = 1f;

        Button toggleBtn = headerRow.gameObject.AddComponent<Button>();
        toggleBtn.onClick.AddListener(Toggle);

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
                t.color = new Color(0.55f, 0.58f, 0.60f, 1f);
                t.raycastTarget = false;
                t.text = "⏳";
                if (_dotsCoroutine == null)
                    _dotsCoroutine = StartCoroutine(AnimateDots());

                // 耗时提示
                _hintText = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
                _hintText.transform.SetParent(_content, false);
                TextMeshProUGUI h = _hintText.GetComponent<TextMeshProUGUI>();
                if (_font != null) h.font = _font;
                h.fontSize = 14;
                h.color = new Color(0.45f, 0.48f, 0.50f, 1f);
                h.text = "大约需要 30–40 秒";
                h.raycastTarget = false;
            }
        }
        else
        {
            if (_dotsCoroutine != null) { StopCoroutine(_dotsCoroutine); _dotsCoroutine = null; }
            if (_dotsText != null) { Destroy(_dotsText); _dotsText = null; }
            if (_hintText != null) { Destroy(_hintText); _hintText = null; }
        }
    }

    /// <summary>面板 Hide/Show 后 Unity 会停掉 Coroutine，重连时调用此方法恢复计时动画。</summary>
    public void ResumeThinkingAnimation()
    {
        // 只在还在思考状态（_dotsText 存在）时恢复
        if (_dotsText == null) return;
        // _dotsCoroutine 可能是 Hide 时 Unity 隐式停掉的死引用，清理后重启
        if (_dotsCoroutine != null) StopCoroutine(_dotsCoroutine);
        _dotsCoroutine = StartCoroutine(AnimateDots());
    }

    private void Update()
    {
        if (_activeRequest == null || _timerText == null) return;
        var elapsed = UnityEngine.Time.realtimeSinceStartup - _activeRequest.StartTime;
        _timerText.text = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0:F1}s · {1}", elapsed, FormatTokens(
                _activeRequest.TotalPromptTokens,
                _activeRequest.TotalCompletionTokens,
                _activeRequest.TotalCacheHitTokens));
    }

    private static string FormatTokens(int prompt, int completion, int cache)
    {
        string F(int n) => n >= 1000 ? $"{n / 1000.0:F1}K" : n.ToString();
        return $"tokens {F(prompt)}↑ {F(completion)}↓ ({F(cache)} cache)";
    }

    private IEnumerator AnimateDots()
    {
        int n = 0;
        while (true)
        {
            n = (n + 1) % 4;
            if (_dotsText != null)
            {
                TextMeshProUGUI t = _dotsText.GetComponent<TextMeshProUGUI>();
                t.text = new string('·', n);
            }
            yield return new WaitForSeconds(0.5f);
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

    public void Toggle()
    {
        if (_collapsed) Expand();
        else Collapse();
    }

    /// <summary>更新思考区下方的提示文字（默认"大约需要 30-40 秒"，压缩时改为"正在压缩历史对话..."）。</summary>
    public void SetHint(string text)
    {
        if (_hintText != null)
        {
            var t = _hintText.GetComponent<TextMeshProUGUI>();
            if (t != null) t.text = text;
        }
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
        UiFactory.Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, 0));
    }

    public void AddToolResult(string name, int iteration)
    {
        // Find the matching ToolCall text and convert ⏳ → ✓
        if (_content == null) return;
        string prefix = string.Format(CultureInfo.InvariantCulture, "ToolCall_{0}_{1}", name, iteration);
        Transform child = _content.Find(prefix);
        if (child != null)
        {
            TextMeshProUGUI t = child.GetComponent<TextMeshProUGUI>();
            if (t != null) t.text = t.text.Replace("⏳", "✓");
        }
    }
}
