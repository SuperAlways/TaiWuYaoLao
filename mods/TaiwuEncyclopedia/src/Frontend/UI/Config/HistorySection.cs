#pragma warning disable CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, IDE0010, IDE0022, IDE0048, RCS1123, CA1307, RCS1238, CA1852, CS0414, RCS1201, RCS1124, IDE0057, CA1031, RCS1001, IDE0370, RCS1085, IDE0028, RCS1161
using System;
using System.Collections;
using System.Collections.Generic;
using TaiwuEncyclopedia.Core.Session;
using TaiwuEncyclopedia.Frontend.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// 历史对话区：可滚动历史列表 + 重命名弹窗。
/// 由 ConfigPanel 组合使用。
/// </summary>
public sealed class HistorySection : MonoBehaviour
{
    private RectTransform? _historyListContent;
    private List<GameObject>? _historyItems;
    private TMP_FontAsset? _font;
    private RectTransform? _rootRt;

    /// <summary>构建历史对话区 UI 并挂载到 content 下。</summary>
    public void Build(Transform content, TMP_FontAsset? font)
    {
        _font = font;
        _rootRt = content.root.GetComponent<RectTransform>();

        GameObject section = CreateSection(content, "历史对话");
        Transform inner = section.transform.Find("Content")!;

        // 历史列表 (可滚动)
        GameObject listBox = new GameObject("ListBox", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
        listBox.transform.SetParent(inner, false);
        listBox.GetComponent<Image>().color = new Color(0, 0, 0, 0.18f);
        LayoutElement lle = listBox.AddComponent<LayoutElement>();
        lle.preferredHeight = 140;

        ScrollRect scroll = listBox.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 20f;

        GameObject listContentGo = new GameObject("ListContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        _historyListContent = listContentGo.GetComponent<RectTransform>();
        _historyListContent.SetParent(listBox.transform, false);
        _historyListContent.anchorMin = new Vector2(0, 1);
        _historyListContent.anchorMax = new Vector2(1, 1);
        _historyListContent.pivot = new Vector2(0.5f, 1);
        _historyListContent.sizeDelta = new Vector2(0, 0);
        VerticalLayoutGroup vlg = listContentGo.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.spacing = 6f;
        vlg.padding = new RectOffset(8, 8, 8, 8);
        ContentSizeFitter csf = listContentGo.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = _historyListContent;
        scroll.viewport = listBox.GetComponent<RectTransform>();

        _historyItems = new List<GameObject>();
    }

    /// <summary>刷新历史列表。</summary>
    public void Refresh()
    {
        RefreshHistoryList();
    }

    // ========== 列表刷新 ==========

    private void RefreshHistoryList()
    {
        if (_historyListContent == null || _historyItems == null) return;

        // 清空现有
        foreach (GameObject go in _historyItems)
            Destroy(go);
        _historyItems.Clear();

        // 异步加载历史列表
        StartCoroutine(LoadHistoryListCoroutine());
    }

    private IEnumerator LoadHistoryListCoroutine()
    {
        var task = FrontendServices.SessionManager.ListConversationsAsync();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted || task.IsCanceled || _historyListContent == null || _historyItems == null) yield break;

        List<ConversationMeta> metas = task.Result;
        if (metas.Count == 0)
        {
            // 显示空提示
            TextMeshProUGUI empty = UiFactory.CreateText(_historyListContent, "EmptyHint",
                "(暂无历史对话)", 16,
                new Color(0.45f, 0.48f, 0.50f, 1f), TextAlignmentOptions.Center);
            _historyItems.Add(empty.gameObject);
            yield break;
        }

        foreach (ConversationMeta meta in metas)
        {
            GameObject item = BuildHistoryItem(meta);
            _historyItems.Add(item);
        }
    }

    private GameObject BuildHistoryItem(ConversationMeta meta)
    {
        GameObject item = new GameObject($"HistoryItem_{meta.WorldId}", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        item.transform.SetParent(_historyListContent!, false);
        item.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.10f, 0.95f);
        LayoutElement ile = item.AddComponent<LayoutElement>();
        ile.preferredHeight = 38;

        HorizontalLayoutGroup hlg = item.GetComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.spacing = 10f;
        hlg.padding = new RectOffset(10, 10, 0, 0);

        // WorldId + 显示名
        string displayName = !string.IsNullOrEmpty(meta.Name) ? meta.Name
            : !string.IsNullOrEmpty(meta.AutoName) ? meta.AutoName
            : $"WorldId#{meta.WorldId}";

        string worldLabel = meta.WorldId == SessionManager.PregameWorldId ? "主界面" : $"WorldId#{meta.WorldId}";

        TextMeshProUGUI nameText = UiFactory.CreateText(item.transform, "Name",
            $"{worldLabel} 「{displayName}」", 16,
            new Color(0.85f, 0.83f, 0.78f, 1f), TextAlignmentOptions.Left);
        LayoutElement nle = nameText.gameObject.AddComponent<LayoutElement>();
        nle.flexibleWidth = 1f;

        // 条数
        TextMeshProUGUI countText = UiFactory.CreateText(item.transform, "Count",
            $"{meta.Count}条", 15,
            new Color(0.55f, 0.58f, 0.60f, 1f), TextAlignmentOptions.Right);
        LayoutElement cle = countText.gameObject.AddComponent<LayoutElement>();
        cle.preferredWidth = 50;

        // 重命名按钮
        Button renameBtn = UiFactory.CreateButton(item.transform, "RenameBtn",
            "重命名", 15,
            new Color(0.25f, 0.30f, 0.32f, 0.95f), out _);
        LayoutElement rle = renameBtn.gameObject.AddComponent<LayoutElement>();
        rle.preferredWidth = 70;
        rle.preferredHeight = 28;
        int capturedWorldId = meta.WorldId;
        string capturedName = displayName;
        renameBtn.onClick.AddListener(delegate { OnRenameConversation(capturedWorldId, capturedName); });

        return item;
    }

    // ========== 重命名 ==========

    private void OnRenameConversation(int worldId, string currentName)
    {
        ShowRenamePopup(worldId, currentName);
    }

    /// <summary>模态重命名弹窗:输入框预填当前名,确定→RenameConversationAsync,取消→关闭。</summary>
    private void ShowRenamePopup(int worldId, string currentName)
    {
        if (_rootRt == null) return;
        Transform parent = _rootRt;

        // 遮罩层(半透明,拦截背景点击)
        GameObject overlay = new GameObject("RenameOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(parent, false);
        overlay.GetComponent<Image>().color = new Color(0, 0, 0, 0.55f);
        RectTransform ort = overlay.GetComponent<RectTransform>();
        ort.anchorMin = Vector2.zero;
        ort.anchorMax = Vector2.one;
        ort.sizeDelta = Vector2.zero;

        // 对话框
        GameObject dialog = new GameObject("RenameDialog", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        dialog.transform.SetParent(overlay.transform, false);
        dialog.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.20f, 1f);
        RectTransform drt = dialog.GetComponent<RectTransform>();
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
        drt.sizeDelta = new Vector2(440, 190);
        drt.pivot = new Vector2(0.5f, 0.5f);
        VerticalLayoutGroup dlg = dialog.GetComponent<VerticalLayoutGroup>();
        dlg.childForceExpandWidth = true;
        dlg.childForceExpandHeight = false;
        dlg.childControlWidth = true;
        dlg.childControlHeight = true;
        dlg.spacing = 12f;
        dlg.padding = new RectOffset(20, 20, 18, 18);

        UiFactory.CreateText(dialog.transform, "Title",
            "重命名对话", 20,
            new Color(0.92f, 0.90f, 0.82f, 1f), TextAlignmentOptions.Left);

        // 输入框
        GameObject inputGo = new GameObject("RenameInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputGo.transform.SetParent(dialog.transform, false);
        inputGo.GetComponent<Image>().color = new Color(1, 1, 1, 0.10f);
        LayoutElement ile = inputGo.AddComponent<LayoutElement>();
        ile.preferredHeight = 40;
        TMP_InputField input = inputGo.GetComponent<TMP_InputField>();
        TextMeshProUGUI textArea = UiFactory.CreateText(inputGo.transform, "Text",
            "", 18,
            new Color(0.92f, 0.90f, 0.82f, 1f), TextAlignmentOptions.Left);
        UiFactory.Anchor(textArea.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 4), new Vector2(-10, -4));
        input.textViewport = textArea.rectTransform;
        input.textComponent = textArea;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.Standard;
        TextMeshProUGUI ph = UiFactory.CreateText(inputGo.transform, "Placeholder",
            "输入新名称", 18,
            new Color(0.5f, 0.52f, 0.55f, 0.7f), TextAlignmentOptions.Left);
        UiFactory.Anchor(ph.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 4), new Vector2(-10, -4));
        ph.raycastTarget = false;
        input.placeholder = ph;
        input.text = currentName ?? "";
        input.caretPosition = (currentName ?? "").Length;

        // 按钮行
        GameObject btnRow = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow.transform.SetParent(dialog.transform, false);
        HorizontalLayoutGroup bhlg = btnRow.GetComponent<HorizontalLayoutGroup>();
        bhlg.childForceExpandWidth = true;
        bhlg.childForceExpandHeight = false;
        bhlg.childControlWidth = true;
        bhlg.childControlHeight = true;
        bhlg.spacing = 12f;
        bhlg.childAlignment = TextAnchor.MiddleRight;

        Button cancelBtn = UiFactory.CreateButton(btnRow.transform, "CancelBtn",
            "取消", 18,
            new Color(0.25f, 0.28f, 0.30f, 1f), out _);
        LayoutElement cle = cancelBtn.gameObject.AddComponent<LayoutElement>();
        cle.preferredWidth = 90;
        cle.preferredHeight = 36;
        cancelBtn.onClick.AddListener(() => { input.DeactivateInputField(); Destroy(overlay); });

        Button okBtn = UiFactory.CreateButton(btnRow.transform, "OkBtn",
            "确定", 18,
            new Color(0.20f, 0.40f, 0.30f, 1f), out _);
        LayoutElement okle = okBtn.gameObject.AddComponent<LayoutElement>();
        okle.preferredWidth = 90;
        okle.preferredHeight = 36;
        okBtn.onClick.AddListener(() =>
        {
            string newName = (input.text ?? "").Trim();
            input.DeactivateInputField();
            Destroy(overlay);
            StartCoroutine(RenameCoroutine(worldId, newName));
        });

        // 输入框获焦
        input.ActivateInputField();
    }

    private IEnumerator RenameCoroutine(int worldId, string newName)
    {
        var task = FrontendServices.SessionManager.RenameConversationAsync(worldId, newName);
        yield return new WaitUntil(() => task.IsCompleted);
        RefreshHistoryList();
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
