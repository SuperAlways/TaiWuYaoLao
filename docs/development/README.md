# 开发维护入口

本文面向维护本模板仓库源码、文档、模板、组包流程和发布流程的人。只想从模板创建、构建或打包自己的 Mod 仓库时，从根
[`README.md`](../../README.md) 开始；提交贡献前的协作入口见根 [`CONTRIBUTING.md`](../../CONTRIBUTING.md)。

## 阅读路径

| 任务 | 入口 |
| --- | --- |
| 构建、检查、打包、发布或验证模板生成项目 | 本文 |
| 维护所有 Mod 共同的组包、插件入口、依赖部署规则 | [`mods/README.md`](../../mods/README.md) |
| 维护内部共享项目共同边界 | [`shared/README.md`](../../shared/README.md) |
| 维护创建/移除命令实现或模板 | [`tools/README.md`](../../tools/README.md)、[`templates/README.md`](../../templates/README.md) |
| 维护文档分层和同步规则 | [文档分层与维护](documentation.md) |
| 维护跨 Mod 复用的机制参考或仓库经验 | [`docs/README.md`](../README.md) |

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

恢复解决方案依赖：

```powershell
dotnet restore Taiwu.Mods.slnx
```

模板仓库刚克隆且尚未注册任何 Mod 时，这个命令只恢复 `tools/Taiwu.Mods.Cli/`，不需要 GitHub token。如果解决方案里已有
Mod 项目，恢复过程会下载 GitHub Packages 上的 `Taiwu.ModKit.*` 包；这时需要准备有 `read:packages` 权限的 GitHub
classic personal access token，并在当前 PowerShell 会话中提供给 NuGet：

```powershell
$env:TAIWU_MODKIT_GITHUB_USER = "<GitHubUser>"
$env:TAIWU_MODKIT_GITHUB_TOKEN = "<GitHubToken>"
dotnet restore Taiwu.Mods.slnx
```

本地可以用未跟踪的 `.env` 保存变量值，但提交内容只能保留变量名或占位值。`NuGet.config` 从
`TAIWU_MODKIT_GITHUB_USER` 和 `TAIWU_MODKIT_GITHUB_TOKEN` 读取凭据。

仓库启用 NuGet lock file，用于固定每个项目的 NuGet 依赖解析结果。新增项目、调整 `PackageReference` 或更新
`Directory.Packages.props` 后，运行 restore 并提交对应项目目录下生成或更新的 `packages.lock.json`。CI 使用
locked restore 校验依赖声明和 lock file 是否一致。

## 构建与检查

构建解决方案：

```powershell
dotnet build Taiwu.Mods.slnx
```

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

## 生成项目与打包

以下 CLI 命令默认以当前目录作为仓库根目录；从其它目录调用时传入 `--repo-root <path>`。

新增实际 Mod：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- create-mod --name MyMod
```

`ModName` 必须是 C# 命名空间风格的标识符，例如 `MyMod` 或 `MyCompany.MyMod`。创建后，生成器会复制
`templates/mod/`，渲染模板变量，并把模板内项目加入 `Taiwu.Mods.slnx`。生成的 `Taiwu.Mod.Pack.proj` 是该 Mod
的可部署目录组包入口。

新增内部共享项目：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- create-shared --name MyCompany.Taiwu.Shared
```

共享项目默认使用 `Shared` 端侧，适合纯共享抽象和通用实现。如果项目面向前端或后端，可以显式指定端侧来选择默认目标框架：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- create-shared --name MyCompany.Taiwu.FrontendSupport --side Frontend
dotnet run --project tools/Taiwu.Mods.Cli -- create-shared --name MyCompany.Taiwu.BackendSupport --side Backend
```

打包某个 Mod 的可部署目录：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- pack-mod --name MyMod
```

`pack-mod` 默认使用 `Release` 运行 `mods/<ModName>/Taiwu.Mod.Pack.proj`，并把该组包入口声明的文件、目录和项目产物
组装到 `artifacts/mods/<ModName>/`。这个目录可直接替换游戏内对应 Mod 目录；组包声明、插件入口、依赖部署和发布目录
项目约定见 [`mods/README.md`](../../mods/README.md)。

从解决方案取消注册某个 Mod，但保留文件：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- remove-mod --name MyMod
```

从解决方案取消注册某个内部共享项目，但保留文件：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- remove-shared --name MyCompany.Taiwu.Shared
```

创建命令只生成初始骨架。项目创建后，真实构建、组包和部署约定由生成出的项目文件、`Taiwu.Mod.Pack.proj`、目录 README、
lock file 和解决方案注册共同维护。

## 发布

发布到 GitHub Release：

```powershell
git tag mods/<ModName>/v<Version>
git push origin mods/<ModName>/v<Version>
```

`mods/<ModName>/v<Version>` 是仓库的发布 tag 约定。推送后，GitHub Actions 会以 `<ModName>` 运行 `pack-mod`，
上传 `<ModName>-<Version>.zip` 到对应 GitHub Release。zip 内包含可直接替换游戏 Mod 目录的 `<ModName>/` 目录；
`ModName` 必须与 `mods/` 下的一级目录名一致。

## 仓库结构

- `mods/`：实际 Mod 源码目录。Mod 目录约定、组包声明、插件项目、Taiwu 引用、Publicizer、插件依赖和发布目录项目约定见
  [`mods/README.md`](../../mods/README.md)。
- `shared/`：内部共享项目目录。共享项目目录约定、共享边界、目标框架和项目级配置入口见
  [`shared/README.md`](../../shared/README.md)。
- `docs/`：模板维护者使用的开发维护文档、跨 Mod 机制参考和仓库经验。
- `tools/`：本仓库辅助命令行工具，负责创建项目、取消解决方案注册和打包可部署目录；实现入口见
  [`tools/README.md`](../../tools/README.md)。
- `templates/`：本仓库创建命令使用的 Scriban 初始骨架；变量和渲染规则见
  [`templates/README.md`](../../templates/README.md)。
- `.github/workflows/`：GitHub Actions 工作流，覆盖 PR 验证和 Mod release 打包。
- `artifacts/mods/`：`pack-mod` 输出的可部署目录；手写源码从 `mods/`、`shared/` 和 `tools/` 进入。
- `Taiwu.Mods.Paths.props`：仓库级 MSBuild 路径 alias，供子目录 props 和项目引用稳定目录。
- `Taiwu.Mods.slnx`：解决方案入口，收录工具、已注册的 Mod 项目和内部共享项目。
- `Directory.Build.props`：仓库级编译、分析器和代码质量规则。
- `Directory.Packages.props`：NuGet 包版本。
- `NuGet.config`：NuGet 包源、包源映射，以及从环境变量读取 GitHub Packages 凭据的配置。
