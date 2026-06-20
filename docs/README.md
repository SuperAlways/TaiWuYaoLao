# docs

仓库级文档目录。

`docs/` 收纳跨具体 Mod 复用的仓库级说明。文档分两类：

- 机制参考：解释太吾游戏、Steam Workshop 或外部平台本身的稳定语义，依据太吾游戏本体、对应平台和公开可观察行为。
- 仓库经验：记录从模板创建出的仓库可复用的发布判断、跨项目约定和协作经验，以仓库内项目和流程为依据。

## 阅读范围

机制参考专注于系统语义；实际 Mod 取值、仓库模板或发布流水线约定见具体 Mod README、目录级 README、根 README
或专门的仓库经验文档。

具体 Mod 的玩法、运行链路和源码模块见 `mods/<ModName>/README.md` 及其子目录 README。共享项目 API 和部署建议见
`shared/<ProjectName>/README.md`。

## 文档入口

| 文档 | 何时阅读 |
| --- | --- |
| [太吾游戏 Mod 配置与 Steam 发布边界](taiwu-mod-steam-publishing-boundary.md) | 理解太吾读取的 `Config.Lua`、用户设置、插件入口、Steam Workshop 字段和上传内容边界时。 |
