# Taiwu.Mods

太吾绘卷模组开发仓库。

当前仓库只初始化 monorepo 骨架，主体目录为 `mods/`。仓库还没有具体 mod 项目。

## 当前内容

- `mods/`：后续 mod 项目的放置目录，目前只有共享构建配置。
- `Taiwu.Mods.slnx`：解决方案入口，目前只保留 `mods/` 文件夹。
- `Directory.Packages.props`：仓库级 NuGet 版本管理。
- `repo.proj`：仓库维护入口。
- `global.json`：.NET SDK 版本约束。
- `mise.toml` / `dprint.json`：格式化工具配置。
- `.env.example`：GitHub Packages token 模板。

## 当前约定

包版本以 `Directory.Packages.props` 为准。`mods/Directory.Build.props` 为 `mods/` 下的项目提供统一的 .NET 构建设置。

## 首次设置

```powershell
mise trust
dotnet msbuild repo.proj -t:InstallTools
```

## 当前命令

```powershell
dotnet build repo.proj
dotnet msbuild repo.proj -t:Check
dotnet msbuild repo.proj -t:Format
```
