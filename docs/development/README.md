# 开发维护入口

本文面向维护本模板仓库源码、文档、模板、组包流程和发布流程的人。只想从模板创建、构建或打包自己的 Mod 仓库时，从根
[`README.md`](../../README.md) 开始；提交贡献前的协作入口见根 [`CONTRIBUTING.md`](../../CONTRIBUTING.md)。

## 阅读路径

| 任务                                                   | 入口                                                                                           |
| ------------------------------------------------------ | ---------------------------------------------------------------------------------------------- |
| 维护检查、工具安装、生成项目与打包验证、发布流水线维护 | 本文                                                                                           |
| 维护所有 Mod 共同的组包、插件入口、依赖部署规则        | [`mods/README.md`](../../mods/README.md)                                                       |
| 维护内部共享项目共同边界                               | [`shared/README.md`](../../shared/README.md)                                                   |
| 维护创建/移除命令实现或模板                            | [`tools/README.md`](../../tools/README.md)、[`templates/README.md`](../../templates/README.md) |
| 维护文档分层和同步规则                                 | [文档分层与维护](documentation.md)                                                             |
| 维护跨 Mod 复用的机制参考或仓库经验                    | [`docs/README.md`](../README.md)                                                               |

生成的具体 Mod 应把 `README.md` 留给该 Mod 自己的使用者说明；源码模块、构建命令、组包细节和内部设计入口放在
`DEVELOPMENT.md`、`docs/` 或源码子目录 README。

## 外部依据

[`taiwu-modkit`](https://github.com/Wanxiang-Sanctum/taiwu-modkit) 是 Wanxiang-Sanctum 组织内部维护太吾 Mod
开发辅助工具、引用包和游戏观察快照的仓库。

本模板仓库按两种方式使用它：

- 生成的 Mod 项目引用 `Taiwu.ModKit.References.*` 和可选的 `Taiwu.ModKit.Dependencies.*` NuGet 包。包切分、打包和发布
  流程归 `taiwu-modkit` 的工具与配置维护；本仓库通过 `Directory.Packages.props` 固定默认版本，并通过 `NuGet.config`
  配置包源。
- 源码维护时，组织内部维护者可以使用 `taiwu-modkit` 仓库根目录下的 `game/` 生成快照对照太吾游戏文件和源码观察结果。
  运行时内容以生成仓库的 Mod 源码、组包入口和发布产物为准；游戏观察快照需要更新时，在 `taiwu-modkit` 中运行对应工具
  重新生成。

## 环境与依赖

解决方案 restore、GitHub Packages token 前置条件和 NuGet lock file 提交规则由根
[`README.md`](../../README.md#项目命令) 维护。

模板维护者本地可以用未跟踪的 `.env` 保存变量值，但提交内容只能保留变量名或占位值。`NuGet.config` 从
`TAIWU_MODKIT_GITHUB_USER` 和 `TAIWU_MODKIT_GITHUB_TOKEN` 读取凭据。

## 构建与检查

解决方案构建命令由根 [`README.md`](../../README.md#项目命令) 维护。
修改 C# 源码后，按影响范围运行解决方案构建或对应项目构建。

检查或格式化仓库文档、配置和项目文件：

```powershell
dotnet msbuild repo.proj -t:Check
dotnet msbuild repo.proj -t:Format
```

这些目标通过 `aqua` 调用仓库声明的维护工具。本机没有 `aqua` 时，Windows 可用 `winget install aquaproj.aqua` 或
`scoop install aqua`。如需提前安装这些工具，运行：

```powershell
dotnet msbuild repo.proj -t:InstallTools
```

更新 `aqua.yml` 中的工具版本后，同步刷新校验文件：

```powershell
dotnet msbuild repo.proj -t:UpdateToolChecksums
```

## 生成项目与打包验证

创建、取消注册和打包命令的用户路径由根 [`README.md`](../../README.md#快速开始) 与
[`项目命令`](../../README.md#项目命令) 维护。命令实现入口见 [`tools/README.md`](../../tools/README.md)，模板变量和输出
文案边界见 [`templates/README.md`](../../templates/README.md)，组包 item 和插件部署语义见
[`mods/README.md`](../../mods/README.md)。

维护模板、CLI 或组包目标时，用根 README 的 `create-mod`、`create-shared` 和 `pack-mod` 路径生成测试项目并验证产物。
创建命令只生成初始骨架；项目创建后的真实约定由生成文件、目录 README、lock file 和解决方案注册共同维护。

## 发布

发布 tag 约定和 Release 产物行为由根 [`README.md`](../../README.md#项目命令) 维护。维护发布流水线时，以
`.github/workflows/` 为工作流实现入口，并用受影响 Mod 的 `pack-mod` 产物验证包内容。

## 结构入口

仓库目录职责和读者路由见根 [`README.md`](../../README.md) 的仓库结构与阅读入口；本节只保留维护者需要的就近入口。

| 范围                                       | 维护入口                                                                                      |
| ------------------------------------------ | --------------------------------------------------------------------------------------------- |
| Mod 目录约定、组包声明、插件入口和依赖部署 | [`mods/README.md`](../../mods/README.md)                                                      |
| 内部共享项目边界、目标框架和引用边界       | [`shared/README.md`](../../shared/README.md)                                                  |
| 仓库级文档、机制参考和仓库经验             | [`docs/README.md`](../README.md)                                                              |
| 命令行工具实现                             | [`tools/README.md`](../../tools/README.md)                                                    |
| 模板目录、模板变量和输出文案边界           | [`templates/README.md`](../../templates/README.md)                                            |
| GitHub Actions 工作流                      | `.github/workflows/`                                                                          |
| 仓库级检查目标                             | `repo.proj`                                                                                   |
| 仓库级 MSBuild 和 NuGet 配置               | `Taiwu.Mods.Paths.props`、`Directory.Build.props`、`Directory.Packages.props`、`NuGet.config` |
| 可部署目录输出                             | `artifacts/mods/`；手写源码从 `mods/`、`shared/` 和 `tools/` 进入。                           |
