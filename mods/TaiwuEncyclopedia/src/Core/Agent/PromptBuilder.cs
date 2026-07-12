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
    private string? _cachedThink;
    private string? _cachedFinal;
    private string _cachedFinalPersonaId = "";

    // 工具使用规范段（静态，引导 skill 索引在 BuildSystemPrompt 中动态追加）
    private const string _toolSpec = @"
## 工具使用规范
你有 4 个工具：retrieve_rag / load_background_skill / load_guidance_skill / lookup_concept。
- ReAct 循环最多 6 轮。你只进行分析和工具调用决策，**不要在此阶段直接给出最终回答**。
- 当你确认资料已收集足够时，调用 0 个工具，系统会自动引导你进入回答阶段。
- 检索策略：先判断需要哪类信息，再选合适工具。复杂问题可分多轮检索。
- 不要重复检索相同内容。已检索到的资料直接用。
- 正文中 [查:xxx] 标记处可调 lookup_concept 查询具体数值或相关章节。同一概念查一次即可。
- RAG 检索 (retrieve_rag) 的 mode / top_k 选择策略详见「通用回答规则」的「RAG 检索策略」段。

## 百晓册阅读策略
- 判断玩家问题涉及的方向 → 调 load_background_skill(depth=""overview"") 加载章节概述
- 概述末尾有子文件索引 → 根据索引用 depth=""detail"" + section 加载具体条目
- 按需逐层深入，不要一次加载所有章节
- 正文中 [查:xxx] 标记 → 调 lookup_concept 查询具体数值";

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
    /// 构建 Think 阶段 system prompt（纯检索助手，无 persona）。
    /// 内容完全静态，不依赖 personaId。
    /// </summary>
    public string BuildThinkPrompt()
    {
        if (_cachedThink != null) return _cachedThink;

        var parts = new StringBuilder();

        // 1. 极简身份
        parts.AppendLine(@"# 太吾绘卷AI检索助手

你是太吾绘卷的游戏知识检索助手。
你的唯一任务是：根据当前玩家问题和玩家提问历史脉络，调用工具检索相关资料。
- 不要尝试直接回答玩家问题
- 不要以任何角色口吻说话
- 当信息收集足够时，只输出简短确认（如""检索完毕""），然后调用 0 个工具
- 最终回答由后续阶段生成
- 即使对话历史中包含类似问题的回答，也必须重新检索确认——历史回答可能已过时或不完整。

");

        // 2. 工具使用规范
        parts.AppendLine(_toolSpec);
        parts.Append(BuildGuidanceIndex());

        parts.AppendLine("\n---\n");

        // 3. RAG 检索策略（从 answer-rules.md 中提取）
        var allRules = _sm.LoadAnswerRules();
        if (!string.IsNullOrEmpty(allRules))
        {
            var ragSection = ExtractSection(allRules, "RAG 检索策略");
            if (!string.IsNullOrEmpty(ragSection))
            {
                parts.AppendLine("## RAG 检索策略（retrieve_rag 工具使用指南）");
                parts.AppendLine(ragSection);
                parts.AppendLine();
            }
        }

        parts.AppendLine("---\n");

        // 4. 百晓册总纲
        var overview = _sm.LoadOverview();
        if (!string.IsNullOrEmpty(overview)) parts.AppendLine(overview);

        _cachedThink = parts.ToString();
        return _cachedThink;
    }

    /// <summary>
    /// 构建 Final 阶段 prompt（persona + 回答规则 + 格式）。
    /// 通过桥接消息注入，不替换 messages[0]。按 personaId 缓存。
    /// </summary>
    public string BuildFinalPrompt(string? personaId = null)
    {
        var pid = string.IsNullOrEmpty(personaId) ? _defaultPersonaId : personaId;
        if (_cachedFinal != null && _cachedFinalPersonaId == pid) return _cachedFinal;

        var parts = new StringBuilder();

        // 1. persona
        var persona = _sm.LoadPersona(pid!);
        if (!string.IsNullOrEmpty(persona)) parts.AppendLine(persona);
        else parts.AppendLine("# 百晓问答助手\n你是太吾绘卷的 AI 助手。");

        parts.AppendLine("\n---\n");

        // 2. 通用回答规则（信息处理 + 玩家保护，不含 RAG 检索策略）
        var allRules = _sm.LoadAnswerRules();
        if (!string.IsNullOrEmpty(allRules))
        {
            var infoSection = ExtractSection(allRules, "信息处理");
            var protectSection = ExtractSection(allRules, "玩家保护");
            if (!string.IsNullOrEmpty(infoSection))
            {
                parts.AppendLine("## 通用回答规则");
                parts.AppendLine();
                parts.AppendLine("### 信息处理");
                parts.AppendLine(infoSection);
                parts.AppendLine();
            }
            if (!string.IsNullOrEmpty(protectSection))
            {
                parts.AppendLine("### 玩家保护");
                parts.AppendLine(protectSection);
                parts.AppendLine();
            }
        }

        parts.AppendLine("---\n");

        // 3. 回答格式
        var style = _sm.LoadOutputStyle();
        if (!string.IsNullOrEmpty(style)) parts.AppendLine(style);

        _cachedFinal = parts.ToString();
        _cachedFinalPersonaId = pid;
        return _cachedFinal;
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

        // === 工具优先(LLM 对开头注意力最重,先把工具摆在前面,对齐 taiwuasker) ===

        // 1. persona(角色口吻 —— taiwuasker 把 persona 放在 prompt 开头)
        var persona = _sm.LoadPersona(pid);
        if (!string.IsNullOrEmpty(persona)) parts.AppendLine(persona);
        else parts.AppendLine("# 百晓问答助手\n你是太吾绘卷的 AI 助手。");

        parts.AppendLine("\n---\n");

        // 2. 工具使用规范 + 引导 skill 索引(LLM 必须首先知道有哪些工具可用)
        parts.AppendLine(_toolSpec);
        parts.Append(BuildGuidanceIndex());

        parts.AppendLine("\n---\n");

        // 3. 通用回答规则(含 RAG 检索策略 —— 工具之后讲怎么用)
        var rules = _sm.LoadAnswerRules();
        if (!string.IsNullOrEmpty(rules)) parts.AppendLine(rules);

        parts.AppendLine("\n---\n");

        // 4. 回答格式
        var style = _sm.LoadOutputStyle();
        if (!string.IsNullOrEmpty(style)) parts.AppendLine(style);

        parts.AppendLine("\n---\n");

        // 5. 百晓册总纲(知识背景后置 —— LLM 先知道工具有哪些,再看预加载的知识)
        var overview = _sm.LoadOverview();
        if (!string.IsNullOrEmpty(overview)) parts.AppendLine(overview);

        _cached = parts.ToString();
        _cachedPersonaId = pid;
        return _cached;
    }

    /// <summary>从 markdown 文本中提取指定 ## 标题下的内容（到下一个 ## 或文末）。</summary>
    private static string? ExtractSection(string markdown, string sectionName)
    {
        var header = $"## {sectionName}";
        var startIdx = markdown.IndexOf(header, System.StringComparison.Ordinal);
        if (startIdx < 0) return null;

        var contentStart = startIdx + header.Length;
        // 跳过标题行剩余部分（如 "（retrieve_rag 工具使用指南）"）
        var newlineIdx = markdown.IndexOf('\n', contentStart);
        if (newlineIdx < 0) return markdown.Substring(contentStart).Trim();
        contentStart = newlineIdx + 1;

        // 找下一个 ## 标题
        var nextHeaderIdx = markdown.IndexOf("\n## ", contentStart, System.StringComparison.Ordinal);
        if (nextHeaderIdx < 0)
        {
            return markdown.Substring(contentStart).Trim();
        }
        return markdown.Substring(contentStart, nextHeaderIdx - contentStart).Trim();
    }
}
