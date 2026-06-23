# 仓库级文档入口

`docs/` 面向模板仓库维护者，也收纳从模板建立的 Mod 仓库可复用的机制参考和仓库经验。使用模板创建、构建或打包自己的
Mod 仓库时，从根 [`README.md`](../README.md) 开始；提交 issue、讨论或 PR 前先看根
[`CONTRIBUTING.md`](../CONTRIBUTING.md)。

本目录按文档职责分三类：

- 开发维护：说明模板维护者的外部依据、维护检查、工具安装、生成项目与打包验证、发布流水线维护和文档结构。
- 机制参考：解释太吾游戏、Steam Workshop 或外部平台的稳定语义。依据归太吾游戏本体、对应平台或公开可观察行为。
- 仓库经验：记录跨具体 Mod 复用的发布判断、维护约定或协作经验，依据归模板仓库流程和已采用的项目约定。

只服务单个 Mod、单个共享项目或单个源码模块的内容留在对应目录内；本目录不复制这些子模块的可变清单。

## 阅读入口

| 文档 | 何时阅读 |
| --- | --- |
| [开发维护入口](development/README.md) | 维护模板仓库的外部依据、检查、工具安装、工作流或生成项目与打包验证时。 |
| [文档分层与维护](development/documentation.md) | 调整 README、DEVELOPMENT、docs、模板文档或贡献入口的受众和同步规则时。 |
| [太吾游戏 Mod 配置与 Steam 发布边界](taiwu-mod-steam-publishing-boundary.md) | 理解太吾读取的 `Config.Lua`、用户设置、插件入口、Steam Workshop 字段和上传内容边界时。 |

## 所有权边界

本目录自己的文档清单由上面的阅读入口维护；跨目录内容按最近拥有者进入。

| 内容 | 所属入口 |
| --- | --- |
| 实际 Mod 索引、组包规则、插件入口和依赖部署 | [`mods/README.md`](../mods/README.md) |
| 内部共享项目索引、目标框架和引用边界 | [`shared/README.md`](../shared/README.md) |
| 创建/移除命令实现入口 | [`tools/README.md`](../tools/README.md) |
| 模板变量、模板目录和渲染规则 | [`templates/README.md`](../templates/README.md) |

具体 Mod 面向使用者的说明归 `mods/<ModName>/README.md`，并由具体 Mod 自己组织；源码模块、内部设计和组包内容归
`mods/<ModName>/DEVELOPMENT.md`、`mods/<ModName>/docs/` 或源码子目录 README。共享项目 API、运行时依赖和部署建议归
`shared/<ProjectName>/README.md`。

常用命令用法归根 README，模板维护同步规则归开发维护文档。更细的放置与同步规则见
[文档分层与维护](development/documentation.md)。
