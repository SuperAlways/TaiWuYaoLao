using Newtonsoft.Json.Linq;

namespace TaiwuEncyclopedia.Core.Agent;

/// <summary>解析 tool_result content, 提取探针降级信息(status/error_code/failed_api/probe)。
/// 非探针 result 无 status 字段, 返回 false。</summary>
public static class ProbeStatusParser
{
    public static bool TryGetProbeStatus(string content, out string status, out string code, out string api, out string probe)
    {
        status = ""; code = ""; api = ""; probe = "";
        if (string.IsNullOrEmpty(content)) return false;
        try
        {
            var jo = JObject.Parse(content);
            var s = jo["status"]?.ToString();
            if (s == null) return false;  // 非探针 result
            status = s;
            code = jo["error_code"]?.ToString() ?? "";
            api = jo["failed_api"]?.ToString() ?? "";
            probe = jo["probe"]?.ToString() ?? "";
            return true;
        }
        catch { return false; }
    }
}
