# tools

模板仓库命令行工具目录。

`tools/Taiwu.Mods.Cli/` 实现本仓库使用的命令行维护工具：创建 Mod、创建内部共享项目、从解决方案取消注册项目，
以及组装可部署 Mod 目录。

## 文档边界

本目录 README 定位工具实现入口。模板使用者的常用命令用法由仓库根 [`README.md`](../README.md) 维护；模板维护检查、工具安装与
同步规则由 [`docs/development/README.md`](../docs/development/README.md) 维护；Mod 组包 item、插件入口、依赖部署和
发布目录项目的语义由 [`mods/README.md`](../mods/README.md) 维护；模板变量、模板目录和渲染规则由
[`templates/README.md`](../templates/README.md) 维护。

具体 Mod 的包内容由该 Mod 自己拥有。某个 Mod 需要额外文件、目录、发布目录或依赖部署动作时，写在该 Mod 的
`Taiwu.Mod.Pack.proj`、项目文件或项目旁 `Taiwu.Mod.props` 中。

## 实现边界

CLI 只负责把仓库级操作串起来，不重新定义 Mod、共享项目或组包 item 的语义。命令入口解析参数后，按仓库根目录定位
`mods/`、`shared/`、`templates/` 和 `artifacts/mods/`；生成命令渲染模板并注册解决方案项目，移除命令只取消解决方案注册，
`pack-mod` 读取 MSBuild 组包目标输出并写入可部署目录。

模板渲染分两层：文件路径总是经过模板变量渲染，只有 `.scriban` 文件会渲染内容并去掉后缀。渲染使用严格变量；目标路径为空、
越过目标根目录或覆盖已有文件时会失败，除非创建命令显式传入 `--force`。

组包执行以 MSBuild 目标 `ResolveTaiwuModPackOutputs` 为边界。CLI 不解析项目文件里的 item 语义，只读取该目标返回的结构化输出，
再执行文件复制、目录复制和入口程序集依赖合并。`TaiwuModPackFile`、`TaiwuModPackDirectory`、`TaiwuModPackProject`、
`TaiwuModPackEntry`、依赖复制和依赖合并的含义仍由 [`mods/README.md`](../mods/README.md) 与 MSBuild targets 维护。

## 代码入口

- `tools/Taiwu.Mods.Cli/CommandLineOptions.cs`：命令、参数和帮助文本。
- `tools/Taiwu.Mods.Cli/Program.cs`：命令调度、路径定位、模板创建和解决方案注册/移除。
- `tools/Taiwu.Mods.Cli/TemplateRenderer.cs`：模板上下文、严格变量和渲染错误。
- `tools/Taiwu.Mods.Cli/TemplateDirectory.cs`：模板目录复制、路径渲染和目标路径保护。
- `tools/Taiwu.Mods.Cli/ModPacker.cs`：调用 MSBuild 组包目标，并将结构化包产物组装成可部署目录。
- `tools/Taiwu.Mods.Cli/ProcessRunner.cs`：外部 `dotnet` 命令执行和错误报告。
