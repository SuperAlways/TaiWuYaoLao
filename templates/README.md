# templates

模板仓库项目脚手架目录。

`templates/` 下的目录由 `tools/Taiwu.Mods.Cli/` 的创建命令使用，用于生成 Mod 项目和内部共享项目。模板路径使用
Scriban 渲染；需要渲染内容的文件以 `.scriban` 结尾，输出时会剥掉这个后缀。

这些模板只描述新项目的初始骨架。Mod 模板会生成面向使用者的 `README.md` 和面向维护者的 `DEVELOPMENT.md`；现有项目的
真实构建和组包约定以项目文件、`Taiwu.Mod.Pack.proj`、目录 README、lock file 和解决方案注册为准。

命令参数、命令注册和工具实现入口见 [`tools/README.md`](../tools/README.md) 与 `tools/Taiwu.Mods.Cli/`。

## 模板目录

| 路径                | 命令            | 可用变量                                                   |
| ------------------- | --------------- | ---------------------------------------------------------- |
| `templates/mod/`    | `create-mod`    | `mod.name`、`mod.version`                                  |
| `templates/shared/` | `create-shared` | `project.name`、`project.side`、`project.target_framework` |

渲染使用严格变量；模板引用未定义变量时，创建命令会失败。只有需要内容渲染的文件使用 `.scriban` 后缀。

## 输出文案边界

面向 Mod 使用者的 README 模板只生成最小入口：说明这是哪个 Mod，并指向源码维护文档。具体功能、安装方式、品牌表达、发布文案、
兼容性和信息结构由具体 Mod 按自己的读者、发布渠道和维护习惯组织，不在模板中预设填空结构。

- `templates/mod/README.md.scriban` 生成面向 Mod 使用者的最小入口。
- `templates/mod/DEVELOPMENT.md.scriban` 生成面向源码维护者的项目维护入口。
- `templates/mod/Config.Lua.scriban` 生成游戏读取的配置字段占位，包含显式的 `Source = 0` 默认值；字段语义由机制参考维护，
  具体展示取值由具体 Mod 修改。
- `templates/shared/README.md.scriban` 生成内部共享项目维护入口，并承接 `shared/README.md` 的共享 API 约定。

调整目录级规则时，同步复核对应模板，让新项目沿用当前文档关系。更细的同步规则见
[`docs/development/documentation.md`](../docs/development/documentation.md)。
