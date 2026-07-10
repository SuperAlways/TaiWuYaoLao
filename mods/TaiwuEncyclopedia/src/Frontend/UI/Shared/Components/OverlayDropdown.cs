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
/// </summary>
public sealed class OverlayDropdown : MonoBehaviour
{
    private GameObject? _root;   // 遮罩 + 列表的共同根节点
    private bool _isOpen;

    public bool IsOpen => _isOpen;

    /// <summary>
    /// 显示下拉列表。对齐 WorldTalk ToggleProviderDropdown 模式：
    /// 创建全屏遮罩（近透明，拦截射线）+ 定位列表 + 选项按钮。
    /// 遮罩和列表共用一个根节点，确保 Hide/Destroy 一起清理。
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
        GameObject listGo = new GameObject("List", typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        listGo.transform.SetParent(_root.transform, false);
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
        // root 与 canvasRoot 完全重合（stretch-fill），所以坐标空间相同
        PositionList(lrt, trigger, canvasRoot, options.Count);

        // 3. 选项按钮
        PopulateItems(listGo, options, currentIndex, onSelect, font);

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
        // 防止组件销毁时泄漏根节点
        if (_root != null)
        {
            Destroy(_root);
            _root = null;
        }
    }

    /// <summary>
    /// 定位列表：在 trigger 下方展开，底部空间不足时向上展开。
    /// root 与 canvasRoot 完全重合（stretch-fill），
    /// 所以列表在 root 内的坐标 = 在 canvasRoot 内的坐标。
    /// </summary>
    private static void PositionList(RectTransform listRt, RectTransform trigger,
        RectTransform canvasRoot, int itemCount)
    {
        // trigger 世界坐标 → canvasRoot 局部坐标（原点在画布中心）
        Vector3[] corners = new Vector3[4];
        trigger.GetWorldCorners(corners);
        // corners: [0]=左下, [1]=左上（Unity UGUI GetWorldCorners 在 ScreenSpace-Overlay 下）

        // 对于 ScreenSpace-Overlay Canvas，GetWorldCorners 返回的已经是屏幕坐标
        // 需要转为 canvasRoot 的局部坐标
        Vector2 localBottomLeft;
        Vector2 localBottomRight;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRoot, corners[0], null, out localBottomLeft);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRoot, corners[3], null, out localBottomRight);

        float triggerWidth = localBottomRight.x - localBottomLeft.x;

        // 画布尺寸
        float halfW = canvasRoot.rect.width * 0.5f;
        float halfH = canvasRoot.rect.height * 0.5f;

        // ScreenPointToLocalPointInRectangle 返回中心相对坐标，
        // anchor (0,1) 需要左上角相对坐标：
        //   leftTopRelative = centerRelative + (halfW, -halfH)
        float anchorX = localBottomLeft.x + halfW;
        float anchorY = localBottomLeft.y - halfH;

        // 估算列表高度
        float estimatedHeight = itemCount * 34f + (itemCount - 1) * 2f + 8f;
        float maxListHeight = Mathf.Min(estimatedHeight + 8f, 300f + 8f);

        // 默认向下展开：pivot 左上角 (0,1)
        listRt.pivot = new Vector2(0, 1);
        listRt.anchorMin = listRt.anchorMax = new Vector2(0, 1);
        listRt.sizeDelta = new Vector2(triggerWidth, 0);  // 高度由 ContentSizeFitter 控制

        if (localBottomLeft.y - maxListHeight < -halfH)
        {
            // 底部空间不足，改为向上展开
            listRt.pivot = new Vector2(0, 0);
            Vector2 localTopLeft;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRoot, corners[1], null, out localTopLeft);
            listRt.anchoredPosition = new Vector2(anchorX, localTopLeft.y - halfH);
        }
        else
        {
            // 向下展开
            listRt.anchoredPosition = new Vector2(anchorX, anchorY);
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

        // 创建选项按钮
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
