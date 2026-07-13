using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TaiwuEncyclopedia.Core.Rag;

using TaiwuEncyclopedia.Frontend.UI;

namespace TaiwuEncyclopedia.UI;

public sealed class ReferencePanel : MonoBehaviour
{
    public TMP_FontAsset? _font;

    public void SetFont(TMP_FontAsset? font) => _font = font;

    public void Build(List<Reference> references)
    {
        if (references == null || references.Count == 0) return;

        // 标题
        TextMeshProUGUI title = NewText("RefTitle", transform, 18, TextAlignmentOptions.Left);
        title.text = "——— 参考文献 ———";
        title.color = new Color(0.55f, 0.58f, 0.6f, 1f);
        UiFactory.Anchor(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(0, 2), new Vector2(0, -2));

        // 逐个文献卡片
        foreach (Reference r in references)
            AddReferenceCard(r);

        // 点击提示
        TextMeshProUGUI hint = NewText("ClickHint", transform, 14, TextAlignmentOptions.Center);
        hint.text = "点击链接可跳转参考源";
        hint.color = new Color(0.40f, 0.42f, 0.45f, 1f);
        hint.raycastTarget = false;
    }

    private void AddReferenceCard(Reference r)
    {
        GameObject go = new GameObject("RefCard", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(transform, false);
        go.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.16f, 0.9f);
        HorizontalLayoutGroup hl = go.GetComponent<HorizontalLayoutGroup>();
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.padding = new RectOffset(10, 10, 8, 8);
        hl.spacing = 8f;

        // source_type badge
        var badgeInfo = GetSourceTypeBadge(r.SourceType);
        TextMeshProUGUI badge = NewText("Badge", go.transform, 16, TextAlignmentOptions.Center);
        badge.text = badgeInfo.Text;
        badge.color = badgeInfo.Color;
        LayoutElement ble = badge.gameObject.AddComponent<LayoutElement>();
        ble.preferredWidth = badgeInfo.Width;
        ble.preferredHeight = 24;

        // 文件名/链接
        string nameText = string.IsNullOrEmpty(r.FilePath)
            ? (string.IsNullOrEmpty(r.SourceUrl) ? r.FullDocId : r.SourceUrl)
            : Path.GetFileNameWithoutExtension(r.FilePath);

        if (!string.IsNullOrEmpty(r.SourceUrl))
        {
            GameObject linkGo = NewButton("LinkBtn", go.transform, nameText, 16, out Button btn);
            linkGo.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            btn.onClick.AddListener(() => Application.OpenURL(r.SourceUrl));
            TextMeshProUGUI? linkText = linkGo.GetComponentInChildren<TextMeshProUGUI>();
            if (linkText != null) linkText.color = UiTheme.Link;
            RectTransform lrt = linkGo.GetComponent<RectTransform>();
            LayoutElement lle = linkGo.AddComponent<LayoutElement>();
            lle.flexibleWidth = 1f;
            lle.preferredHeight = 24;
        }
        else
        {
            TextMeshProUGUI nameLabel = NewText("Name", go.transform, 16, TextAlignmentOptions.Left);
            nameLabel.text = nameText;
            nameLabel.color = new Color(0.82f, 0.80f, 0.75f, 1f);
            LayoutElement nle = nameLabel.gameObject.AddComponent<LayoutElement>();
            nle.flexibleWidth = 1f;
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

    private TextMeshProUGUI NewText(string name, Transform parent, float size, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.fontSize = size;
        t.alignment = align;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    private GameObject NewButton(string name, Transform parent, string label, float size, out Button btn)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = UiTheme.Accent;
        btn = go.GetComponent<Button>();
        TextMeshProUGUI t = NewText("L", go.transform, size, TextAlignmentOptions.Left);
        UiFactory.Anchor(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        t.text = label;
        t.raycastTarget = false;
        return go;
    }
}