#pragma warning disable CA1711, IDE0011, CA1062, IDE0008
using System.Collections.Generic;

namespace TaiwuEncyclopedia.UI;

/// <summary>
/// 面板导航栈。Push 时隐藏当前顶层面板并显示新面板；Pop 时隐藏当前面板并重新显示上层面板。
/// </summary>
public static class PanelStack
{
    private static readonly Stack<IPanel> _stack = new();

    /// <summary>
    /// 是否有面板打开。
    /// </summary>
    public static bool AnyOpen => _stack.Count > 0;

    /// <summary>
    /// 推入面板到栈顶。
    /// </summary>
    public static void Push(IPanel panel)
    {
        if (_stack.Count > 0) _stack.Peek().Hide();
        _stack.Push(panel);
        panel.Show();
    }

    /// <summary>
    /// 从栈顶弹出面板。
    /// </summary>
    public static void Pop()
    {
        if (_stack.Count == 0) return;
        var panel = _stack.Pop();
        panel.Hide();
        if (_stack.Count > 0) _stack.Peek().Show();
    }
}
#pragma warning restore CA1711, IDE0011, CA1062, IDE0008
