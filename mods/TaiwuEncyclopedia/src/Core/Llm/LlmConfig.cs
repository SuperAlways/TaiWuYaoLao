namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>LLM 配置（从 LocalSettings.json / llm.json 读）。v1.0 单玩家，无 user_id 维度。</summary>
public sealed class LlmConfig
{
    /// <summary>API 密钥。</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>模型名称。</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>API 基础 URL。</summary>
    public string BaseUrl { get; set; } = string.Empty;
}
