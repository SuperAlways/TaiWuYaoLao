#pragma warning disable CS8604, CS8618, IDE0008, IDE0011, RCS1181, IDE0090, IDE0031, RCS1146, IDE0058, IDE0074, RCS1048, CA1822, CA1812, IDE0051, IDE0052, CA1001, CA2012, IDE0055, IDE0110, IDE0010, IDE0022, IDE0048, RCS1123, CA1307, RCS1238, CA1852, CS0414, RCS1201, RCS1124, IDE0057, CA1031, RCS1001, IDE0370, RCS1085, IDE0028, RCS1161
using System;
using System.Collections;
using System.IO;
using System.Text;
using TaiwuEncyclopedia.Core.Session;
using TaiwuEncyclopedia.Core.Soul;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TaiwuEncyclopedia.Frontend.UI;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// 数据与日志区：存档路径 + 打开目录/日志按钮 + SoulProfile/SoulWorld 折叠 + 重置/清除按钮。
/// 由 ConfigPanel 组合使用。
/// </summary>
public sealed class DataSection : MonoBehaviour
{
    private TMP_FontAsset? _font;

    private TextMeshProUGUI? _runtimePathText;
    private TextMeshProUGUI? _soulProfileText;
    private TextMeshProUGUI? _soulWorldText;
    private bool _soulProfileExpanded;
    private bool _soulWorldExpanded;

    /// <summary>打开日志回调，由 ConfigPanel 连接到 PlayerLogViewer。</summary>
    public event Action? OnOpenLog;

    /// <summary>构建数据与日志区 UI 并挂载到 content 下。</summary>
    public void Build(Transform content, TMP_FontAsset? font)
    {
        _font = font;

        GameObject section = CreateSection(content, "数据与日志");
        Transform inner = section.transform.Find("Content")!;

        // 路径显示
        _runtimePathText = UiFactory.CreateText(inner, "PathText",
            "存档目录：加载中...", 15,
            new Color(0.55f, 0.58f, 0.60f, 1f), TextAlignmentOptions.Left);
        _runtimePathText.enableWordWrapping = true;

        // 按钮行1: 打开存档目录 + 打开日志
        GameObject btnRow1 = new GameObject("BtnRow1", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow1.transform.SetParent(inner, false);
        HorizontalLayoutGroup hlg1 = btnRow1.GetComponent<HorizontalLayoutGroup>();
        hlg1.childForceExpandWidth = false;
        hlg1.childForceExpandHeight = false;
        hlg1.childControlWidth = true;
        hlg1.childControlHeight = true;
        hlg1.spacing = 10f;
        hlg1.padding = new RectOffset(0, 0, 6, 6);

        Button openDirBtn = UiFactory.CreateButton(btnRow1.transform, "OpenDirBtn",
            "打开存档目录", 17, UiTheme.Accent, out _);
        LayoutElement odle = openDirBtn.gameObject.AddComponent<LayoutElement>();
        odle.preferredWidth = 150;
        odle.preferredHeight = 34;
        openDirBtn.onClick.AddListener(OnOpenRuntimeDir);

        Button openLogBtn = UiFactory.CreateButton(btnRow1.transform, "OpenLogBtn",
            "打开日志", 17, UiTheme.Accent, out _);
        LayoutElement olle = openLogBtn.gameObject.AddComponent<LayoutElement>();
        olle.preferredWidth = 110;
        olle.preferredHeight = 34;
        openLogBtn.onClick.AddListener(OnOpenLogsDir);

        // SoulProfile (可折叠)
        BuildSoulCollapsible(inner, "SoulProfile (跨档全局)", isProfile: true);

        // SoulWorld (可折叠)
        BuildSoulCollapsible(inner, "SoulWorld (当前档)", isProfile: false);

        // 底部按钮行: 重置 SoulProfile + 清除当前对话历史
        GameObject btnRow2 = new GameObject("BtnRow2", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow2.transform.SetParent(inner, false);
        HorizontalLayoutGroup hlg2 = btnRow2.GetComponent<HorizontalLayoutGroup>();
        hlg2.childForceExpandWidth = false;
        hlg2.childForceExpandHeight = false;
        hlg2.childControlWidth = true;
        hlg2.childControlHeight = true;
        hlg2.spacing = 10f;
        hlg2.padding = new RectOffset(0, 0, 6, 2);

        Button resetProfileBtn = UiFactory.CreateButton(btnRow2.transform, "ResetProfileBtn",
            "重置 SoulProfile", 17, UiTheme.Accent, out _);
        LayoutElement rple = resetProfileBtn.gameObject.AddComponent<LayoutElement>();
        rple.preferredWidth = 160;
        rple.preferredHeight = 34;
        resetProfileBtn.onClick.AddListener(OnResetSoulProfile);

        Button clearHistoryBtn = UiFactory.CreateButton(btnRow2.transform, "ClearHistoryBtn",
            "清除当前对话历史", 17, UiTheme.Accent, out _);
        LayoutElement chle = clearHistoryBtn.gameObject.AddComponent<LayoutElement>();
        chle.preferredWidth = 170;
        chle.preferredHeight = 34;
        clearHistoryBtn.onClick.AddListener(OnClearCurrentHistory);
    }

    /// <summary>刷新所有数据区内容。</summary>
    public void Refresh()
    {
        RefreshRuntimePath();
        RefreshSoulProfile();
        RefreshSoulWorld();
    }

    // ========== Soul 折叠区 ==========

    private void BuildSoulCollapsible(Transform parent, string title, bool isProfile)
    {
        GameObject box = new GameObject(isProfile ? "SoulProfileBox" : "SoulWorldBox",
            typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        box.transform.SetParent(parent, false);
        box.GetComponent<Image>().color = new Color(0, 0, 0, 0.12f);
        VerticalLayoutGroup vlg = box.GetComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(8, 8, 6, 8);

        // 标题 (可点击)
        GameObject headerGo = new GameObject("Header", typeof(RectTransform), typeof(Button));
        headerGo.transform.SetParent(box.transform, false);
        Button headerBtn = headerGo.GetComponent<Button>();
        LayoutElement hle = headerGo.AddComponent<LayoutElement>();
        hle.preferredHeight = 26;

        TextMeshProUGUI headerText = UiFactory.CreateText(headerGo.transform, "HeaderText",
            $"{title} >>展开", 17,
            new Color(0.70f, 0.72f, 0.75f, 1f), TextAlignmentOptions.Left);
        UiFactory.Anchor(headerText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // 内容 (初始隐藏)
        GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        contentGo.transform.SetParent(box.transform, false);
        contentGo.SetActive(false);

        TextMeshProUGUI bodyText = UiFactory.CreateText(contentGo.transform, "BodyText",
            "加载中...", 15,
            new Color(0.75f, 0.78f, 0.82f, 1f), TextAlignmentOptions.TopLeft);
        bodyText.enableWordWrapping = true;
        LayoutElement ble = bodyText.gameObject.AddComponent<LayoutElement>();
        ble.minHeight = 60;

        if (isProfile)
            _soulProfileText = bodyText;
        else
            _soulWorldText = bodyText;

        headerBtn.onClick.AddListener(delegate {
            if (isProfile)
            {
                _soulProfileExpanded = !_soulProfileExpanded;
                contentGo.SetActive(_soulProfileExpanded);
                headerText.text = title + (_soulProfileExpanded ? " >>收起" : " >>展开");
                if (_soulProfileExpanded) RefreshSoulProfile();
            }
            else
            {
                _soulWorldExpanded = !_soulWorldExpanded;
                contentGo.SetActive(_soulWorldExpanded);
                headerText.text = title + (_soulWorldExpanded ? " >>收起" : " >>展开");
                if (_soulWorldExpanded) RefreshSoulWorld();
            }
        });
    }

    // ========== 刷新 ==========

    private void RefreshRuntimePath()
    {
        if (_runtimePathText != null)
            _runtimePathText.text = $"存档目录：{Bootstrap.RuntimeRoot}";
    }

    private void RefreshSoulProfile()
    {
        if (_soulProfileText == null) return;
        StartCoroutine(LoadSoulProfileCoroutine());
    }

    private IEnumerator LoadSoulProfileCoroutine()
    {
        var store = FrontendServices.SoulStore;
        var task = store.LoadProfileAsync();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted || task.IsCanceled || _soulProfileText == null) yield break;

        SoulProfile profile = task.Result;
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("跨档全局 SoulProfile：");
        sb.AppendLine($"  玩法偏好：{(string.IsNullOrEmpty(profile.Playstyle) ? "(未设置)" : profile.Playstyle)}");
        sb.AppendLine($"  技术水平：{(string.IsNullOrEmpty(profile.TechnicalLevel) ? "(未设置)" : profile.TechnicalLevel)}");
        sb.AppendLine($"  提问习惯：{(string.IsNullOrEmpty(profile.QuestionHabits) ? "(未设置)" : profile.QuestionHabits)}");
        sb.AppendLine($"  保护字段：{(profile.ProtectedFields.Count == 0 ? "(无)" : string.Join(", ", profile.ProtectedFields))}");

        _soulProfileText.text = sb.ToString();
    }

    private void RefreshSoulWorld()
    {
        if (_soulWorldText == null) return;

        int worldId = WorldIdReader.CurrentWorldId();
        if (worldId == SessionManager.PregameWorldId)
        {
            _soulWorldText.text = "进入存档后可用";
            return;
        }

        StartCoroutine(LoadSoulWorldCoroutine(worldId));
    }

    private IEnumerator LoadSoulWorldCoroutine(int worldId)
    {
        var store = FrontendServices.SoulStore;
        var task = store.LoadWorldAsync(worldId);
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted || task.IsCanceled || _soulWorldText == null) yield break;

        SoulWorld world = task.Result;
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"当前档 SoulWorld (WorldId#{worldId})：");
        sb.AppendLine($"  门派：{(string.IsNullOrEmpty(world.Sect) ? "(未设置)" : world.Sect)}");
        sb.AppendLine($"  阶段：{(string.IsNullOrEmpty(world.Stage) ? "(未设置)" : world.Stage)}");
        sb.AppendLine($"  失败经历：{(string.IsNullOrEmpty(world.Failures) ? "(未设置)" : world.Failures)}");
        sb.AppendLine($"  历史摘要：{(string.IsNullOrEmpty(world.Summary) ? "(未设置)" : world.Summary)}");

        _soulWorldText.text = sb.ToString();
    }

    // ========== 按钮回调 ==========

    private void OnOpenRuntimeDir()
    {
        try
        {
            string path = Bootstrap.RuntimeRoot;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Application.OpenURL(path);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[TaiwuEncyclopedia] Failed to open runtime dir: {e}");
        }
    }

    private void OnOpenLogsDir()
    {
        // 打开日志 — 通过事件委托给 ConfigPanel 编排层
        OnOpenLog?.Invoke();
    }

    private void OnResetSoulProfile()
    {
        StartCoroutine(ResetSoulProfileCoroutine());
    }

    private IEnumerator ResetSoulProfileCoroutine()
    {
        var store = FrontendServices.SoulStore;
        var task = store.SaveProfileAsync(new SoulProfile());
        yield return new WaitUntil(() => task.IsCompleted);

        if (_soulProfileExpanded) RefreshSoulProfile();
    }

    private void OnClearCurrentHistory()
    {
        int worldId = WorldIdReader.CurrentWorldId();
        StartCoroutine(ClearHistoryCoroutine(worldId));
    }

    private IEnumerator ClearHistoryCoroutine(int worldId)
    {
        var session = FrontendServices.SessionManager;
        var task = session.ClearAsync(worldId);
        yield return new WaitUntil(() => task.IsCompleted);

        // 同时清除 SoulWorld (仅档内)
        if (worldId != SessionManager.PregameWorldId)
        {
            var store = FrontendServices.SoulStore;
            var task2 = store.SaveWorldAsync(worldId, new SoulWorld());
            yield return new WaitUntil(() => task2.IsCompleted);
        }

        if (_soulWorldExpanded) RefreshSoulWorld();
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
