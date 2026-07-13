using System.Collections.Generic;
using System;

namespace TaiwuEncyclopedia.Core.Llm;

public sealed record ApiProviderPreset(string Id, string DisplayName, string BaseUrl);

public static class ApiProviderPresets
{
    public static readonly IReadOnlyList<ApiProviderPreset> All = new ApiProviderPreset[]
    {
        new("deepseek", "DeepSeek", "https://api.deepseek.com"),
        new("openai", "OpenAI / GPT", "https://api.openai.com/v1"),
        new("grok", "Grok", "https://api.x.ai/v1"),
        new("gemini", "Gemini", "https://generativelanguage.googleapis.com/v1beta/openai"),
        new("minimax", "MiniMax", "https://api.minimaxi.com/v1"),
        new("qwen", "Qwen(北京)", "https://dashscope.aliyuncs.com/compatible-mode/v1"),
        new("volcengine", "火山方舟", "https://ark.cn-beijing.volces.com/api/v3"),
        new("zhipu", "智谱", "https://open.bigmodel.cn/api/paas/v4"),
        new("siliconflow", "硅基流动", "https://api.siliconflow.cn/v1"),
        new("custom", "自定义兼容协议", ""),
    };

    /// <summary>用 build 后的 chat/completions URL 匹配预设，找不到返回 null。</summary>
    public static ApiProviderPreset? Match(string apiBase)
    {
        string? inputEndpoint = EndpointResolver.BuildChatCompletionsUrl(apiBase);
        if (inputEndpoint == null) return null;

        foreach (var preset in All)
        {
            if (string.IsNullOrEmpty(preset.BaseUrl)) continue;
            string? presetEndpoint = EndpointResolver.BuildChatCompletionsUrl(preset.BaseUrl);
            if (presetEndpoint != null
                && string.Equals(presetEndpoint, inputEndpoint, StringComparison.OrdinalIgnoreCase))
                return preset;
        }
        return null;
    }
}
