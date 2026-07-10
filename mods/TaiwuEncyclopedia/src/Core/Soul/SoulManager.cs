using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TaiwuEncyclopedia.Core.Agent;
using TaiwuEncyclopedia.Core.Diagnostics;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Session;
using TaiwuEncyclopedia.Core.Storage;

namespace TaiwuEncyclopedia.Core.Soul;

/// <summary>
/// 玩家画像管理：L2 摘要时提取 + 更新，玩家主动填时标记保护。
/// 搬 v0.5 SoulManager，但拆 SoulProfile（跨档）+ SoulWorld（按 WorldId）。
/// </summary>
public sealed class SoulManager
{
    private readonly ISoulStore _store;

    private static readonly string[] _profileFields = { "Playstyle", "TechnicalLevel", "QuestionHabits" };
    private static readonly string[] _worldFields = { "Sect", "Stage", "Failures" };

    private static readonly Dictionary<string, string> _fieldCn = new()
    {
        ["Playstyle"] = "玩法偏好",
        ["TechnicalLevel"] = "技术水平",
        ["QuestionHabits"] = "提问习惯",
        ["Sect"] = "门派",
        ["Stage"] = "游戏阶段",
        ["Failures"] = "失败经历",
    };

    private const string _extractPrompt = @"请分析以下对话历史，提取玩家状态信息，并压缩历史为摘要。

【旧摘要】（如有，请在此基础上更新）
{old_summary}

【新对话】
{history}

请返回 JSON（不要 markdown 代码块），格式如下：
{""summary"": ""对话摘要"", ""profile_fields"": {""Playstyle"": """", ""TechnicalLevel"": """", ""QuestionHabits"": """"}, ""world_fields"": {""Sect"": """", ""Stage"": """", ""Failures"": """"}}

注意：
- 如果提供了旧摘要，请融合新旧内容生成更新后的摘要
- 摘要核心目的是把握玩家的提问脉络和回答方向：玩家主要问了哪些方向的问题、AI 给了什么方向的指引、得出了什么结论
- 摘要长度按对话信息量自适应，不设硬性字数上限，但应保持精炼、不堆砌原文
- 只填写对话中明确提到的字段，不确定的留空字符串
- Playstyle: 玩法偏好（如苟道流、速通、全收集）
- TechnicalLevel: 技术水平（如新手、老手、精通）
- QuestionHabits: 提问习惯
- Sect: 门派名
- Stage: 游戏阶段
- Failures: 失败经历简述";

    /// <summary>
    /// 创建 SoulManager 实例。
    /// </summary>
    /// <param name="store">Soul 存储接口</param>
    public SoulManager(ISoulStore store)
    {
        _store = store;
    }

    /// <summary>获取 soul 摘要文本，用于注入 messages。无数据时返回空字符串。</summary>
    public async Task<string> GetSoulSummaryAsync(int worldId)
    {
        var profile = await _store.LoadProfileAsync();
        var world = await _store.LoadWorldAsync(worldId);
        var parts = new List<string>();

        foreach (var field in _profileFields)
        {
            var val = typeof(SoulProfile).GetProperty(field)?.GetValue(profile) as string;
            if (!string.IsNullOrEmpty(val))
            {
                parts.Add($"{_fieldCn[field]}: {val}");
            }
        }
        foreach (var field in _worldFields)
        {
            var val = typeof(SoulWorld).GetProperty(field)?.GetValue(world) as string;
            if (!string.IsNullOrEmpty(val))
            {
                parts.Add($"{_fieldCn[field]}: {val}");
            }
        }
        return string.Join("；", parts);
    }

    /// <summary>
    /// L2 摘要时从被压缩的历史中提取玩家状态，更新 soul。
    /// 返回历史摘要文本（用于 messages 重建）。LLM 失败时不崩溃，返回空摘要。
    /// </summary>
    public async Task<string> UpdateFromCompressAsync(
        int worldId,
        string earlyHistoryText,
        ILlmClient llmClient,
        LlmConfig llmConfig,
        string? oldSummary = null,
        IAgentTrace? trace = null)
    {
        var profile = await _store.LoadProfileAsync();
        var world = await _store.LoadWorldAsync(worldId);
        var protectedSet = new HashSet<string>(profile.ProtectedFields);

        string summary;
        try
        {
            var oldSummarySection = string.IsNullOrEmpty(oldSummary) ? "（无）" : oldSummary;
            var prompt = _extractPrompt
                .Replace("{old_summary}", oldSummarySection)
                .Replace("{history}", earlyHistoryText); // 不截断，全文传入
            var promptMsgs = new List<LlmMessage> { new() { Role = "user", Content = prompt } };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            trace?.LlmCall(0, "intent", "soul_extract", promptMsgs, null);
            var response = await llmClient.ChatAsync(
                AgentLLMRole.Intent,
                llmConfig,
                promptMsgs);
            sw.Stop();
            trace?.LlmResponse(0, "intent", response.Content, null, "stop",
                response.Usage, (int)sw.ElapsedMilliseconds);
            var parsed = JObject.Parse(response.Content ?? "{}");
            summary = parsed["summary"]?.ToString() ?? "";

            // 合并 profile 字段（protected 不覆盖）
            var profileFields = parsed["profile_fields"] as JObject;
            if (profileFields != null)
            {
                foreach (var field in _profileFields)
                {
                    var val = profileFields[field]?.ToString();
                    if (!string.IsNullOrEmpty(val))
                    {
                        if (protectedSet.Contains(field) && !string.IsNullOrEmpty(
                            typeof(SoulProfile).GetProperty(field)?.GetValue(profile) as string))
                        {
                            continue; // 保护玩家填的字段
                        }
                        typeof(SoulProfile).GetProperty(field)?.SetValue(profile, val);
                    }
                }
            }

            // 合并 world 字段（SoulWorld 无 protected，直接覆盖）
            // 主界面（PregameWorldId）跳过 world 字段——无档内情境，不写 World--1.json
            if (worldId != SessionManager.PregameWorldId)
            {
                var worldFields = parsed["world_fields"] as JObject;
                if (worldFields != null)
                {
                    foreach (var field in _worldFields)
                    {
                        var val = worldFields[field]?.ToString();
                        if (!string.IsNullOrEmpty(val))
                        {
                            typeof(SoulWorld).GetProperty(field)?.SetValue(world, val);
                        }
                    }
                }
            }
        }
        catch
        {
            return ""; // LLM 失败，不更新 soul，返回空摘要
        }

        await _store.SaveProfileAsync(profile);
        if (worldId != SessionManager.PregameWorldId)
        {
            world.Summary = summary;
            await _store.SaveWorldAsync(worldId, world);
        }
        return summary;
    }

    /// <summary>玩家主动设置 profile 字段，标记为 protected（L2 不覆盖）。</summary>
    public async Task SetPlayerFieldsAsync(Dictionary<string, string> fields)
    {
        var profile = await _store.LoadProfileAsync();
        foreach (var (key, value) in fields)
        {
            if (System.Array.IndexOf(_profileFields, key) >= 0)
            {
                typeof(SoulProfile).GetProperty(key)?.SetValue(profile, value);
                if (!profile.ProtectedFields.Contains(key))
                {
                    profile.ProtectedFields.Add(key);
                }
            }
        }
        await _store.SaveProfileAsync(profile);
    }
}
