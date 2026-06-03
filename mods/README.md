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

打包可部署目录：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- pack-mod --name MyMod
```

`pack-mod` 默认使用 `Release` 构建前后端项目，并把 `Config.Lua` 和插件 DLL 组装到
`artifacts/mods/MyMod/`。普通 `dotnet build` 使用 SDK 默认的 `bin/` 和 `obj/` 输出目录。

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

## 依赖内部化

插件项目默认使用 `ILRepack.Lib.MSBuild.Task` 把 runtime/copy-local DLL 合并进插件主 DLL，并对
这些输入程序集做内部化和重命名，降低不同 mod 携带同名依赖时的冲突风险。被合并的 DLL 会从插件
输出目录中移除；默认打包结果只需要插件主 DLL。

进入 NuGet `ref/` 目录的编译期引用，以及标记为 `CopyLocal=false` 的引用，保持为编译输入。
太吾游戏引用包因此会保留为外部游戏依赖。

```xml
<PropertyGroup>
  <InternalizeRuntimeDependencies>false</InternalizeRuntimeDependencies>
</PropertyGroup>
```

上面的配置可以关闭默认内部化。如果某个 runtime/copy-local DLL 需要保持为独立文件并随插件部署，
在 `Taiwu.Mod.props` 中排除对应程序集文件名，不带 `.dll`：

```xml
<ItemGroup>
  <KeepDependencySeparate Include="Your.Dependency.AssemblyName" />
</ItemGroup>
```
