#pragma warning disable CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, IDE0010, IDE0022, IDE0048, RCS1123, CA1307, RCS1238, CA1852, CS0414, RCS1201, RCS1124, IDE0057, CA1031, RCS1001, IDE0370, RCS1085, IDE0028, RCS1161
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TaiwuEncyclopedia.Frontend.UI;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// RAG 远程检索配置区：开关 Toggle + 警告文本 + 状态显示。
/// 由 ConfigPanel 组合使用。
/// </summary>
public sealed class RagSection : MonoBehaviour
{
    private Toggle? _ragToggle;
    private TextMeshProUGUI? _statusText;

    /// <summary>当前 RAG 开关状态。</summary>
    public bool RagEnabled => _ragToggle?.isOn ?? true;

    /// <summary>构建 RAG 配置区 UI 并挂载到 content 下。</summary>
    public void Build(Transform content, TMP_FontAsset? font)
    {
        GameObject section = CreateSection(content, "RAG 远程检索");
        Transform inner = section.transform.Find("Content")!;

        // Toggle 开关行
        GameObject toggleRow = new GameObject("ToggleRow",
            typeof(RectTransform), typeof(HorizontalLayoutGroup));
        toggleRow.transform.SetParent(inner, false);
        var tHLG = toggleRow.GetComponent<HorizontalLayoutGroup>();
        tHLG.childForceExpandWidth = false;
        tHLG.childForceExpandHeight = false;
        tHLG.childControlWidth = true;
        tHLG.childControlHeight = true;
        tHLG.spacing = 10f;

        // Toggle (checkbox 风格)
        GameObject toggleGo = new GameObject("RagToggle",
            typeof(RectTransform), typeof(Toggle), typeof(Image));
        toggleGo.transform.SetParent(toggleRow.transform, false);
        toggleGo.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);
        LayoutElement tle = toggleGo.AddComponent<LayoutElement>();
        tle.preferredWidth = 24; tle.preferredHeight = 24;
        _ragToggle = toggleGo.GetComponent<Toggle>();
        _ragToggle.isOn = true;

        // Checkmark（Toggle 开启时显示）
        GameObject checkGo = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        checkGo.transform.SetParent(toggleGo.transform, false);
        checkGo.GetComponent<Image>().color = UiTheme.Accent;
        UiFactory.Anchor(checkGo.GetComponent<RectTransform>(),
            Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
        _ragToggle.graphic = checkGo.GetComponent<Image>();

        // Label
        UiFactory.CreateText(toggleRow.transform, "Label",
            "启用远程攻略检索", 17,
            new Color(0.92f, 0.90f, 0.82f, 1f), TextAlignmentOptions.Left);

        // 警告文本
        GameObject warningGo = UiFactory.CreateText(inner, "WarningText",
            "开启后将检索玩家社区攻略，可能包含剧透内容\n检索耗时较长（通常 10-60 秒），请耐心等待",
            14,
            new Color(0.85f, 0.75f, 0.35f, 1f),
            TextAlignmentOptions.Left).gameObject;
        LayoutElement wle = warningGo.AddComponent<LayoutElement>();
        wle.preferredHeight = 40;

        // 状态文本
        _statusText = UiFactory.CreateText(inner, "StatusText",
            "当前状态：已启用", 15,
            new Color(0.45f, 0.72f, 0.45f, 1f),
            TextAlignmentOptions.Left);

        _ragToggle.onValueChanged.AddListener(OnToggleChanged);
    }

    /// <summary>刷新（RagSection 无动态数据，保留空实现以对齐 Section 接口约定）。</summary>
    public void Refresh() { }

    // ========== Helpers ==========

    private void OnToggleChanged(bool isOn)
    {
        if (_statusText != null)
        {
            _statusText.text = isOn ? "当前状态：已启用" : "当前状态：已关闭";
            _statusText.color = isOn
                ? new Color(0.45f, 0.72f, 0.45f, 1f)
                : new Color(0.85f, 0.70f, 0.30f, 1f);
        }
    }

    private static GameObject CreateSection(Transform parent, string title)
    {
        GameObject section = new GameObject("Section_" + title.Replace(" ", "_"),
            typeof(RectTransform), typeof(VerticalLayoutGroup));
        section.transform.SetParent(parent, false);
        VerticalLayoutGroup vlg = section.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.spacing = 6f;

        UiFactory.CreateText(section.transform, "SectionTitle",
            $"-- {title} --", 20,
            new Color(0.75f, 0.78f, 0.82f, 1f), TextAlignmentOptions.Left);

        GameObject content = new GameObject("Content",
            typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(Image));
        content.transform.SetParent(section.transform, false);
        content.GetComponent<Image>().color = new Color(0, 0, 0, 0.16f);
        VerticalLayoutGroup clg = content.GetComponent<VerticalLayoutGroup>();
        clg.childForceExpandWidth = true;
        clg.childForceExpandHeight = false;
        clg.childControlWidth = true;
        clg.childControlHeight = true;
        clg.spacing = 8f;
        clg.padding = new RectOffset(12, 12, 12, 12);

        return section;
    }
}
#pragma warning restore CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, IDE0010, IDE0022, IDE0048, RCS1123, CA1307, RCS1238, CA1852, CS0414, RCS1201, RCS1124, IDE0057, CA1031, RCS1001, IDE0370, RCS1085, IDE0028, RCS1161
