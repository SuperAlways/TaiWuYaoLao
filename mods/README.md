# mods

Mod 源码目录。

每个一级子目录是一个独立 mod。新建 mod：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- create-mod --name MyMod
```

新建后，mod 目录包含游戏读取的 `Config.Lua`、项目内 README，以及前端和后端两个插件项目。

```text
mods/MyMod/
  Config.Lua
  README.md
  src/
    Frontend/
    Backend/
```

每个插件项目通过项目旁的 `Taiwu.Mod.props` 标记端侧。前端项目默认目标框架为
`netstandard2.1`，后端项目默认目标框架为 `net8.0`；这两个默认值由
`mods/Directory.Build.props` 根据端侧设置。

构建插件项目时可以直接使用 `dotnet build`。普通构建只负责 SDK 默认的 `bin/` 和 `obj/` 输出，
不生成打包清单、不组装 `artifacts/mods/`，也不执行依赖合并。

打包可部署目录：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- pack-mod --name MyMod
```

`pack-mod` 默认使用 `Release` 构建前后端项目，再把 `Config.Lua`、插件入口 DLL，以及按声明合并
或复制的依赖组装到 `artifacts/mods/MyMod/`。

## Taiwu 引用和 Publicizer

插件项目默认引用 `Taiwu.ModKit.References.Plugin`。需要访问更宽的游戏 API 时，再按实际代码需要
添加 `Taiwu.ModKit.References.Frontend` 或 `Taiwu.ModKit.References.Backend` 等引用包。

插件项目默认具备编译期 Publicizer 支持，但不会自动公开化游戏 DLL。需要在编译期访问游戏 DLL
的非 public API 时，在 `Taiwu.Mod.props` 中声明具体 `Publicize` 项；只声明实际需要的程序集、类型或成员。

```xml
<ItemGroup>
  <Publicize
    Include="Assembly-CSharp"
    IncludeCompilerGeneratedMembers="false"
    IncludeVirtualMembers="false"
  />
</ItemGroup>
```

前端通常从 `Assembly-CSharp` 开始，后端通常从 `GameData` 开始；如果只需要具体类型或成员，优先写
更窄的 `Publicize Include`。Publicizer 运行时策略由仓库按端侧固定选择，不作为普通 mod 配置入口。

需要关闭默认 Publicizer 支持时，可以在 `Taiwu.Mod.props` 中设置：

```xml
<PropertyGroup>
  <UsePublicizer>false</UsePublicizer>
</PropertyGroup>
```

## 插件入口和依赖部署

太吾读取 `Config.Lua` 中的 `FrontendPlugins` 和 `BackendPlugins`，并从 mod 的 `Plugins/`
目录按文件名加载这些插件入口 DLL。`FrontendPlugins` 和 `BackendPlugins` 只列插件入口 DLL；
独立依赖 DLL 同样部署到 `Plugins/` 下。

`pack-mod` 会把 `Config.Lua` 中声明的插件入口 DLL 部署到 `Plugins/`。额外依赖需要在插件项目旁的
`Taiwu.Mod.props` 或项目文件中显式声明。普通 `dotnet build` 负责生成项目常规输出；`pack-mod`
在构建后读取这些声明生成打包清单。

依赖部署有两种动作。需要作为独立文件复制到 `Plugins/` 时，声明：

```xml
<ItemGroup>
  <TaiwuModCopyDependency Include="Other.Assembly.dll" />
</ItemGroup>
```

需要合并到入口插件 DLL 时，声明：

```xml
<ItemGroup>
  <TaiwuModMergeDependency Include="Your.Assembly.dll" />
</ItemGroup>
```

每个依赖选择一种动作。同一个 DLL 同时写进 `TaiwuModMergeDependency` 和
`TaiwuModCopyDependency` 会报错；复制依赖写入 `Plugins/<DLL 文件名>`。

`TaiwuModMergeDependency` 和 `TaiwuModCopyDependency` 都只从入口项目本次构建的 copy-local 输出 DLL 中
解析。也就是说，`pack-mod` 复用标准 MSBuild/NuGet 已经做出的“哪些程序集应该进入入口项目输出目录”
决策，只按 DLL 文件名从这些输出里筛选，再执行 copy 或 merge。

`Include` 使用 DLL 文件名；NuGet 缓存路径、被引用项目的输出路径，以及残留在入口项目输出目录但本次构建
不再 copy-local 的同名 DLL 都不参与匹配。需要随 mod 部署的依赖，要先通过项目自身的 MSBuild/NuGet
引用配置进入入口项目 copy-local 输出，再用 `TaiwuModMergeDependency` 或 `TaiwuModCopyDependency`
声明打包动作。游戏或运行时已经提供的 DLL 作为外部运行时依赖处理。

被合并的依赖会内部化并重命名，降低不同 mod 携带同名依赖时的冲突风险。需要调整内部化策略时，在项目中设置：

```xml
<PropertyGroup>
  <InternalizeMergedDependencies>false</InternalizeMergedDependencies>
</PropertyGroup>
```

前后端共同引用的内部共享项目如果要随入口一起部署，由前端和后端入口项目分别声明需要合并或复制的
DLL。这样前后端各自生成自己的最终入口 DLL。
