# 仓库维护

这个文件面向维护模板仓库的人，收纳“修改文档、模板和工具时应该同步什么”的规则。读者只想创建、构建或打包 mod 时，从
`README.md` 开始即可。

## 文档维护

仓库文档按读者任务分层：

- 根 `README.md` 说明模板仓库身份、常用命令、仓库结构、阅读入口和外部依据。
- `docs/` 收纳跨具体 Mod 复用的机制参考和仓库经验。
- 目录级 README 说明该目录的共同规则和选择入口。
- 具体 Mod、共享项目和源码子目录的 README 说明各自的运行链路、模块边界、API、部署建议和项目内约定。

新增仓库级文档时，先判断它是机制参考还是仓库经验。机制参考要把依据归到太吾游戏本体、外部平台或公开可观察行为；仓库经验要把依据归到本仓库流程。
只属于单个 Mod 的实现细节放在该 Mod 自己的 README 或 `mods/<ModName>/docs/`。

目录级入口表只保留选择信息。mod 的玩法、配置、运行链路和源码模块说明留在对应
`mods/<ModName>/README.md`；共享库 API、运行时依赖和部署建议留在对应
`shared/<ProjectName>/README.md`。

文档中引用跨仓库路径时，首次出现应给出可点击仓库名，并说明后续路径相对哪个仓库根目录。引用组织内部仓库时，同时说明它承载的是工具、包还是同步快照角色。

太吾游戏版本更新后，如果 Mod 管理界面、上传流程或 `Config.Lua` 字段发生变化，按新游戏版本复核相关机制参考。

## 仓库检查与格式化

检查或格式化仓库文档、配置和项目文件：

```powershell
dotnet msbuild repo.proj -t:Check
dotnet msbuild repo.proj -t:Format
```

这些目标通过 `aqua` 调用仓库声明的维护工具。本机没有 `aqua` 时，Windows 可用
`winget install aquaproj.aqua` 或 `scoop install aqua`。如需提前安装这些工具，运行：

```powershell
dotnet msbuild repo.proj -t:InstallTools
```

更新 `aqua.yml` 中的工具版本后，同步刷新校验文件：

```powershell
dotnet msbuild repo.proj -t:UpdateToolChecksums
```

## 模板和工具维护

新增或调整 CLI 命令时，同步更新：

- `tools/Taiwu.Mods.Cli/CommandLineOptions.cs` 中的命令、参数和帮助文本。
- 根 `README.md` 中的常用命令说明。
- 受影响目录自己的 README。

模板上下文变量或模板选择规则变化时，同步更新：

- `tools/Taiwu.Mods.Cli/TemplateRenderer.cs`。
- `templates/README.md` 中的模板变量表。
- 受影响的 `templates/*` 模板内容。

模板只描述新项目的初始骨架。现有项目的真实构建和组包约定以项目文件、`Taiwu.Mod.Pack.proj`、目录 README 和解决方案注册为准。

新增或调整组包 helper、项目默认组包目标或 `pack-mod` 读取项目包产物的流程时，同步复核
`mods/README.md` 中对 `TaiwuModPackFile`、`TaiwuModPackDirectory`、`TaiwuModPackProject`、
`TaiwuModPackEntry` 和发布目录组包目标的说明。`ResolveTaiwuModPackOutputs` 是 CLI 读取项目包产物的
MSBuild 边界；普通 Mod 文档只说明公开 item 和项目配置入口。

## 生成内容

需要更新 `taiwu-modkit` 的游戏观察快照时，在 `taiwu-modkit` 仓库内运行对应工具重新生成；不要在本仓库复制或手写快照内容。
