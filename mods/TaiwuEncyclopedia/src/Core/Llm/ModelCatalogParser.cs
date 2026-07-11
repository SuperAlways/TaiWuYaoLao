using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TaiwuEncyclopedia.Core.Llm;

/// <summary>
/// Pure parsing logic extracted from ModelCatalogClient.
/// ParseModelsResponse → Parse, ClassifyError → ClassifyError (int overload).
/// </summary>
public static class ModelCatalogParser
{
    /// <summary>
    /// Parse a /v1/models JSON response into a sorted, deduplicated list of model ids.
    /// Accepts both OpenAI format (data[].id) and alternative formats (models[].id,
    /// plain string elements, name fallback).
    /// </summary>
    public static List<string> Parse(string json)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var root = JObject.Parse(json);

            // Try "data" array then "models" array
            foreach (var key in new[] { "data", "models" })
            {
                var arr = root[key] as JArray;
                if (arr == null) continue;

                foreach (var item in arr)
                {
                    if (item.Type == JTokenType.String)
                        ids.Add(item.Value<string>()!);
                    else if (item.Type == JTokenType.Object)
                    {
                        var idVal = item["id"]?.Value<string>();
                        if (idVal != null)
                            ids.Add(idVal);
                        else
                        {
                            var nameVal = item["name"]?.Value<string>();
                            if (nameVal != null) ids.Add(nameVal);
                        }
                    }
                }
                break; // only parse first matching array
            }
        }
        catch { /* parse failure → empty list */ }

        var result = ids.ToList();
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    /// <summary>
    /// Classify an HTTP status code into a user-facing Chinese error message.
    /// </summary>
    public static string ClassifyError(int statusCode, string? rawError = null)
    {
        var prefix = statusCode switch
        {
            401 or 403 => "Key 无效或无权访问模型列表接口",
            404 or 405 => "服务商未提供模型列表接口，请手动填写模型名",
            >= 500 and < 600 => "API 基址不可用或服务商暂时不可用",
            _ => $"HTTP {statusCode}"
        };
        if (!string.IsNullOrWhiteSpace(rawError))
            prefix += $" ({rawError})";
        return prefix;
    }
}
