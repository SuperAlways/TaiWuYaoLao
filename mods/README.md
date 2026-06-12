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
  Taiwu.Mod.Pack.proj
  README.md
  src/
    Frontend/
    Backend/
```

每个插件项目通过项目旁的 `Taiwu.Mod.props` 标记端侧。前端项目默认目标框架为
`netstandard2.1`，后端项目默认目标框架为 `net8.0`；这两个默认值由
`mods/Directory.Build.props` 根据端侧设置。

构建插件项目时可以直接使用 `dotnet build`。普通构建只负责 SDK 默认的 `bin/` 和 `obj/` 输出，
不解析包产物、不组装 `artifacts/mods/`，也不执行依赖合并。

打包可部署目录：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- pack-mod --name MyMod
```

`pack-mod` 默认使用 `Release` 运行 `mods/MyMod/Taiwu.Mod.Pack.proj`，并把该组包入口
声明的文件、目录和项目产物组装到 `artifacts/mods/MyMod/`。

## 组包声明

每个 mod 的 `Taiwu.Mod.Pack.proj` 是可部署目录的组包入口。它只描述最终目录由哪些文件、目录和
项目组成，不给项目额外标记类型；太吾插件、共享 DLL、发布目录和普通静态文件都走同一条
组合路径。

```xml
<Project>
  <Import
    Project="$([MSBuild]::GetPathOfFileAbove('Taiwu.Mods.Paths.props', '$(MSBuildThisFileDirectory)'))"
  />
  <Import Project="$(TaiwuModsModsDir)Taiwu.Mod.Pack.targets" />

  <ItemGroup>
    <TaiwuModPackFile Include="Config.Lua" PackagePath="Config.Lua" />
    <TaiwuModPackProject Include="src/Frontend/MyMod.Frontend.csproj" />
    <TaiwuModPackProject Include="src/Backend/MyMod.Backend.csproj" />
  </ItemGroup>
</Project>
```

在组包入口中，`TaiwuModPackFile` 复制单个文件，`TaiwuModPackDirectory` 复制目录，
`TaiwuModPackProject` 引入一个参与组包的项目。包内路径写在 `PackagePath` 元数据中，必须是相对路径。

被 `TaiwuModPackProject` 引入的项目通过项目级包产物进入最终目录。`mods/Directory.Build.targets`
已经为 `mods/` 下的普通 SDK 项目导入默认项目组包目标；前端和后端插件项目还会自动把入口 DLL
声明为 `Plugins/<TargetFileName>`。模板生成的前后端项目通常只需要在 `Taiwu.Mod.Pack.proj`
中被引用，不需要手写入口程序集声明。

项目自身需要额外输出文件或目录时，在项目文件或项目旁的 `Taiwu.Mod.props` 中声明：

```xml
<ItemGroup>
  <TaiwuModPackFile Include="$(TargetPath)" PackagePath="Plugins/MyMod.Ipc.dll" />
</ItemGroup>
```

项目级可用声明包括：

- `TaiwuModPackFile`：复制单个文件。
- `TaiwuModPackDirectory`：复制目录。
- `TaiwuModPackEntry`：入口程序集。只有项目需要自行声明入口 DLL 并参与依赖合并时才直接使用。

`TaiwuModPackProject` 只用于 mod 的组包入口，不在项目级继续嵌套。

需要把某个项目的 `dotnet publish` 输出目录整体放进包内时，在该项目中导入发布目录组包目标：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <TaiwuModPublishPackagePath>Tools/Worker</TaiwuModPublishPackagePath>
  </PropertyGroup>

  <Import Project="$(TaiwuModsModsDir)Taiwu.Mod.PublishDirectory.Pack.targets" />
</Project>
```

然后在 mod 的 `Taiwu.Mod.Pack.proj` 中加入：

```xml
<ItemGroup>
  <TaiwuModPackProject Include="src/Worker/MyMod.Worker.csproj" />
</ItemGroup>
```

`pack-mod` 会先运行该项目的 `Publish` target，再把 `$(PublishDir)` 复制到 `Tools/Worker/`。
没有显式设置 `TaiwuModPublishPackagePath` 时，发布目录默认进入 `Processes/<ProjectName>/`。
项目可以用普通 .NET publish 属性控制是否 self-contained、single-file、RID 等发布细节。

只有维护新的组包 helper，或项目不使用仓库默认项目组包目标时，才需要直接关心
`ResolveTaiwuModPackOutputs`。这是 `pack-mod` 读取项目包产物的 MSBuild 边界；CLI 使用
MSBuild 目标结果 JSON，不要求项目生成额外清单文件。

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

前端和后端插件项目会自动把自身入口 DLL 声明为 `Plugins/<TargetFileName>`。额外依赖需要在插件项目旁
的 `Taiwu.Mod.props` 或项目文件中显式声明。普通 `dotnet build` 负责生成项目常规输出；`pack-mod`
在构建后读取项目包产物组装最终包。

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
`TaiwuModCopyDependency` 会报错；复制依赖写入 `Plugins/<DLL 文件名>`。这两个依赖声明只表达太吾
插件入口的 DLL 处理方式；非插件项目的运行时依赖应放在项目自己的发布目录中。

`Include` 只写 DLL 文件名。`pack-mod` 不读取 NuGet 缓存路径或任意项目输出路径，而是在入口项目
本次构建后，从进入该项目输出目录的 DLL 中按文件名匹配，再执行复制或合并。

需要随 mod 部署的依赖，要先通过项目自身的 `ProjectReference`、`PackageReference` 等标准引用进入
入口项目输出目录，再用 `TaiwuModMergeDependency` 或 `TaiwuModCopyDependency` 声明打包动作。
游戏或运行时已经提供的 DLL 作为外部运行时依赖处理。

被合并的依赖会内部化并重命名，降低不同 mod 携带同名依赖时的冲突风险。需要调整内部化策略时，在项目中设置：

```xml
<PropertyGroup>
  <InternalizeMergedDependencies>false</InternalizeMergedDependencies>
</PropertyGroup>
```

前后端共同引用的内部共享项目如果要随入口一起部署，由前端和后端入口项目分别声明需要合并或复制的
DLL。这样前后端各自生成自己的最终入口 DLL。
