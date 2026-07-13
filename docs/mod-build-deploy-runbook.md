# TaiwuEncyclopedia Mod 构建、打包与部署到游戏 — 操作手册

本操作手册记录把 `TaiwuEncyclopedia` mod 从源码构建、打包成可部署目录、部署到本机游戏目录并在游戏内验收的完整流程。依据是 2026-07-03 的一次端到端部署（master `a2a8dab` + 本次 RCS1161 修复）。本文服务下次部署借鉴，不重复 `README.md` 的模板通用说明。

## 核心结论

- **三个缺一不可的产物文件**：`Config.Lua`（mod 清单，游戏靠它识别 mod + 声明 FrontendPlugins）、`Taiwu.Mod.Pack.proj`（组包入口）、编译后的 `Plugins/TaiwuEncyclopedia.Frontend.dll`。前两个**不在 git 里**（见下文「已知坑」），需从 `feat/frontend` worktree 或备份取得。
- **本机已绕开 GitHub Packages 403**：`da3e4af` commit 用 `ReferencePackager` 从游戏 DLL 生成本地引用包到 `artifacts/reference-packages/1.0.44/`，`NuGet.config` 的 `taiwu-modkit-local` 源指向这里。只要游戏装在本机、该目录存在，`dotnet restore` 不需要 PAT。
- **打包用 `pack-mod`**：`dotnet run --project tools/Taiwu.Mods.Cli -- pack-mod --name TaiwuEncyclopedia`，产物到 `artifacts/mods/TaiwuEncyclopedia/`。IL Repack 自动把 Frontend + Core + Markdown 合并成单个 `TaiwuEncyclopedia.Frontend.dll`。
- **部署 = 替换游戏 `Mod/TaiwuEncyclopedia/`**，但**必须保留 `Settings.Lua`**（玩家本机 mod 设置，游戏运行时写回，不在打包产物里）。
- **Frontend 的 `TreatWarningsAsErrors=true`**（`Directory.Build.props`）+ Roslynator 4.15.0：任何 style 诊断都会让 build 失败。ConfigPanel.cs 用文件级 `#pragma warning disable` 列表抑制，**新增诊断 ID 要记得加进列表**。

## 前置条件

| 项 | 值 / 位置 |
|---|---|
| .NET SDK | 10.0.300（`global.json` 锁定，PATH 里的 `dotnet`） |
| 游戏目录 | `D:\game\Steam\steamapps\common\The Scroll Of Taiwu` |
| 游戏 DLL | `The Scroll of Taiwu_Data/Managed/`（Unity）、`Backend/`（游戏后端） |
| 本地引用包 | `artifacts/reference-packages/1.0.44/*.nupkg`（5 个：Plugin/Shared/Unity/Frontend/Backend） |
| 仓库 | `D:/shuoshu/taiwuask/TaiWuYaoLao`，分支 `master` |
| 缺失文件来源 | `feat/frontend` worktree：`D:/shuoshu/taiwuask/TaiWuYaoLao-frontend/mods/TaiwuEncyclopedia/{Config.Lua,Taiwu.Mod.Pack.proj}` |

## 端到端流程

### 1. 确认本地引用包存在

```bash
ls artifacts/reference-packages/1.0.44/*.nupkg
# 期望 5 个 .nupkg。若缺，用 ReferencePackager 从游戏目录重新生成：
#   TAIWU_MODKIT_GAME_DIR='<游戏目录>' dotnet run --project tools/Taiwu.ModKit.ReferencePackager -- pack --version 1.0.44
```

### 2. 还原 + 构建

```bash
cd "D:/shuoshu/taiwuask/TaiWuYaoLao"
dotnet restore Taiwu.Mods.slnx
dotnet build Taiwu.Mods.slnx
# 期望：0 errors, 0 warnings
```

若 Frontend 报 Roslynator 诊断（如 `RCS1161`），把该诊断 ID 加进报错文件的 `#pragma warning disable` / `restore` 列表（ConfigPanel.cs 第 1 行与第 1168 行），与该文件既有约定一致。

### 3. 确保组包入口存在

```bash
ls mods/TaiwuEncyclopedia/{Config.Lua,Taiwu.Mod.Pack.proj}
# 若缺（master 上没有），从 feat/frontend worktree 复制：
#   cp "D:/shuoshu/taiwuask/TaiWuYaoLao-frontend/mods/TaiwuEncyclopedia/Config.Lua" mods/TaiwuEncyclopedia/
#   cp "D:/shuoshu/taiwuask/TaiWuYaoLao-frontend/mods/TaiwuEncyclopedia/Taiwu.Mod.Pack.proj" mods/TaiwuEncyclopedia/
```

`Taiwu.Mod.Pack.proj` 声明的打包内容（当前配置）：

| Item | 类型 | 说明 |
|---|---|---|
| `Config.Lua` | File | mod 清单，PackagePath=`Config.Lua` |
| `src/Frontend/TaiwuEncyclopedia.Frontend.csproj` | Project | 入口程序集，IL Repack 合并 Core+Markdown |
| `Skills` | Directory | 百晓册知识库（总纲 + 10 章 + glossary + registry + concept_index） |

### 4. 版本号自增（每次迭代 +1）

源码 `Config.Lua` 的 `Version` 格式 `1.0.NN`，每次部署把末位 `NN` 加 1，同时更新 `Title` 里的尾缀。

```bash
cd "D:/shuoshu/taiwuask/TaiWuYaoLao"
CFG="mods/TaiwuEncyclopedia/Config.Lua"

# 读当前 Version（如 1.0.01），取末位 +1（01 → 02），补零到两位
CUR=$(grep '^  Version = ' "$CFG" | sed 's/.*"\(.*\)".*/\1/')
SUF=$(echo "$CUR" | sed 's/.*\.//')
NEW_SUF=$(printf "%02d" $((10#$SUF + 1)))
NEW_VER=$(echo "$CUR" | sed "s/\.[0-9]*\$/.$NEW_SUF/")

# 更新 Version 和 Title（Title 形如 "太吾药老 V1.0.01" → "太吾药老 V1.0.02"）
sed -i "s/Version = \"$CUR\"/Version = \"$NEW_VER\"/" "$CFG"
sed -i "s/Title = \"\(.*\) V$CUR\"/Title = \"\1 V$NEW_VER\"/" "$CFG"

grep -E "Title|Version" "$CFG"
```

提交版本号变更：

```bash
git add "$CFG"
git commit -m "chore: bump version to $NEW_VER"
```

### 5. 打包

```bash
dotnet run --project tools/Taiwu.Mods.Cli -- pack-mod --name TaiwuEncyclopedia
# 产物：artifacts/mods/TaiwuEncyclopedia/
#   Config.Lua
#   Plugins/TaiwuEncyclopedia.Frontend.dll  (IL Repack 合并产物)
#   Skills/  (6435 md + concept_index.json + registry.yaml + guidance + personas + ...)
```

验证产物完整性：

```bash
find artifacts/mods/TaiwuEncyclopedia/Skills -name "*.md" | wc -l   # 期望 6435
ls artifacts/mods/TaiwuEncyclopedia/Skills/background/               # 期望 10 章 + overview.md，含 zhandou 不含 zhan-dou
ls artifacts/mods/TaiwuEncyclopedia/Plugins/                          # 期望 TaiwuEncyclopedia.Frontend.dll
```

### 6. 部署到游戏（保留 Settings.Lua）

```bash
GAME_MOD="D:/game/Steam/steamapps/common/The Scroll Of Taiwu/Mod/TaiwuEncyclopedia"
GAME_MOD_DIR="D:/game/Steam/steamapps/common/The Scroll Of Taiwu/Mod"
ART="D:/shuoshu/taiwuask/TaiWuYaoLao/artifacts/mods/TaiwuEncyclopedia"
TIMESTAMP=$(date +%Y%m%d-%H%M%S)

# 删除之前的老备份（Mod 目录内外都扫一遍，避免游戏扫到 Mod 目录内的备份）
rm -rf "${GAME_MOD_DIR}/../TaiwuEncyclopedia.bak."*
rm -rf "${GAME_MOD_DIR}/TaiwuEncyclopedia.bak."*

# 备份当前部署（含 Settings.Lua，可回滚）
cp -r "$GAME_MOD" "${GAME_MOD_DIR}/../TaiwuEncyclopedia.bak.${TIMESTAMP}"

# 清空游戏 mod 目录，但保留 Settings.Lua
find "$GAME_MOD" -mindepth 1 ! -name "Settings.Lua" -exec rm -rf {} +

# 复制新产物进去
cp -r "$ART"/* "$GAME_MOD"/

# 在 Config.Lua 的 Title 上追加时间戳，游戏内模组管理可见
sed -i "s/Title = \"\(.*\)\"/Title = \"\1 (${TIMESTAMP})\"/" "$GAME_MOD/Config.Lua"
```

`Settings.Lua` 是游戏运行时写回的玩家设置（mod 配置面板里的值），**不在打包产物里**，清空时必须显式保留，否则玩家配置丢失。

### 7. 游戏内验收

启动太吾绘卷，按 frontend plan 的验收清单检查：

1. **启动**：mod 加载无报错 / 事件窗出现「百晓问答」按钮 / F8 开关 ChatPanel / 无配置时点发送提示去配置 / 未进档显示「请先进入存档」+ 禁用输入
2. **功能**：配 LLM 测试连接通过 / 问答流式 + markdown 渲染 / RAG 思考步骤 + 参考文献 / skill 加载 / 多轮上下文 / persona 切换下条消息用新风格
3. **百晓册**：提问触发 `load_background_skill` 加载章节 overview / `lookup_concept` 查概念 / 总纲常驻（去重后纯叙事无 `[查:xxx]`）

## 已知坑

### 坑 1：`Config.Lua` 与 `Taiwu.Mod.Pack.proj` 不在 master 上

`create-mod` 本该生成这两个文件，但 `TaiwuEncyclopedia` 的这两个文件只存在于 `feat/frontend` worktree（`D:/shuoshu/taiwuask/TaiWuYaoLao-frontend/mods/TaiwuEncyclopedia/`），从未提交到 master。master 上 clean checkout 后 `pack-mod` 会报：

```
错误：未找到 mod 组包入口：.../mods/TaiwuEncyclopedia/Taiwu.Mod.Pack.proj
```

**应对**：从 worktree 复制（见步骤 3），或从游戏已部署目录回拷 `Config.Lua`（`Taiwu.Mod.Pack.proj` 不在游戏目录，必须从 worktree）。长期应把这两个文件提交到 master。

### 坑 2：Frontend `TreatWarningsAsErrors=true` + Roslynator

`Directory.Build.props` 设 `TreatWarningsAsErrors=true`、`AnalysisLevel=latest`，引用 Roslynator.Analyzers 4.15.0。任何 style 诊断（RCS/IDE/CA）直接判 error。ConfigPanel.cs 用文件级 `#pragma warning disable` 列表抑制了一大批，但列表可能漏 ID（如本次 `RCS1161`：enum 未声明显式值）。

**应对**：build 报错时，把诊断 ID 加进报错文件的 pragma disable/restore 列表（两处都要加），不要改成显式值——与该文件既有约定一致。其它 Frontend 文件若报错，参照其各自的 pragma 列表或文件级 `#pragma warning disable` 模式。

### 坑 3：游戏目录路径硬编码差异

不同文档/参照 mod 的游戏路径写法不一：
- 本机实际：`D:\game\Steam\steamapps\common\The Scroll Of Taiwu`
- handoff 旧文：`D:/software/steam/steamapps/common/...`
- jianghu-youling 参照：硬编码同格式

**应对**：以本机 `D:\game\Steam\...` 为准；脚本里用变量 `GAME_MOD` 而非硬编码，便于换机。

### 坑 4：`find -exec rm` 在大目录上的竞态

清空游戏 mod 目录时，`find ... -exec rm -rf {} +` 偶发报 `No such file or directory`（find 遍历中目录已被删）。这是无害的竞态，不影响最终结果——只要复制后用 `find "$GAME_MOD/Skills" -name "*.md" | wc -l` 验证完整性即可。

### 坑 5：Config.lua 字段须对齐 Workshop 标准

自编的 TagList / GameVersion 等字段值在 Steam Workshop 上不合法。参照已发布 mod（如太吾WorldTalk `3729307604`）逐字段对齐：

| 字段 | 要求 | 说明 |
|------|------|------|
| `TagList` | 必须用 Workshop 合法标签 | 已知合法：Extensions / Stories / Frameworks / Compatible Mods / Optimizations / Modifications / Display / Configurations |
| `GameVersion` | 填实际游戏版本号 | 不是 `"1.0.0"` 占位符。参照同期已发布 mod 的 GameVersion |
| `Cover` | 必须有，指向封面图 | 和 `WorkshopCover` 值相同 |
| `Visibility` | `0` | 显式声明，0 = 公开可见 |
| `HasArchive` | `false` | 除非 mod 确实使用游戏存档数据 |

**应对**：每次发布前检查上述字段，以已上架 mod 的 `config.lua` 为参照。

## 回滚

```bash
GAME_MOD="D:/game/Steam/steamapps/common/The Scroll Of Taiwu/Mod/TaiwuEncyclopedia"
GAME_MOD_DIR="D:/game/Steam/steamapps/common/The Scroll Of Taiwu/Mod"
ls -d "${GAME_MOD_DIR}/../TaiwuEncyclopedia.bak."*          # 列出备份
# 选一个备份恢复：
rm -rf "$GAME_MOD"
mv "${GAME_MOD_DIR}/../TaiwuEncyclopedia.bak.<timestamp>" "$GAME_MOD"
```

## 相关文档

- [`README.md`](../README.md) — 模板通用构建、打包、发布说明
- [太吾游戏 Mod 配置与 Steam 发布边界](taiwu-mod-steam-publishing-boundary.md) — `Config.Lua` / `Settings.Lua` / Workshop 边界
- `docs/superpowers/plans/2026-07-01-taiwu-frontend.md` Step 3 — 游戏内手动验收清单原始来源
