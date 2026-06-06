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

`pack-mod` 默认使用 `Release` 构建前后端项目，再把 `Config.Lua`、插件入口 DLL，以及按声明合并
或复制的依赖组装到 `artifacts/mods/MyMod/`。这个目录可直接替换游戏内对应 mod 目录；插件入口和
依赖部署约定见 `mods/README.md`。

发布到 GitHub Release：

```powershell
git tag mods/MyMod/v1.2.3
git push origin mods/MyMod/v1.2.3
```

`mods/<ModName>/v<Version>` 是仓库的发布 tag 约定。推送后，GitHub Actions 会以
`<ModName>` 运行 `pack-mod`，上传 `MyMod-v1.2.3.zip` 到对应 GitHub Release。zip
内包含可直接替换游戏 mod 目录的 `MyMod/` 目录；`ModName` 必须与 `mods/` 下的一级目录名一致。

从解决方案取消注册某个 mod，但保留文件：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- remove-mod --name MyMod
```

从解决方案取消注册某个内部共享项目，但保留文件：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- remove-shared --name MyCompany.Taiwu.Shared
```

## 仓库结构

- `tools/Taiwu.Mods.Cli/`：创建 mod、内部共享项目、取消解决方案注册和打包可部署目录的命令行工具。
- `mods/`：实际 mod 源码目录。前后端插件项目、Taiwu 引用、Publicizer 和依赖部署约定见
  `mods/README.md`。
- `shared/`：内部共享项目目录。共享边界、目标框架和项目级配置入口见 `shared/README.md`。
- `templates/`：命令行工具创建项目时使用的 Scriban 模板。模板维护约定见
  `templates/README.md`。
- `.github/workflows/`：GitHub Actions 工作流，覆盖 PR 验证和 mod release 打包。
- `artifacts/mods/`：`pack-mod` 输出的可部署目录。
- `Taiwu.Mods.Paths.props`：仓库级 MSBuild 路径 alias，供子目录 props 和项目引用稳定目录。
- `Taiwu.Mods.slnx`：解决方案入口，收录工具、已注册的 mod 项目和内部共享项目。
- `Directory.Build.props`：仓库级编译、分析器和代码质量规则。
- `Directory.Packages.props`：NuGet 包版本。
- `NuGet.config`：NuGet 包源、包源映射，以及从环境变量读取 GitHub Packages 凭据的配置。

## 仓库维护

需要检查或格式化仓库文档、配置和项目文件时运行：

```powershell
dotnet msbuild repo.proj -t:Check
dotnet msbuild repo.proj -t:Format
```

这些目标通过 `aqua` 调用仓库声明的维护工具。本机没有 `aqua` 时，Windows 可用 `winget install aquaproj.aqua` 或 `scoop install aqua`。如需提前安装这些工具，运行：

```powershell
dotnet msbuild repo.proj -t:InstallTools
```

更新 `aqua.yml` 中的工具版本后，同步刷新校验文件：

```powershell
dotnet msbuild repo.proj -t:UpdateToolChecksums
```
