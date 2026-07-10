using System;
using System.Collections.Generic;
using TMPro;
using TaiwuEncyclopedia.Frontend.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// 通用下拉组件：全屏透明遮罩 + 选项列表。
/// 解决 Unity TMP_Dropdown 在 ScrollRect 中裁切的问题。
/// 参考 WorldTalk WorldTalkSettingsUiHost Provider/Model Dropdown 模式：
///   - 遮罩+列表共用一个根节点，Destroy 一起清理
///   - 近透明遮罩（0.004 alpha），仅拦截射线，不遮挡视觉
///   - 弹出后为模态：必须选择或点空白关闭，不能同时操作面板
///   - 短列表（≤3）：在 trigger 按钮下方弹出
///   - 长列表（>3）：画布中央弹出（anchor 百分比定位，对齐 WorldTalk 模式）
/// </summary>
public sealed class OverlayDropdown : MonoBehaviour
{
    private GameObject? _root;
    private bool _isOpen;

    public bool IsOpen => _isOpen;

    private const int CenterThreshold = 3;
    private const float ItemHeight = 34f;
    private const float ItemSpacing = 2f;
    private const float ListPadding = 4f;

    // 中央弹出列表的 anchor 百分比（对齐 WorldTalk ProviderList 比例）
    private const float CenterAnchorXMin = 0.28f;
    private const float CenterAnchorXMax = 0.72f;
    private const float CenterAnchorYMin = 0.22f;
    private const float CenterAnchorYMax = 0.78f;

    /// <summary>
    /// 显示下拉列表。
    /// 短列表（≤3）：在 trigger 按钮正下方弹出。
    /// 长列表（>3）：画布中央弹出（anchor 百分比定位，模态），对齐 WorldTalk Provider/Model Dropdown 模式。
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

        bool useCenter = options.Count > CenterThreshold;

        // 1. 根节点（全屏拉伸，拦截射线，近透明遮罩）
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
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform lrt = listGo.GetComponent<RectTransform>();

        // 3. 定位
        if (useCenter)
        {
            // 中央弹出：anchor 百分比定位，对齐 WorldTalk ProviderList
            lrt.anchorMin = new Vector2(CenterAnchorXMin, CenterAnchorYMin);
            lrt.anchorMax = new Vector2(CenterAnchorXMax, CenterAnchorYMax);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.anchoredPosition = Vector2.zero;
            lrt.sizeDelta = Vector2.zero;
            // 中央模式不需要 ContentSizeFitter（anchor 撑满固定区域）
            csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }
        else
        {
            // trigger 下方弹出
            PositionBelowTrigger(lrt, trigger, canvasRoot);
        }

        // 4. 填充选项
        PopulateItems(listGo, options, currentIndex, onSelect, font);

        // 5. 确保列表在遮罩之上渲染
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

    /// <summary>短列表定位：在 trigger 正下方弹出。</summary>
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

        // 中心相对 → 左上角相对
        float anchorX = localBottomLeft.x + halfW;
        float anchorY = localBottomLeft.y - halfH;

        // 统一向下展开
        listRt.pivot = new Vector2(0, 1);
        listRt.anchorMin = listRt.anchorMax = new Vector2(0, 1);
        listRt.anchoredPosition = new Vector2(anchorX, anchorY);
        listRt.sizeDelta = new Vector2(triggerWidth, 0);
    }

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
