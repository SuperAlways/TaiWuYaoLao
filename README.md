# 太吾药老 TaiWu Yao Lao

太吾绘卷游戏内 AI 助手。F8 唤起悬浮问答面板，接入 LLM + 百晓册知识库 + 远程 LightRAG 知识图谱服务，ReAct Agent 自主决定何时检索、读哪章、查哪个概念。

QQ 交流群：**1058899374** | 欢迎提出 Issue、提交 PR，也欢迎参与引导 Skill 编写和攻略资料共建。

---

## 1. 核心设计思路：Agentic RAG + Think/Final 双阶段

传统的 RAG 系统是"搜一次就回答"的固定流水线。太吾药老不同——它是一个 **Agentic RAG** 应用，核心理念是让 LLM 在 ReAct 循环中**自主路由信息源**：

```
玩家提问
  → AgentRunner 构建 Think Prompt（纯检索助手，无 Persona，工具导向）
  → AgentLoop ReAct 迭代（最多 6 轮）：
      ┌─ Thinking: LLM 分析问题 → 决定是检索还是直接回答
      ├─ tool_calls?
      │   ├─ retrieve_rag("查询词")           → 远程 LightRAG 查 wiki/攻略
      │   ├─ load_background_skill("章节")    → 本地百晓册章节（两段式）
      │   ├─ load_guidance_skill("技能")      → 攻略思维框架
      │   ├─ lookup_concept("概念")           → 查概念索引
      │   └─ complete_retrieval(confirmed)    → 信号工具：标记检索完毕
      ├─ 工具结果回写 messages（role:tool）
      └─ 下一轮 Thinking 基于新获取的资料继续推理
  → 条件收敛（无工具调用 / complete_retrieval 快速路径 / Jaccard 循环检测 / 耗尽轮次）
  → AgentRunner 构建 Final Prompt（Persona + 回答规则 + 格式要求）
  → FinalChunk 流式输出 → 保存完整对话
```

**为什么不直接做单一 RAG 搜索？** 一个玩家问题往往需要多步推理：先查攻略 → 发现需要了解机制 → 加载百晓册相关章节 → 综合回答。每步都是 LLM 根据已有信息自主决策"还需要什么"，而不是预先写死的检索流水线。

### 关键机制

- **Think/Final 双阶段 Prompt 分离**：Think 阶段用纯检索助手 prompt（无 Persona、工具导向）作为 system message。Final 阶段不替换 system message，而是将较轻的 Persona + 回答规则 + 格式要求作为一条新的 user message 追加在 ReAct 对话尾部。这样 Think 阶段的 system prompt + 工具调用 + 工具结果构成的完整前缀全部命中 DeepSeek prefix caching，只有最后一条 user message 是新的。
- **complete_retrieval 快速路径 + 防幻觉机制**：LLM 检索完毕后调用 `complete_retrieval(confirmed=true, topics_found=..., missing=...)` 信号工具。**`missing` 字段是设计核心**——LLM 必须诚实列出本轮检索中未查清的概念/数据，AgentLoop 将缺失信息写入桥接消息并强调"对于未检索到的内容，不要编造"，从机制上防止 Final 阶段凭空捏造。`confirmed=false` 则忽略该调用继续检索。
- **百晓册两段式加载**：LLM 先调 `load_background_skill(depth="overview")` 加载章节概述（含子文件索引），再根据索引用 `depth="detail"` 加载具体条目。6400+ 篇文章不会一次性塞进上下文。
- **Jaccard 循环检测**：连续两轮调用相同工具会被判定为死循环，强制 LLM 基于已有资料直接回答。`retrieve_rag` 仅比较 query 值（忽略 top_k/mode 参数变化）。
- **API 重试体系**：指数退避 + 随机抖动，区分前景角色（Thinking/Answer，Chat 重试 3 次、Stream 重试 2 次）和背景角色（Intent/Testing，首次失败即抛）。529 过载连续 3 次则告知玩家。
- **上下文压缩**：当历史对话超过 80000 tokens，LLM 自动生成摘要替代早期消息，同时在对话流末尾写入压缩边界标记。
- **Force Compress 兜底**：API 仍然报 context too long 时，截断最长的 tool result（保留前 100000 字符），不做摘要以节省时间。ContextTooLong 错误类型会触发 ForceCompress。

---

## 2. 系统架构全景

```
┌─ Unity 进程 (Mono/net48) ────────────────────┐    ┌─ .NET 8 进程 ──┐    ┌─ 远程 ──┐
│                                               │    │                 │    │         │
│  Frontend (Unity UGUI)                        │    │  Backend        │    │  LLM    │
│  ├─ UI/Chat/ (问答面板 + Markdown)             │    │  (Phase 1 空壳)  │    │  API    │
│  ├─ UI/Config/ (设置面板 + 厂商预设 + RAG 开关) │    │                 │    │         │
│  ├─ UI/Log/ (调试日志查看器)                    │    │                 │    │         │
│  ├─ UI/Shared/ (UI 工厂/主题/面板栈)            │    │                 │    │         │
│  ├─ Networking/                                │    │                 │    │         │
│  │   ├─ LlmTransportHost (ILlmClient 实现)     │    │                 │    │         │
│  │   └─ RagTransportHost (IRagClient 实现)     │    │                 │    │         │
│  ├─ AgentRunnerHost (DontDestroyOnLoad)        │    │                 │    │         │
│  └─ FrontendServices (DI 组合根)               │    │                 │    │         │
│       │ 引用                                   │    │                 │    │         │
│       ▼                                        │    │                 │    │         │
│  Core (netstandard2.1, 零游戏 API)              │    │                 │    │         │
│  ├─ Agent/ (AgentRunner + AgentLoop            │    │                 │    │         │
│  │         + PromptBuilder + ILlmClient)       │    │                 │    │         │
│  ├─ Llm/ (接口 + 重试 + 厂商预设 +              │    │                 │    │         │
│  │        ChatResponseParser/ModelCatalogParser)│    │                 │    │         │
│  ├─ Tools/ (5 个知识工具 + 注册表/执行器)        │────┼── HTTP ────────→│    │         │
│  ├─ Context/ (消息组装 + 压缩)                   │    │  LightRAG       │    │         │
│  ├─ Skills/ (百晓册 + 引导 Skill + Persona)      │    │  知识图谱服务    │    │         │
│  ├─ Session/ (对话存储)                          │    │  (taiwuasker)   │    │         │
│  ├─ Soul/ (跨档/按档记忆档案)                     │    │                 │    │         │
│  ├─ Rag/ (IRagClient + RagResponseParser)       │    │                 │    │         │
│  └─ Storage/ (JSON 文件存储 + 原子写入)           │    │                 │    │         │
└───────────────────────────────────────────────┘    └─────────────────┘    └─────────┘
```

**核心原则**：
- **Core 不碰游戏 API**：netstandard2.1 纯逻辑类库，只依赖 Newtonsoft.Json + YamlDotNet，可独立编译和单测
- **Frontend 做胶水**：Unity UGUI 渲染 + 游戏状态读取（Phase 2 探针），组合 Core 的所有组件。`LlmTransportHost`/`RagTransportHost` 在 Frontend 层以 UnityWebRequest 实现 Core 定义的 `ILlmClient`/`IRagClient` 接口
- **Backend 后置**：Phase 1 完全不需要 Backend——简单只读全在 Frontend 用游戏原生 AsyncCall 就地解决。Phase 2/3 需要事件 Hook / 写操作时才启用 Backend

---

## 3. 信息源体系

太吾药老有五层信息源，LLM 根据问题类型自主选择：

| 层 | 来源 | 工具 | 数据规模 | 说明 |
|----|------|------|---------|------|
| **攻略资料** | 远程 LightRAG 服务 | `retrieve_rag` | 数万篇 wiki/攻略/帖子 | 知识图谱 + 向量检索，返回带来源链接的参考文献，前端可点击跳转原地址（详见 4.6 双赢机制） |
| **游戏机制** | 本地百晓册 Skills/ | `load_background_skill` | 6400+ 篇 Markdown（10 章） | 太吾机制全文，两段式加载（overview → detail） |
| **思考框架** | 本地引导 Skill | `load_guidance_skill` | 11 个引导 Skill | 预设思维框架（问题特征→追问维度→检索建议→回答骨架） |
| **概念速查** | 本地概念索引 | `lookup_concept` | concept_index.json | 概念到文件的映射表，快速查具体数值和定义 |
| **检索完毕 + 防幻觉** | Agent 内部 | `complete_retrieval` | — | 信号工具，核心字段 `missing` 诚实列出未查清的概念，防止 Final 阶段编造 |

### RAG 检索的 5 种模式

LLM 根据问题性质选择合适的检索模式：

| 模式 | 适用场景 | 示例 |
|------|---------|------|
| `local` | 查具体名词/实体 | "郭彦的属性是什么" |
| `global` | 查体系框架/关系 | "各门派功法克制关系" |
| `hybrid` | 通用默认 | 多数日常问题 |
| `mix` | 跨文档综合攻略 | "剑冢全攻略" |
| `naive` | 纯原文片段匹配 | 查特定攻略原文 |

### 参考文献去重与熔断

- **跨轮去重**：同一篇 wiki 被多次检索到，按 `full_doc_id` 合并、累加 `hit_count`
- **熔断**：远程 RAG 失败后标记 `unreachable`，后续轮次跳过不再调用
- **兜底**：远程不可用时只靠本地百晓册回答

---

## 4. LightRAG 远程知识服务

远程 RAG 服务（`taiwuasker`）基于 [LightRAG](https://github.com/HKUDS/LightRAG) 框架，部署在低配服务器上。这是整个项目中**最核心的内容服务**——虽然代码不在本仓库，但理解了它的检索原理才能理解太吾药老是怎么"找到答案"的。

### 4.1 LightRAG 是什么

LightRAG 是一个基于**知识图谱 + 向量检索双引擎**的 RAG 框架。与传统 RAG 只做向量相似度搜索不同，LightRAG 在文档摄入时先抽取实体和关系构建知识图谱，检索时结合图遍历和向量搜索，能够在"宏观概念关联"和"微观精确匹配"两个层面同时查找信息。

### 4.2 数据管线：文档是怎样变成知识的

```
外部文档（灰机wiki / B站 / 贴吧 / NGA / bwiki）
  → 下载抓取（按来源分类存储，标注 source_type / game_version）
  → 分块（chunking）：按 token 数切分，块间有重叠，保留上下文连续性
  → 实体抽取（entity extraction）：LLM 从每个块中提取关键实体（功法名、NPC名、地名、物品名…）
  → 关系抽取（relation extraction）：LLM 识别实体间的关系（克制、属于、产出、需要…）
  → 双存储：
      ├─ 向量库（pgvector）：实体向量 → entities_vdb；关系向量 → relationships_vdb；原文块向量 → chunks_vdb
      └─ 图数据库（PostgreSQL 图存储）：实体节点 + 关系边 → chunk_entity_relation graph
```

### 4.3 双层级检索：高层与低层

LightRAG 将查询分为两个层次，这是它最核心的设计：

**低层检索（Low-Level / "local"）**
- **目标**：找**具体的、上下文相关的**信息
- **机制**：向量相似度搜索 → 命中相关实体 → 通过图遍历找到关联的原文块
- **典型问题**："郭彦的属性是什么""狮相门的内功有哪些"

**高层检索（High-Level / "global"）**
- **目标**：找**宏观的、体系性的**信息
- **机制**：向量搜索命中关系 → 图遍历展开关系网 → 获得跨文档的主题脉络
- **典型问题**："各门派功法克制关系""内力属性搭配体系"

**混合模式（hybrid / mix）** 则同时运行两种检索，合并结果后去重排序。

### 4.4 三种存储后端

LightRAG 用 PostgreSQL 统一管理三种存储：

| 存储 | 实现 | 内容 |
|------|------|------|
| **向量存储** | pgvector 扩展 | 实体向量、关系向量、原文块向量，支持余弦相似度搜索 |
| **图存储** | PostgreSQL 节点+边表 | 实体节点（含属性）、关系边（含权重）、chunk-entity 归属边 |
| **KV 存储** | PostgreSQL key-value 表 | 全文缓存、chunk 文本、LLM 响应缓存、embedding 缓存、文档状态 |

太吾知识库额外扩展了 `TAIWU_DOC_META` 表（文档元数据：source_url、source_type、author、knowledge_type、game_version），检索结果自动富化来源信息。

### 4.5 检索流程（一个 query 怎样变成答案）

```
用户 query 到达 POST /api/retrieve
  → LLM 提取高层关键词（宏观概念）+ 低层关键词（具体实体）
  → 双路并行检索：
      ├─ 高层路径：关键词 → 关系向量检索 → 图遍历扩展 → top_k 条关系
      └─ 低层路径：关键词 → 实体向量检索 → 图遍历找关联 chunk → top_k 个实体
  → 合并 → Rerank 重排序（可选）→ 截断到 token 预算
  → chunk 检索（向量搜索命中原文片段）
  → 组装 context 纯文本 + references 列表
  → 富化：join TAIWU_DOC_META 补 source_url/source_type/game_version
  → 返回给 yaolao（含带来源链接的参考文献）
```

### 4.6 双赢机制：玩家学得会，创作者被看见

远程 RAG 检索不是一个黑盒搜索引擎——它是一条**玩家与内容创作者之间的双向桥梁**。

**对玩家：专项聚合搜索 + AI 讲解**

玩家问"新手村怎么发育"，yaolao 不是丢一个泛泛的搜索结果，而是：

1. 从知识库中检索到匹配的 B 站视频、NGA 攻略帖、贴吧讨论串
2. 大模型阅读理解这些资料后，用自然语言向玩家讲解其中与当前问题相关的部分
3. 前端**参考文献组件**列出每条被引用的来源——标题、来源类型（视频/文章/帖子）、作者

这本质上是一个**针对太吾绘卷的专项聚合搜索**：通用搜索引擎搜出来的结果鱼龙混杂、版本过期、缺少上下文，而 yaolao 检索的是经过筛选整理的知识库，并由 AI 帮玩家"读懂"这些资料。

**对创作者：曝光反哺**

每条被引用的攻略资料，在 yaolao 前端的参考文献面板中都会显示**可点击的来源链接**（如 B 站视频原地址、NGA 帖子链接）。玩家如果觉得某篇攻略讲得透彻、想深度学习，点击链接即可跳转到原作者的内容页面。

这意味着：
- 创作者的视频/文章不再依赖平台推荐算法被看到，而是通过玩家**精准的提问意图**被匹配和曝光
- 越是高质量的攻略，AI 引用的频率越高，曝光量越大
- 玩家获得了知识，创作者获得了流量——**双方都受益**

**对原作者：尊重与退出机制**

本 mod 充分尊重原作者的版权和意愿。引用的来源均为公开可访问的互联网内容，且前端明确标注来源信息。如果原作者发现自己的内容被引用但不同意展示，请联系 mod 作者（QQ 群 **1058899374**），我们将立即下架相关内容并致以歉意。

---

## 5. 引导 Skill 体系

太吾药老不只是检索知识——它有自己的"思考框架"。**11 个引导 Skill** 告诉 LLM 面对不同类型的玩家问题时该如何分步推理：

| 引导 Skill | 适用场景 | 关联百晓册章 |
|-----------|---------|------------|
| 武学搭配 | 功法搭配/内力分配/运功建议 | 修习、战斗 |
| 产业规划 | 太吾村经营/资源规划/建筑优先级 | 产业 |
| NPC互动 | 人际关系/送礼/拜师/结义/结婚 | 交互、人物 |
| 剑冢攻略 | 剑冢打法/配装/BOSS 机制 | 启程 |
| 战斗指导 | 战斗操作/站位/身法/武器选择 | 战斗 |
| 战斗 build 指引 | 完整流派构建/功法体系规划 | 战斗 |
| 门派探索 | 门派选择/支持度提升/门派功法 | 门派 |
| 开局选择 | 角色创建/出身/性别/立场 | 启程 |
| 问题诊断 | 内息紊乱/功法冲突/资源缺口 | 全部 |
| 机制解释 | 游戏机制规则/数值/公式 | 全部 |
| 风险预判 | 当前阶段潜在危险/应对准备 | 全部 |

每个引导 Skill 包含 4 个部分：**问题特征**（匹配条件）→ **追问维度**（需补充的信息）→ **检索建议**（RAG mode/keywords/百晓册章节）→ **回答骨架**（输出结构）。

### 未来：引导 Skill + 游戏探针联动

当前引导 Skill 的"追问维度"需要玩家手动补充信息（"你现在学什么功法？""好感多少？"）。游戏探针工具上线后，这些信息可以自动获取。

---

## 6. Soul 记忆档案

太吾药老不是每次都从零开始——它通过两层记忆档案了解玩家的背景：

| 层级 | 存储 | 内容 | 生命周期 |
|------|------|------|---------|
| **SoulProfile** | profile.json | 玩法偏好、技术水平、提问习惯 | 跨存档持久 |
| **SoulWorld** | world_{id}.json | 门派、游戏阶段、失败经历、对话摘要 | 按存档隔离 |

- **L2 压缩时自动提取**：当历史对话超过 80000 tokens 触发压缩时，LLM 同时从历史中提取玩法偏好、技术水平等信息更新 Soul
- **玩家保护字段**：玩家主动填写的字段标记为 `Protected`，后续自动提取不会覆盖
- **注入位置**：Soul 摘要注入在 system prompt 之后、历史消息之前，利用 DeepSeek prefix caching

---

## 7. LLM 配置体系

v1.0.08 的配置面板提供了完整的 LLM 接入体验：

- **9 家厂商预设**：DeepSeek / OpenAI / Grok / Gemini / MiniMax / Qwen(北京) / 火山方舟 / 智谱 / 硅基流动，外加自定义兼容协议
- **厂商自动识别**：输入 Base URL 后自动匹配预设，填充标准地址
- **模型列表拉取**：通过 `GET /v1/models` 自动获取可用模型，支持下拉选择 + 手动输入
- **连接测试**：保存前发送测试请求验证 API Key 和模型可达性
- **RAG 远程检索开关**：独立 Toggle 控制是否启用远程攻略检索，含剧透警告和耗时提示
- **URL 规范化**：`EndpointResolver` 自动处理 `/chat/completions`、`/models` 后缀剥离和 `/v1` 补全
- **配置原子写入**：`AtomicFile.WriteJsonAsync` 先写临时文件再原子重命名，防崩溃丢配置

---

## 8. 代码结构

```
mods/TaiwuEncyclopedia/
├── Config.Lua                        # Mod 清单（入口）
├── Taiwu.Mod.Pack.proj               # 组包 MSBuild 项目
├── Skills/                           # 百晓册知识库数据
│   ├── registry.yaml                 # 技能注册表（章节/引导/Persona）
│   ├── concept_index.json            # 概念索引
│   ├── answer-rules.md               # 通用回答规则（含 RAG 检索策略）
│   ├── output-style.md               # 回答格式规范
│   ├── background/{章节}/             # 百晓册章节（overview.md + detail/*.md）
│   ├── guidance/*.md                 # 引导 Skill（11 个）
│   └── personas/*.md                 # AI 人格定义
└── src/
    ├── Core/                         # 核心逻辑（netstandard2.1，不依赖 Unity）
    │   ├── Agent/
    │   │   ├── AgentRunner.cs         # 编排入口：构建 Think/Final prompt → 调 loop → 保存会话
    │   │   ├── AgentLoop.cs           # ReAct 迭代引擎：Thinking→工具调用→complete_retrieval 快速路径→Final 回答
    │   │   ├── AgentEvent.cs          # 流式事件定义（Start/ToolCall/ToolResult/FinalChunk/Usage/References/Status/End）
    │   │   ├── ILlmClient.cs          # LLM 客户端接口（StreamChatAsync/ChatAsync），解耦 Core 与传输层
    │   │   ├── LoopDetector.cs        # Jaccard 循环检测（retrieve_rag 仅比较 query 值）
    │   │   └── PromptBuilder.cs       # BuildThinkPrompt()（单例缓存）+ BuildFinalPrompt()（按 personaId 缓存），finalPrompt 作为 user message 追加到 ReAct 尾部以最大化 prefix-cache 命中
    │   ├── Llm/
    │   │   ├── AgentLLMRole.cs        # 调用角色枚举（Thinking/Answer/Intent/Testing）
    │   │   ├── ApiErrorType.cs        # 错误类型枚举（Auth/Overload/Timeout/RateLimit/ContextTooLong/NetworkError/...）
    │   │   ├── ApiException.cs        # API 异常（含 ErrorType 分类）
    │   │   ├── ApiProviderPresets.cs  # 9 家厂商预设 + 自定义
    │   │   ├── ApiRetryPolicy.cs      # 指数退避重试决策引擎（前景/背景分离）
    │   │   ├── ChatResponseParser.cs  # 请求体构建 + 非流式响应解析 + SSE chunk 解析（纯静态）
    │   │   ├── EndpointResolver.cs    # URL 规范化 + 资源 URL 构建
    │   │   ├── LlmConfig.cs           # LLM 配置 DTO
    │   │   ├── LlmMessage.cs          # 消息 DTO（兼容 OpenAI 格式）
    │   │   ├── LlmRequest.cs          # LLM 请求 DTO
    │   │   ├── LlmRawResponse.cs      # LLM 原始响应 DTO
    │   │   ├── LlmResponse.cs         # 响应 DTO（含 ToolCalls）
    │   │   ├── ModelCatalogParser.cs  # GET /v1/models 响应解析 + 错误分类（纯静态）
    │   │   ├── ModelCatalogResult.cs  # 模型列表结果 DTO
    │   │   ├── TokenTracker.cs        # Token 用量追踪
    │   │   ├── TokenUsage.cs          # Token 用量 DTO
    │   │   └── ToolCall.cs            # 工具调用 DTO
    │   ├── Tools/
    │   │   ├── ToolBase.cs            # 工具基类 + JSON Schema 参数定义 + CancellationToken 支持
    │   │   ├── ToolRegistry.cs        # 注册表 + OpenAI FC JSON Schema 生成
    │   │   ├── ToolExecutor.cs        # 并行执行（CancellationTokenSource.CancelAfter 超时）
    │   │   ├── ToolMetadata.cs        # 工具元数据 DTO
    │   │   ├── ToolResult.cs          # 工具执行结果 DTO
    │   │   ├── RetrieveRagTool.cs      # 远程 RAG 检索（支持 RagEnabled 开关）
    │   │   ├── LoadBackgroundSkillTool.cs  # 百晓册章节加载（两段式 overview/detail）
    │   │   ├── LoadGuidanceSkillTool.cs    # 引导 Skill 加载
    │   │   ├── LookupConceptTool.cs        # 概念查询
    │   │   └── CompleteRetrievalTool.cs    # 检索完毕信号工具（传递 topics_found/missing 到 Final）
    │   ├── Context/
    │   │   └── ContextManager.cs       # 初始消息组装 + 压缩检测 + ForceCompress
    │   ├── Session/
    │   │   ├── SessionManager.cs       # 对话保存/加载/重命名/清空
    │   │   ├── MessageRecord.cs        # 消息模型（含 ThinkingContent/References）
    │   │   ├── ConversationMeta.cs     # 会话元数据 DTO
    │   │   └── ReactTrace.cs           # ReAct 追踪 DTO
    │   ├── Skills/
    │   │   ├── SkillManager.cs         # registry.yaml 解析 + 文件加载 + 概念查询
    │   │   └── SkillManifest.cs        # 注册表强类型模型（SkillRegistry/GuidanceSkillManifest/...）
    │   ├── Soul/
    │   │   ├── SoulManager.cs          # 跨存档/按档记忆管理（L2 提取 + 保护字段）
    │   │   ├── SoulProfile.cs          # 跨存档记忆档案模型
    │   │   └── SoulWorld.cs            # 按存档记忆档案模型
    │   ├── Rag/
    │   │   ├── IRagClient.cs           # RAG 客户端接口（RetrieveAsync），解耦 Core 与传输层
    │   │   ├── RagResponseParser.cs    # RAG API 响应解析（纯静态）
    │   │   ├── RagRetrieveRequest.cs   # RAG 检索请求 DTO
    │   │   ├── RagRetrieveResult.cs    # RAG 检索结果 DTO
    │   │   └── Reference.cs            # 参考文献 DTO
    │   ├── Storage/
    │   │   ├── AtomicFile.cs           # 原子文件写入（写临时文件→重命名）
    │   │   ├── IKeyValueStore.cs       # KV 存储接口
    │   │   ├── ISessionStore.cs        # 会话存储接口
    │   │   ├── ISoulStore.cs           # Soul 存储接口
    │   │   ├── JsonKeyValueStore.cs    # JSON KV 存储实现
    │   │   ├── JsonSessionStore.cs     # JSON 会话存储实现
    │   │   └── JsonSoulStore.cs        # JSON Soul 存储实现
    │   ├── Diagnostics/
    │   │   ├── CoreLog.cs              # Core 层日志桥接
    │   │   ├── ModLog.cs               # 游戏内日志
    │   │   ├── IAgentTrace.cs          # Agent 追踪接口
    │   │   ├── JsonlAgentTrace.cs      # JSONL 文件追踪实现
    │   │   └── NullAgentTrace.cs       # 空追踪实现（默认）
    │   └── Util/
    │       └── TokenEstimator.cs       # Token 数量估算
    └── Frontend/                       # UI 层（Unity UGUI）
        ├── Agent/
        │   ├── AgentRunnerHost.cs      # DontDestroyOnLoad 持久宿主（面板关后继续跑）
        │   └── ActiveRequest.cs        # 活跃请求 DTO（支持重连恢复）
        ├── Networking/
        │   ├── LlmTransportHost.cs     # ILlmClient 的 UnityWebRequest 实现（协程桥接 async、重试、ContextTooLong 检测）
        │   └── RagTransportHost.cs     # IRagClient 的 UnityWebRequest 实现（60s 超时、CancellationToken 支持）
        ├── UI/Chat/
        │   ├── ChatPanel.cs            # 问答面板编排（Open/Show/Hide/OnSend）
        │   ├── ChatPanelHost.cs        # 面板宿主 + F8 快捷键
        │   ├── ChatPanelView.cs        # 面板框架（Canvas/标题栏/ScrollRect）
        │   ├── ChatInputBar.cs         # 输入框 + 发送/中断按钮
        │   ├── MessageListView.cs      # 消息渲染（气泡/思考面板/参考文献面板）
        │   ├── ThinkingPanel.cs        # 思考链展示（工具调用/折叠/计时）
        │   └── ReferencePanel.cs       # 参考文献卡片列表
        ├── UI/Config/
        │   ├── ConfigPanel.cs          # 设置面板编排层（组合 5 个 Section）
        │   ├── ConfigPanelView.cs      # 设置面板框架
        │   ├── LlmConfigSection.cs     # LLM 配置区（厂商下拉 + URL/Key/Model + 拉取/测试/保存）
        │   ├── RagSection.cs           # RAG 远程检索开关（Toggle + 剧透警告 + 状态文本）
        │   ├── PersonaSection.cs       # Persona 选择区
        │   ├── HistorySection.cs       # 历史对话管理区
        │   └── DataSection.cs          # 数据与日志区
        ├── UI/Log/
        │   └── PlayerLogViewer.cs      # 游戏内日志查看器
        ├── UI/Shared/
        │   ├── IPanel.cs               # 面板接口（Show/Hide）
        │   ├── PanelStack.cs           # 面板栈管理（显示/隐藏/层级）
        │   ├── UiFactory.cs            # UI 工厂方法（按钮/文本/输入框）
        │   ├── UiTheme.cs              # 统一颜色主题
        │   ├── MarkdownBinder.cs       # Markdown 渲染绑定
        │   └── Components/
        │       └── OverlayDropdown.cs  # 通用下拉组件（解决 ScrollRect 裁切）
        ├── Threading/
        │   └── MainThreadDispatcher.cs # 主线程调度器
        ├── FrontendServices.cs         # 组合根（DI 容器 + 配置加载/保存 + AgentRunner 构建）
        ├── Bootstrap.cs                # 启动引导（路径初始化）
        ├── Plugin.cs                   # Mod 入口
        ├── TaiwuNameReader.cs          # 太吾姓名读取（游戏 API 反射）
        └── WorldIdReader.cs            # 世界 ID 读取（游戏 API 反射）
```

---

## 9. 构建与部署

```bash
# 构建
dotnet build Taiwu.Mods.slnx -c Release

# 打包 Mod
dotnet run --project tools/Taiwu.Mods.Cli -- pack-mod --name TaiwuEncyclopedia

# 部署
cp -r artifacts/mods/TaiwuEncyclopedia/* "游戏目录/Mod/TaiwuEncyclopedia/"
```

详见 `docs/mod-build-deploy-runbook.md`。

---

## 10. 知识共建

太吾药老的知识质量依赖社区贡献。欢迎通过以下方式参与：

- **引导 Skill**：`Skills/guidance/*.md` 定义了 AI 面对不同问题时的思考框架。如果你对某个领域（武学搭配/NPC 互动/产业规划…）有独到理解，欢迎改进或新增引导 Skill
- **百晓册章节**：`Skills/background/*/detail/*.md` 是游戏机制知识库。如果你发现机制描述有误或缺失，欢迎修正补充
- **攻略资料**：远程 LightRAG 知识库持续收集 wiki、B站、贴吧、NGA 等来源的攻略。欢迎推荐高质量攻略来源
- **代码贡献**：Fork → Feature Branch → PR。改动前建议先在 Issue 中讨论方案

---

## 11. 当前局限与待完善

本节诚实描述当前架构中尚未精细打磨的部分，既是 TODO 清单，也是贡献方向的指引。

### 11.1 远程 RAG 攻略仓库

当前远程 LightRAG 知识库的数据来源**仅覆盖灰机 wiki**，B站、贴吧、NGA、bwiki 等渠道的攻略资料尚未系统接入。这意味着：

- 社区中大量高质量攻略（流派 build、剑冢打法、门派路线）未被索引
- 时效性内容（版本更新后的新机制、新玩法）可能缺失
- 不同来源对同一机制的说法无法交叉验证

理想状态下，知识库应覆盖多渠道、标注来源权重、定期增量更新。欢迎推荐优质攻略来源或协助数据整理。

### 11.2 引导 Skill

当前 11 个引导 Skill（武学搭配、产业规划、NPC 互动等）是**由大模型粗略生成的框架**，尚未经过资深玩家的系统性打磨。主要问题：

- **问题特征匹配不够精准**：部分 skill 的触发条件过于宽泛或狭窄
- **追问维度缺乏游戏经验支撑**：不知道什么信息对玩家决策真正关键
- **回答骨架偏向泛化模板**：缺少针对具体场景（如特定剑冢、特定流派）的细化分支

引导 Skill 的质量直接决定 AI 回答的结构化程度。如果你对某个领域有深入理解，改进对应的引导 Skill 是对项目价值最大的贡献方式之一。

### 11.3 Soul 记忆档案

Soul 系统（SoulProfile 跨档记忆 + SoulWorld 按档记忆）目前仅有**基础雏形**：

- **提取维度粗粒度**：仅在 L2 压缩时被动提取，未设计精细的记忆维度（如玩家偏好的功法类型、常用流派、提问风格特征）
- **无主动学习机制**：不会从多轮对话中主动归纳玩家的知识盲区和兴趣偏好
- **保护字段未验证实效**：`Protected` 标记机制已实现，但未经过真实玩家数据的验证
- **记忆衰减/冲突处理缺失**：玩家改变玩法风格后，旧记忆如何衰减、新旧偏好如何加权，均未涉及

Soul 的终极目标是让 AI 越来越"懂"这个玩家。当前只搭好了骨架，血肉还需要大量真实对话数据的喂养和迭代。

### 11.4 游戏探针（Phase 2）

README 中多次提到"游戏状态探针"——自动读取玩家当前的地点、属性、背包、事件等。**这一整块尚未实现。** 目前 AI 完全依赖玩家手动描述现状。详见下方 v1.1 开发计划。

---

## 12. v1.1 开发计划：游戏探针 + Agent 联动

v1.1 的核心目标是让 yaolao 从"完全脱离游戏"升级为"实时感知游戏状态"——玩家问"我现在该干嘛"时，Agent 不需要玩家描述现状，而是自动探查后给出上下文感知的指引。

### 12.1 设计哲学：军师，不是报表面板

玩家用眼睛就能看到血量、位置、背包——不会来问 yaolao。玩家问的是**"怎么玩"**：怎么搭配功法、怎么经营村子、怎么追 NPC。yaolao 的角色是**军师**，不是报表面板。

因此探针的目标不是把游戏 UI 的数据搬进聊天窗，而是提供**策略建议所需的上下文**——知道玩家是什么人、什么阶段、周围有什么机会。

### 12.2 架构：Core 接口 + Frontend 实现，零 Backend 依赖

参考 jianghu-youling、InGameHelper 等项目的实践，**全部只读数据在 Frontend 用游戏原生 AsyncCall 就地读取**，不走 Backend RPC：

```
Core/Probe/
├── IGameStateProvider.cs          # 接口（Core 定义）
├── Dto/                           # 8 个数据传输对象
│   ├── TaiwuStatusSnapshot.cs     # 自动注入用快照
│   ├── CombatSkillsSnapshot.cs    # 功法+运功+技艺
│   ├── VillageSnapshot.cs         # 太吾村建筑+产出
│   ├── NpcDetail.cs               # NPC 全量信息
│   ├── NpcBrief.cs                # 附近 NPC 简要
│   ├── InventorySnapshot.cs       # 背包+仓库
│   ├── CurrentSituation.cs        # 经历+秘闻+通知
│   └── UiContextSnapshot.cs       # 当前 UI 窗口状态
├── Tools/                         # 7 个探针工具（ToolBase 子类）
│   ├── ProbeCombatSkillsTool.cs
│   ├── ProbeVillageTool.cs
│   ├── ProbeNpcDetailTool.cs
│   ├── ProbeNearbyNpcsTool.cs
│   ├── ProbeInventoryTool.cs
│   ├── ProbeCurrentSituationTool.cs
│   └── ProbeUiContextTool.cs
└── ContextInjector.cs             # 自动上下文注入器

Frontend/
└── GameStateProvider.cs           # IGameStateProvider 实现（游戏原生 AsyncCall + Traverse + VariableExtractor）
```

**核心原则**：
- Core 保持 netstandard2.1 纯逻辑，不引用任何游戏 DLL
- 前端用游戏原生 API 实现全部 7 个探针：`CharacterDomainMethod.AsyncCall.*`、`CombatSkillDomainMethod.AsyncCall.*`、`LifeRecordDomainMethod.AsyncCall.*`、Harmony `Traverse` 私有字段、VariableExtractor UI 反射
- Phase 1 **零 Backend 依赖**

### 12.3 三层数据策略

**第一层：自动上下文注入**（每次对话默认带）

太吾基本状态快照在 system prompt 构建时自动注入，≤ 600 字符，不占工具调用配额：

```
[太吾当前状态]
太吾: {姓名} | {年龄}岁 {性别} | 立场: {立场}
内力: 纯阳{}/玄阴{}/金刚{}/紫霞{}/归元{}
属性: 膂力{}/灵敏{}/定力{}/体质{}/根骨{}/悟性{}
运功: {功法1(正/逆)} {功法2(正/逆)} ...
技艺: 音律{}/弈棋{}/... (最高的 4-5 项)
日期: 第{x}年{y}月({季节}) | 位置: {区域名}
剑冢: {已击破数}/{总数} | 声望: {} | 银钱: {}
资源: 食{} 木{} 金{} 玉{} 织{} 药{} 钱{} 威{}
门派: {门派名} | 支持度: {}%
```

**第二层：探针工具**（Agent 按需探查）

Agent 根据玩家问题自主决定调用哪个探针获取深层信息。全部设置 `RequiresSaveGame = true`，返回结构化 JSON：

| 工具 | 参数 | 解决场景 |
|------|------|---------|
| `probe_combat_skills` | 无 | 功法搭配/突破建议/内力分配/阅读进度 |
| `probe_village` | 无 | 太吾村经营/资源规划/建筑优先级 |
| `probe_npc_detail` | `npc_name: string` | 人际关系/送礼/拜师/NPC 动向 |
| `probe_nearby_npcs` | 无 | 附近机会/门派人物/潜在师父 |
| `probe_inventory` | `item_type?: string` | 送礼选择/材料利用/背包清理 |
| `probe_current_situation` | 无 | 经历回顾/秘闻查询/月度通知 |
| `probe_ui_context` | 无 | 事件决策/当前对话 NPC |

**第三层：被动推送**（Phase 2，本期不实现）

Backend Hook 游戏事件（月度推进/战斗/事件触发）→ `RpcPeer.Notify()` 推送前端 → 更新 ContextInjector 缓存。本期仅预留 `IGameEventBus` 接口。

### 12.4 实施步骤

| 步骤 | 内容 | 产出 |
|------|------|------|
| 1 | Core 接口 + 8 个 DTO | `IGameStateProvider` + `IGameEventBus`(预留) + `IGameCommandExecutor`(预留) + 8 个 DTO 类 |
| 2 | ContextInjector | 自动上下文注入器，支持快照 + UI 上下文双输入 |
| 3 | 7 个探针工具 | ToolBase 子类，依赖 `IGameStateProvider` |
| 4 | PromptBuilder 改造 | 构造函数接受 `IGameStateProvider`，`BuildThinkPrompt()` 追加快照段 |
| 5 | Frontend GameStateProvider | 参考 jianghu-youling NpcSnapshotReader 模式，用游戏原生 AsyncCall + Traverse + VariableExtractor 实现 |
| 6 | FrontendServices 集成 | 注册 7 个探针工具 + 注入 GameStateProvider |
| 7 | 引导 Skill 联动（软设计） | 11 个 guidance skill 的"追问维度"标注探针提示，Agent 自主决定是否调用 |

### 12.5 关键设计决策

- **读数据全在 Frontend**：参考 WorldTalk / Xiangshu / Guanxiangtai / jianghu-youling 四个项目的共同规律——没有项目把游戏状态读取做成 Backend RPC。游戏原生 AsyncCall 在 Frontend 就能读所有数据
- **探针是军师的眼睛，不是玩家的复读机**：不从"玩家能看到什么数据"出发，而从"玩家需要什么策略建议"反推需要哪些游戏上下文
- **主动探查 + 自动注入混用**：核心身份/阶段信息自动注入（零开销），深层详情由 Agent 按需调用探针工具
- **入口驱动注入（Phase 2）**：从事件窗口/NPC 对话框按钮打开的 yaolao 自动注入 UI 上下文；F9 热键打开的不注入，避免无意义的 token 消耗
- **候选驱动防 ID 编造（Phase 3 远期）**：写操作时探针返回 `snapshot_id + candidate_index`，Backend handler 验证 snapshot 有效期，防止 LLM 编造不存在的物品/功法/NPC ID

### 12.6 验证方式

完成后在游戏中实测：

1. "我现在在哪" → Agent 从自动注入快照直接回答位置（验证自动注入链）
2. "附近有什么人" → Agent 调 `probe_nearby_npcs` 返回 NPC 列表（验证探针工具链）
3. "怎么追郭彦" → Agent 调 `probe_npc_detail` + `probe_inventory` 综合回答（验证多探针协作）
4. "这个事件选什么" → Agent 调 `probe_ui_context` 返回当前事件+选项（验证 UI 上下文感知）

---

## 13. 致谢

- [万象 Sanctum 的 taiwu-mods 脚手架](https://github.com/Wanxiang-Sanctum/taiwu-mods) — Mod 构建工具链、游戏引用包（Taiwu.ModKit）和解决方案模板
- [LightRAG](https://github.com/HKUDS/LightRAG) — 知识图谱 RAG 引擎，远程检索服务的核心框架
- 交流群 **1058899374** 的所有玩家 — 反馈、测试和建议
  
