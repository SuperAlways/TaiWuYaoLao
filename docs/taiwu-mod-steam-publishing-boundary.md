# 太吾游戏 Mod 配置与 Steam 发布边界

这份机制参考说明太吾绘卷游戏内 Mod 配置、用户设置、插件入口和 Steam Workshop 发布之间的边界。依据是太吾读取和
写回 `Config.Lua` 的游戏行为、太吾内置上传流程，以及 Steam Workshop 内容目录、item 属性和 custom metadata 语义。

本文只解释机制边界。具体 Mod 的字段取值、模板初始值、`pack-mod` 组包声明和发布流水线见具体 Mod README、
`mods/README.md`、根 README 或专门的仓库经验文档。

## 核心结论

- `Config.Lua` 是太吾绘卷读取的 Mod 配置文件，不是 Steam 自己定义的配置文件。Steam Workshop 相关字段只是写在同一个
  Lua table 里、由太吾同步到 Steam item 状态的字段。
- Steam Workshop 内容包不是按 `Config.Lua` 字段生成的白名单。太吾上传时把当前 Mod 目录作为 Steamworks
  `ISteamUGC.SetItemContent` 的内容目录交给 Steam。
- 标题、简介、标签、可见性、依赖、更新说明、预览图和 custom metadata 通过 Steam API 写入 Workshop item 状态；其中
  一部分也会写回 `Config.Lua`，但不因此成为包内额外文件。
- 普通“编辑后上传/更新”会临时移走 Mod 根目录下的 `Settings.Lua`，避免把玩家本机设置值发布出去；“直接上传”不会执行这个
  保护步骤。

## 文件与数据归属

| 位置或数据 | 归属 | 含义 |
| --- | --- | --- |
| Mod 目录 | 太吾本地 Mod / Steam 内容目录 | `Config.Lua`、太吾识别的运行目录，以及具体 Mod 自己放入的文件和目录的共同目录；上传时作为 Steam 内容目录。 |
| `Config.Lua` | 太吾 Mod 配置 | Mod 信息、插件入口、用户设置定义和 Workshop 发布字段。它是一个返回 Lua table 的文件，放在每个 Mod 目录根部。 |
| `Settings.Lua` | 玩家本机 Mod 设置 | 玩家实际修改后的设置值，由游戏写在 Mod 目录下；普通上传会临时移出，直接上传不会自动排除。 |
| `Config/*.lua` | 修改游戏配置表机制 | 由 `ModConfigDataManager` 读取，和顶层 `Config.Lua` 是两套入口。 |
| `ModSettings.Lua` | 游戏档案目录 | 启用 Mod、排序、白名单和本地临时 FileId 缓存，由游戏维护；不在单个 Mod 目录中。 |
| Steam item 状态 | Steam Workshop | 标题、简介、标签、可见性、依赖、更新说明、预览图和 custom metadata；通过 Steam API 写入或更新，不是内容目录下的文件。 |

## Steam 上传内容边界

太吾内置上传流程不是按 `Config.Lua` 字段枚举文件。提交更新时，游戏把当前 Mod 目录
`_curEditModInfo.DirectoryName` 传给 Steamworks `ISteamUGC.SetItemContent`，作为 Workshop item 的内容目录
（content folder）。订阅端安装完成后，太吾也会在 Steam 返回的安装目录根部读取 `Config.Lua`。

因此，实际随 Workshop 内容包携带的是提交时 Mod 目录下的文件树。只要文件或目录仍在 Mod 目录内，就会作为内容目录的
一部分交给 Steam，例如：

- `Config.Lua`。上传过程中游戏会把它写成发布状态：`Source = 1`、`FileId` 为 published file id，并更新
  `GameVersion` 和 `UpdateLogList`。
- 由 `Config.Lua` 字段或游戏读取路径引用的运行内容，例如 `Plugins/`（插件入口和独立依赖）、
  `LegacyPlugins/`（旧版兼容入口）、`Config/`（配置表修改）和 `Events/`（事件包及事件资源）。这不是内容包白名单。
- `Cover`、`WorkshopCover`、`DetailImageList` 指向且实际存在于 Mod 目录内的图片文件。这些图片如果留在内容目录中，
  会作为普通内容文件随包存在；太吾还会另外通过 Steam API 把封面和详情图设置为 Workshop 预览图。
- 具体 Mod 或组包流程放入的其他文件和目录。它们会随包携带，是因为它们在上传的内容目录里；是否有运行时意义由
  具体 Mod 或组包约定决定，不是太吾或 Steam 的通用目录分类。

判断边界时，把两个问题分开：是否随 Workshop 内容包携带，只看提交给 `SetItemContent` 的目录当时包含什么；是否有运行时
意义，则看太吾读取路径、`Config.Lua` 字段语义或具体 Mod 自己的约定。

普通“编辑后上传/更新”路径有一个明确例外：上传前游戏会临时把 Mod 根目录下的 `Settings.Lua` 移到
`Mod/.TempFileForUploading/Settings.Lua`，提交完成或失败后再移回。因此玩家本机设置值不会随普通上传内容包发布。
这个临时目录位于各 Mod 目录的上一级，也不会随当前 Mod 的内容目录上传。

“直接上传”路径不同：界面只校验所选目录存在且含 `Config.Lua`，读取配置后直接调用同一套 Steam 上传逻辑，并且不会先运行
编辑界面的保存、封面复制、插件复制、清理或 `Settings.Lua` 临时移出逻辑。因此直接上传时，所选目录当时存在的完整文件树会
交给 Steam；如果目录里有 `Settings.Lua`，它也可能随内容包发布。

## Steam 发布状态关系

- `Source = 0` 表示本地/外部 Mod。游戏从本地 `Mod/` 目录读取时，会把非 `0` 的 `Source` 改回 `0` 并写回。
- `Source = 1` 表示 Steam Workshop Mod。上传成功后，太吾会把 Steam 返回的 published file id 写入 `FileId`。
- `Title`、`Description`、`TagList`、`Visibility`、`WorkshopCover`、`DetailImageList` 等字段会参与太吾内置
  Workshop 上传或展示流程。
- `Dependencies` 对应 Steam Workshop item 依赖。Steam 侧查询结果也会把远端依赖同步回太吾的 Mod 信息。
- `UpdateLogList` 记录太吾维护的历史更新说明；当次上传的更新说明还会作为 `SubmitItemUpdate` 的 change note 提交。
- `Author`、`ChangeConfig`、`HasArchive` 和 `NeedRestartWhenSettingChanged` 会被太吾写入 Steam custom metadata；
  其中 `NeedRestartWhenSettingChanged` 在远端 metadata 中使用 `NeedRestart` 键。查询 Workshop item 时，太吾也会从
  custom metadata 还原这些展示和风险标记。

## Config.Lua 字段

下面的字段说明描述太吾读取和写回 `Config.Lua` 的语义，不是 Steam API schema，也不是发布内容白名单。
模板初始字段由 `templates/mod/Config.Lua.scriban` 和生成项目 README 说明。下面列出的字段由游戏读取、写回或
具体功能使用；未出现在模板起步字段中的发布、设置、依赖、封面、风险提示等字段，按实际 Mod 需要添加即可。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `Title` | string | 游戏内和 Workshop 展示名称。 |
| `Source` | number | 太吾 `ModSource`：`0` 是本地/外部 Mod，`1` 是 Steam Workshop，`2` 是 DLC。游戏从本地 `Mod/` 目录读取时会把非 `0` 改回 `0` 并写回。 |
| `FileId` | number | `ModId` 的文件 id。Steam Mod 使用 Workshop `PublishedFileId`；本地 Mod 可为 `0`，游戏会按目录名生成并缓存临时 id。 |
| `Version` | string 或 number | Mod 版本。字符串会按 .NET `System.Version` 解析成 `ModId.Version`；点分数字版本可被直接解析。 |
| `GameVersion` | string | 该 Mod 记录的太吾游戏版本，用于过期判断和旧版插件兼容分支。字段缺省时会被视为未声明兼容版本；游戏写回时会更新为当前游戏版本。 |
| `Author` | string | 作者名；上传时也会写入 Steam custom metadata。 |
| `Description` | string | Mod 简介；上传时同步为 Workshop 描述。 |
| `Cover` | string 或 nil | 本地展示封面路径。上传时如果 `WorkshopCover` 为空，会尝试用它作为 Workshop 预览图。 |
| `WorkshopCover` | string 或 nil | Workshop 预览图路径；为空时回退到 `Cover`。 |
| `DetailImageList` | string list | Workshop/详情页附加预览图路径列表。字段缺省时按空列表处理；无详情图时游戏写回会移除该字段。 |
| `Visibility` | number | Workshop 可见性：`0` public，`1` friends only，`2` private，`3` unlisted。 |
| `TagList` | string list | Mod 标签；上传时同步到 Workshop tags，也用于游戏内 Workshop 过滤。 |
| `Dependencies` | number list | Workshop 依赖的 published file id 列表。它表达 Steam Workshop item 之间的依赖关系，不是 DLL 依赖清单。 |
| `DefaultSettings` | table list | Mod 设置项定义和默认值。玩家实际值写入 `Settings.Lua`，运行时进入 `SerializableModData`。 |
| `SettingGroups` | string list | 设置界面分组顺序；设置项的 `GroupName` 可引用这里的名字。 |
| `UpdateLogList` | table list | 太吾上传流程维护的更新日志历史，元素包含 `Timestamp` 和 `LogList`。 |
| `ChangeConfig` | bool | “修改游戏配置”风险标记。若 Mod 修改游戏配置表，开启后存档读取界面可在 Mod 缺失时提示风险；上传时也会写入 Steam custom metadata。 |
| `HasArchive` | bool | “含有存档数据”风险标记。若 Mod 会向存档写入自身数据，开启后存档读取界面可在 Mod 缺失时提示风险；上传时也会写入 Steam custom metadata。 |
| `NeedRestartWhenSettingChanged` | bool | 设置修改后是否需要重启。开启后玩家修改设置时，游戏会标记需要重启并弹出确认提示；上传时写入 Steam custom metadata 的 `NeedRestart` 键。 |
| `BackendPlugins` | string list | 后端插件入口 DLL，路径相对 `Plugins/`。太吾后端从这些入口加载插件。 |
| `BackendPluginsLegacy` | string list | 后端旧版插件入口兼容字段；当 legacy 列表存在且版本判断需要兼容入口时，游戏会回退使用。 |
| `BackendPatches` | string list | 后端 patch 清单字段。 |
| `FrontendPlugins` | string list | 前端插件入口 DLL，路径相对 `Plugins/`。太吾前端从这些入口加载插件。 |
| `FrontendPluginsLegacy` | string list | 前端旧版插件入口兼容字段；当 legacy 列表存在且版本判断需要兼容入口时，游戏会回退使用。 |
| `FrontendPatches` | string list | 前端 patch 清单字段。 |
| `EventPackages` | string list | 事件包 DLL 清单。后端会从 Mod 的 `Events/` 目录加载这些事件包。 |

## Description 格式边界

`Description` 是太吾读取和写回的 Mod 简介字段；上传时会同步为 Steam Workshop item 描述。Workshop 页面渲染这段文本时
使用 Steam 社区格式，涉及可用标签、解析差异和字符限制时，参考 Steam 社区指南
[`Comprehensive Formatting Help`](https://steamcommunity.com/sharedfiles/filedetails/?id=2807121939) 中的
UGC description/summary 条目。

本文只记录太吾如何读取和同步该字段，不维护 BBCode 标签、解析差异或限制清单。具体 Mod 的简介文案仍由各自的
`Config.Lua` 维护；展示效果以 Steam 实际编辑或预览结果为准。

## DefaultSettings

每个设置项都有这些公共字段：

| 字段 | 含义 |
| --- | --- |
| `SettingType` | 设置类型，只能是 `Toggle`、`ToggleGroup`、`InputField`、`Slider`、`Dropdown` 之一。 |
| `Key` | 运行时读取设置值的键名。前后端插件用 `ModManager.GetSetting` 或 `DomainManager.Mod.GetSetting` 读取。 |
| `DisplayName` | 设置界面显示名。 |
| `Description` | 设置说明。 |
| `GroupName` | 可选分组名；配合顶层 `SettingGroups` 使用。 |

各类型的专属字段：

| `SettingType` | 专属字段 | 值类型 |
| --- | --- | --- |
| `Toggle` | `DefaultValue` | bool |
| `InputField` | `DefaultValue` | string |
| `Dropdown` | `Options`、`DefaultValue` | `Options` 是 string list；`DefaultValue` 是选项索引。 |
| `ToggleGroup` | `Toggles`、`DefaultValue` | `Toggles` 是 string array；`DefaultValue` 是选项索引。 |
| `Slider` | `MinValue`、`MaxValue`、`StepSize`、`DefaultValue` | int；`StepSize` 省略时默认为 `1`。 |

`Dropdown`、`ToggleGroup` 和 `Slider` 的值会被游戏夹到合法范围内。玩家改值后，游戏把实际值写入
`Settings.Lua`，再同步给前后端运行时；插件收到设置更新时会调用 `OnModSettingUpdate`。

## 依据

本文的机制依据来自太吾游戏侧的 `ModManager`、`ModInfoWithDisplayData`、`SteamManager`、
`UI_ModPanel`、`Game.Views.Mod.Upload.ModUploadEditPanel`、`ModDirectlyUploadPanel`、
`GameData.Domains.Mod.ModInfo`、`ModSource`、`EModVisibility` 和设置项类型读取路径；Steam 内容目录和 item 状态语义参考
Steamworks [`ISteamUGC`](https://partner.steamgames.com/doc/api/ISteamUGC) 与
[`Workshop Implementation Guide`](https://partner.steamgames.com/doc/features/workshop/implementation)。组织内部可通过
[`taiwu-modkit`](https://github.com/Wanxiang-Sanctum/taiwu-modkit) 仓库根目录下的 `game/` 生成快照检索这些游戏侧路径；
快照只是检索入口，不是机制来源。
