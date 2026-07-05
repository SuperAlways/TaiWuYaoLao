using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TaiwuEncyclopedia.Core.Http;

namespace TaiwuEncyclopedia.UI;

public sealed class ReferencePanel : MonoBehaviour
{
    public TMP_FontAsset? _font;

    public void SetFont(TMP_FontAsset? font) => _font = font;

    public void Build(List<Reference> references)
    {
        if (references == null || references.Count == 0) return;

        TextMeshProUGUI title = new GameObject("RefTitle", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        title.transform.SetParent(transform, false);
        if (_font != null) title.font = _font;
        title.fontSize = 17;
        title.text = "——— 参考文献 ———";
        title.color = new Color(0.55f, 0.58f, 0.60f, 1f);
        title.alignment = TextAlignmentOptions.Center;
        LayoutElement tle = title.gameObject.AddComponent<LayoutElement>();
        tle.preferredHeight = 30;

        foreach (Reference r in references)
            AddReferenceCard(r);
    }

    private void AddReferenceCard(Reference r)
    {
        GameObject go = new GameObject("RefCard", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(transform, false);
        go.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.10f, 0.95f);
        HorizontalLayoutGroup hlg = go.GetComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.spacing = 8f;
        hlg.padding = new RectOffset(10, 10, 6, 6);

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 40;

        TextMeshProUGUI num = new GameObject("RefNum", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        num.transform.SetParent(go.transform, false);
        if (_font != null) num.font = _font;
        num.fontSize = 15;
        num.text = "📄";
        num.color = new Color(0.55f, 0.58f, 0.60f, 1f);
        num.alignment = TextAlignmentOptions.Center;
        num.GetComponent<RectTransform>().sizeDelta = new Vector2(24, 0);

        TextMeshProUGUI title = new GameObject("RefTitle", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        title.transform.SetParent(go.transform, false);
        if (_font != null) title.font = _font;
        title.fontSize = 16;
        title.text = r.FilePath ?? "参考资料";
        title.color = new Color(0.85f, 0.83f, 0.78f, 1f);
        title.alignment = TextAlignmentOptions.Left;
        LayoutElement tle = title.gameObject.AddComponent<LayoutElement>();
        tle.flexibleWidth = 1f;

        // Source-type badge
        var (badgeText, badgeColor, badgeWidth) = GetSourceTypeBadge(r.SourceType);
        TextMeshProUGUI badge = new GameObject("Badge", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        badge.transform.SetParent(go.transform, false);
        if (_font != null) badge.font = _font;
        badge.fontSize = 14;
        badge.text = badgeText;
        badge.color = badgeColor;
        badge.alignment = TextAlignmentOptions.Center;
        badge.GetComponent<RectTransform>().sizeDelta = new Vector2(badgeWidth, 0);

        // URL link (if available)
        if (!string.IsNullOrEmpty(r.SourceUrl))
        {
            Button linkBtn = go.AddComponent<Button>();
            linkBtn.onClick.AddListener(delegate
            {
                Application.OpenURL(r.SourceUrl);
            });
        }
    }

    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    private static (string Text, Color Color, float Width) GetSourceTypeBadge(string? sourceType)
    {
        return sourceType switch
        {
            "wiki" => ("Wiki", new Color(0.3f, 0.5f, 0.7f, 1f), 46f),
            "steam" => ("Steam", new Color(0.3f, 0.6f, 0.3f, 1f), 56f),
            "mod" => ("Mod", new Color(0.6f, 0.4f, 0.2f, 1f), 44f),
            _ => (sourceType ?? "?", new Color(0.4f, 0.4f, 0.4f, 1f), 50f),
        };
    }
}