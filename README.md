# Taiwu.Mods

[![zread](https://img.shields.io/badge/Ask_Zread-_.svg?style=flat&color=00b0aa&labelColor=000000&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB3aWR0aD0iMTYiIGhlaWdodD0iMTYiIHZpZXdCb3g9IjAgMCAxNiAxNiIgZmlsbD0ibm9uZSIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj4KPHBhdGggZD0iTTQuOTYxNTYgMS42MDAxSDIuMjQxNTZDMS44ODgxIDEuNjAwMSAxLjYwMTU2IDEuODg2NjQgMS42MDE1NiAyLjI0MDFWNC45NjAxQzEuNjAxNTYgNS4zMTM1NiAxLjg4ODEgNS42MDAxIDIuMjQxNTYgNS42MDAxSDQuOTYxNTZDNS4zMTUwMiA1LjYwMDEgNS42MDE1NiA1LjMxMzU2IDUuNjAxNTYgNC45NjAxVjIuMjQwMUM1LjYwMTU2IDEuODg2NjQgNS4zMTUwMiAxLjYwMDEgNC45NjE1NiAxLjYwMDFaIiBmaWxsPSIjZmZmIi8%2BCjxwYXRoIGQ9Ik00Ljk2MTU2IDEwLjM5OTlIMi4yNDE1NkMxLjg4ODEgMTAuMzk5OSAxLjYwMTU2IDEwLjY4NjQgMS42MDE1NiAxMS4wMzk5VjEzLjc1OTlDMS42MDE1NiAxNC4xMTM0IDEuODg4MSAxNC4zOTk5IDIuMjQxNTYgMTQuMzk5OUg0Ljk2MTU2QzUuMzE1MDIgMTQuMzk5OSA1LjYwMTU2IDE0LjExMzQgNS42MDE1NiAxMy43NTk5VjExLjAzOTlDNS42MDE1NiAxMC42ODY0IDUuMzE1MDIgMTAuMzk5OSA0Ljk2MTU2IDEwLjM5OTlaIiBmaWxsPSIjZmZmIi8%2BCjxwYXRoIGQ9Ik0xMy43NTg0IDEuNjAwMUgxMS4wMzg0QzEwLjY4NSAxLjYwMDEgMTAuMzk4NCAxLjg4NjY0IDEwLjM5ODQgMi4yNDAxVjQuOTYwMUMxMC4zOTg0IDUuMzEzNTYgMTAuNjg1IDUuNjAwMSAxMS4wMzg0IDUuNjAwMUgxMy43NTg0QzE0LjExMTkgNS42MDAxIDE0LjM5ODQgNS4zMTM1NiAxNC4zOTg0IDQuOTYwMVYyLjI0MDFDMTQuMzk4NCAxLjg4NjY0IDE0LjExMTkgMS42MDAxIDEzLjc1ODQgMS42MDAxWiIgZmlsbD0iI2ZmZiIvPgo8cGF0aCBkPSJNNCAxMkwxMiA0TDQgMTJaIiBmaWxsPSIjZmZmIi8%2BCjxwYXRoIGQ9Ik00IDEyTDEyIDQiIHN0cm9rZT0iI2ZmZiIgc3Ryb2tlLXdpZHRoPSIxLjUiIHN0cm9rZS1saW5lY2FwPSJyb3VuZCIvPgo8L3N2Zz4K&logoColor=ffffff)](https://zread.ai/Wanxiang-Sanctum/taiwu-mods)

太吾绘卷 Mod monorepo 模板仓库，用于从一个仓库维护多个实际 Mod、内部共享项目、组包规则和发布流水线。

这个仓库有两类主要读者：

- 模板使用者：从 GitHub 模板创建自己的 Mod 仓库，并使用项目生成、构建、打包和发布能力。
- 模板维护者：维护本模板仓库的文档、模板、MSBuild 骨架、CLI 和 GitHub Actions。

使用模板创建仓库后，`mods/` 放置一个或多个实际 Mod，`shared/` 放置可被多个 Mod 引用的内部共享项目。仓库命令行工具是
`tools/Taiwu.Mods.Cli/`：新增 Mod、内部共享项目、取消解决方案注册和打包可部署目录都通过它执行。

本文保留模板使用者的常用路径和稳定阅读入口。Mod 目录、共享项目、模板和工具的细节由各自目录的 README 维护。

维护模板仓库本身时，从 [`docs/development/README.md`](docs/development/README.md) 开始；提交 issue、讨论或 PR 前看
[`CONTRIBUTING.md`](CONTRIBUTING.md)。

## 快速开始

以下命令默认在仓库根目录运行。需要从其它目录调用 CLI 时，传入 `--repo-root <path>`。

创建一个 Mod：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- create-mod --name MyMod
```

`ModName` 必须是 C# 命名空间风格的标识符，例如 `MyMod` 或
`MyCompany.MyMod`。创建后，生成器会复制 `templates/mod/`，渲染模板变量，并把
模板内项目加入 `Taiwu.Mods.slnx`。生成的 `Taiwu.Mod.Pack.proj` 是该 Mod 的可部署目录组包入口。

创建一个内部共享项目：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- create-shared --name MyCompany.Taiwu.Shared
```

共享项目默认使用 `Shared` 端侧，适合纯共享抽象和通用实现。如果项目面向前端或后端，可以显式
指定端侧来选择默认目标框架：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- create-shared --name MyCompany.Taiwu.FrontendSupport --side Frontend
dotnet run --project tools/Taiwu.Mods.Cli -- create-shared --name MyCompany.Taiwu.BackendSupport --side Backend
```

## 项目命令

常用命令面向从模板建立的仓库。模板仓库维护命令、格式化和文档同步规则见
[`docs/development/README.md`](docs/development/README.md)。

恢复解决方案依赖：

```powershell
dotnet restore Taiwu.Mods.slnx
```

刚从模板创建且尚未注册任何 Mod 时，这个命令只恢复 `tools/Taiwu.Mods.Cli/`，不需要 GitHub
token。如果解决方案里已有 Mod 项目，恢复过程会下载 GitHub Packages 上的 `Taiwu.ModKit.*`
游戏引用包；这时需要准备一个有 `read:packages` 权限的 GitHub classic personal access token，
并在当前 PowerShell 会话中提供给 NuGet：

```powershell
$env:TAIWU_MODKIT_GITHUB_USER = "<GitHubUser>"
$env:TAIWU_MODKIT_GITHUB_TOKEN = "<GitHubToken>"
dotnet restore Taiwu.Mods.slnx
```

`NuGet.config` 只读取环境变量；不要把真实 token 写入已跟踪文件。

仓库启用 NuGet lock file，用于固定每个项目的 NuGet 依赖解析结果。新增项目、调整
`PackageReference` 或更新 `Directory.Packages.props` 后，运行上面的 restore 命令并提交对应
项目目录下生成或更新的 `packages.lock.json`。CI 使用 locked restore 校验依赖声明和 lock file
是否一致。

构建解决方案：

```powershell
dotnet build Taiwu.Mods.slnx
```

打包可部署目录：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- pack-mod --name MyMod
```

`pack-mod` 默认使用 `Release` 运行 `mods/MyMod/Taiwu.Mod.Pack.proj`，并把该组包入口
声明的文件、目录和项目产物组装到 `artifacts/mods/MyMod/`。这个目录可直接替换游戏内对应 Mod
目录；组包声明、插件入口、依赖部署和发布目录项目约定见 [`mods/README.md`](mods/README.md)。

发布到 GitHub Release：

```powershell
git tag mods/MyMod/v1.2.3
git push origin mods/MyMod/v1.2.3
```

`mods/<ModName>/v<Version>` 是仓库的发布 tag 约定。推送后，GitHub Actions 会以
`<ModName>` 运行 `pack-mod`，上传 `MyMod-v1.2.3.zip` 到对应 GitHub Release。zip
内包含可直接替换游戏 Mod 目录的 `MyMod/` 目录；`ModName` 必须与 `mods/` 下的一级目录名一致。

从解决方案取消注册某个 Mod，但保留文件：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- remove-mod --name MyMod
```

从解决方案取消注册某个内部共享项目，但保留文件：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- remove-shared --name MyCompany.Taiwu.Shared
```

## 仓库结构

- `mods/`：实际 Mod 源码目录。组包声明、前后端插件项目、Taiwu 引用、Publicizer、插件依赖和
  发布目录项目约定见 [`mods/README.md`](mods/README.md)。
- `shared/`：内部共享项目目录。共享边界、目标框架和项目级配置入口见 [`shared/README.md`](shared/README.md)。
- `templates/`：命令行工具创建项目时使用的 Scriban 模板。模板目录和渲染规则见
  [`templates/README.md`](templates/README.md)。
- `tools/`：创建 Mod、内部共享项目、取消解决方案注册和打包可部署目录的命令行工具，实现入口见
  [`tools/README.md`](tools/README.md)。
- `.github/workflows/`：GitHub Actions 工作流，覆盖 PR 验证和 Mod Release 打包。
- `docs/`：模板维护文档、跨具体 Mod 复用的太吾机制、平台机制、发布经验和跨项目判断；入口见
  [`docs/README.md`](docs/README.md)。
- `artifacts/mods/`：`pack-mod` 输出的可部署目录。
- `Taiwu.Mods.Paths.props`：仓库级 MSBuild 路径 alias，供子目录 props 和项目引用稳定目录。
- `Taiwu.Mods.slnx`：解决方案入口，收录工具、已注册的 Mod 项目和内部共享项目。
- `Directory.Build.props`：仓库级编译、分析器和代码质量规则。
- `Directory.Packages.props`：NuGet 包版本。
- `NuGet.config`：NuGet 包源、包源映射，以及从环境变量读取 GitHub Packages 凭据的配置。

## 阅读入口

| 入口 | 何时阅读 |
| --- | --- |
| [`docs/development/README.md`](docs/development/README.md) | 维护模板仓库、文档、模板、工具和工作流。 |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | 提交 issue、讨论或 PR 前确认入口和检查项。 |
| [`mods/README.md`](mods/README.md) | 维护实际 Mod 的目录约定、组包、插件入口、引用和依赖部署规则。 |
| [`shared/README.md`](shared/README.md) | 维护内部共享项目边界、目标框架和项目级配置入口。 |
| [`templates/README.md`](templates/README.md) | 维护模板目录、模板变量和渲染规则。 |
| [`tools/README.md`](tools/README.md) | 维护仓库命令行工具实现。 |
| [`docs/README.md`](docs/README.md) | 阅读跨具体 Mod 复用的太吾机制、平台机制、发布经验和跨项目判断。 |

具体 Mod 面向使用者的说明由对应 `mods/<ModName>/README.md` 自己组织。源码模块、组包内容和内部设计见
`mods/<ModName>/DEVELOPMENT.md`、`mods/<ModName>/docs/` 或源码子目录 README。内部共享项目自己的 API、运行时依赖和
部署建议见 `shared/<ProjectName>/README.md`。

## 外部依据

模板生成的 Mod 项目引用 `Taiwu.ModKit.References.*` NuGet 包。包切分、打包和发布流程由
[`taiwu-modkit`](https://github.com/Wanxiang-Sanctum/taiwu-modkit) 的工具与配置管理；本仓库通过
`Directory.Packages.props` 固定版本，并通过 `NuGet.config` 配置包源。

涉及游戏机制、游戏文本、运行时行为或 Steam Workshop 语义时，文档以太吾绘卷游戏本体和对应外部平台为依据。
模板维护者需要复核游戏侧路径时，可在 `taiwu-modkit` 中使用组织内部生成的游戏观察快照作为检索入口。
