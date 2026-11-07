# World Conqueror 2 — C# port

**Language / 语言:** [English](#english) | [中文](#chinese)

<a id="english"></a>

A C# / MonoGame port of the C++ remake in `Reference/WC2-C` (which itself ran on the original
Android `libeasytech` binary). Everything, including the engine layer the binary used to provide
(renderer, GUI toolkit, bitmap fonts, atlases, particle effects, state machine), is now managed code.

Branches: `original` is the faithful restoration and stays frozen as the reference; `newEngine`
carries the modernisation described in `NEW_ENGINE_PLAN.md` (asset tree by purpose, JSON game data
with schemas, mods, separate engine/data/game/host projects, deterministic tests). Windows and Android
are the platforms.

```
WC2.slnx                 solution with all projects
assets/                  everything the game reads, by purpose: audio/, image/, data/ (json/ authored
                         data and schemas, xml/ original data), scripts/; originals unchanged (ASSET_LAYOUT.md)
mods/                    mods mounted by development builds (MODDING.md): example-balance (disabled),
                         map-natural-earth (the generated world map with a conquest, MAP_PIPELINE.md)
generated/               asset_index.json, kept current by every build (original name -> path)
Engine/                  WC2.Engine: the engine library (no reference to data, game or hosts)
Data/                    WC2.Data: data definitions (paths, schemas mapping, terrain, rules, layouts, audio, countries)
Game/                    WC2.Game: rules, AI, scene, screens, data readers, the shared MonoGame host
Hosts/Desktop/           WC2.Desktop: the Windows program (options, window, debug tools)   →  dotnet run
Hosts/Android/           WC2.Android: the APK (activity, life cycle, APK asset access)     →  dotnet build -c Release
Tools/                   WC2.Tools: build-pack (asset tree → runtime package, runs with every build),
                         validate (asset checker), compare-image (screenshot references), build-map
                         (Natural Earth → map pack and playable map mod)
Tests/                   WC2.Tests (unit tests) and the scripted regression suite (run-tests.ps1, goldens)
Reference/WC2-C/         original C++ remake (read only, not in git)
Reference/decompiled/    Hex-Rays pseudo-C of the original libeasytech.so (not in git)
```

## Run on the desktop

```powershell
dotnet run --project Hosts/Desktop
```

Options: `--width 1280 --height 720` (window size; the logical layout follows the aspect ratio like
the original: 480/534/568/640 x 320, 1024 x 768 below 4:3, `data/json/layout/profiles.json`), `--ipad`
(a 4:3 window), `--lang zh_CN|English|ja|kr|ru|zh_TW`, `--assets PATH` (a package, the `assets/` tree,
a flat folder of the original files, or a `.zip`/`.apk`), `--mods DIR`, `--no-mods`, `--all-mods`,
`--docs DIR` (saves and settings), `--log FILE`, `--texture-stats`, `--ai classic|standard|hard` (AI
profile; new games use `standard`), `--scenario FILE --country ID` (start a conquest with that scenario
right away, e.g. `--scenario conquest_10.xml --country fr` for the generated world map).
Test options: `--seed N --fixed-step` (replayable run), `--exit-after SEC --screenshot FILE`,
`--script FILE` (scripted input, see `Tests/README.md`).
Tools: `--verify-data [--mods DIR]`, `--convert-data`, `--test-audio`, `--test-xml`, `--test-fonts`,
`--decode-pkm`.

Controls: left mouse = finger, mouse wheel = zoom, right mouse drag = second finger (pinch),
`Esc`/`Backspace` = Android back key, `F11` = full screen.

Saves, settings and mods live in `%LOCALAPPDATA%\WC2CSharp` (`mods\` for mods).

## Projects

* `Engine` — re-implementation of the easytech engine API (`ecGraphics`, `ecImage`, `ecTextureRes`,
  `ecUniFont`/`ecText`, `GUIElement`/`GUIManager`/widgets, `CStateManager`, `CCSoundBox`, `ecEffect`,
  `TiXml` facade over System.Xml, ETC1/PKM decoder, SkiaSharp image loading), plus `IO/AssetSystem`
  (the asset layer), `IO/ModManager`, `IO/JsonData` (JSON merging) and `IO/JsonSchema` (validation).
  It references nothing of the game; the compiler enforces the layering.
* `Data` — definitions that are data: `DataPaths` (file paths and the schema of each), `TerrainDefs`,
  `GameRules`, `LayoutProfiles`, `AudioDefs`, `CountryDefs`, the enums, `DataCompare`.
* `Game` — the ported game code, one folder per original module (`BasicElements`, `MapScene`,
  `Render`, `Fights`, `AI`, `GameStages`, `GUI/Menus`, `GUI/InGame`, ...). Class, method and field names
  of the rules follow the C++ on purpose (`PORTING_GUIDE.md`). `Game/Data` holds the readers that need
  game types (scenario index, war medals, XML→JSON migration); `Game/Host` the MonoGame host and the
  script driver shared by desktop and Android.
* `Hosts/Desktop`, `Hosts/Android` — only input, output, window and life cycle.

## Game data and mods

The game's definitions are JSON with a schema each (`DATA_SCHEMA.md`): units, cards, commanders,
terrain, war medals, rules, campaigns, conquests, countries, audio, layout profiles, texts, the options
layout, the per-area map table and the AI profiles. Scenarios and tutorial scripts stay the original
XML, listed by a generated index. Every file is checked against its schema when it loads. Mods are
folders that add files or merge single entries into the data (`MODDING.md`, example in
`mods/example-balance`).

The AI's numbers are data (`data/json/ai/profiles.json`): `classic` is the original AI exactly,
`standard` (the default) defends threatened capitals, ports, airfields, cities and home areas and buys
cards more readily, `hard` pushes harder against the player.

Next to the classic map, `WC2.Tools build-map` generates a world base map from Natural Earth geography
and CShapes historical borders (`MAP_PIPELINE.md`): one map whose regions follow the borders of 1910,
1914, 1936, 1939, 1945, 1949 and today, as a data pack (`assets/data/maps/natural-earth`) and a playable
mod (`mods/map-natural-earth`: conquest 10 "WORLD 1939" in the conquest list; the map wraps left-right).
How mods work, and how to change the game package directly: `MODDING.md`. A total conversion needs no
code: `WC2.Tools build-scenario` writes a conquest with new countries, factions and timed war and peace
from a rules file; `samples/mods/world-1962` (an original alternate 1962, 65 countries) is the example,
not loaded unless you copy it into a mods folder (MODDING.md, section 4).

## Android

`Hosts/Android` compiles the engine, data and game sources with `MonoGame.Framework.Android`. The
generated package ships inside the APK as `assets/pack/` and is read in place through its index; only
when that fails is it extracted once. The world map mod is built in (conquest 10). Mods go to `Android/data/com.wc2remake.csharp/files/mods/`.
Requirements: the .NET Android workload, an Android SDK (platform 34+), JDK 17. The project points at
`C:\Android` and `C:\Program Files\Java\jdk-17.0.2` when they exist; override with
`-p:AndroidSdkDirectory=... -p:JavaSdkDirectory=...`.

```powershell
.\Hosts\Android\build-apk.ps1   # builds Release, copies WC2-android-arm64.apk to the repository root
# or: dotnet build Hosts/Android -c Release   (APK: Hosts\Android\bin\Release\net9.0-android\*-Signed.apk)
```

The activity mirrors the original `Wc2Activity`: layout buckets from the screen aspect, `nativePause` /
`nativeResume` on the life cycle, the back key through `CallNativeExit`, immersive full-screen window,
the original launcher icons and localised app name. Map tiles are uploaded as ETC textures where the
GPU supports it. Package id `com.wc2remake.csharp`, arm64-v8a only, debug-signed. None of this has been
run on a real device yet; the desktop build can run straight from the APK (`--assets <apk>`), which is
how its contents are checked. The device checklist is in `DIFFERENCES_THREE_WAY.md` section 5.

## Tests

```powershell
dotnet build Hosts/Desktop
.\Tests\run-tests.ps1
```

runs the unit tests (`Tests/WC2.Tests.csproj`), the audio self-test, the data verification (schemas,
and every JSON file against the original XML or built-in tables), the asset checker, and every script
in deterministic mode (fixed seed, fixed time step, fresh documents folder): game-state dumps are
compared byte for byte with `Tests/golden`, screenshots with reference images, and the hit-test
scripts check that hidden and off-screen controls refuse touches on phone, tablet and letterboxed
windows. Several scripts let the AI play whole games (`autoplay`), on the classic and the generated
map; the AI tests check what the improved AI defends. About four minutes. Details in `Tests/README.md`.

## Status / known gaps

* Not ported (same as the C++ remake): battle animation (the remake never starts it either),
  multiplayer, service/promotion panels, map editor. In-app purchases are removed. The tutorial *is*
  ported (from the binary; the remake ran the original code for it).
* The game logic was audited against the decompiled original; the remake's accidental deviations were
  corrected, its deliberate redesigns kept. `DIFFERENCES_THREE_WAY.md` is the original vs C++ remake vs
  C# matrix.
* Music is decoded from the original MP3 files with MP3Sharp (the tracks are MPEG 2.5 Layer III at
  8 kHz, which NLayer mis-frames).
* Text in Japanese, Traditional Chinese and Korean misses glyphs: neither the remake's Kai/Hei fonts
  nor the original bitmap fonts cover those texts (the original drew them with the Android system font).

---

<a id="chinese"></a>

# 世界征服者2 — C# 移植版

本项目是 `Reference/WC2-C` 中 C++ 重制版的 C# / MonoGame 移植（该重制版本身运行在原版 Android `libeasytech` 二进制库之上）。所有代码，包括原先由该二进制库提供的引擎层（渲染器、GUI 工具包、位图字体、图集、粒子特效、状态机），现在都已是托管代码。

分支：`original` 是忠实还原的版本，作为参考保持冻结；`newEngine` 承载 `NEW_ENGINE_PLAN.md` 中描述的现代化改造（按用途组织的资源树、带 schema 的 JSON 游戏数据、模组（mod）、相互独立的引擎/数据/游戏/宿主程序项目、确定性测试）。目标平台为 Windows 和 Android。

```
WC2.slnx                 solution with all projects
assets/                  everything the game reads, by purpose: audio/, image/, data/ (json/ authored
                         data and schemas, xml/ original data), scripts/; originals unchanged (ASSET_LAYOUT.md)
mods/                    mods mounted by development builds (MODDING.md): example-balance (disabled),
                         map-natural-earth (the generated world map with a conquest, MAP_PIPELINE.md)
generated/               asset_index.json, kept current by every build (original name -> path)
Engine/                  WC2.Engine: the engine library (no reference to data, game or hosts)
Data/                    WC2.Data: data definitions (paths, schemas mapping, terrain, rules, layouts, audio, countries)
Game/                    WC2.Game: rules, AI, scene, screens, data readers, the shared MonoGame host
Hosts/Desktop/           WC2.Desktop: the Windows program (options, window, debug tools)   →  dotnet run
Hosts/Android/           WC2.Android: the APK (activity, life cycle, APK asset access)     →  dotnet build -c Release
Tools/                   WC2.Tools: build-pack (asset tree → runtime package, runs with every build),
                         validate (asset checker), compare-image (screenshot references), build-map
                         (Natural Earth → map pack and playable map mod)
Tests/                   WC2.Tests (unit tests) and the scripted regression suite (run-tests.ps1, goldens)
Reference/WC2-C/         original C++ remake (read only, not in git)
Reference/decompiled/    Hex-Rays pseudo-C of the original libeasytech.so (not in git)
```

## 在桌面上运行

```powershell
dotnet run --project Hosts/Desktop
```

选项：`--width 1280 --height 720`（窗口大小；逻辑布局与原版一样随宽高比变化：480/534/568/640 x 320，宽高比低于 4:3 时为 1024 x 768，见 `data/json/layout/profiles.json`）、`--ipad`（4:3 窗口）、`--lang zh_CN|English|ja|kr|ru|zh_TW`、`--assets PATH`（游戏资源包、`assets/` 资源树、存放原版文件的扁平文件夹，或者一个 `.zip`/`.apk`）、`--mods DIR`、`--no-mods`、`--all-mods`、`--docs DIR`（存档和设置）、`--log FILE`、`--texture-stats`、`--ai classic|standard|hard`（AI 配置；新游戏使用 `standard`）、`--scenario FILE --country ID`（立即以该剧本开始一局征服模式，例如对生成的世界地图使用 `--scenario conquest_10.xml --country fr`）。
测试选项：`--seed N --fixed-step`（可回放的运行）、`--exit-after SEC --screenshot FILE`、`--script FILE`（脚本化输入，见 `Tests/README.md`）。
工具：`--verify-data [--mods DIR]`、`--convert-data`、`--test-audio`、`--test-xml`、`--test-fonts`、`--decode-pkm`。

操作：鼠标左键 = 手指，鼠标滚轮 = 缩放，按住鼠标右键拖动 = 第二根手指（双指缩放），`Esc`/`Backspace` = Android 返回键，`F11` = 全屏。

存档、设置和模组位于 `%LOCALAPPDATA%\WC2CSharp`（模组放在 `mods\` 中）。

## 项目

* `Engine` — easytech 引擎 API 的重新实现（`ecGraphics`、`ecImage`、`ecTextureRes`、`ecUniFont`/`ecText`、`GUIElement`/`GUIManager`/各种控件、`CStateManager`、`CCSoundBox`、`ecEffect`、基于 System.Xml 的 `TiXml` 外观层、ETC1/PKM 解码器、SkiaSharp 图像加载），另外还包括 `IO/AssetSystem`（资源层）、`IO/ModManager`、`IO/JsonData`（JSON 合并）和 `IO/JsonSchema`（校验）。它不引用任何游戏代码；这种分层由编译器强制保证。
* `Data` — 以数据形式存在的定义：`DataPaths`（文件路径及每个文件对应的 schema）、`TerrainDefs`、`GameRules`、`LayoutProfiles`、`AudioDefs`、`CountryDefs`、各枚举类型、`DataCompare`。
* `Game` — 移植过来的游戏代码，每个原版模块对应一个文件夹（`BasicElements`、`MapScene`、`Render`、`Fights`、`AI`、`GameStages`、`GUI/Menus`、`GUI/InGame`……）。规则部分的类名、方法名和字段名有意与 C++ 保持一致（见 `PORTING_GUIDE.md`）。`Game/Data` 存放需要用到游戏类型的读取器（剧本索引、战争勋章、XML→JSON 迁移）；`Game/Host` 存放桌面端与 Android 共用的 MonoGame 宿主程序和测试脚本驱动器。
* `Hosts/Desktop`、`Hosts/Android` — 只负责输入、输出、窗口和生命周期。

## 游戏数据与模组

游戏的各种定义均为 JSON，每个文件都有各自的 schema（见 `DATA_SCHEMA.md`）：单位、卡牌、将领、地形、战争勋章、规则、战役、征服、国家、音频、布局配置、文本、选项界面布局、按地区划分的地图表以及 AI 配置。剧本和教程脚本仍保留原版 XML 格式，由一个自动生成的索引列出。每个文件在加载时都会根据其 schema 进行校验。模组是一些文件夹，它们可以向数据中添加文件，或将单个条目合并进数据（见 `MODDING.md`，示例在 `mods/example-balance`）。

AI 的各项数值也是数据（`data/json/ai/profiles.json`）：`classic` 与原版 AI 完全一致；`standard`（默认）会防守受到威胁的首都、港口、机场、城市和本土地区，并更积极地购买卡牌；`hard` 会对玩家施加更大的压力。

除经典地图之外，`WC2.Tools build-map` 还能根据 Natural Earth 地理数据和 CShapes 历史边界生成一张世界底图（见 `MAP_PIPELINE.md`）：一张区域同时遵循 1910、1914、1936、1939、1945、1949 年和今天边界的地图，输出为数据包（`assets/data/maps/natural-earth`）和可游玩的模组（`mods/map-natural-earth`：征服列表中的征服 10 “WORLD 1939”；地图左右循环）。模组的工作方式以及如何直接修改游戏资源包，见 `MODDING.md`。全面改造无需编写代码：`WC2.Tools build-scenario` 可以根据规则文件生成一场带有新国家、阵营以及定时战争与和平的征服；`samples/mods/world-1962`（原创的 1962 年架空历史，65 个国家）就是示例，只有复制到模组文件夹后才会加载（MODDING.md 第 4 节）。

## Android

`Hosts/Android` 使用 `MonoGame.Framework.Android` 编译引擎、数据和游戏的源代码。生成的游戏资源包以 `assets/pack/` 的形式随 APK 一起发布，并通过其索引就地读取；只有在就地读取失败时才会将其解压一次。世界地图模组是内置模组（征服 10）。模组放在 `Android/data/com.wc2remake.csharp/files/mods/` 中。
环境要求：.NET Android 工作负载、Android SDK（platform 34+）、JDK 17。如果 `C:\Android` 和 `C:\Program Files\Java\jdk-17.0.2` 存在，项目默认指向它们；可通过 `-p:AndroidSdkDirectory=... -p:JavaSdkDirectory=...` 覆盖。

```powershell
.\Hosts\Android\build-apk.ps1   # builds Release, copies WC2-android-arm64.apk to the repository root
# or: dotnet build Hosts/Android -c Release   (APK: Hosts\Android\bin\Release\net9.0-android\*-Signed.apk)
```

Activity 仿照原版的 `Wc2Activity`：根据屏幕宽高比选择布局档位，在生命周期中调用 `nativePause` / `nativeResume`，返回键经由 `CallNativeExit` 处理，沉浸式全屏窗口，原版的启动器图标和本地化的应用名称。在 GPU 支持的情况下，地图贴图块以 ETC 纹理的形式上传。包名为 `com.wc2remake.csharp`，仅支持 arm64-v8a，使用调试签名。以上这些都还没有在真机上运行过；桌面版可以直接从 APK 运行（`--assets <apk>`），APK 的内容就是以这种方式检查的。真机检查清单见 `DIFFERENCES_THREE_WAY.md` 第 5 节。

## 测试

```powershell
dotnet build Hosts/Desktop
.\Tests\run-tests.ps1
```

以上命令会运行单元测试（`Tests/WC2.Tests.csproj`）、音频自检、数据校验（schema 校验，以及将每个 JSON 文件与原版 XML 或内置表进行比对）、资源检查器，并以确定性模式（固定随机种子、固定时间步长、全新的文档文件夹）运行每个测试脚本：游戏状态转储与 `Tests/golden` 中的基准（golden）逐字节比较，截图与参考图像比较，点击测试（hit test）脚本则检查在手机、平板以及带黑边（letterbox）的窗口中，隐藏的控件和位于屏幕外的控件都会拒绝触摸。有几个测试脚本让 AI 完整地打完整局游戏（`autoplay`），分别在经典地图和生成的地图上进行；AI 测试检查改进后的 AI 会防守哪些地方。耗时约四分钟。详见 `Tests/README.md`。

## 状态 / 已知缺失

* 未移植（与 C++ 重制版相同）：战斗动画（重制版也从未启动它）、多人游戏、服务/推广面板、地图编辑器。应用内购买已被移除。教程*已经*移植（从二进制库移植而来；重制版中的教程运行的是原版代码）。
* 游戏逻辑已对照反编译的原版进行了审查；重制版中无意造成的偏差已被修正，有意为之的重新设计则予以保留。`DIFFERENCES_THREE_WAY.md` 是原版、C++ 重制版与 C# 版的三方对照表。
* 音乐使用 MP3Sharp 从原版 MP3 文件解码（这些音轨是 8 kHz 的 MPEG 2.5 Layer III，NLayer 会对其错误分帧）。
* 日文、繁体中文和韩文文本存在缺字：无论是重制版的楷体（Kai）/黑体（Hei）字体，还是原版的位图字体，都不能覆盖这些文本（原版使用 Android 系统字体来绘制它们）。
