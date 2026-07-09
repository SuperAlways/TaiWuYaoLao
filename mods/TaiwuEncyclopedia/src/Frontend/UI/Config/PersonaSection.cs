#pragma warning disable CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, IDE0010, IDE0022, IDE0048, RCS1123, CA1307, RCS1238, CA1852, CS0414, RCS1201, RCS1124, IDE0057, CA1031, RCS1001, IDE0370, RCS1085, IDE0028, RCS1161
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TaiwuEncyclopedia.Core.Skills;
using TaiwuEncyclopedia.Frontend.UI;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// 对话风格 (Persona) 选择区：OverlayDropdown 选择 + 预览文本。
/// 由 ConfigPanel 组合使用。
/// </summary>
public sealed class PersonaSection : MonoBehaviour
{
    private TextMeshProUGUI? _personaBtnLabel;
    private TextMeshProUGUI? _personaPreviewText;
    private Button? _personaButton;
    private RectTransform? _canvasRt;
    private TMP_FontAsset? _font;

    private List<string> _personaIdList = [];
    private int _currentPersonaIdx = -1;

    /// <summary>当前选中的 Persona ID；未选择时返回空字符串。</summary>
    public string SelectedPersonaId
    {
        get
        {
            if (_personaIdList == null || _currentPersonaIdx < 0 || _currentPersonaIdx >= _personaIdList.Count)
                return "";
            return _personaIdList[_currentPersonaIdx];
        }
    }

    public event Action? OnConfigChanged;

    public void Build(Transform content, TMP_FontAsset? font, RectTransform canvasRt)
    {
        _font = font;
        _canvasRt = canvasRt;

        GameObject section = CreateSection(content, "对话风格 (Persona)");
        Transform inner = section.transform.Find("Content")!;

        // 风格选择行
        CreatePersonaRow(inner);

        // 预览区
        CreatePreviewBox(inner);

        // 提示文本
        TextMeshProUGUI hint = UiFactory.CreateText(inner, "Hint",
            "切换后下一条消息生效，历史消息不改写", 15,
            new Color(0.55f, 0.58f, 0.60f, 1f), TextAlignmentOptions.Left);
        hint.enableWordWrapping = true;
    }

    public void Refresh()
    {
        _personaIdList = new List<string>();

        SkillManager? sm = FrontendServices.SkillManager;
        if (sm == null)
        {
            if (_personaBtnLabel != null) _personaBtnLabel.text = "(技能目录未就绪)";
            if (_personaButton != null) _personaButton.interactable = false;
            if (_personaPreviewText != null) _personaPreviewText.text = "进入游戏后可选择对话风格";
            return;
        }

        List<string> personaIds = sm.GetPersonaIds();
        if (personaIds.Count == 0)
        {
            if (_personaBtnLabel != null) _personaBtnLabel.text = "(无可用 Persona)";
            if (_personaButton != null) _personaButton.interactable = false;
            if (_personaPreviewText != null) _personaPreviewText.text = "请检查 Skills/registry.yaml 配置";
            return;
        }

        if (_personaButton != null) _personaButton.interactable = true;
        _personaIdList.AddRange(personaIds);

        // 选中当前保存的 persona，找不到时默认 sword-will
        string savedPersona = FrontendServices.SelectedPersonaId;
        int idx = _personaIdList.IndexOf(savedPersona);
        if (idx < 0) idx = _personaIdList.IndexOf("sword-will");
        if (idx < 0 && _personaIdList.Count > 0) idx = 0;
        _currentPersonaIdx = idx;

        UpdatePersonaButtonLabel();
        OnPersonaChanged(_currentPersonaIdx);
    }

    // ========== 构建 UI ==========

    private void CreatePersonaRow(Transform parent)
    {
        GameObject row = new GameObject("DropRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);
        HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.spacing = 10f;
        hlg.padding = new RectOffset(0, 0, 4, 4);

        UiFactory.CreateText(row.transform, "Label", "选择风格：", 18,
            new Color(0.80f, 0.78f, 0.70f, 1f), TextAlignmentOptions.Left)
            .gameObject.AddComponent<LayoutElement>().preferredWidth = 100;

        // 风格按钮 (点击打开 OverlayDropdown)
        _personaButton = UiFactory.CreateButton(row.transform, "PersonaBtn", "", 18,
            new Color(1, 1, 1, 0.12f), out _personaBtnLabel);
        LayoutElement dle = _personaButton.gameObject.AddComponent<LayoutElement>();
        dle.flexibleWidth = 1f;
        dle.preferredHeight = 36;

        _personaButton.onClick.AddListener(OnPersonaButtonClicked);
    }

    private void CreatePreviewBox(Transform parent)
    {
        GameObject previewBox = new GameObject("PreviewBox", typeof(RectTransform), typeof(Image));
        previewBox.transform.SetParent(parent, false);
        previewBox.GetComponent<Image>().color = new Color(0, 0, 0, 0.18f);
        LayoutElement pble = previewBox.AddComponent<LayoutElement>();
        pble.preferredHeight = 120;

        TextMeshProUGUI previewTitle = UiFactory.CreateText(previewBox.transform, "PreviewTitle",
            "当前风格预览：", 16,
            new Color(0.65f, 0.68f, 0.70f, 1f), TextAlignmentOptions.Left);
        UiFactory.Anchor(previewTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(10, -26), new Vector2(-10, -6));

        _personaPreviewText = UiFactory.CreateText(previewBox.transform, "PreviewText",
            "加载中...", 17,
            new Color(0.85f, 0.83f, 0.78f, 1f), TextAlignmentOptions.TopLeft);
        _personaPreviewText.enableWordWrapping = true;
        _personaPreviewText.overflowMode = TextOverflowModes.Ellipsis;
        UiFactory.Anchor(_personaPreviewText.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
            new Vector2(10, 6), new Vector2(-10, -32));
    }

    // ========== 事件处理 ==========

    private void OnPersonaButtonClicked()
    {
        if (_personaIdList == null || _personaIdList.Count == 0) return;
        if (_canvasRt == null || _personaButton == null) return;

        // 构建 displayName 列表
        SkillManager? sm = FrontendServices.SkillManager;
        List<string> displayNames = new List<string>(_personaIdList.Count);
        foreach (string id in _personaIdList)
        {
            string cn = sm != null ? sm.PersonaCnName(id) : id;
            displayNames.Add(cn);
        }

        var dropdown = _personaButton.gameObject.AddComponent<OverlayDropdown>();
        dropdown.Show(_canvasRt, _personaButton.GetComponent<RectTransform>(),
            displayNames, _currentPersonaIdx, idx =>
        {
            _currentPersonaIdx = idx;
            UpdatePersonaButtonLabel();
            OnPersonaChanged(idx);
            OnConfigChanged?.Invoke();
        }, _font);
    }

    private void UpdatePersonaButtonLabel()
    {
        if (_personaBtnLabel == null || _personaIdList == null) return;
        if (_currentPersonaIdx < 0 || _currentPersonaIdx >= _personaIdList.Count) return;
        SkillManager? sm = FrontendServices.SkillManager;
        string cn = sm != null ? sm.PersonaCnName(_personaIdList[_currentPersonaIdx]) : _personaIdList[_currentPersonaIdx];
        _personaBtnLabel.text = cn + "  >>";
    }

    private void OnPersonaChanged(int idx)
    {
        if (_personaIdList == null || idx < 0 || idx >= _personaIdList.Count) return;

        string personaId = _personaIdList[idx];
        SkillManager? sm = FrontendServices.SkillManager;

        if (_personaPreviewText != null)
        {
            if (sm == null)
            {
                _personaPreviewText.text = "(技能管理器未就绪)";
            }
            else
            {
                string desc = sm.PersonaDescription(personaId);
                string preview = !string.IsNullOrWhiteSpace(desc)
                    ? desc
                    : ExtractPersonaSummary(sm.LoadPersona(personaId) ?? "");
                _personaPreviewText.text = preview;
            }
        }
    }

    /// <summary>从 persona markdown 提取简介:跳过标题行,取首段实际内容,限 120 字。</summary>
    private static string ExtractPersonaSummary(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) { if (sb.Length > 0) break; continue; }
            if (line.StartsWith('#')) continue;
            if (line.StartsWith('-')) continue;
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(line);
            if (sb.Length >= 120) break;
        }
        string summary = sb.ToString().Trim();
        if (summary.Length > 120) summary = summary.Substring(0, 120) + "...";
        return string.IsNullOrEmpty(summary) ? "(无简介)" : summary;
    }

    // ========== Helpers ==========

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
