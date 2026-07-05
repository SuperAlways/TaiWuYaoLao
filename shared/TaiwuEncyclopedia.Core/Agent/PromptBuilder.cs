using System.Text;
using TaiwuEncyclopedia.Core.Skills;

namespace TaiwuEncyclopedia.Core.Agent;

/// <summary>
/// 构建静态 system prompt（五段：百晓册总纲 + 通用回答规则 + 回答格式 + 工具规范 + persona）。
/// 结果缓存（按 personaId，DeepSeek prefix caching 友好）。
/// </summary>
public sealed class PromptBuilder
{
    private readonly SkillManager _sm;
    private readonly string _defaultPersonaId;
    private string? _cached;
    private string _cachedPersonaId = "";

    // 工具使用规范段（静态，引导 skill 索引在 BuildSystemPrompt 中动态追加）
    private const string _toolSpec = @"
## 工具使用规范
你有 4 个工具：retrieve_rag / load_background_skill / load_guidance_skill / lookup_concept。
- ReAct 循环最多 6 轮，每轮可选调工具或直接回答。
- 检索策略：先判断需要哪类信息，再选合适工具。复杂问题可分多轮检索。
- 不要重复检索相同内容。已检索到的资料直接用。
- 正文中 [查:xxx] 标记处可调 lookup_concept 查询具体数值或相关章节。同一概念查一次即可。
- 最终回答时以选中 persona 的口吻给出。
- RAG 检索 (retrieve_rag) 的 mode / top_k 选择策略详见「通用回答规则」的「RAG 检索策略」段。";

    /// <summary>构建引导 skill 索引段（10 个 skill，从 registry 动态生成）。</summary>
    private string BuildGuidanceIndex()
    {
        var ids = _sm.GetGuidanceEnum();
        if (ids is not { Count: > 0 }) return "";
        var sb = new StringBuilder();
        sb.AppendLine("\n---\n");
        sb.AppendLine("## 引导骨架索引\n");
        foreach (var id in ids)
        {
            var cn = _sm.GuidanceCnName(id);
            sb.AppendLine($"- `{id}`: {cn}");
        }
        sb.AppendLine("\n判断玩家问题属于以上某类时，调 load_guidance_skill(skill=\"{id}\") 加载对应骨架，按骨架组织思考和检索策略。");
        return sb.ToString();
    }

    /// <summary>
    /// 初始化 PromptBuilder。
    /// </summary>
    /// <param name="skillManager">技能管理器。</param>
    /// <param name="defaultPersonaId">默认 persona ID。</param>
    public PromptBuilder(SkillManager skillManager, string defaultPersonaId = "sword-will")
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
        if (_cached != null && _cachedPersonaId == pid) return _cached;

        var parts = new StringBuilder();

        // 1. 百晓册总纲
        var overview = _sm.LoadOverview();
        parts.AppendLine(overview ?? "## 百晓册总纲\n（总纲未就绪）");

        parts.AppendLine("\n---\n");

        // 2. 通用回答规则
        var rules = _sm.LoadAnswerRules();
        if (!string.IsNullOrEmpty(rules)) parts.AppendLine(rules);

        parts.AppendLine("\n---\n");

        // 3. 回答格式
        var style = _sm.LoadOutputStyle();
        if (!string.IsNullOrEmpty(style)) parts.AppendLine(style);

        parts.AppendLine("\n---\n");

        // 4. 工具使用规范
        parts.AppendLine(_toolSpec);

        // 4b. 引导 skill 索引
        parts.Append(BuildGuidanceIndex());

        parts.AppendLine("\n---\n");

        // 5. persona
        var persona = _sm.LoadPersona(pid);
        if (!string.IsNullOrEmpty(persona)) parts.AppendLine(persona);

        _cached = parts.ToString();
        _cachedPersonaId = pid;
        return _cached;
    }
}
