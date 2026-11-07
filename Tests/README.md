# Tests

**Language / 语言:** [English](#english) | [中文](#chinese)

<a id="english"></a>

`run-tests.ps1` (after `dotnet build Hosts/Desktop` in the repository root) runs, in this order:

0. **unit**: `dotnet test Tests` — the in-process unit tests in `Unit/` (schema validator, every data
   file against its schema, invalid mod data falls back, asset layer resolution order, original names,
   variant folders, layout buckets, hit testing rules, the IAP removal, AI evaluation, map pipeline).
1. **audio**: `--test-audio` decodes every music file and loads every sound effect.
2. **data**: `--verify-data` checks every JSON data file against its schema, then loads every type from
   the JSON and from the original XML (or built-in table) it was converted from and compares the
   definitions field by field. A deliberate data change shows up here; that is intended
   (the game data is meant to match the original until someone changes it on purpose).
3. **assets**: `WC2.Tools validate` checks the built package: every original is reachable by its old
   name, every index entry exists, every asset the data names exists, the whole tree is packaged.
4. **scripts**: every `*.txt` here, in deterministic mode: `--seed 20261106 --fixed-step` and a fresh
   documents folder per script (`out/<script>/docs`). Each `dump NAME` writes the rule state
   (countries, areas, units, commander) as JSON; the runner compares it byte for byte with
   `golden/<script>/NAME.json`. Each `shot NAME` is compared with `golden/<script>/NAME.jpg`
   (`WC2.Tools compare-image`: scaled to the reference size, a pixel counts as changed when a channel
   differs by more than 40, the shot fails above 1 % changed pixels; a `NAME.diff.png` marks them).
   A script whose first line is `#! ARGS` gets ARGS on the command line (window size).

```powershell
.\Tests\run-tests.ps1                 # everything
.\Tests\run-tests.ps1 -Filter conquest
.\Tests\run-tests.ps1 -Record         # store this run's dumps as the new goldens
.\Tests\run-tests.ps1 -RecordShots    # store this run's screenshots as the new references
.\Tests\run-tests.ps1 -RealTime       # real clock, no goldens (the old smoke-test behaviour)
.\Tests\run-tests.ps1 -Bin DIR        # the desktop build in DIR (dotnet build Hosts/Desktop -o DIR), e.g. while the game runs
```

Record new goldens only when a rule change is intended, and new reference screenshots only when a
visual change is intended; say so in the commit. Screenshots and logs go to `out/`.

## Script commands

Coordinates are logical screen units (568 x 320 phone layout; scripts named `ipad_*.txt` run with
`--ipad` in the 1024 x 768 tablet layout). Times are game seconds.

| Command | Effect |
|---|---|
| `wait S` | wait S seconds |
| `click X Y`, `press X Y`, `move X Y`, `release X Y` | touch input |
| `drag X1 Y1 X2 Y2 [S]` | press, move in steps, release |
| `back` | the Android back key (`CallNativeExit`: a panel or state that handles it closes, otherwise the exit box appears) |
| `wheel DELTA` | mouse wheel |
| `camera X Y [SCALE]` | put the map camera on a scene position (the zoom limit and the wrap still apply) and log where it ended up |
| `count NAME` | write `NAME.json` next to the dumps: land units at sea (transport ships), ships at sea, harbours, land units with a transport (checked by `.expect` lines `NAME FIELD>=N`) |
| `diplomacy NAME` | write `NAME.json`: countries, factions, sides, relations, pairs at peace; the first call notes every area's owner, later ones count the areas that changed hands and those now held by a country at peace with the one that held them (`.expect`: `NAME takenInPeace<=0`; `FIELD<=N` and `FIELD>=N` both work) |
| `autoplay ROUNDS` | play N rounds without input: ends the human turn (the AI hosting option plays it), fast-forwards AI turns and closes boxes; stops early when the battle ends |
| `settle` | close boxes and finish AI moves until the human side can act (ends no turn) |
| `dump NAME` | write the rule state as `NAME.json` next to the screenshots |
| `hittest LABEL` | every button off the screen is touched there (at a window pixel in the letterbox bars when the window has them, and at a synthetic point) through the input entry and straight through the GUI tree; every hidden button must refuse input. Buttons do not fire while probing; each one that would have fires a `hittest failed` log line |
| `dumpafter COUNTRY NAME` | write the rule state as `NAME.json` the moment COUNTRY's turn ends (its own decisions, before the next country acts; AI tests) |
| `startconquest FILE COUNTRY` | start a conquest with any scenario file as COUNTRY (`conquest_N.xml` plays conquest N with its conquests.json options; the world map: `conquest_10.xml`) |
| `texstats LABEL` | log the live texture memory and the last frame's draw calls; lines whose labels differ only by a trailing number must report the same memory (resources are released when screens close) |
| `shot NAME` | screenshot |
| `log TEXT` | write to the log |
| `exit` | quit |

Lines starting with `#` are comments. A first line `#! ARGS` adds command line options (`$TESTS` is this
folder). Scripts run with `--no-mods --ai classic` unless their `#!` line says otherwise (the goldens
were recorded with the original AI).

A script `X.txt` may have `X.expect`: lines `DUMP AREA owner COUNTRY` or `DUMP AREA units>=N` checked
against the dumps (the AI tests).

## What the scripts cover

| Script | Covers |
|---|---|
| `campaign_autoplay` | the first campaign battle played by the AI to its end |
| `conquest_autoplay` | conquest 1 played by the AI for 16 rounds |
| `campaign_13_rounds`, `conquest_7_rounds`, `conquest_ai_turns` | round flow with fixed waits |
| `attack_box`, `card_shop`, `commander_and_options`, `pause_options_save`, `save_and_load` | screens and flows |
| `tutorial`, `ipad_tutorial` | the tutorial on the phone and tablet layouts |
| `campaign_start`, `conquest_start` | starting both modes |
| `hit_test`, `hit_test_wide`, `ipad_hit_test` | hidden and off-screen controls refuse touches: phone layout, a 1600x600 window (640x320 layout, bars left and right), a 1000x1000 window (tablet layout, bars above and below); `card_shop`, `commander_and_options`, `conquest_start`, `pause_options_save`, `tutorial` probe their screens too |
| `resource_cycle` | menu -> conquest -> menu twice: the same texture memory on every visit |
| `ai_defends_marseille_classic`, `ai_defends_marseille_standard` | the test mod `mods/ai-marseille` (an Italian stack next to a weakly held Marseille): the standard AI reinforces and holds Marseille (`.expect`), the classic AI loses it |
| `ai_standard_autoplay` | conquest 1 played by the standard AI for 16 rounds |
| `world_menu`, `world_autoplay` | the generated world map, WORLD 1939 (`mods/map-natural-earth`, MAP_PIPELINE.md, 7 737 provinces drawn as a mesh, from the polar islands to Cape Horn): through the conquest menu (list, preview, game); as France with the seam at the Bering Strait, fully zoomed out (no black bars) and fully zoomed out at the seam (Asia left of America, no empty water), fully zoomed in (units fitted to the provinces), the flag markers, four rounds of the standard AI and the naval count (`world_autoplay.expect`: harbours, transports and ships at sea) |
| `world1962_autoplay` | the sample total conversion `samples/mods/world-1962` (`--mods samples/mods`): 66 countries on ten faction sides, new countries' flags on the map (Dijon), the successor states and East Asia; four rounds of the standard AI: borders move in the wars, none between countries at peace (`.expect`) |
| `flag_override` | the test mod `Tests/mods-flags/flag-override` replaces the flags of France (a plain picture on an atlas pole) and Britain (a complete map flag with its pole) on the world map |

## Unit tests (`Unit/`, `dotnet test Tests`)

| File | Covers |
|---|---|
| `SchemaAndDataTests.cs` | the schema validator; every data file against its schema; invalid mod data falls back to the game's data; the asset layer's resolution order; original names; variant folders; layout buckets |
| `HitTestTests.cs` | points off the screen never hit; only the visible part of an edge control hits; hidden and disabled controls refuse input |
| `NoPurchaseTests.cs` | no purchase code, billing permission, purchase types or buy button |
| `AiProfileTests.cs` | classic equals the original numbers; inheritance; the command line override |
| `MapPackTests.cs` | the world base map (consistency, wrapping, the units of every era, historical borders splitting provinces, coverage up to the polar islands and over the date line, capitals of today and of earlier eras, Marseille); the pipeline and the classic export on a synthetic world with a historical border, a harbour and transports; the mesh covers every region exactly and the game reads it; ships pass the straits and canals, the lakes are inland seas; every province one landmass, the date line seamless; Memel and South Tyrol by era |
| `CameraTests.cs` | zooming out stops when the map fills the screen; a wrapping map scrolls past its edges and the camera takes the short way over the seam |
| `BuiltInModTests.cs` | mods built into the package mount and merge like folder mods, a folder mod replaces one with the same id, the world map is built in |
| `DiplomacyTests.cs` | factions are sides of their own; relations by selector, round and order (the last that holds decides); lasting peace; no relations = the original rules; the wars and truces of the 1962 sample, every new country registered with its pictures |

---

<a id="chinese"></a>

# 测试

`run-tests.ps1`（需先在仓库根目录执行 `dotnet build Hosts/Desktop`）按以下顺序运行：

0. **unit**（单元测试）：`dotnet test Tests` — `Unit/` 中的进程内单元测试（schema 校验器、每个数据文件对照其 schema 的校验、无效模组数据的回退、资源层的解析顺序、原版名称、变体文件夹、布局档位、点击测试（hit test）规则、内购移除、AI 评估、地图管线）。
1. **audio**（音频）：`--test-audio` 解码每个音乐文件，并加载每个音效。
2. **data**（数据）：`--verify-data` 根据 schema 检查每个 JSON 数据文件，然后分别从 JSON 以及它所转换自的原版 XML（或内置表）加载每种类型，并逐字段比较这些定义。有意的数据改动会在这里显示出来；这是预期行为（在有人有意修改之前，游戏数据应与原版保持一致）。
3. **assets**（资源）：`WC2.Tools validate` 检查构建出的游戏资源包：每个原版文件都能通过其旧名称访问到，每个索引条目都存在，数据中引用的每个资源都存在，整个资源树都已打包。
4. **scripts**（测试脚本）：以确定性模式运行本文件夹中的每个 `*.txt`：`--seed 20261106 --fixed-step`，并且每个测试脚本使用一个全新的文档文件夹（`out/<script>/docs`）。每条 `dump NAME` 将规则状态（国家、地区、部队、将领）写成 JSON；运行器将其与 `golden/<script>/NAME.json` 逐字节比较。每条 `shot NAME` 与 `golden/<script>/NAME.jpg` 比较（`WC2.Tools compare-image`：缩放到参考图尺寸，某个像素的任一通道差值超过 40 即视为该像素已改变，改变的像素超过 1 % 时该截图判为失败；生成的 `NAME.diff.png` 会标出这些像素）。第一行为 `#! ARGS` 的测试脚本会在命令行中获得 ARGS（窗口大小）。

```powershell
.\Tests\run-tests.ps1                 # everything
.\Tests\run-tests.ps1 -Filter conquest
.\Tests\run-tests.ps1 -Record         # store this run's dumps as the new goldens
.\Tests\run-tests.ps1 -RecordShots    # store this run's screenshots as the new references
.\Tests\run-tests.ps1 -RealTime       # real clock, no goldens (the old smoke-test behaviour)
.\Tests\run-tests.ps1 -Bin DIR        # the desktop build in DIR (dotnet build Hosts/Desktop -o DIR), e.g. while the game runs
```

仅在有意修改规则时才录制新的基准（golden），仅在有意修改画面时才录制新的参考截图；并在提交说明中注明。截图和日志输出到 `out/`。

## 测试脚本命令

坐标使用逻辑屏幕单位（568 x 320 手机布局；名为 `ipad_*.txt` 的测试脚本以 `--ipad` 运行，使用 1024 x 768 平板布局）。时间以游戏内的秒为单位。

| 命令 | 效果 |
|---|---|
| `wait S` | 等待 S 秒 |
| `click X Y`、`press X Y`、`move X Y`、`release X Y` | 触摸输入 |
| `drag X1 Y1 X2 Y2 [S]` | 按下，分步移动，松开 |
| `back` | Android 返回键（`CallNativeExit`：能处理它的面板或状态会关闭，否则会弹出退出对话框） |
| `wheel DELTA` | 鼠标滚轮 |
| `camera X Y [SCALE]` | 把地图镜头放到某个场景坐标（缩放限制和左右循环仍然生效），并在日志中记录最终位置 |
| `count NAME` | 在转储文件旁写出 `NAME.json`：海上的陆军（运输船）、海上的舰船、港口数、带运输船的陆军数（由 `.expect` 中形如 `NAME FIELD>=N` 的行检查） |
| `diplomacy NAME` | 写出 `NAME.json`：国家数、阵营数、阵营方数、外交关系数、处于和平的国家对数；第一次调用记下每个地区的归属，之后的调用统计易手的地区，以及其中现在由与原主处于和平状态的国家占有的地区（`.expect`：`NAME takenInPeace<=0`；`FIELD<=N` 和 `FIELD>=N` 都可用） |
| `autoplay ROUNDS` | 在没有输入的情况下进行 N 个回合：结束人类玩家的回合（由 AI 托管选项代为进行），快进 AI 回合并关闭对话框；战斗结束时提前停止 |
| `settle` | 关闭对话框并完成 AI 的行动，直到人类一方可以操作为止（不结束任何回合） |
| `dump NAME` | 将规则状态写为 `NAME.json`，与截图放在一起 |
| `hittest LABEL` | 每个位于屏幕外的按钮都会在其所在位置被触摸（如果窗口有黑边，则在黑边内的某个窗口像素处触摸；此外还会在一个合成点处触摸），分别经由输入入口和直接通过 GUI 树进行；每个隐藏的按钮都必须拒绝输入。探测期间按钮不会真正触发；每个本应触发的按钮都会改为输出一行 `hittest failed` 日志 |
| `dumpafter COUNTRY NAME` | 在 COUNTRY 的回合结束的那一刻将规则状态写为 `NAME.json`（即它自己的决策结果，在下一个国家行动之前；用于 AI 测试） |
| `startconquest FILE COUNTRY` | 以任意剧本文件、作为 COUNTRY 开始一局征服（`conquest_N.xml` 按征服 N 及其 conquests.json 中的选项进行；世界地图：`conquest_10.xml`） |
| `texstats LABEL` | 在日志中记录当前的纹理内存占用和上一帧的绘制调用数；标签仅在末尾数字上有所不同的各行必须报告相同的内存（界面关闭时资源会被释放） |
| `shot NAME` | 截图 |
| `log TEXT` | 写入日志 |
| `exit` | 退出 |

以 `#` 开头的行是注释。第一行为 `#! ARGS` 时会添加命令行选项（`$TESTS` 表示本文件夹）。除非 `#!` 行另有指定，测试脚本均以 `--no-mods --ai classic` 运行（基准（golden）是用原版 AI 录制的）。

测试脚本 `X.txt` 可以附带一个 `X.expect`：其中的 `DUMP AREA owner COUNTRY` 或 `DUMP AREA units>=N` 行会与转储结果进行核对（用于 AI 测试）。

## 各测试脚本的覆盖范围

| 测试脚本 | 覆盖内容 |
|---|---|
| `campaign_autoplay` | 战役的第一场战斗，由 AI 一直打到结束 |
| `conquest_autoplay` | 征服 1，由 AI 进行 16 个回合 |
| `campaign_13_rounds`、`conquest_7_rounds`、`conquest_ai_turns` | 使用固定等待时间的回合流程 |
| `attack_box`、`card_shop`、`commander_and_options`、`pause_options_save`、`save_and_load` | 各个界面和流程 |
| `tutorial`、`ipad_tutorial` | 手机布局和平板布局下的教程 |
| `campaign_start`、`conquest_start` | 启动两种模式 |
| `hit_test`、`hit_test_wide`、`ipad_hit_test` | 隐藏的控件和屏幕外的控件拒绝触摸：手机布局；1600x600 窗口（640x320 布局，左右两侧有黑边）；1000x1000 窗口（平板布局，上下两侧有黑边）；`card_shop`、`commander_and_options`、`conquest_start`、`pause_options_save`、`tutorial` 也会探测各自的界面 |
| `resource_cycle` | 菜单 -> 征服 -> 菜单，重复两次：每次进入时纹理内存都相同 |
| `ai_defends_marseille_classic`、`ai_defends_marseille_standard` | 测试模组 `mods/ai-marseille`（一支意大利部队堆叠在防守薄弱的马赛旁边）：standard AI 会增援并守住马赛（`.expect`），classic AI 则会丢掉马赛 |
| `ai_standard_autoplay` | 征服 1，由 standard AI 进行 16 个回合 |
| `world_menu`、`world_autoplay` | 生成的世界地图 WORLD 1939（`mods/map-natural-earth`，MAP_PIPELINE.md，以网格绘制的 7 737 个省份，从北极群岛到合恩角）：通过征服菜单进入（列表、预览、游戏）；以法国开局，查看白令海峡处的接缝、最大缩小（没有黑边）以及在接缝处最大缩小（亚洲在美洲左侧，没有空白海面）、最大放大（部队缩放到省份内）、旗帜标记、standard AI 进行四个回合，以及海军统计（`world_autoplay.expect`：港口、海上的运输船和舰船） |
| `world1962_autoplay` | 全面改造示例 `samples/mods/world-1962`（`--mods samples/mods`）：分属十个阵营的 66 个国家，新国家在地图上的国旗（第戎），苏联继承国和东亚；standard AI 进行四个回合：战争中边界变化，处于和平的国家之间没有任何地区易手（`.expect`） |
| `flag_override` | 测试模组 `Tests/mods-flags/flag-override` 在世界地图上替换法国（挂在图集旗杆上的普通图片）和英国（带旗杆的完整地图旗帜）的国旗 |

## 单元测试（`Unit/`，`dotnet test Tests`）

| 文件 | 覆盖内容 |
|---|---|
| `SchemaAndDataTests.cs` | schema 校验器；每个数据文件对照其 schema 的校验；无效的模组数据回退到游戏自身的数据；资源层的解析顺序；原版名称；变体文件夹；布局档位 |
| `HitTestTests.cs` | 屏幕外的点永远不会命中；边缘控件只有可见部分能被命中；隐藏和禁用的控件拒绝输入 |
| `NoPurchaseTests.cs` | 不存在购买代码、计费权限、购买类型或购买按钮 |
| `AiProfileTests.cs` | classic 与原版数值完全相同；继承；命令行覆盖 |
| `MapPackTests.cs` | 世界底图（一致性、左右循环、各时代的单元、历史边界拆分省份、覆盖到北极群岛并跨越日期变更线、今天和早期时代的首都、马赛）；在一个带历史边界、港口和运输船的合成世界上测试生成流程和经典地图导出；网格精确覆盖每个区域，并且游戏能读取它；舰船能通过海峡和运河，湖泊是内海；每个省份都是一块完整的陆地，日期变更线处无缝；梅梅尔和南蒂罗尔的各时代归属 |
| `CameraTests.cs` | 缩小到地图填满屏幕时停止；循环地图可以越过边缘滚动，镜头跨越接缝走近路 |
| `BuiltInModTests.cs` | 内置于游戏资源包中的模组与文件夹模组一样挂载和合并；文件夹模组会替换 id 相同的内置模组；世界地图是内置的 |
| `DiplomacyTests.cs` | 每个阵营都是独立的一方；外交关系按选择器、回合和顺序生效（最后一条有效的关系起决定作用）；永久和平；没有外交关系时即原版规则；1962 示例中的战争与停战，每个新国家都已登记并带有图片 |
