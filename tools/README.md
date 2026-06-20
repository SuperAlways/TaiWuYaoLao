# tools

模板仓库命令行工具目录。

`tools/Taiwu.Mods.Cli/` 实现本仓库使用的命令行维护工具：创建 Mod、创建内部共享项目、从解决方案取消注册项目，
以及组装可部署 Mod 目录。

## 文档边界

本目录 README 定位工具实现入口。模板使用者的常用命令用法由仓库根 [`README.md`](../README.md) 维护；模板维护命令和
同步规则由 [`docs/development/README.md`](../docs/development/README.md) 维护；Mod 组包 item、插件入口、依赖部署和
发布目录项目的语义由 [`mods/README.md`](../mods/README.md) 维护；模板变量、模板目录和渲染规则由
[`templates/README.md`](../templates/README.md) 维护。

具体 Mod 的例外规则由该 Mod 自己拥有。某个 Mod 需要额外文件、目录、发布目录或依赖部署动作时，写在该 Mod 的
`Taiwu.Mod.Pack.proj`、项目文件或项目旁 `Taiwu.Mod.props` 中。

## 维护入口

- `tools/Taiwu.Mods.Cli/CommandLineOptions.cs`：命令、参数和帮助文本入口。
- `tools/Taiwu.Mods.Cli/TemplateRenderer.cs`：模板变量和严格渲染规则。
- `tools/Taiwu.Mods.Cli/TemplateDirectory.cs`：模板目录复制和路径渲染。
- `tools/Taiwu.Mods.Cli/ModPacker.cs`：调用 MSBuild 组包目标并组装可部署目录。
