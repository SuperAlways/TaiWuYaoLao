#pragma warning disable CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, CA1031
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using TaiwuEncyclopedia.Frontend.UI;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// 输入栏组件：输入框 + 发送/中断按钮。
/// 对标 WorldTalk 的输入层，通过事件与 ChatPanel 通信。
/// </summary>
public sealed class ChatInputBar : MonoBehaviour
{
    private TMP_InputField? _input;
    private Button? _sendBtn;
    private Button? _interruptBtn;
    private TMP_FontAsset? _font;

    public event Action<string>? OnSubmit;
    public event Action? OnInterrupt;

    public void Build(Transform panelRoot, TMP_FontAsset? font)
    {
        _font = font;

        // 输入框
        GameObject inputGo = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputGo.transform.SetParent(panelRoot, false);
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
        ph.text = "在此提问...(回车发送)";
        ph.color = new Color(0.6f, 0.6f, 0.56f, 0.7f);
        ph.raycastTarget = false;
        _input.placeholder = ph;
        _input.onSubmit.AddListener(delegate { if (OnSubmit != null) OnSubmit(_input?.text?.Trim() ?? ""); });

        // 中断按钮
        GameObject interruptGo = NewButton("InterruptBtn", panelRoot, "中断", 20, out Button iBtn);
        Anchor(interruptGo.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-170, 12), new Vector2(-90, 58));
        _interruptBtn = iBtn;
        iBtn.onClick.AddListener(() => OnInterrupt?.Invoke());
        iBtn.interactable = false;

        // 发送按钮
        GameObject sendGo = NewButton("SendBtn", panelRoot, "发送", 20, out Button btn);
        Anchor(sendGo.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-88, 12), new Vector2(-12, 58));
        _sendBtn = btn;
        btn.onClick.AddListener(() => { if (OnSubmit != null) OnSubmit(_input?.text?.Trim() ?? ""); });
    }

    public void SetBusy(bool busy)
    {
        if (_sendBtn != null) _sendBtn.interactable = !busy;
        if (_interruptBtn != null) _interruptBtn.interactable = busy;
    }

    public void ClearInput()
    {
        if (_input != null) _input.text = "";
    }

    public IEnumerator RefocusCoroutine()
    {
        yield return null;
        if (_input != null && gameObject.activeInHierarchy)
        {
            _input.ActivateInputField();
            _input.caretPosition = _input.text?.Length ?? 0;
        }
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
#pragma warning restore CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, CA1031