return {
  Title = "太吾药老 V1.0.09",
  Source = 0,
  FileId = 0,
  Version = "1.0.09",
  GameVersion = "1.0.20",
  Author = "alwayssuper",
  WorkshopCover = "封面.png",
  Description = [[
太吾药老 —— 你的AI百晓生助手

[使用方法]
按 F8 打开/关闭问答面板，输入问题后回车发送。
等待 AI 检索百晓册知识库并生成回答，流式输出。
F8 是唯一入口，对话记录自动保存。

[前提条件]
需要先在设置（面板右上角 ⚙）中配置大模型 API：
- Base URL（API 地址）
- API Key（密钥）
- Model（模型名称）
推荐使用 DeepSeek 等兼容 OpenAI 接口的服务。

[注意事项]
- 首次使用必须配置大模型，否则无法回答。
- 回答质量取决于 AI 模型和百晓册知识库覆盖范围。
- 面板关闭后对话不丢失，重开 F8 可查看历史记录。
- 若回答中断，点「中断」按钮停止；思考过程可点击展开查看。
- 远程 RAG 检索服务部署在低配服务器上，首次查询约需 30-40 秒，后续会更快。
- 知识库资料正在持续收集中，部分内容可能存在滞后或缺失，敬请谅解。
]],
  BackendPlugins = {
  },
  FrontendPlugins = {
    "TaiwuEncyclopedia.Frontend.dll",
  },
  Visibility = 0,
  TagList = {"游戏助手", "AI", "问答", "知识库", "百晓册"},
  ChangeConfig = false,
  HasArchive = true,
  NeedRestartWhenSettingChanged = false,
}
