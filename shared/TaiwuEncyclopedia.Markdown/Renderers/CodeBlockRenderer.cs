namespace TaiwuEncyclopedia.Markdown.Renderers;

/// <summary>代码块（``` 围栏）→ 等宽 + mark 背景。</summary>
public static class CodeBlockRenderer
{
    /// <summary>渲染代码块内容行（每行包 mark）。</summary>
    public static string Render(string codeContent)
    {
        return "<mark>" + codeContent + "</mark>";
    }
}
