using System.Collections.Generic;
using TaiwuEncyclopedia.Core.Llm;

namespace TaiwuEncyclopedia.Core.Agent;

/// <summary>
/// 循环检测：连续两轮工具调用相似度 > 阈值 → 检测到循环。
/// 搬 v0.5 is_loop_similar（difflib.SequenceMatcher → C# 用字符 bigram Jaccard 替代）。
/// </summary>
public static class LoopDetector
{
    /// <summary>
    /// 检测当前工具调用是否与上一轮构成循环。
    /// </summary>
    /// <param name="current">当前轮工具调用列表。</param>
    /// <param name="previous">上一轮工具调用列表。</param>
    /// <param name="threshold">相似度阈值（默认 0.8）。</param>
    /// <returns>如果检测到循环返回 true，否则返回 false。</returns>
    public static bool IsLoopSimilar(
        List<ToolCall> current,
        List<ToolCall>? previous,
        double threshold = 0.8)
    {
        if (previous == null || previous.Count == 0 || current.Count == 0)
        {
            return false;
        }

        var currentSig = string.Join(" ", GetSignatures(current));
        var previousSig = string.Join(" ", GetSignatures(previous));
        var ratio = JaccardSimilarity(currentSig, previousSig);
        return ratio > threshold;
    }

    private static List<string> GetSignatures(List<ToolCall> calls)
    {
        var sigs = new List<string>();
        foreach (var tc in calls)
        {
            if (tc.Function.Name == "retrieve_rag")
            {
                // 对 retrieve_rag：只取 query 值做签名，忽略 top_k/mode 等参数
                var query = ExtractJsonValue(tc.Function.Arguments, "query");
                sigs.Add($"retrieve_rag:{query}");
            }
            else
            {
                sigs.Add($"{tc.Function.Name}:{tc.Function.Arguments}");
            }
        }
        return sigs;
    }

    /// <summary>从 JSON 字符串中提取指定 key 的字符串值。简易实现，不依赖 Newtonsoft。</summary>
    private static string ExtractJsonValue(string json, string key)
    {
        var searchKey = $"\"{key}\"";
        var keyIdx = json.IndexOf(searchKey, StringComparison.Ordinal);
        if (keyIdx < 0) return json;
        var colonIdx = json.IndexOf(':', keyIdx + searchKey.Length);
        if (colonIdx < 0) return json;
        var valStart = json.IndexOf('"', colonIdx + 1);
        if (valStart < 0) return json;
        var valEnd = json.IndexOf('"', valStart + 1);
        if (valEnd < 0) return json;
        return json.Substring(valStart + 1, valEnd - valStart - 1);
    }

    /// <summary>
    /// 字符 bigram Jaccard 相似度。替代 Python difflib.SequenceMatcher.ratio()。
    /// </summary>
    private static double JaccardSimilarity(string a, string b)
    {
        if (a.Length < 2 || b.Length < 2) return a == b ? 1.0 : 0.0;
        var setA = new HashSet<string>();
        for (int i = 0; i < a.Length - 1; i++) setA.Add(a.Substring(i, 2));
        var setB = new HashSet<string>();
        for (int i = 0; i < b.Length - 1; i++) setB.Add(b.Substring(i, 2));

        int intersection = 0;
        foreach (var bigram in setA)
        {
            if (setB.Contains(bigram)) intersection++;
        }
        int union = setA.Count + setB.Count - intersection;
        return union == 0 ? 0.0 : (double)intersection / union;
    }
}
