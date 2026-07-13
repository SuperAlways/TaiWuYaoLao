# Taiwu.ModKit.ReferencePackager

引用包工具。它从本机游戏目录按配置收集 DLL，按分类生成编译期 NuGet 引用包，并可推送到 GitHub Packages。

这些包服务 mod 项目编译；运行时仍从游戏本体加载实际程序集。

使用方通过 NuGet 引用这些包，并在自己的包版本文件中固定版本。包 ID、包拆分、DLL 选择和发布目标以本工具的
`references.yml` 为准。

## 运行

`pack` 负责从本机游戏目录收集 DLL 并生成 `.nupkg`。它需要 `TAIWU_MODKIT_GAME_DIR` 和 NuGet 包版本。

命令示例直接设置当前 PowerShell 会话变量，不依赖 `.env`；如果变量已经来自系统环境变量或仓库根 `.env`，可省略设置行。

```powershell
$env:TAIWU_MODKIT_GAME_DIR = '<本机游戏目录>'
dotnet run --project tools/Taiwu.ModKit.ReferencePackager -- pack --version '<NuGet 包版本>'
```

查看命令行帮助：

```powershell
dotnet run --project tools/Taiwu.ModKit.ReferencePackager -- --help
```

## 输出

`pack` 写入 `artifacts/reference-packages/<version>/`。每个配置包输出一个 `<id>.<version>.nupkg`。

生成包把 DLL 放进 NuGet 的 `ref/<TFM>/` 目录，用作编译期引用；包内 DLL 不承担运行时分发。

## 发布到 GitHub Packages

`publish` 面向已经生成的 `.nupkg`，按指定版本发布到配置中的 NuGet 源。发布时需要 NuGet 包版本和 `TAIWU_MODKIT_GITHUB_TOKEN`。命令示例直接设置当前 PowerShell 会话 token；如果 token 已经来自系统环境变量或仓库根 `.env`，可省略设置行。

```powershell
$env:TAIWU_MODKIT_GITHUB_TOKEN = '<PAT 或 GitHub Actions token>'
dotnet run --project tools/Taiwu.ModKit.ReferencePackager -- publish --version '<NuGet 包版本>'
```

发布源由 `references.yml` 中的 `publish.packageSource` 决定。重复版本默认跳过；需要让重复版本发布失败时可传 `--no-skip-duplicate`。

注意：这些包包含游戏 DLL，发布目标应限定为你有权使用的私有或受控包源。

## 使用方式

先把 GitHub Packages 加成 NuGet 源，再引用需要的包：

```powershell
dotnet nuget add source --username '<GitHub 用户名>' --password '<PAT>' --store-password-in-clear-text --name taiwu-modkit 'https://nuget.pkg.github.com/<OWNER>/index.json'
dotnet add package '<packages[].id>' --version '<NuGet 包版本>'
```

包含个人 token 的 `NuGet.Config` 应留在本机；团队项目建议用本机用户级 NuGet source、CI secret，或私有仓库里的受控配置。

## 包选择

完整包清单以 `references.yml` 的 `packages[]` 为准。每个包的 `id` 是 NuGet 包 ID，`description` 说明适用场景。

选择包时按 mod 代码实际接触的 API 来收窄引用：入口项目从插件基础包开始；前后端复用的内部库优先引用共享契约包；前端 UI、渲染或客户端内部 API 对应前端相关包；后端领域类型对应后端包。引用前端或后端内部包时，基础依赖会由 NuGet 自动补齐。

同一个包可以包含多个 `assetGroups`。NuGet 会按引用项目的目标框架选择 `ref/<TFM>/` 下的 DLL；这适用于前后端物理 DLL 版本不同，或同一开发面需要尊重游戏前后端目录来源的场景。前后端共享项目可以多目标编译，并按目标框架使用对应资产组；后端资产组也可以暴露只存在于后端目录、但仍属于共享契约面的程序集。

## 配置

`references.yml` 决定输出目录、发布目标和分类清单。NuGet 包版本是运行输入，必须通过 `--version` 提供。
配置读取遵循仓库根 README 的统一 YAML 约定；本工具额外按 NuGet 规则解析包 ID、版本和目标框架。

- `outputRoot`：仓库内 `.nupkg` 输出根目录。
- `authors`：NuGet 包作者。
- `repositoryUrl`：NuGet 包源码仓库 URL，工具会按绝对 URI 验证后写入 nuspec。
- `publish.packageSource`：NuGet 发布源；可为 URL、已命名 NuGet 源或本地路径。
- `packages[]`：一个分类包。
- `packages[].id`：NuGet 包 ID。
- `packages[].title`：NuGet 包标题。
- `packages[].description`：包选择提示和 NuGet 包描述。
- `packages[].dependsOn`：可选，声明对同一批游戏引用包的精确版本依赖。
- `packages[].assetGroups[]`：该包内的一组目标框架资产。
- `packages[].assetGroups[].targetFramework`：该组写入 NuGet `ref/` 目录时使用的目标框架，也是 NuGet 选择资产的分派键。
- `packages[].assetGroups[].directory`：相对游戏根目录的 DLL 目录。
- `packages[].assetGroups[].rootAssemblies`：该组主动暴露的根 DLL 文件名，必须是精确文件名。
- `packages[].assetGroups[].followReferences`：设为 `true` 时，工具会从根 DLL 出发递归补齐同目录里的非框架程序集引用；设为 `false` 时只打包 `rootAssemblies`。
- `packages[].assetGroups[].exclude`：可选，从自动补齐的引用中排除 DLL 文件名模式；如果排除了 `rootAssemblies` 中的根 DLL，工具会报错。

`dependsOn` 生成 NuGet 依赖关系，并由 NuGet 按引用项目目标框架解析依赖包资产。工具只校验依赖 ID
指向同一批配置中的引用包，避免在这里复刻 NuGet 的 TFM 兼容规则。当前包包含的 DLL 仍由各资产组的
`rootAssemblies`、`followReferences` 和 `exclude` 决定。可共享程序集应放在更基础的包里，高层包通过
`dependsOn` 引用，并用 `exclude` 避免自动补齐时重复携带。

新增分类前先确认它代表新的 mod 开发面；已有包内部细分通常继续放在原包配置中。
