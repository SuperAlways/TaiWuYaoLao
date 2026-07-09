#pragma warning disable CA1031, CA1062, IDE0008, IDE0011, IDE0051, RCS1124, RCS1213
using System;
using TMPro;
using UnityEngine;
using TaiwuEncyclopedia.Markdown;

namespace TaiwuEncyclopedia.UI;

public class MarkdownBinder : MonoBehaviour
{
    private TMP_Text? _target;
    private Camera? _uiCamera;

    public static MarkdownBinder Bind(TMP_Text target, string markdown)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        MarkdownBinder binder = target.gameObject.GetComponent<MarkdownBinder>()
                     ?? target.gameObject.AddComponent<MarkdownBinder>();
        binder._target = target;
        binder._uiCamera = target.canvas?.worldCamera;
        binder.Rebind(markdown);
        return binder;
    }

    public void Rebind(string markdown)
    {
        if (_target == null)
        {
            return;
        }
        string parsed = MarkdownParser.Parse(markdown) ?? markdown;
        _target.text = parsed;
    }

    private void Update()
    {
        if (_target == null || !Input.GetMouseButtonDown(0))
        {
            return;
        }

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(_target, Input.mousePosition, _uiCamera);
        if (linkIndex < 0)
        {
            return;
        }

        TMP_LinkInfo linkInfo = _target.textInfo.linkInfo[linkIndex];
        string url = linkInfo.GetLinkID();
        if (!string.IsNullOrEmpty(url))
        {
            try
            {
                Application.OpenURL(url);
            }
            catch (Exception e)
            {
                Debug.LogError($"[TaiwuEncyclopedia] Failed to open URL: {url}. Error: {e}");
            }
        }
    }
}
#pragma warning restore CA1031, CA1062, IDE0008, IDE0011, IDE0051, RCS1124, RCS1213
