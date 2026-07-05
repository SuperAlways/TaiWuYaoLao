#pragma warning disable IDE0008, IDE0032, RCS1085, IDE0063, CA1031, CA1305, RCS1181, IDE0011, CA1054, IDE0074, IDE0058, CA1308
using System;
using System.IO;
using TaiwuEncyclopedia.Core.Agent;
using TaiwuEncyclopedia.Core.Context;
using TaiwuEncyclopedia.Core.Http;
using TaiwuEncyclopedia.Core.Llm;
using TaiwuEncyclopedia.Core.Session;
using TaiwuEncyclopedia.Core.Skills;
using TaiwuEncyclopedia.Core.Soul;
using TaiwuEncyclopedia.Core.Storage;
using TaiwuEncyclopedia.Core.Tools;
using UnityEngine;

namespace TaiwuEncyclopedia;

/// <summary>
/// 前端服务容器：
/// - SessionManager (已存在)
/// - LlmConfig 加载/保存
/// - SkillManager (来自 SkillsRoot)
/// - AgentRunner 完整构建
/// - SoulStore/ SoulManager
/// </summary>
public static class FrontendServices
{
    // 把 CoreLog(shared,无 UnityEngine)桥接到 Unity Debug.Log,让 RAG/LLM 调用可见
    static FrontendServices() { Core.Diagnostics.CoreLog.OnLog += UnityEngine.Debug.Log; }

    // ========== 持久化配置 ==========
    private static LlmConfig? _loadedLlmConfig;
    private static string? _selectedPersonaId;
    private static string? _ragBaseUrl;

    /// <summary>
    /// 加载的 LLM 配置 (从 config.json 读取)。
    /// </summary>
    public static LlmConfig LoadedLlmConfig
    {
        get
        {
            if (_loadedLlmConfig == null) LoadConfigFromDisk();
            return _loadedLlmConfig ?? new LlmConfig();
        }
    }

    /// <summary>
    /// 当前选择的 Persona ID。
    /// </summary>
    public static string SelectedPersonaId
    {
        get
        {
            if (_selectedPersonaId == null) LoadConfigFromDisk();
            return _selectedPersonaId ?? "sword-will";
        }
    }

    private static string ConfigPath => Path.Combine(Bootstrap.RuntimeRoot, "config.json");

    // ========== 服务实例 ==========
    private static SessionManager? _sessionManager;
    private static ISoulStore? _soulStore;
    private static SoulManager? _soulManager;
    private static SkillManager? _skillManager;
    private static AgentRunner? _agentRunner;
    private static OpenAiCompatibleClient? _llmClient;
    private static ToolRegistry? _toolRegistry;
    private static ToolExecutor? _toolExecutor;
    private static ContextManager? _contextManager;
    private static PromptBuilder? _promptBuilder;
    private static RagHttpClient? _ragHttpClient;

    /// <summary>
    /// 会话管理器。
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
    /// Soul 存储。
    /// </summary>
    public static ISoulStore SoulStore
    {
        get
        {
            if (_soulStore == null)
            {
                var root = Bootstrap.RuntimeRoot;
                if (string.IsNullOrEmpty(root))
                {
                    root = Application.persistentDataPath;
                }
                _soulStore = new JsonSoulStore(root);
            }
            return _soulStore;
        }
    }

    /// <summary>
    /// SkillManager (可能为 null 如果 SkillsRoot 不存在)。
    /// </summary>
    public static SkillManager? SkillManager
    {
        get
        {
            if (_skillManager == null)
            {
                string skillsRoot = Bootstrap.SkillsRoot;
                if (Directory.Exists(skillsRoot))
                {
                    try
                    {
                        _skillManager = new SkillManager(skillsRoot);
                        Debug.Log($"[TaiwuEncyclopedia] SkillManager initialized from: {skillsRoot}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[TaiwuEncyclopedia] Failed to initialize SkillManager: {e.Message}");
                        _skillManager = null;
                    }
                }
                else
                {
                    Debug.LogWarning($"[TaiwuEncyclopedia] SkillsRoot not found: {skillsRoot}");
                }
            }
            return _skillManager;
        }
    }

    /// <summary>
    /// AgentRunner (可能为 null 如果配置未完成)。
    /// </summary>
    public static AgentRunner? AgentRunner => _agentRunner;

    /// <summary>
    /// 是否已配置 AgentRunner (可用于检查是否已填 LLM 配置)。
    /// </summary>
    public static bool IsAgentReady => _agentRunner != null;

    // ========== 配置加载/保存 ==========
    private static void LoadConfigFromDisk()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var saved = Newtonsoft.Json.JsonConvert.DeserializeObject<SavedConfig>(json);
                if (saved != null)
                {
                    _loadedLlmConfig = new LlmConfig
                    {
                        BaseUrl = saved.BaseUrl ?? "",
                        ApiKey = saved.ApiKey ?? "",
                        Model = saved.Model ?? ""
                    };
                    _selectedPersonaId = saved.PersonaId ?? "sword-will";
                    _ragBaseUrl = saved.RagBaseUrl;
                    Debug.Log("[TaiwuEncyclopedia] Config loaded from disk");
                    return;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TaiwuEncyclopedia] Failed to load config: {e.Message}");
        }

        // 回退到默认
        _loadedLlmConfig = new LlmConfig();
        _selectedPersonaId = "sword-will";
    }

    /// <summary>
    /// 保存 LLM 配置并重建 AgentRunner。
    /// </summary>
    public static void SaveLlmConfig(string baseUrl, string apiKey, string model, string personaId)
    {
        try
        {
            // 保存到磁盘
            var saved = new SavedConfig
            {
                BaseUrl = baseUrl,
                ApiKey = apiKey,
                Model = model,
                PersonaId = personaId,
                RagBaseUrl = _ragBaseUrl
            };
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(saved, Newtonsoft.Json.Formatting.Indented);
            string? dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(ConfigPath, json);

            // 更新内存
            _loadedLlmConfig = new LlmConfig
            {
                BaseUrl = baseUrl,
                ApiKey = apiKey,
                Model = model
            };
            _selectedPersonaId = personaId;

            // 重建 AgentRunner
            RebuildAgentRunner();

            Debug.Log("[TaiwuEncyclopedia] Config saved and AgentRunner rebuilt");
        }
        catch (Exception e)
        {
            Debug.LogError($"[TaiwuEncyclopedia] Failed to save config: {e}");
        }
    }

    // ========== AgentRunner 构建 ==========
    /// <summary>
    /// 初始化时调用：尝试从已有配置构建 AgentRunner。
    /// </summary>
    public static void TryInitializeAgentRunner()
    {
        // 确保配置已加载
        if (_loadedLlmConfig == null) LoadConfigFromDisk();

        // 如果配置完整，构建 AgentRunner
        if (!string.IsNullOrEmpty(_loadedLlmConfig?.BaseUrl) &&
            !string.IsNullOrEmpty(_loadedLlmConfig?.ApiKey) &&
            !string.IsNullOrEmpty(_loadedLlmConfig?.Model))
        {
            RebuildAgentRunner();
        }

        // 初始化 AgentRunnerHost（DontDestroyOnLoad 单例）
        var _ = AgentRunnerHost.Instance;
    }

    private static void RebuildAgentRunner()
    {
        if (_loadedLlmConfig == null) return;

        try
        {
            // 1. LLM Client
            _llmClient = new OpenAiCompatibleClient();

            // 2. SkillManager
            SkillManager? sm = SkillManager; // 可能为 null

            // 3. ToolRegistry (注册所有 4 个工具)
            _toolRegistry = new ToolRegistry();

            // RetrieveRagTool (需要 RagHttpClient)
            if (_ragHttpClient == null)
            {
                // RAG 服务端点:优先用 config.json 的 rag_base_url,缺省用远程服务。
                string ragUrl = !string.IsNullOrWhiteSpace(_ragBaseUrl) ? _ragBaseUrl : "https://rag.goodcooking.top";
                _ragHttpClient = new RagHttpClient(ragUrl);
            }
            _toolRegistry.Register(new RetrieveRagTool(_ragHttpClient));

            // LoadBackgroundSkillTool (需要 SkillManager)
            if (sm != null)
            {
                _toolRegistry.Register(new LoadBackgroundSkillTool(sm));
                _toolRegistry.Register(new LoadGuidanceSkillTool(sm));
                _toolRegistry.Register(new LookupConceptTool(sm));
            }

            // 4. ToolExecutor
            _toolExecutor = new ToolExecutor(_toolRegistry);

            // 5. SoulManager
            _soulManager = new SoulManager(SoulStore);

            // 6. ContextManager
            _contextManager = new ContextManager(
                _soulManager,
                _llmClient,
                _loadedLlmConfig,
                maxHistoryRounds: 5,
                collapseThresholdTokens: 40000);

            // 7. PromptBuilder
            _promptBuilder = sm != null
                ? new PromptBuilder(sm, _selectedPersonaId ?? "sword-will")
                : new PromptBuilder(new FallbackSkillManager(), _selectedPersonaId ?? "sword-will");

            // 8. AgentRunner
            _agentRunner = new AgentRunner(
                _llmClient,
                _loadedLlmConfig,
                _toolRegistry,
                _toolExecutor,
                _contextManager,
                _soulManager,
                SessionManager,
                _promptBuilder,
                maxIter: 6);

            Debug.Log("[TaiwuEncyclopedia] AgentRunner built successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"[TaiwuEncyclopedia] Failed to build AgentRunner: {e}");
            _agentRunner = null;
        }
    }

    // ========== 保存配置的 DTO ==========
    private sealed class SavedConfig
    {
        public string? BaseUrl { get; set; }
        public string? ApiKey { get; set; }
        public string? Model { get; set; }
        public string? PersonaId { get; set; }
        public string? RagBaseUrl { get; set; }
    }

    // ========== Fallback SkillManager (当 SkillsRoot 不可用时) ==========
    private sealed class FallbackSkillManager : SkillManager
    {
        public FallbackSkillManager() : base(GetTempSkillsDir()) { }

        private static string GetTempSkillsDir()
        {
            // 创建一个空的临时目录
            string temp = Path.Combine(Path.GetTempPath(), "TaiwuEncyclopedia_EmptySkills");
            Directory.CreateDirectory(temp);
            string registry = Path.Combine(temp, "registry.yaml");
            if (!File.Exists(registry))
            {
                File.WriteAllText(registry, @"
background: []
guidance: []
personas:
  - id: sword-will
    cn_name: 天道残魂
    description: '太吾天道在远古 天帝伐天时被击碎，四散人间，演化种种。作为其中之一的你与上古被再次镇压后投入伏虞剑的炼制中，在伏虞剑碎后显化而出，由天道权柄晓知世间种种。'
    file: personas/sword-will.md
");
                string personaDir = Path.Combine(temp, "personas");
                Directory.CreateDirectory(personaDir);
                string personaFile = Path.Combine(personaDir, "sword-will.md");
                if (!File.Exists(personaFile))
                {
                    File.WriteAllText(personaFile, @"
# 天道残魂

## 外在形象与口吻
- 形象：太吾剑柄中凝出的半透明虚影，沧桑老者模样
- 自称：本座
- 称呼玩家：太吾
- 语气：沉稳沧桑
- 用词倾向：融入太吾世界观词汇

## 剧透策略
- 机制类问题可以详讲
- 剧情类问题留白
");
                }
            }
            return temp;
        }
    }
}
#pragma warning restore IDE0008, IDE0032, RCS1085, IDE0063, CA1031, CA1305
