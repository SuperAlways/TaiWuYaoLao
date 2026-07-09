using System;
using System.Collections.Generic;
using TMPro;
using TaiwuEncyclopedia.Frontend.UI;
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

    private static void PositionList(RectTransform listRt, RectTransform trigger,
        RectTransform canvasRoot, int itemCount)
    {
        // trigger 在 Canvas 空间中的世界坐标
        Vector3[] corners = new Vector3[4];
        trigger.GetWorldCorners(corners);
        // corners: [0]=左上, [1]=左下, [2]=右下, [3]=右上
        Vector2 triggerBottomLeft = corners[1];   // trigger 左下角
        Vector2 triggerBottomRight = corners[2];  // trigger 右下角

        // 转为 canvasRoot 的局部坐标
        Vector2 localBottomLeft;
        Vector2 localBottomRight;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRoot, triggerBottomLeft, null, out localBottomLeft);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRoot, triggerBottomRight, null, out localBottomRight);

        float triggerWidth = localBottomRight.x - localBottomLeft.x;
        float triggerX = localBottomLeft.x;

        // 估算列表高度：每项 34px + spacing(2)* (N-1) + padding(8)
        float estimatedHeight = itemCount * 34f + (itemCount - 1) * 2f + 8f;
        float maxListHeight = Mathf.Min(estimatedHeight + 8f, 300f + 8f);  // 含 padding

        // canvasRoot 底部 Y（最底部）
        float canvasBottom = -canvasRoot.rect.height / 2f;
        float triggerBottomY = localBottomLeft.y;

        // 默认向下展开：列表 pivot 为左上角 (0,1)
        listRt.pivot = new Vector2(0, 1);
        listRt.anchorMin = listRt.anchorMax = new Vector2(0, 1);
        listRt.sizeDelta = new Vector2(triggerWidth, 0);  // 高度由 ContentSizeFitter 控制

        if (triggerBottomY - maxListHeight < canvasBottom)
        {
            // 底部空间不足，改为向上展开
            // pivot 设为左下角 (0,0)，锚定 trigger 顶部
            listRt.pivot = new Vector2(0, 0);
            Vector2 localTopLeft;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRoot, corners[0], null, out localTopLeft);
            float triggerTopY = localTopLeft.y;
            listRt.anchoredPosition = new Vector2(triggerX, triggerTopY);
        }
        else
        {
            // 向下展开
            listRt.anchoredPosition = new Vector2(triggerX, triggerBottomY);
        }
    }

    private const int ScrollThreshold = 8;

    private void PopulateItems(GameObject listGo, IReadOnlyList<string> options,
        int currentIndex, Action<int> onSelect, TMP_FontAsset? font)
    {
        Transform itemsParent;
        if (options.Count > ScrollThreshold)
        {
            // 长列表：在 listGo 内嵌套 ScrollRect
            RectTransform scrollContent = UiFactory.CreateScroll(listGo.transform, "ScrollContent", spacing: 2f);
            scrollContent.anchorMin = scrollContent.anchorMax = new Vector2(0, 1);
            scrollContent.pivot = new Vector2(0.5f, 1);
            scrollContent.anchoredPosition = Vector2.zero;
            scrollContent.sizeDelta = new Vector2(0, 0);

            // 限制 ScrollRect Viewport 高度为 300px
            ScrollRect sr = scrollContent.parent.parent.GetComponent<ScrollRect>();
            RectTransform viewportRt = scrollContent.parent.GetComponent<RectTransform>();
            viewportRt.sizeDelta = new Vector2(0, 300);

            // Scrollbar
            GameObject scrollbarGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            scrollbarGo.transform.SetParent(sr.transform, false);
            RectTransform sbrt = scrollbarGo.GetComponent<RectTransform>();
            sbrt.anchorMin = new Vector2(1, 0);
            sbrt.anchorMax = new Vector2(1, 1);
            sbrt.pivot = new Vector2(1, 1);
            sbrt.anchoredPosition = Vector2.zero;
            sbrt.sizeDelta = new Vector2(10, 0);
            Scrollbar sb = scrollbarGo.GetComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.targetGraphic = scrollbarGo.GetComponent<Image>();
            scrollbarGo.GetComponent<Image>().color = UiTheme.Accent;
            sr.verticalScrollbar = sb;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            itemsParent = scrollContent;
        }
        else
        {
            itemsParent = listGo.transform;
        }

        // 创建选项按钮（同 Task 1）
        for (int i = 0; i < options.Count; i++)
        {
            int idx = i;
            bool isSelected = (idx == currentIndex);

            GameObject itemGo = new GameObject($"Item_{idx}", typeof(RectTransform),
                typeof(Image), typeof(Button), typeof(LayoutElement));
            itemGo.transform.SetParent(itemsParent, false);

            itemGo.GetComponent<Image>().color = isSelected ? UiTheme.Accent : Color.clear;

            LayoutElement le = itemGo.GetComponent<LayoutElement>();
            le.preferredHeight = 34f;

            Button btn = itemGo.GetComponent<Button>();
            btn.onClick.AddListener(() => { onSelect(idx); Hide(); });

            CreateItemLabel(itemGo.transform, options[i], isSelected, font);
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
