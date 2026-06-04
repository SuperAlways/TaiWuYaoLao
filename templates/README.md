# Templates

`templates/` 下的目录由 `tools/Taiwu.Mods.Cli/` 的创建命令消费，用于生成 mod
项目和内部共享项目。模板路径使用 Scriban 渲染；需要渲染内容的文件以 `.scriban`
结尾，输出时会剥掉这个后缀。

## 模板目录

| 路径                | 命令            | 可用变量                                                   |
| ------------------- | --------------- | ---------------------------------------------------------- |
| `templates/mod/`    | `create-mod`    | `mod.name`、`mod.version`                                  |
| `templates/shared/` | `create-shared` | `project.name`、`project.side`、`project.target_framework` |

渲染使用严格变量；模板引用未定义变量时，创建命令会失败。新增模板上下文变量时，同步更新
`tools/Taiwu.Mods.Cli/TemplateRenderer.cs` 和这个文件。不需要内容渲染的文件不要添加
`.scriban` 后缀。
