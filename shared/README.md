# shared

内部共享项目目录。

每个一级子目录是一个可被多个 mod 引用的内部 C# 项目。共享项目为插件项目提供内部库；部署输出由
引用它们的插件项目在 `pack-mod` 时产生。

新建内部共享项目：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- create-shared --name MyCompany.Taiwu.Shared
```

新建后，项目目录包含项目内 README 和一个 C# class library 项目。

```text
shared/MyCompany.Taiwu.Shared/
  README.md
  MyCompany.Taiwu.Shared.csproj
```

共享项目的目标框架、Taiwu 引用和 Publicizer 配置写在项目自己的 `.csproj` 中。默认 `Shared`
和 `Frontend` 项目目标框架为 `netstandard2.1`，`Backend` 项目目标框架为 `net8.0`。纯共享抽象
或通用实现可以保持为普通 C# class library。

需要访问游戏 API 时，再按实际代码需要添加 `Taiwu.ModKit.References.Frontend` 或
`Taiwu.ModKit.References.Backend` 等引用包。需要访问游戏 DLL 的非 public API 时，在项目自己的
`.csproj` 中显式添加 `Krafs.Publicizer` 引用、启用 `UsePublicizer`，并声明具体 `Publicize` 项。
