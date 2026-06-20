# 贡献入口

本文面向准备向本模板仓库提交 issue、讨论或 PR 的人，帮助贡献者先找到变更入口，再完成提交前检查。完整开发维护手册见
[`docs/development/README.md`](docs/development/README.md)。

只想从模板创建、构建或打包自己的 Mod 仓库时，从根 [`README.md`](README.md) 开始。

## 先判断变更类型

| 变更 | 先读 |
| --- | --- |
| 使用模板创建仓库、创建项目、构建、打包和发布 | [`README.md`](README.md) |
| 维护模板仓库的构建、检查、模板、工具或工作流 | [`docs/development/README.md`](docs/development/README.md) |
| 文档结构、受众分层、模板文档同步 | [`docs/development/documentation.md`](docs/development/documentation.md) |
| Mod 目录约定、组包、插件入口、Taiwu 引用和依赖部署规则 | [`mods/README.md`](mods/README.md) |
| 生成 Mod 的最小 README 入口和维护入口模板 | [`templates/mod/README.md.scriban`](templates/mod/README.md.scriban)、[`templates/mod/DEVELOPMENT.md.scriban`](templates/mod/DEVELOPMENT.md.scriban) |
| 内部共享项目目录约定和共同边界 | [`shared/README.md`](shared/README.md) |
| 共享项目 README 模板 | [`templates/shared/README.md.scriban`](templates/shared/README.md.scriban) |
| 创建/移除命令实现、模板变量和渲染规则 | [`tools/README.md`](tools/README.md)、[`templates/README.md`](templates/README.md) |

## 提交前检查

- 提交文档变更时，先确认主要读者；不要把模板使用入口、贡献入口、维护手册、生成 Mod 的最小 README 入口和生成 Mod 的维护入口混在同一处。
- 修改生成模板的 README 或 DEVELOPMENT 时，同步复核生成后的读者路径，避免新项目重新带出旧的文档关系。
- 修改 `PackageReference`、`Directory.Packages.props` 或新增项目后，运行 restore，并提交对应项目的
  `packages.lock.json`。
- 修改文档、配置或项目文件后，运行 `dotnet msbuild repo.proj -t:Check`。
- 修改 C# 源码后，按影响范围运行 `dotnet build Taiwu.Mods.slnx` 或对应项目构建。
- 修改组包入口、发布目录或插件依赖部署后，运行受影响 Mod 的 `pack-mod` 命令，或用生成出的测试 Mod 验证。
- 不要提交 `.env`、真实 token 或本机路径；文档和配置只保留环境变量名、占位值或可复现的相对路径。

需要更细的命令、环境变量、发布 tag 和模板同步规则时，回到 [`docs/development/README.md`](docs/development/README.md)。
