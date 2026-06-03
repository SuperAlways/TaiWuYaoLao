# {{ModName}}

太吾绘卷 Mod。

## 开发

从仓库根目录构建插件项目：

```powershell
dotnet build mods/{{ModName}}/src/Frontend/{{ModName}}.Frontend.csproj
dotnet build mods/{{ModName}}/src/Backend/{{ModName}}.Backend.csproj
```

打包可部署目录：

```powershell
dotnet run --project tools/Taiwu.Mods.Cli -- pack-mod --name {{ModName}}
```

`pack-mod` 会把 `Config.Lua` 和插件 DLL 组装到仓库根目录的
`artifacts/mods/{{ModName}}/`。

插件项目的本地构建设置写在对应项目的 `Taiwu.Mod.props`。

## 项目结构

- `Config.Lua`：游戏读取的 mod 配置。
- `src/Frontend/`：前端插件项目。
- `src/Backend/`：后端插件项目。
