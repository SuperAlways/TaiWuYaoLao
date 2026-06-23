# shared

内部共享项目目录。

每个一级子目录是一个可被同一仓库内多个 Mod 引用的内部 C# 项目。共享项目为插件项目提供内部库；部署共享项目 DLL
或其 runtime 依赖的动作，由引用它的前端或后端插件项目声明，具体 item 见 [`mods/README.md`](../mods/README.md)。

本目录 README 说明共享项目的共同边界。共享库自己的 API、运行时依赖、部署建议和维护入口由
`shared/<ProjectName>/README.md` 维护；引用它的 Mod 负责决定是否合并、复制或不部署该 DLL。

## 目录约定

`shared/` 下的一级子目录就是内部共享项目边界。目录名通常与项目文件名一致，例如
`shared/<ProjectName>/<ProjectName>.csproj`。

## 公开 API 约定

`shared/` 下的 `public` 类型和成员默认视为同一仓库内其它 Mod 可复用的共享入口。内部实现保持 `internal` 或更窄可见性；
公开成员用 XML 文档说明调用方需要依赖的契约。

项目 README 说明模块职责、运行边界和使用路径；XML 文档说明具体公开成员。父级索引只保留选择信息，不复制子项目的公开成员
清单。

共享项目的构建检查服务于这条边界：公开成员需要能说明调用契约，源码引用需要反映真实依赖面。

从模板创建出的仓库如果维护多个内部共享项目，可以在本节放置一级目录索引；索引只保留选择信息和稳定入口，共享库 API、
运行时依赖和部署建议留在项目自己的 README 里。

## 新建共享项目

以下命令默认从仓库根目录运行。需要从其它目录调用 CLI 时，传入 `--repo-root <path>`。

新建内部共享项目：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- create-shared --name MyCompany.Taiwu.Shared
```

`ProjectName` 必须是 C# 命名空间风格的标识符。共享项目默认使用 `Shared` 端侧；只服务前端或后端时，可以传入
`--side Frontend` 或 `--side Backend` 选择默认目标框架。

新建后，项目目录包含项目内 README 和一个 C# class library 项目。

创建命令生成共享项目的初始骨架。项目创建后，目标框架、Taiwu 引用、Publicizer 和部署建议以项目自己的 `.csproj`、
README，以及引用它的插件项目配置为准。

```text
shared/MyCompany.Taiwu.Shared/
  README.md
  MyCompany.Taiwu.Shared.csproj
```

共享项目的目标框架、Taiwu 引用和 Publicizer 配置写在项目自己的 `.csproj` 中。默认 `Shared`
和 `Frontend` 项目目标框架为 `netstandard2.1`，`Backend` 项目目标框架为 `net8.0`。纯共享抽象
或通用实现可以保持为普通 C# class library。

## 引用与部署边界

同一个共享项目如果会同时被前端和后端插件引用，并且依赖 `Taiwu.ModKit.References.*` 游戏引用包，
需要同时产出前端和后端运行时目标框架，例如 `netstandard2.1;net8.0`。这样前端插件消费
`netstandard2.1` 产物，后端插件消费 `net8.0` 产物，NuGet 会按目标框架选择对应的游戏引用资产。

需要访问游戏 API 时，再按实际代码需要添加 `Taiwu.ModKit.References.Frontend` 或
`Taiwu.ModKit.References.Backend` 等引用包。需要访问游戏 DLL 的非 public API 时，在项目自己的
`.csproj` 中显式添加 `Krafs.Publicizer` 引用、启用 `UsePublicizer`，并声明具体 `Publicize` 项。

`Taiwu.ModKit.References.*` 包的生成、分类和发布归组织内部
[`taiwu-modkit`](https://github.com/Wanxiang-Sanctum/taiwu-modkit) 仓库的工具配置管理；共享项目通过稳定包 ID 和本仓库固定版本
引用这些包，DLL 清单以该内部仓库的工具配置为准。

共享项目不自动进入 Mod 可部署目录。需要随某个 Mod 部署时，由引用它的前端或后端插件项目通过
`TaiwuModMergeDependency`、`TaiwuModCopyDependency` 或项目自己的发布目录声明具体动作；共享项目 README 可以说明建议，
但不替引用方决定最终包内容。
