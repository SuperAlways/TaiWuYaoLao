#pragma warning disable IDE0008, IDE0032, RCS1085
using System;
using TaiwuEncyclopedia.Core.Agent;
using TaiwuEncyclopedia.Core.Session;
using TaiwuEncyclopedia.Core.Storage;
using UnityEngine;

namespace TaiwuEncyclopedia;

/// <summary>
/// 前端服务容器。Task 6 仅构造 SessionManager（可用 Bootstrap.RuntimeRoot）；
/// AgentRunner 需要 Task 7 的 LlmConfig + SkillManager 等，暂置空留待后续填充。
/// </summary>
public static class FrontendServices
{
    private static SessionManager? _sessionManager;
    private static AgentRunner? _agentRunner;

    /// <summary>
    /// 会话管理器（已初始化）。
    /// </summary>
    public static SessionManager SessionManager
    {
        get
        {
            if (_sessionManager == null)
            {
                var root = Bootstrap.RuntimeRoot;
                if (string.IsNullOrEmpty(root))
                {
                    Debug.LogWarning("[TaiwuEncyclopedia] Bootstrap.RuntimeRoot not ready, using fallback");
                    root = Application.persistentDataPath;
                }
                var store = new JsonSessionStore(root);
                _sessionManager = new SessionManager(store);
            }
            return _sessionManager;
        }
    }

    /// <summary>
    /// AgentRunner（Task 7 配置 LLM 后设置）。
    /// </summary>
    public static AgentRunner? AgentRunner
    {
        get => _agentRunner;
        set => _agentRunner = value;
    }

    /// <summary>
    /// 是否已配置 AgentRunner（可用于检查是否已填 LLM 配置）。
    /// </summary>
    public static bool IsAgentReady => _agentRunner != null;
}
#pragma warning restore IDE0008, IDE0032, RCS1085
