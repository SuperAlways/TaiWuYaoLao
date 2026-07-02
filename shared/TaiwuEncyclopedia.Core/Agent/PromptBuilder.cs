using System.Text;
using TaiwuEncyclopedia.Core.Skills;

namespace TaiwuEncyclopedia.Core.Agent;

/// <summary>
/// 构建静态 system prompt（三段：百晓册总纲 + persona + 工具规范，spec 第 214 行）。
/// 搬 v0.5 AgentPromptLoader。结果缓存（多次调用返回同一字符串，DeepSeek prefix caching 友好）。
/// </summary>
public sealed class PromptBuilder
{
    private readonly SkillManager _sm;
    private readonly string _defaultPersonaId;
    private string? _cached;
    private string _cachedPersonaId = "";

    // 工具使用规范段（静态，搬 v0.5 system.md 的工具规范部分）
    private const string _toolSpec = @"
## 工具使用规范
你有 4 个工具：retrieve_rag / load_background_skill / load_guidance_skill / lookup_concept。
- ReAct 循环最多 6 轮，每轮可选调工具或直接回答。
- 检索策略：先判断需要哪类信息，再选合适工具。复杂问题可分多轮检索。
- 不要重复检索相同内容。已检索到的资料直接用。
- 正文中 [查:xxx] 标记处可调 lookup_concept 查询具体数值或相关章节。同一概念查一次即可。
- 最终回答时以选中 persona 的口吻给出。";

    /// <summary>
    /// 初始化 PromptBuilder。
    /// </summary>
    /// <param name="skillManager">技能管理器。</param>
    /// <param name="defaultPersonaId">默认 persona ID。</param>
    public PromptBuilder(SkillManager skillManager, string defaultPersonaId = "ring-elder")
    {
        _sm = skillManager;
        _defaultPersonaId = defaultPersonaId;
    }

    /// <summary>
    /// 构建完整 system prompt。personaId 为空时用默认 persona。结果缓存。
    /// </summary>
    /// <param name="personaId">persona ID（可选）。</param>
    /// <returns>完整的 system prompt 字符串。</returns>
    public string BuildSystemPrompt(string? personaId = null)
    {
        var pid = string.IsNullOrEmpty(personaId) ? _defaultPersonaId : personaId;
        // 缓存只对同一 personaId 生效
        if (_cached != null && _cachedPersonaId == pid) return _cached;

        var parts = new StringBuilder();

        // 1. 百晓册总纲（第一段，常驻世界背景知识）
        var zongang = LoadZongang();
        parts.AppendLine("## 百晓册总纲（全文常驻）");
        parts.AppendLine(zongang);

        parts.AppendLine("\n---\n");

        // 2. persona（第二段，当前选中的对话风格）
        var persona = _sm.LoadPersona(pid);
        if (!string.IsNullOrEmpty(persona))
        {
            parts.AppendLine(persona);
        }

        parts.AppendLine("\n---\n");

        // 3. 工具使用规范（第三段）
        parts.AppendLine(_toolSpec);

        _cached = parts.ToString();
        _cachedPersonaId = pid;
        return _cached;
    }

    /// <summary>
    /// 读百晓册总纲。v1.0 从 background/overview.md 独立文件加载（spec 9.1）。
    /// </summary>
    private string LoadZongang()
    {
        var overview = _sm.LoadOverview();
        return overview ?? "（百晓册总纲未找到，请检查 Skills/background/overview.md 配置。）";
    }
}
