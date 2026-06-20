# shared

内部共享项目目录。

每个一级子目录是一个可被多个 mod 引用的内部 C# 项目。共享项目为插件项目提供内部库；部署共享项目 DLL
或其 runtime 依赖的动作，由引用它的前端或后端插件项目声明，具体 item 见 `mods/README.md`。

本 README 说明共享项目的共同边界。共享库自己的 API、运行时依赖和部署建议写在
`shared/<ProjectName>/README.md`；引用它的 mod 负责决定是否合并、复制或不部署该 DLL。

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

同一个共享项目如果会同时被前端和后端插件引用，并且依赖 `Taiwu.ModKit.References.*` 游戏引用包，
需要同时产出前端和后端运行时目标框架，例如 `netstandard2.1;net8.0`。这样前端插件消费
`netstandard2.1` 产物，后端插件消费 `net8.0` 产物，NuGet 会按目标框架选择对应的游戏引用资产。

需要访问游戏 API 时，再按实际代码需要添加 `Taiwu.ModKit.References.Frontend` 或
`Taiwu.ModKit.References.Backend` 等引用包。需要访问游戏 DLL 的非 public API 时，在项目自己的
`.csproj` 中显式添加 `Krafs.Publicizer` 引用、启用 `UsePublicizer`，并声明具体 `Publicize` 项。

`Taiwu.ModKit.References.*` 包的生成、分类和发布归组织内部
[`taiwu-modkit`](https://github.com/Wanxiang-Sanctum/taiwu-modkit) 仓库的工具配置管理；共享项目通过稳定包 ID 和本仓库固定版本
引用这些包，DLL 清单以该内部仓库的工具配置为准。
