# tools

仓库命令行工具目录。

`tools/Taiwu.Mods.Cli/` 实现本仓库使用的命令行工具：创建 mod、创建内部共享项目、从解决方案取消注册项目，
以及组装可部署 mod 目录。

## 职责边界

本 README 面向查看或修改 CLI 的读者，说明工具代码在文档树中的位置和实现入口。常用命令入口见仓库根
`README.md`；mod 组包 item、插件入口、依赖部署和发布目录项目的语义见 `mods/README.md`；模板变量、模板目录和渲染规则见
`templates/README.md`。

工具提供通用创建、解决方案注册和组包能力。某个 mod 的额外文件、目录、发布目录或依赖部署动作属于该 mod 的
`Taiwu.Mod.Pack.proj`、项目文件或项目旁 `Taiwu.Mod.props`。

## 实现入口

- `tools/Taiwu.Mods.Cli/CommandLineOptions.cs`：命令、参数和帮助文本入口。
- `tools/Taiwu.Mods.Cli/TemplateRenderer.cs`：模板变量和严格渲染规则。
- `tools/Taiwu.Mods.Cli/TemplateDirectory.cs`：模板目录复制和路径渲染。
- `tools/Taiwu.Mods.Cli/ModPacker.cs`：调用 MSBuild 组包目标并组装可部署目录。
