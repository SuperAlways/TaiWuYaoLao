# shared

内部共享项目目录。

每个一级子目录是一个可被多个 mod 引用的内部 C# 项目。共享项目为插件项目提供内部库，不作为独立
插件入口写入 mod 包；需要部署共享项目 DLL 或其 runtime 依赖时，在引用它的前端或后端插件项目中
声明，具体 item 见 `mods/README.md`。

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

如果同一个共享项目同时被前端和后端插件引用，且依赖 `Taiwu.ModKit.References.*` 游戏引用包，
需要多目标编译，例如 `netstandard2.1;net8.0`。这样前端插件消费 `netstandard2.1` 产物，
后端插件消费 `net8.0` 产物，NuGet 会按目标框架选择对应的游戏引用资产。

需要访问游戏 API 时，再按实际代码需要添加 `Taiwu.ModKit.References.Frontend` 或
`Taiwu.ModKit.References.Backend` 等引用包。需要访问游戏 DLL 的非 public API 时，在项目自己的
`.csproj` 中显式添加 `Krafs.Publicizer` 引用、启用 `UsePublicizer`，并声明具体 `Publicize` 项。
