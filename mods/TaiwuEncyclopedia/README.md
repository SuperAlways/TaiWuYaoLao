# 太吾药老 (Taiwu Yao Lao)

太吾绘卷 AI 助手 Mod —— 游戏内悬浮问答窗，接入大语言模型 + 百晓册知识库，流式回答 + 参考文献 + 工具调用思考过程。

## 使用方式

1. 按 **F8** 打开问答面板
2. 点击右上角 ⚙ 配置大模型 API（Base URL / API Key / Model）
3. 输入问题回车发送，等待 AI 回答
4. F8 再次按下关闭面板，对话记录自动保存

推荐使用 [DeepSeek](https://platform.deepseek.com/) 等兼容 OpenAI 接口的服务。

## 功能

- **百晓册 RAG 检索**：联网查询太吾绘卷攻略资料，返回带来源链接的参考文献
- **ReAct 工具链**：思考过程可展开查看，展示 AI 调用了哪些工具及结果
- **流式输出**：回答实时逐字显示，支持 Markdown 渲染
- **多轮对话**：上下文持续记忆，支持追问
- **断点重连**：关闭面板后对话不丢失，回答继续在后台运行
- **Persona 切换**：可选不同 AI 回答风格

## 代码架构

```
mods/TaiwuEncyclopedia/
├── Config.Lua                  # Mod 清单
├── Taiwu.Mod.Pack.proj         # 组包入口
├── Skills/                     # 百晓册知识库数据（6400+ 篇）
└── src/
    ├── Core/                   # 核心逻辑（netstandard2.1，不依赖 Unity）
    │   ├── Agent/              # Agent 循环、LLM 客户端、工具注册
    │   ├── Session/            # 对话存储、消息模型
    │   ├── Http/               # RAG HTTP 客户端、熔断器、BM25 缓存
    │   ├── Tools/              # RAG 检索、百晓册加载、概念查询
    │   ├── Context/            # 上下文管理、Prompt 构建
    │   ├── Soul/               # Soul 档案（跨存档记忆）
    │   └── Storage/            # JSON 文件存储
    └── Frontend/               # UI 层
        ├── UI/                 # ChatPanel、ThinkingPanel、MessageListView 等
        ├── Agent/              # AgentRunnerHost（持久宿主）
        ├── Threading/          # 主线程调度
        └── Hooks/              # （已移除，仅保留 F8 入口）
```

### 前端组件拆分

| 组件 | 职责 |
|------|------|
| `ChatPanel.cs` | 编排层（Open/Show/Hide/OnSend/HandleAgentEvent） |
| `ChatPanelView.cs` | 面板框架（Canvas/标题栏/ScrollRect/滚动条） |
| `MessageListView.cs` | 消息渲染（气泡/思考面板/参考文献面板） |
| `ChatInputBar.cs` | 输入框 + 发送/中断按钮 |
| `ThinkingPanel.cs` | 思考链展示（工具调用/折叠/计时） |
| `ReferencePanel.cs` | 参考文献卡片列表 |
| `ConfigPanel.cs` | LLM 配置面板 |

## 构建与部署

```bash
dotnet build Taiwu.Mods.slnx -c Release
dotnet run --project tools/Taiwu.Mods.Cli -- pack-mod --name TaiwuEncyclopedia
```

产物在 `artifacts/mods/TaiwuEncyclopedia/`，复制到游戏 `Mod/TaiwuEncyclopedia/` 即可。

详见 [`docs/mod-build-deploy-runbook.md`](docs/mod-build-deploy-runbook.md)。

## 远程服务

RAG 检索依赖远程 LightRAG 服务（`rag.goodcooking.top`），部署在低配服务器上，首次查询约需 30-40 秒。服务端代码在 [`taiwuasker`](https://gitee.com/alwayssuper/taiwuasker) 仓库。

## 致谢

- [太吾绘卷](https://store.steampowered.com/app/838350/) —— ConchShip Games
- [taiwu-modkit](https://github.com/Wanxiang-Sanctum/taiwu-modkit) —— 万象 Sanctum 提供的太吾 Mod 脚手架
- [LightRAG](https://github.com/HKUDS/LightRAG) —— 知识图谱 RAG 引擎
- [DeepSeek](https://platform.deepseek.com/) —— LLM API 服务