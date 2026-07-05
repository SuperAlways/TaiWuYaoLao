# 太吾药老 Taiwu Yao Lao

太吾绘卷 AI 助手 Mod —— 游戏内悬浮问答窗，接入大语言模型 + 百晓册知识库。

**F8 打开面板 → 配置 API → 提问 → 流式回答 + 参考文献 + 工具调用思考过程。**

## 代码架构

```
TaiWuYaoLao/
├── mods/TaiwuEncyclopedia/       # 太吾药老 Mod
│   ├── Config.Lua                # Mod 清单与 Workshop 发布配置
│   ├── Skills/                   # 百晓册知识库（6400+ 篇攻略/机制/概念）
│   └── src/
│       ├── Core/                 # 核心逻辑（netstandard2.1，不依赖 UnityEngine）
│       │   ├── Agent/            # Agent 循环、AgentLoop、AgentRunner、LLM 客户端
│       │   ├── Session/          # 对话存储（MessageRecord/SessionManager）
│       │   ├── Http/             # RAG HTTP 客户端、熔断器、BM25 本地缓存
│       │   ├── Tools/            # RAG 检索、百晓册加载、概念查询工具
│       │   ├── Context/          # 上下文管理、Prompt 构建、摘要折叠
│       │   ├── Soul/             # 跨存档记忆档案
│       │   ├── Storage/          # JSON 文件存储、原子写入
│       │   └── Diagnostics/      # 日志桥接、IAgentTrace 追踪接口
│       └── Frontend/             # UI 层
│           ├── UI/               # ChatPanel/ChatPanelView/MessageListView/ChatInputBar
│           │                     # ThinkingPanel/ReferencePanel/ConfigPanel/UiTheme
│           ├── Agent/            # AgentRunnerHost（DontDestroyOnLoad 持久宿主）
│           ├── Threading/        # MainThreadDispatcher
│           └── Bootstrap/        # Bootstrap + FrontendServices
├── shared/                       # 共享项目
│   ├── TaiwuEncyclopedia.Core/   # （已迁移到 mods/，待清理）
│   └── TaiwuEncyclopedia.Markdown/ # Markdown 渲染
├── tests/                        # 单元测试（xUnit）
├── tools/                        # CLI 工具（pack-mod 等）
└── docs/                         # 设计文档、实施计划、部署手册
```

### 前端组件

| 组件 | 行数 | 职责 |
|------|------|------|
| `ChatPanel.cs` | 278 | 编排层（Open/Show/Hide/OnSend/HandleAgentEvent） |
| `ChatPanelView.cs` | 179 | 面板框架（Canvas/ScrollRect/滚动条/子组件装配） |
| `MessageListView.cs` | 196 | 消息渲染（气泡/思考面板/参考文献面板） |
| `ChatInputBar.cs` | 124 | 输入框 + 发送/中断按钮 |
| `ThinkingPanel.cs` | 199 | 思考链展示（工具调用/折叠/计时动画） |
| `ReferencePanel.cs` | 129 | 参考文献卡片列表 |
| `ConfigPanel.cs` | 1266 | LLM 配置/Persona/历史/日志查看 |

### 数据流

```
玩家输入 → ChatInputBar → ChatPanel.OnSend
  → AgentRunnerHost.StartRequest
    → AgentRunner.RunAsync
      → AgentLoop（ReAct 循环：思考→工具调用→回答）
        → RetrieveRagTool → RagHttpClient → rag.goodcooking.top
        → LoadBackgroundSkillTool → 本地百晓册
      → SaveConversationAsync（存储 user+assistant+思考链+参考文献）
    → 流式事件 → ChatPanel.HandleAgentEvent → UI 更新
```

## 构建与部署

```bash
# 构建
dotnet build Taiwu.Mods.slnx -c Release

# 打包
dotnet run --project tools/Taiwu.Mods.Cli -- pack-mod --name TaiwuEncyclopedia

# 部署到游戏
GAME_MOD="游戏目录/Mod/TaiwuEncyclopedia"
cp -r artifacts/mods/TaiwuEncyclopedia/* "$GAME_MOD/"
```

详见 [`docs/mod-build-deploy-runbook.md`](docs/mod-build-deploy-runbook.md)。

## 远程服务

RAG 知识库检索依赖 LightRAG 服务端（`rag.goodcooking.top`）。服务端代码见 [taiwuasker](https://gitee.com/alwayssuper/taiwuasker)。

## 致谢

- [太吾绘卷 The Scroll of Taiwu](https://store.steampowered.com/app/838350/) — ConchShip Games
- [taiwu-modkit](https://github.com/Wanxiang-Sanctum/taiwu-modkit) — 万象 Sanctum 提供的太吾 Mod 开发脚手架，包含游戏引用包、构建工具链和模板
- [LightRAG](https://github.com/HKUDS/LightRAG) — HKUDS 知识图谱 RAG 引擎
- [DeepSeek](https://platform.deepseek.com/) — 大模型 API 服务