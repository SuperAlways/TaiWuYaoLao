using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TaiwuEncyclopedia.Frontend.UI;

/// <summary>公共 UI 工具,对标 WorldTalk 的 NativeUiResources。
/// 字体解析(打分:HasCommonChinese+SDF)、程序化组件创建、布局锚定。</summary>
public static class UiFactory
{
    private static TMP_FontAsset? _font;
    private static bool _initAttempted;

    public static TMP_FontAsset? Font
    {
        get { if (!_initAttempted) InitFont(); return _font; }
    }

    private static void InitFont()
    {
        _initAttempted = true;
        TextMeshProUGUI[] texts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        TextMeshProUGUI? best = null;
        int bestScore = int.MinValue;
        foreach (var t in texts)
        {
            if (t.font == null) continue;
            int score = ScoreFont(t.font);
            if (score > bestScore) { best = t; bestScore = score; }
        }
        if (best != null) _font = best.font;
        Debug.Log("[UiFactory] font resolved: " + (_font?.name ?? "NULL") + " score=" + bestScore);
    }

    private static int ScoreFont(TMP_FontAsset f)
    {
        string name = f.name ?? "";
        bool sdf = name.IndexOf("SDF", System.StringComparison.OrdinalIgnoreCase) >= 0;
        bool gb = name.IndexOf("GB2312", System.StringComparison.OrdinalIgnoreCase) >= 0;
        bool cn = HasCommonChinese(f);
        if (name == "Font SDF GB2312") return 1000;
        if (gb && sdf) return 950;
        if (sdf && cn) return 900;
        if (cn) return 800;
        if (sdf) return 600;
        return 100;
    }

    private static bool HasCommonChinese(TMP_FontAsset f)
    {
        try { return f.HasCharacter('的') && f.HasCharacter('太') && f.HasCharacter('吾'); }
        catch { return false; }
    }

    public static void ApplyFont(TextMeshProUGUI text)
    {
        if (_font != null) text.font = _font;
    }

    public static TextMeshProUGUI CreateText(Transform parent, string name,
        string text, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.fontSize = size;
        t.alignment = align;
        t.richText = true;
        t.color = color;
        t.text = text ?? "";
        return t;
    }

    public static Button CreateButton(Transform parent, string name,
        string label, float size, Color bgColor, out TextMeshProUGUI labelTmp)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = bgColor;
        Button btn = go.GetComponent<Button>();
        labelTmp = CreateText(go.transform, "Label", label, size,
            new Color(0.92f, 0.90f, 0.82f, 1f), TextAlignmentOptions.Center);
        labelTmp.raycastTarget = false;
        Anchor(labelTmp.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return btn;
    }

    public static void Anchor(RectTransform rt, Vector2 min, Vector2 max,
        Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
    }

    public static RectTransform CreateScroll(Transform parent, string name,
        float spacing = 4f)
    {
        GameObject scrollGo = new GameObject(name, typeof(RectTransform), typeof(Image),
            typeof(ScrollRect), typeof(RectMask2D));
        scrollGo.transform.SetParent(parent, false);
        scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.0f);
        ScrollRect scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform),
            typeof(Image));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        viewportGo.GetComponent<Image>().color = new Color(1, 1, 1, 0);
        viewportGo.AddComponent<Mask>().showMaskGraphic = false;
        Anchor(viewportGo.GetComponent<RectTransform>(),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        scrollRect.viewport = viewportGo.GetComponent<RectTransform>();

        GameObject contentGo = new GameObject("Content", typeof(RectTransform),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewportGo.transform, false);
        RectTransform crt = contentGo.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0, 1);
        crt.anchorMax = new Vector2(1, 1);
        crt.pivot = new Vector2(0.5f, 1);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = Vector2.zero;
        VerticalLayoutGroup vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.spacing = spacing;
        vlg.padding = new RectOffset(8, 8, 8, 8);
        ContentSizeFitter csf = contentGo.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = crt;
        return crt;
    }
}