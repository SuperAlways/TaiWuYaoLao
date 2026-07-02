using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
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

对话历史：
{history}

请返回 JSON（不要 markdown 代码块），格式如下：
{""summary"": ""对话摘要（500字内）"", ""profile_fields"": {""Playstyle"": """", ""TechnicalLevel"": """", ""QuestionHabits"": """"}, ""world_fields"": {""Sect"": """", ""Stage"": """", ""Failures"": """"}}

注意：
- 只填写对话中明确提到的字段，不确定的留空字符串
- Playstyle: 玩法偏好（如苟道流、速通、全收集）
- TechnicalLevel: 技术水平（如新手、老手、精通）
- QuestionHabits: 提问习惯（偏好问什么类型的问题）
- Sect: 门派名（如少林、峨眉）
- Stage: 游戏阶段（如开局、中期、剑冢前）
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
        OpenAiCompatibleClient llmClient,
        LlmConfig llmConfig)
    {
        var profile = await _store.LoadProfileAsync();
        var world = await _store.LoadWorldAsync(worldId);
        var protectedSet = new HashSet<string>(profile.ProtectedFields);

        string summary;
        try
        {
            var prompt = _extractPrompt.Replace("{history}", earlyHistoryText[..System.Math.Min(8000, earlyHistoryText.Length)]);
            var response = await llmClient.Chat(
                AgentLLMRole.Intent,
                new List<LlmMessage> { new() { Role = "user", Content = prompt } },
                llmConfig);
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
