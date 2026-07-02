#pragma warning disable IDE0055, RCS1181
using UnityEngine;

namespace TaiwuEncyclopedia.UI;

public static class UiTheme
{
    public static readonly Color PanelBg = new(0.12f, 0.12f, 0.14f, 0.95f);
    public static readonly Color PlayerBubble = new(0.20f, 0.40f, 0.62f, 1.0f);
    public static readonly Color AgentBubble = new(0.22f, 0.22f, 0.26f, 1.0f);
    public static readonly Color SysText = new(0.70f, 0.70f, 0.72f, 1.0f);
    public static readonly Color Accent = new(0.90f, 0.76f, 0.40f, 1.0f);
    public static readonly Color LinkBlue = new(0.29f, 0.62f, 1.00f, 1.0f);

    // Task 6 additions
    public static readonly Color PlayerText = new(0.90f, 0.95f, 1.00f, 1f);
    public static readonly Color AgentText = new(0.94f, 0.91f, 0.80f, 1f);
    public static readonly Color ErrorText = new(0.95f, 0.35f, 0.35f, 1f);
    public static readonly Color TitleBarBg = new(0.14f, 0.16f, 0.15f, 0.98f);
    public static readonly Color Link = LinkBlue;  // Alias for easier use
}
#pragma warning restore IDE0055, RCS1181
