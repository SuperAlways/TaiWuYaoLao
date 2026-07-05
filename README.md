# 太吾药老 Taiwu Yao Lao

太吾绘卷游戏内 AI 助手。F8 唤起悬浮问答面板，接入 LLM + 百晓册知识库 + 远程 RAG，ReAct Agent 自主决定何时检索、读哪章、查哪个概念。

## 整体思路

这是一个 **Agentic RAG** 应用，核心理念是让 LLM 在 ReAct 循环中自主路由信息源：

```
玩家提问
  → AgentRunner 构建 System Prompt（注入百晓册目录 + 可用工具列表）
  → AgentLoop 迭代（最多 6 轮）：
      ┌─ Thinking: LLM 分析问题 → 决定是检索还是直接回答
      ├─ tool_calls?
      │   ├─ retrieve_rag("查询词") → 调用远程 RAG 查 wiki/攻略
      │   ├─ load_background_skill("章节") → 加载本地百晓册章节目录
      │   ├─ load_guidance_skill("技能") → 加载本地攻略思维框架
      │   └─ lookup_concept("概念") → 查概念索引
      ├─ 工具结果回写 messages（role:tool）
      └─ 下一轮 Thinking 基于新获取的资料继续推理
  → 条件收敛（无工具调用/答案生成/循环检测/兜底）
  → FinalChunk 流式输出 → 保存完整对话
```

**为什么不直接做 RAG 搜索？** 单一检索命中质量差，Agent 需要多步推理：先查攻略网站 → 发现需要了解具体机制 → 加载百晓册相关章节 → 最后综合回答。每步都是 LLM 自主决策。

## 四层信息源

| 层 | 来源 | 工具 | 说明 |
|----|------|------|------|
| **互联网** | 远程 LightRAG 服务（rag.goodcooking.top） | `retrieve_rag` | 知识图谱 + 向量检索，覆盖 wiki/Steam/攻略帖，返回带来源链接的参考文献 |
| **百晓册** | 本地 Skills/ 目录（6400+ 篇 Markdown） | `load_background_skill` | 太吾机制全文分章（战斗/内功/技艺/修炼...），AI 决定读哪章 |
| **攻略框架** | 本地 Skills/ 目录 | `load_guidance_skill` | 预设思考框架（如"如何分析一个剑冢"），引导 AI 按结构回答 |
| **概念索引** | 本地 Skills/ 目录 | `lookup_concept` | 概念对概念的映射表，快速查某个名词的含义和相关概念 |

## 关键设计决策

### LLM 不读全文，读目录

百晓册 6400 篇文章太庞大。Agent 拿到的是**章节目录 + 概念索引**（~2000 tokens），而不是全文。当 LLM 决定需要查"战斗机制"时，才调 `load_background_skill("zhandou")` 加载该章 overview（~500 tokens）。这样 6 轮迭代不爆上下文。

### 远程 RAG 去重 + 熔断

- **跨轮去重**：同一篇 wiki 被多次检索到，只保留一次，累加 hit_count
- **熔断**：远程 RAG 失败后跳过不再调用
- **兜底**：远程不可用时只用本地百晓册回答

### AgentRunnerHost 持久宿主

ChatPanel 关闭时 Agent 在后台继续运行。重开面板自动重连到正在跑的请求，恢复已有回答内容 + 思考链状态。

## 代码结构

```
src/
├── Core/                        # 核心逻辑（netstandard2.1，不依赖 Unity）
│   ├── Agent/
│   │   ├── AgentRunner.cs       # 编排入口：构建 prompt → 调 loop → 保存会话
│   │   ├── AgentLoop.cs         # ReAct 迭代引擎：Thinking→工具调用→回答
│   │   └── AgentEvent.cs        # 流式事件定义
│   ├── Llm/
│   │   └── OpenAiCompatibleClient.cs  # OpenAI 兼容 LLM 客户端（Chat + StreamChat）
│   ├── Tools/
│   │   ├── ToolBase.cs          # 工具基类 + schema 定义
│   │   ├── ToolRegistry.cs      # 注册表 + BuildOpenaiTools()
│   │   ├── ToolExecutor.cs      # 并行执行（Task.WhenAll）
│   │   ├── RetrieveRagTool.cs   # 调远程 RAG HTTP API
│   │   ├── LoadBackgroundSkillTool.cs  # 加载百晓册章节
│   │   ├── LoadGuidanceSkillTool.cs    # 加载攻略思维框架
│   │   └── LookupConceptTool.cs        # 查概念索引
│   ├── Http/
│   │   ├── RagHttpClient.cs     # RAG API HTTP 客户端
│   │   ├── RagCircuitBreaker.cs # 熔断器（Closed→Open→HalfOpen）
│   │   └── RagCacheStore.cs     # BM25 本地缓存
│   ├── Context/
│   │   └── ContextManager.cs    # 消息构建 + 上下文折叠
│   ├── Session/
│   │   ├── SessionManager.cs    # 对话保存/加载
│   │   └── MessageRecord.cs     # 消息模型（含 ThinkingContent/References）
│   ├── Skills/                  # 技能管理（加载百晓册目录/概念索引）
│   ├── Soul/                    # 跨存档记忆档案
│   ├── Storage/                 # JSON 文件存储（原子写入）
│   └── Diagnostics/             # 日志桥接 + IAgentTrace 接口
└── Frontend/                    # UI 层（Unity UGUI）
    ├── Agent/AgentRunnerHost.cs # DontDestroyOnLoad 持久宿主
    ├── UI/                      # ChatPanel/ChatPanelView/MessageListView...
    └── FrontendServices.cs      # 组合根（DI 容器）
```

## 构建与部署

```bash
dotnet build Taiwu.Mods.slnx -c Release
dotnet run --project tools/Taiwu.Mods.Cli -- pack-mod --name TaiwuEncyclopedia
cp -r artifacts/mods/TaiwuEncyclopedia/* "游戏目录/Mod/TaiwuEncyclopedia/"
```

详见 `docs/mod-build-deploy-runbook.md`。

## 致谢

- 本项目的 Mod 构建工具链、游戏引用包（Taiwu.ModKit）和解决方案模板来自 [万象 Sanctum 的 taiwu-mods 脚手架](https://github.com/Wanxiang-Sanctum/taiwu-mods)
- [太吾绘卷 The Scroll of Taiwu](https://store.steampowered.com/app/838350/) — ConchShip Games
- [LightRAG](https://github.com/HKUDS/LightRAG) — 知识图谱 RAG 引擎
- [DeepSeek](https://platform.deepseek.com/) — 大模型 API