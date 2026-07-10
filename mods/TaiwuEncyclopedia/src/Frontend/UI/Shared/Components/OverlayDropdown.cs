using System;
using System.Collections.Generic;
using TMPro;
using TaiwuEncyclopedia.Frontend.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// 通用下拉组件：全屏透明遮罩 + 选项列表。
/// 三种显示模式：
///   ≤3 项：trigger 下方弹出
///   4~12 项：画布中央弹出，高度随项数自适应
///   >12 项：画布中央弹出，固定最大高度 + ScrollRect 滚动
/// </summary>
public sealed class OverlayDropdown : MonoBehaviour
{
    private GameObject? _root;
    private bool _isOpen;

    public bool IsOpen => _isOpen;

    private const int CenterThreshold = 3;
    private const int ScrollThreshold = 12;
    private const float ItemHeight = 34f;
    private const float ItemSpacing = 2f;
    private const float ListPadding = 4f;

    // 中央弹出列表的水平 anchor 百分比
    private const float CenterAnchorXMin = 0.28f;
    private const float CenterAnchorXMax = 0.72f;
    // ScrollRect 模式下的垂直 anchor 百分比
    private const float ScrollAnchorYMin = 0.22f;
    private const float ScrollAnchorYMax = 0.78f;

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

        bool useScroll = options.Count > ScrollThreshold;
        bool useCenter = options.Count > CenterThreshold;

        // 1. 根节点（全屏拉伸，拦截射线）
        _root = new GameObject("DropdownRoot", typeof(RectTransform), typeof(Image), typeof(Button));
        _root.transform.SetParent(canvasRoot, false);
        RectTransform rootRt = _root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.sizeDelta = Vector2.zero;
        rootRt.SetAsLastSibling();

        Image rootImg = _root.GetComponent<Image>();
        rootImg.color = new Color(0f, 0f, 0f, useCenter ? 0.35f : 0.004f);
        rootImg.raycastTarget = true;
        Button rootBtn = _root.GetComponent<Button>();
        rootBtn.targetGraphic = rootImg;
        rootBtn.onClick.AddListener(Hide);

        // 2. 列表容器
        GameObject listGo = new GameObject("List", typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        listGo.transform.SetParent(_root.transform, false);
        listGo.GetComponent<Image>().color = UiTheme.PanelBg;

        VerticalLayoutGroup vlg = listGo.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.spacing = ItemSpacing;
        vlg.padding = new RectOffset((int)ListPadding, (int)ListPadding, (int)ListPadding, (int)ListPadding);

        ContentSizeFitter csf = listGo.GetComponent<ContentSizeFitter>();
        RectTransform lrt = listGo.GetComponent<RectTransform>();

        // 3. 定位
        if (useCenter)
        {
            // 水平：anchor 百分比撑满
            lrt.anchorMin = new Vector2(CenterAnchorXMin, 0.5f);
            lrt.anchorMax = new Vector2(CenterAnchorXMax, 0.5f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.anchoredPosition = Vector2.zero;

            if (useScroll)
            {
                // >12 项：固定垂直区域 + ScrollRect
                lrt.anchorMin = new Vector2(CenterAnchorXMin, ScrollAnchorYMin);
                lrt.anchorMax = new Vector2(CenterAnchorXMax, ScrollAnchorYMax);
                lrt.sizeDelta = Vector2.zero;
                csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            }
            else
            {
                // 4~12 项：高度自适应，ContentSizeFitter 控制高度
                // 宽度由 anchor 控制，高度设 0 让 CSF 自动撑开
                lrt.sizeDelta = new Vector2(0, 0);
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }
        else
        {
            // ≤3 项：trigger 下方弹出
            PositionBelowTrigger(lrt, trigger, canvasRoot);
        }

        // 4. 填充选项
        if (useScroll)
        {
            PopulateScrollItems(listGo, options, currentIndex, onSelect, font);
        }
        else
        {
            PopulateItems(listGo, options, currentIndex, onSelect, font);
        }

        // 5. 确保列表在遮罩之上渲染
        listGo.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (_root != null)
        {
            Destroy(_root);
            _root = null;
        }
        _isOpen = false;
    }

    private void OnDestroy()
    {
        if (_root != null)
        {
            Destroy(_root);
            _root = null;
        }
    }

    private static void PositionBelowTrigger(RectTransform listRt, RectTransform trigger, RectTransform canvasRoot)
    {
        Vector3[] corners = new Vector3[4];
        trigger.GetWorldCorners(corners);

        Vector2 localBottomLeft;
        Vector2 localBottomRight;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRoot, corners[0], null, out localBottomLeft);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRoot, corners[3], null, out localBottomRight);

        float triggerWidth = localBottomRight.x - localBottomLeft.x;
        float halfW = canvasRoot.rect.width * 0.5f;
        float halfH = canvasRoot.rect.height * 0.5f;

        float anchorX = localBottomLeft.x + halfW;
        float anchorY = localBottomLeft.y - halfH;

        listRt.pivot = new Vector2(0, 1);
        listRt.anchorMin = listRt.anchorMax = new Vector2(0, 1);
        listRt.anchoredPosition = new Vector2(anchorX, anchorY);
        listRt.sizeDelta = new Vector2(triggerWidth, 0);
    }

    /// <summary>>12 项：在 listGo 内嵌套 ScrollRect。</summary>
    private void PopulateScrollItems(GameObject listGo, IReadOnlyList<string> options,
        int currentIndex, Action<int> onSelect, TMP_FontAsset? font)
    {
        RectTransform scrollContent = UiFactory.CreateScroll(listGo.transform, "ScrollContent", spacing: ItemSpacing);
        scrollContent.anchorMin = scrollContent.anchorMax = new Vector2(0, 1);
        scrollContent.pivot = new Vector2(0.5f, 1);
        scrollContent.anchoredPosition = Vector2.zero;
        scrollContent.sizeDelta = new Vector2(0, 0);

        ScrollRect sr = scrollContent.parent.parent.GetComponent<ScrollRect>();

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

        for (int i = 0; i < options.Count; i++)
        {
            int idx = i;
            bool isSelected = (idx == currentIndex);

            GameObject itemGo = new GameObject($"Item_{idx}", typeof(RectTransform),
                typeof(Image), typeof(Button), typeof(LayoutElement));
            itemGo.transform.SetParent(scrollContent, false);

            itemGo.GetComponent<Image>().color = isSelected ? UiTheme.Accent : Color.clear;

            LayoutElement le = itemGo.GetComponent<LayoutElement>();
            le.preferredHeight = ItemHeight;

            Button btn = itemGo.GetComponent<Button>();
            btn.onClick.AddListener(() => { onSelect(idx); Hide(); });

            CreateItemLabel(itemGo.transform, options[i], isSelected, font);
        }
    }

    /// <summary>≤12 项：直接在 listGo 内创建选项。</summary>
    private void PopulateItems(GameObject listGo, IReadOnlyList<string> options,
        int currentIndex, Action<int> onSelect, TMP_FontAsset? font)
    {
        for (int i = 0; i < options.Count; i++)
        {
            int idx = i;
            bool isSelected = (idx == currentIndex);

            GameObject itemGo = new GameObject($"Item_{idx}", typeof(RectTransform),
                typeof(Image), typeof(Button), typeof(LayoutElement));
            itemGo.transform.SetParent(listGo.transform, false);

            itemGo.GetComponent<Image>().color = isSelected ? UiTheme.Accent : Color.clear;

            LayoutElement le = itemGo.GetComponent<LayoutElement>();
            le.preferredHeight = ItemHeight;

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
