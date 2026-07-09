using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// 通用下拉组件：全屏透明遮罩 + 独立定位选项列表。
/// 解决 Unity TMP_Dropdown 在 ScrollRect 中裁切的问题。
/// 参考 WorldTalk WorldTalkSettingsUiHost Provider Dropdown。
/// </summary>
public sealed class OverlayDropdown : MonoBehaviour
{
    private GameObject? _overlay;
    private bool _isOpen;

    public bool IsOpen => _isOpen;

    public void Show(
        RectTransform canvasRoot,
        RectTransform trigger,
        IReadOnlyList<string> options,
        int currentIndex,
        Action<int> onSelect,
        TMP_FontAsset? font = null)
    {
        if (_isOpen) Hide();
        if (options.Count == 0) return;

        _isOpen = true;

        // 1. 全屏遮罩
        _overlay = new GameObject("DropdownOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        _overlay.transform.SetParent(canvasRoot, false);
        RectTransform ort = _overlay.GetComponent<RectTransform>();
        ort.anchorMin = Vector2.zero;
        ort.anchorMax = Vector2.one;
        ort.sizeDelta = Vector2.zero;
        ort.SetAsLastSibling();

        _overlay.GetComponent<Image>().color = new Color(0, 0, 0, 0.55f);
        _overlay.GetComponent<Button>().onClick.AddListener(Hide);

        // 2. 列表容器（定位在 trigger 下方）
        GameObject listGo = new GameObject("List", typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        listGo.transform.SetParent(ort, false);
        RectTransform lrt = listGo.GetComponent<RectTransform>();
        listGo.GetComponent<Image>().color = UiTheme.PanelBg;

        VerticalLayoutGroup vlg = listGo.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.spacing = 2f;
        vlg.padding = new RectOffset(4, 4, 4, 4);

        ContentSizeFitter csf = listGo.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 定位：trigger 下方，水平对齐 trigger 左边缘
        PositionList(lrt, trigger, canvasRoot, options.Count);

        // 3. 选项按钮
        PopulateItems(listGo, options, currentIndex, onSelect, font);
    }

    public void Hide()
    {
        if (_overlay != null)
        {
            Destroy(_overlay);
            _overlay = null;
        }
        _isOpen = false;
    }

    private void PositionList(RectTransform listRt, RectTransform trigger, RectTransform canvasRoot, int itemCount)
    {
        // Placeholder: 简单定位在 trigger 下方
        // Task 3 会做完整定位 + 向上展开逻辑
        listRt.anchorMin = listRt.anchorMax = listRt.pivot = new Vector2(0, 1);
        listRt.anchoredPosition = new Vector2(0, 0);
        listRt.sizeDelta = new Vector2(200, 0);  // placeholder width
    }

    private void PopulateItems(GameObject listGo, IReadOnlyList<string> options,
        int currentIndex, Action<int> onSelect, TMP_FontAsset? font)
    {
        for (int i = 0; i < options.Count; i++)
        {
            int idx = i;  // capture for closure
            bool isSelected = (idx == currentIndex);

            GameObject itemGo = new GameObject($"Item_{idx}", typeof(RectTransform),
                typeof(Image), typeof(Button), typeof(LayoutElement));
            itemGo.transform.SetParent(listGo.transform, false);

            // 背景色：选中高亮，未选中透明
            itemGo.GetComponent<Image>().color = isSelected ? UiTheme.Accent : Color.clear;

            LayoutElement le = itemGo.GetComponent<LayoutElement>();
            le.preferredHeight = 34f;

            Button btn = itemGo.GetComponent<Button>();
            btn.onClick.AddListener(() => { onSelect(idx); Hide(); });

            // 文字标签
            TextMeshProUGUI label = CreateItemLabel(itemGo.transform, options[i], isSelected, font);
        }
    }

    private static TextMeshProUGUI CreateItemLabel(Transform parent, string text,
        bool isSelected, TMP_FontAsset? font)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = 16;
        t.alignment = TextAlignmentOptions.Left;
        t.richText = false;
        t.raycastTarget = false;
        t.color = isSelected
            ? new Color(0.95f, 0.95f, 0.95f, 1f)
            : new Color(0.60f, 0.60f, 0.60f, 1f);
        t.text = text;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10, 2);
        rt.offsetMax = new Vector2(-10, -2);
        return t;
    }
}
