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
/// 参考 WorldTalk WorldTalkSettingsUiHost Provider/Model Dropdown 模式：
///   - 遮罩+列表共用一个根节点，Destroy 一起清理
///   - 近透明遮罩（0.004 alpha），仅拦截射线，不遮挡视觉
///   - 弹出后为模态：必须选择或点空白关闭，不能同时操作面板
///   - 短列表：VerticalLayoutGroup + ContentSizeFitter（自适应高度）
///   - 长列表：ScrollRect + 固定高度（不用 ContentSizeFitter，避免布局冲突）
/// </summary>
public sealed class OverlayDropdown : MonoBehaviour
{
    private GameObject? _root;   // 遮罩 + 列表的共同根节点
    private bool _isOpen;

    public bool IsOpen => _isOpen;

    private const int ScrollThreshold = 8;
    private const float ItemHeight = 34f;
    private const float ItemSpacing = 2f;
    private const float ListPadding = 4f;  // padding per side
    private const float MaxScrollHeight = 300f;

    /// <summary>
    /// 显示下拉列表。对齐 WorldTalk ToggleProviderDropdown 模式：
    /// 创建全屏遮罩（近透明，拦截射线）+ 定位列表 + 选项按钮。
    /// </summary>
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

        // 1. 根节点（全屏拉伸，拦截射线，近透明遮罩）
        _root = new GameObject("DropdownRoot", typeof(RectTransform), typeof(Image), typeof(Button));
        _root.transform.SetParent(canvasRoot, false);
        RectTransform rootRt = _root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.sizeDelta = Vector2.zero;
        rootRt.SetAsLastSibling();

        // 近透明遮罩（WorldTalk 用 0.004 alpha，只拦截射线不遮挡视觉）
        Image rootImg = _root.GetComponent<Image>();
        rootImg.color = new Color(0f, 0f, 0f, 0.004f);
        rootImg.raycastTarget = true;
        Button rootBtn = _root.GetComponent<Button>();
        rootBtn.targetGraphic = rootImg;
        rootBtn.onClick.AddListener(Hide);

        // 2. 列表容器（作为根节点的子节点，一起销毁）
        bool useScroll = options.Count > ScrollThreshold;
        GameObject listGo;

        if (useScroll)
        {
            // 长列表：VerticalLayoutGroup + ScrollRect，不用 ContentSizeFitter
            // 对齐 WorldTalk ToggleModelDropdown 模式：固定高度，ScrollRect 内部滚动
            listGo = new GameObject("List", typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup));
            listGo.transform.SetParent(_root.transform, false);
            listGo.GetComponent<Image>().color = UiTheme.PanelBg;

            VerticalLayoutGroup vlg = listGo.GetComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.spacing = ItemSpacing;
            vlg.padding = new RectOffset((int)ListPadding, (int)ListPadding, (int)ListPadding, (int)ListPadding);
        }
        else
        {
            // 短列表：VerticalLayoutGroup + ContentSizeFitter（自适应高度）
            listGo = new GameObject("List", typeof(RectTransform), typeof(Image),
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
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        RectTransform lrt = listGo.GetComponent<RectTransform>();

        // 定位
        float listHeight = PositionList(lrt, trigger, canvasRoot, options.Count, useScroll);

        // 3. 填充选项
        if (useScroll)
        {
            PopulateScrollItems(listGo, listHeight, options, currentIndex, onSelect, font);
        }
        else
        {
            PopulateItems(listGo, options, currentIndex, onSelect, font);
        }

        // 4. 确保列表在遮罩之上渲染
        listGo.transform.SetAsLastSibling();
    }

    /// <summary>关闭下拉列表，销毁遮罩+列表的根节点。</summary>
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

    /// <summary>
    /// 定位列表：在 trigger 下方展开，底部空间不足时向上展开。
    /// 返回列表可用高度（供 ScrollRect 使用）。
    /// </summary>
    private static float PositionList(RectTransform listRt, RectTransform trigger,
        RectTransform canvasRoot, int itemCount, bool useScroll)
    {
        // trigger 世界坐标 → canvasRoot 局部坐标
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

        // 中心相对 → 左上角相对
        float anchorX = localBottomLeft.x + halfW;
        float triggerBottomY = localBottomLeft.y - halfH;

        // 估算列表自然高度
        float naturalHeight = itemCount * ItemHeight + (itemCount - 1) * ItemSpacing + ListPadding * 2f;
        float listHeight = useScroll ? Mathf.Min(naturalHeight, MaxScrollHeight) : naturalHeight;

        // 向下/向上展开判定
        bool expandDown = triggerBottomY - listHeight >= -halfH;

        listRt.pivot = new Vector2(0, expandDown ? 1f : 0f);
        listRt.anchorMin = listRt.anchorMax = new Vector2(0, 1);

        if (expandDown)
        {
            listRt.anchoredPosition = new Vector2(anchorX, triggerBottomY);
        }
        else
        {
            // 向上展开：定位到 trigger 顶部
            Vector2 localTopLeft;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRoot, corners[1], null, out localTopLeft);
            float triggerTopY = localTopLeft.y - halfH;
            listRt.anchoredPosition = new Vector2(anchorX, triggerTopY);
        }

        // 设置宽度，高度（短列表由 ContentSizeFitter 控制，这里设 0）
        listRt.sizeDelta = new Vector2(triggerWidth, useScroll ? listHeight : 0f);

        return listHeight;
    }

    /// <summary>长列表：在 listGo 内创建 ScrollRect 容纳选项。</summary>
    private void PopulateScrollItems(GameObject listGo, float listHeight,
        IReadOnlyList<string> options, int currentIndex, Action<int> onSelect, TMP_FontAsset? font)
    {
        // 在 listGo 内创建 ScrollRect
        RectTransform scrollContent = UiFactory.CreateScroll(listGo.transform, "ScrollContent", spacing: ItemSpacing);
        scrollContent.anchorMin = scrollContent.anchorMax = new Vector2(0, 1);
        scrollContent.pivot = new Vector2(0.5f, 1);
        scrollContent.anchoredPosition = Vector2.zero;
        scrollContent.sizeDelta = new Vector2(0, 0);

        // ScrollRect 的 ScrollGo 在 VerticalLayoutGroup 控制下需要设置 preferredHeight
        LayoutElement scrollLe = scrollContent.parent.parent.gameObject.AddComponent<LayoutElement>();
        scrollLe.preferredHeight = listHeight - ListPadding * 2f;

        // Scrollbar
        ScrollRect sr = scrollContent.parent.parent.GetComponent<ScrollRect>();
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

        // 选项按钮
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

    /// <summary>短列表：直接在 listGo 内创建选项按钮。</summary>
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
