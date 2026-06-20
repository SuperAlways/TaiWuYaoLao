# Templates

`templates/` 下的目录由 `tools/Taiwu.Mods.Cli/` 的创建命令使用，用于生成 mod
项目和内部共享项目。模板路径使用 Scriban 渲染；需要渲染内容的文件以 `.scriban`
结尾，输出时会剥掉这个后缀。

这些模板只描述新项目的初始骨架；现有项目的真实构建和组包约定以项目文件、
`Taiwu.Mod.Pack.proj`、目录 README 和解决方案注册为准。

命令参数、命令注册和工具实现入口见 `tools/README.md` 与 `tools/Taiwu.Mods.Cli/`。

## 模板目录

| 路径                | 命令            | 可用变量                                                   |
| ------------------- | --------------- | ---------------------------------------------------------- |
| `templates/mod/`    | `create-mod`    | `mod.name`、`mod.version`                                  |
| `templates/shared/` | `create-shared` | `project.name`、`project.side`、`project.target_framework` |

渲染使用严格变量；模板引用未定义变量时，创建命令会失败。只有需要内容渲染的文件使用 `.scriban` 后缀。
