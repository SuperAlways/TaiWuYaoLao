# Taiwu.Mods

太吾绘卷 mod monorepo 模板仓库。

从 GitHub 模板创建自己的仓库后，在 `mods/` 下维护一个或多个 mod，在 `shared/`
下维护可被多个 mod 引用的内部共享项目。仓库命令行工具是 `tools/Taiwu.Mods.Cli/`：
新增 mod、内部共享项目、取消解决方案注册和打包可部署目录都通过它执行。

## 开始

创建一个 mod：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- create-mod --name MyMod
```

`ModName` 必须是 C# 命名空间风格的标识符，例如 `MyMod` 或
`MyCompany.MyMod`。创建后，生成器会复制 `templates/mod/`，渲染模板变量，并把
前后端项目加入 `Taiwu.Mods.slnx`。

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

恢复解决方案依赖：

```powershell
dotnet restore Taiwu.Mods.slnx
```

刚从模板创建且尚未注册任何 mod 时，这个命令只恢复 `tools/Taiwu.Mods.Cli/`，不需要 GitHub
token。如果解决方案里已有 mod 项目，恢复过程会下载 GitHub Packages 上的 `Taiwu.ModKit.*`
游戏引用包；这时需要准备一个有 `read:packages` 权限的 GitHub classic personal access token，
并在当前 PowerShell 会话中提供给 NuGet：

```powershell
$env:TAIWU_MODKIT_GITHUB_USER = "<GitHubUser>"
$env:TAIWU_MODKIT_GITHUB_TOKEN = "<GitHubToken>"
dotnet restore Taiwu.Mods.slnx
```

构建解决方案：

```powershell
dotnet build Taiwu.Mods.slnx
```

打包可部署目录：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- pack-mod --name MyMod
```

`pack-mod` 默认使用 `Release` 构建前后端项目，并把 `Config.Lua` 和插件 DLL
组装到 `artifacts/mods/MyMod/`。这个目录可直接替换游戏内对应 mod 目录。

从解决方案取消注册某个 mod，但保留文件：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- remove-mod --name MyMod
```

从解决方案取消注册某个内部共享项目，但保留文件：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- remove-shared --name MyCompany.Taiwu.Shared
```

## 仓库维护

`repo.proj` 承载仓库维护目标，维护命令通过 `dotnet msbuild` 执行。维护工具由
`aqua.yaml` 声明，下载校验由 `aqua-checksums.json` 固定；首次使用维护目标前，确保本机已安装
`aqua`，例如 Windows 可用 `winget install aquaproj.aqua` 或 `scoop install main/aqua`。

```powershell
dotnet msbuild repo.proj -t:InstallTools
```

检查和格式化仓库文件：

```powershell
dotnet msbuild repo.proj -t:Check
dotnet msbuild repo.proj -t:Format
```

更新 `aqua.yaml` 中的工具版本后，同步刷新校验文件：

```powershell
dotnet msbuild repo.proj -t:UpdateToolChecksums
```

## 仓库结构

常用目录和文件如下。

- `tools/Taiwu.Mods.Cli/`：创建 mod、内部共享项目、取消解决方案注册和打包可部署目录的命令行工具。
- `mods/`：实际 mod 源码目录。前后端插件项目、Taiwu 引用、Publicizer 和依赖内部化约定见
  `mods/README.md`。
- `shared/`：内部共享项目目录。共享边界、目标框架和项目级配置入口见 `shared/README.md`。
- `templates/`：命令行工具创建项目时使用的 Scriban 模板。模板维护约定见
  `templates/README.md`。
- `repo.proj`：安装本地工具、检查和格式化命令。
- `aqua.yaml`、`aqua-checksums.json`：仓库维护工具版本和下载校验。
- `Taiwu.Mods.slnx`：解决方案入口，收录工具、已注册的 mod 项目和内部共享项目。
- `Directory.Build.props`：仓库级编译、分析器和代码质量规则。
- `Directory.Packages.props`：NuGet 包版本。
- `NuGet.config`：NuGet 包源、包源映射，以及从环境变量读取 GitHub Packages 凭据的配置。
