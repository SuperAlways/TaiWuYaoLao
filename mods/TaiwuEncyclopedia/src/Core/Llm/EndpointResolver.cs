using System;

namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>
/// URL 规范化工具。参考 WorldTalk 的 API Base 输入处理。
/// </summary>
public static class EndpointResolver
{
    private const string DefaultBaseUrl = "https://api.deepseek.com";
    private const string DefaultPathPrefix = "/v1";

    /// <summary>剥 /chat/completions /models 等后缀，返回干净的 base URL。</summary>
    public static string NormalizeApiBase(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return DefaultBaseUrl;

        url = url.TrimEnd('/');

        if (url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            url = url.Substring(0, url.Length - "/chat/completions".Length);
        else if (url.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
            url = url.Substring(0, url.Length - "/models".Length);

        return url.Length == 0 ? DefaultBaseUrl : url;
    }

    /// <summary>拼接完整资源 URL，处理 /v1 补全逻辑。</summary>
    public static string? BuildResourceUrl(string apiBase, string resourcePath)
    {
        string normalized = NormalizeApiBase(apiBase);
        resourcePath = resourcePath.TrimStart('/');

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var baseUri))
            return null;
        if (baseUri.Scheme != "http" && baseUri.Scheme != "https")
            return null;

        // 路径为空时补 /v1，路径非空（如 OpenAI /v1、Gemini /v1beta/openai）则不补
        string basePath = baseUri.AbsolutePath.TrimEnd('/');
        if (basePath.Length == 0)
            normalized += DefaultPathPrefix;

        return $"{normalized.TrimEnd('/')}/{resourcePath}";
    }

    public static string? BuildChatCompletionsUrl(string apiBase) =>
        BuildResourceUrl(apiBase, "/chat/completions");

    public static string? BuildModelsUrl(string apiBase) =>
        BuildResourceUrl(apiBase, "/models");
}
