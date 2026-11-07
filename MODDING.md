# Modding
**Language / 语言:** [English](#english) | [中文](#chinese)

<a id="english"></a>

This page explains how the game finds its files and the three ways to change them: editing the game
package itself (what you did with the original game), a mod folder next to the game, and a mod built
into the package. All three work; they differ in what you have to redo after an update and in how easy
it is to undo.

## In short

| Way | Where | Good for | Undo |
|---|---|---|---|
| **Edit the package** | `pack\` next to `WC2.exe` (Windows), `assets/pack/` inside the APK (Android), `assets/` in the repository | quick personal changes, the "old way" | restore the file (or reinstall); an update overwrites your changes |
| **Mod folder** (recommended) | `%LOCALAPPDATA%\WC2CSharp\mods\<mod>\` or `mods\` next to the program (Windows), `Android/data/com.wc2remake.csharp/files/mods/<mod>/` (Android) | changes you want to keep, share or switch off | delete the folder or set `"enabled": false` |
| **Built-in mod** | a mod folder listed in `assets/build.json` `builtInMods`, packaged under `mods/<id>/` | content that ships with the game (the world map) | remove it from `builtInMods` and rebuild, or set `"enabled": false` in its `mod.json` |

A mod never changes the package: it lies on top of it. The game looks at the mods first and at the
package below them, so removing the mod brings back the original.

## How the game finds a file

The game reads every file through one asset layer (`Engine/IO/AssetSystem.cs`). It is a stack:

```
top     mods, in ascending "priority" (then by id): the last one wins
        (folder mods and built-in mods alike; a folder mod replaces a built-in mod with the same id)
bottom  the game package: pack\ (Windows), assets/pack/ in the APK, or the repository's assets/ tree
```

A name is looked up in this order:

1. **As a path** (`image/ui/phone-568-hd/mainbg.webp`, `data/json/units/units.json`): the top-most layer
   that has a file at that path wins.
2. **As an original name** (`mainbg-568h@2x.webp`, the name the original game used and the ported code
   still asks for): the package index `generated/asset_index.json` says where that file lives in the tree
   (language variants first: `zh_CN.lproj/...` for Chinese), and that path is looked up as in 1, so a
   mod that replaces the path wins. A mod (or a file at the package root) may also replace a file by its
   original name: a file named `mainbg-568h@2x.webp` at the root of the mod (or in an `X.lproj` folder)
   is found under that name before the index is used.

`assets/legacy-names.json` lists every original name with its path in the tree; `ASSET_LAYOUT.md` explains
the tree.

**Game data is merged, not replaced.** For a JSON data file (`data/json/**/*.json`) the game reads the
package's file and every mod's file with the same path, and merges them in mod order:

* objects merge key by key, so a mod lists only what it changes;
* arrays and plain values replace the value below;
* `null` removes a key, e.g. `{"countries": {"others": {"realbomber": null}}}` in `data/json/units/units.json`;
* `"$replace": true` inside an object replaces the object below instead of merging.

Then the merged result is checked against the file's schema (`data/json/schemas/`). If a mod makes it
invalid, the log names the place (`$.countries.others.tank.price: -5 is below the minimum 0`) and that
mod's changes to that file are left out; the game keeps running with the rest.

Every other file (images, atlases, fonts, sounds, music, scenario XML, binary map data) is **replaced** as
a whole by the top-most layer that has it.

## 1. Editing the game package directly

This still works, and it is the fastest way to try something. What changed is where the files are and
which of them the game actually reads.

**Where the package is**

| | Package | After changing it |
|---|---|---|
| Windows release | the `pack\` folder next to `WC2.exe` | start the game again |
| Android | `assets/pack/` inside the APK | rebuild and re-sign the APK (below) |
| Development | `assets/` in the repository; every build turns it into `pack\` (desktop) or the APK's `assets/pack/` (`WC2.Tools build-pack`) | `dotnet build Hosts/Desktop`, or run with `--assets assets` to read the tree without a build |

**Replacing a file in the package**

* At its path: find it in `generated/asset_index.json` or `assets/legacy-names.json` (the original name
  `mainbg-568h@2x.webp` is `image/ui/phone-568-hd/mainbg.webp`) and overwrite it there.
* Or by its original name: put the file at the root of the package (`pack\mainbg-568h@2x.webp`). The
  game asks for original names and looks them up as a path first, so the root file wins.
* Font pages (`fontkt*`, `fontht*`) have a generated runtime copy, `generated/<path>.wctex`, which the
  game prefers within the same layer: when you replace a font page inside the package, delete its
  `.wctex` too (or rebuild). A mod does not have this problem.

**The important difference to the original package: game data is JSON now.** The game reads these files,
not the XML they were converted from:

| Original file | Read now |
|---|---|
| `armydef.xml` | `data/json/units/units.json` |
| `carddef.xml` | `data/json/cards/cards.json` |
| `commanderdef.xml` | `data/json/commanders/commanders.json` |
| `generalphotos.xml` | `data/json/commanders/portraits.json` |
| `battlelist.xml` | `data/json/campaigns/campaigns.json` |
| `conquestlist.xml` | `data/json/conquests/conquests.json` |
| `areatax1.xml` | `data/json/maps/1/areas.json` |
| `guioptions.xml` | `data/json/layout/options.json` |
| `Localizable.strings` and the string tables | `data/json/text/<language>/*.json` |
| numbers in the code (terrain, rules, medals, AI) | `data/json/terrain/terrain.json`, `rules/rules.json`, `rules/medals.json`, `ai/profiles.json` |

The original XML files are still in the package (`data/xml/`, untouched), but the game reads one only
when its JSON file is missing; `--verify-data` compares the two. **Editing `armydef.xml` changes nothing**:
edit `units.json` instead (the fields are in `DATA_SCHEMA.md`). Scenarios (`data/xml/scenarios/*.xml`),
tutorial scripts, texture atlases (the `.xml` next to an image) and the binary map files keep their
original formats and are read as before.

**Android: changing the APK.** Unzip the APK, change files under `assets/pack/`, zip it again, align it
(`zipalign -p 4`) and sign it (`apksigner sign --ks <your keystore>`). A re-signed APK has another
signature, so uninstall the old one first (that deletes its saves; copy
`Android/data/com.wc2remake.csharp/files/` away before). A mod folder needs none of this; prefer it on
Android.

## 2. Mod folders (recommended)

A mod is a folder with a `mod.json` and files laid out like the package (the tree, or original names at
its root). Folders that are read:

| Platform | Folder |
|---|---|
| Windows | `%LOCALAPPDATA%\WC2CSharp\mods\<mod>\`, `mods\<mod>\` next to `WC2.exe`, and in a development build the repository's `mods/` folder (or only the folder passed with `--mods <folder>`) |
| Android | `Android/data/com.wc2remake.csharp/files/mods/<mod>/` (reachable over USB) and `files/documents/mods/` |

`--no-mods` starts the desktop build without any mod; `--all-mods` also loads mods marked
`"enabled": false`. `mods/example-balance` is a working example (shipped disabled).

**mod.json**

```json
{
  "id": "my-mod",
  "name": "My mod",
  "version": "1.0",
  "author": "you",
  "description": "What the mod does.",
  "priority": 0,
  "enabled": true
}
```

`id` names the mod (a folder mod replaces a built-in mod with the same id); mods apply in ascending
`priority` (then by id), so a higher priority wins; `enabled: false` switches the mod off.

**Examples**

A stronger tank for everyone (only the changed fields):

```json
// mods/my-mod/data/json/units/units.json
{ "countries": { "others": { "tank": { "strength": 120, "maxAttack": 10 } } } }
```

One English text:

```json
// mods/my-mod/data/json/text/en/stringtable_en.json
{ "strings": { "Tank": "Medium Tank" } }
```

A new main menu background, by its original name or by its path (either works):

```
mods/my-mod/mainbg-568h@2x.webp
mods/my-mod/image/ui/phone-568-hd/mainbg.webp
```

A changed conquest: copy `data/xml/scenarios/conquest_1.xml` from the package to
`mods/my-mod/conquest_1.xml` (original name at the root) or `mods/my-mod/data/xml/scenarios/conquest_1.xml`
(same path) and edit the copy.

The standard AI defends more (one number of a profile):

```json
// mods/my-mod/data/json/ai/profiles.json
{ "profiles": { "standard": { "defendCapital": 4.0 } } }
```

**A new conquest**: its scenario XML, an entry in `data/json/scenarios/index.json` (`"file"` says where the
XML is), its entry in `data/json/conquests/conquests.json` (`centerX`, `centerY`, `flags`, and the options
`fogOfWar` and `leftRightWrap`), its texts (`conquest name N`, `conquest country name N <id>`, ... in
`data/json/text/<language>/stringtable_<language>.json`) and its button `button_conquestN@2x.png`.
`mods/map-natural-earth` does all of this for conquest 10 and is a complete example. A battle exported
with the pause box **Save** button (`game.json`, the JSON scenario format in `DATA_SCHEMA.md`) can be put
into a mod as `data/json/scenarios/<name>.json`; it is then read instead of the XML of that name.

**A new map**: map geometry is binary (`area<id>.bin`, `adjion<id>.bin`, `areamark<id>.raw`, tiles, zone
atlases). `WC2.Tools build-map` generates all of it from GeoJSON as a mod (`MAP_PIPELINE.md`); a new map
takes the next free map id.

**Checking a mod**

```powershell
dotnet run --project Hosts/Desktop -- --verify-data --mods <folder that contains your mod>
```

lists every definition the mods change compared with the original game data, field by field, and every
schema problem. The game log (`--log FILE`) names every mounted mod and how many files it brings.

## 3. Built-in mods

`assets/build.json` `builtInMods` lists mod folders that `WC2.Tools build-pack` copies into the package
under `mods/<id>/` (in the APK too). The game mounts them like folder mods, so the package's own files stay
untouched and the content can be switched off. The world map ships this way (`mods/map-natural-earth`,
built in, enabled). To turn it off: set `"enabled": false` in its `mod.json` and rebuild, or put a folder
mod with the id `map-natural-earth` and `"enabled": false` into a mods folder (a folder mod replaces the
built-in one with the same id).

## Limits

* Map geometry and scenario area masks are binary; new maps come from `WC2.Tools build-map`.
* New area types (`data/json/terrain/terrain.json`) are fully data driven; new installation or building
  kinds are not (their behaviour is code), but their numbers are data.
* New countries need flags and commanders; the game knows 31 countries (`data/json/countries/countries.json`).
* There is no in-game mod list yet; mods are switched with `enabled` in `mod.json`.

---

<a id="chinese"></a>
# 模组（Mod）制作

本页说明游戏如何查找文件，以及修改游戏的三种方式：直接修改游戏资源包（也就是你以前对原版游戏所做的）、
放在游戏旁边的模组文件夹、以及内置在资源包里的模组。三种方式都可以用，区别在于更新后需要重做什么、以及
撤销是否方便。

## 概览

| 方式 | 位置 | 适合 | 撤销 |
|---|---|---|---|
| **直接修改资源包** | `WC2.exe` 旁边的 `pack\`（Windows）、APK 内的 `assets/pack/`（Android）、仓库中的 `assets/` | 快速的个人修改，即“老办法” | 恢复原文件（或重新安装）；游戏更新会覆盖你的修改 |
| **模组文件夹**（推荐） | `%LOCALAPPDATA%\WC2CSharp\mods\<模组>\` 或程序旁边的 `mods\`（Windows），`Android/data/com.wc2remake.csharp/files/mods/<模组>/`（Android） | 想长期保留、分享或可随时关闭的修改 | 删除文件夹，或设置 `"enabled": false` |
| **内置模组** | 在 `assets/build.json` 的 `builtInMods` 中列出的模组文件夹，打包到 `mods/<id>/` | 随游戏一起发布的内容（世界地图） | 从 `builtInMods` 中移除并重新构建，或在其 `mod.json` 中设置 `"enabled": false` |

模组从不修改资源包本身：它叠加在资源包之上。游戏先查模组，再查下面的资源包，所以删除模组即可恢复原样。

## 游戏如何查找文件

游戏的所有文件都通过同一个资源层读取（`Engine/IO/AssetSystem.cs`）。它是一个栈：

```
top     mods, in ascending "priority" (then by id): the last one wins
        (folder mods and built-in mods alike; a folder mod replaces a built-in mod with the same id)
bottom  the game package: pack\ (Windows), assets/pack/ in the APK, or the repository's assets/ tree
```

即：最上层是模组，按 `priority` 从小到大（相同时按 id）叠加，后加载的覆盖先加载的（文件夹模组与内置模组
同样处理；文件夹模组会替换同 id 的内置模组）；最底层是游戏资源包。

查找一个名字的顺序：

1. **按路径**（`image/ui/phone-568-hd/mainbg.webp`、`data/json/units/units.json`）：在该路径上有文件的
   最上层获胜。
2. **按原版文件名**（`mainbg-568h@2x.webp`，即原版游戏使用、移植代码仍在请求的名字）：资源包索引
   `generated/asset_index.json` 给出该文件在目录树中的路径（语言变体优先：中文先查 `zh_CN.lproj/...`），
   然后按第 1 步查找该路径，因此替换了该路径的模组会获胜。模组（或资源包根目录下的文件）也可以按原版
   文件名替换文件：放在模组根目录（或某个 `X.lproj` 文件夹）中、名为 `mainbg-568h@2x.webp` 的文件，会在
   使用索引之前按这个名字被找到。

`assets/legacy-names.json` 列出了每个原版文件名及其在目录树中的路径；目录树的说明见 `ASSET_LAYOUT.md`。

**游戏数据是合并，而不是替换。** 对于 JSON 数据文件（`data/json/**/*.json`），游戏读取资源包中的文件以及
每个模组中同一路径的文件，并按模组顺序合并：

* 对象按键逐个合并，所以模组只需列出它修改的内容；
* 数组和普通值会替换下层的值；
* `null` 删除一个键，例如 `data/json/units/units.json` 中的 `{"countries": {"others": {"realbomber": null}}}`；
* 对象内写 `"$replace": true` 表示替换下层的对象而不是合并。

合并结果随后按该文件的模式（schema，`data/json/schemas/`）校验。如果某个模组使文件无效，日志会指出位置
（`$.countries.others.tank.price: -5 is below the minimum 0`），并忽略该模组对这个文件的修改；游戏用其余
内容继续运行。

其他所有文件（图片、图集、字体、音效、音乐、剧本 XML、二进制地图数据）由拥有该文件的最上层**整体替换**。

## 1. 直接修改游戏资源包

这种方式仍然可用，而且是尝试修改最快的办法。变化在于文件的位置，以及游戏实际读取的是哪些文件。

**资源包在哪里**

| | 资源包 | 修改之后 |
|---|---|---|
| Windows 发布版 | `WC2.exe` 旁边的 `pack\` 文件夹 | 重新启动游戏 |
| Android | APK 内的 `assets/pack/` | 重新打包并签名 APK（见下文） |
| 开发环境 | 仓库中的 `assets/`；每次构建都会把它生成为 `pack\`（桌面版）或 APK 的 `assets/pack/`（`WC2.Tools build-pack`） | 执行 `dotnet build Hosts/Desktop`，或用 `--assets assets` 运行，不经构建直接读取目录树 |

**替换资源包中的文件**

* 按路径：在 `generated/asset_index.json` 或 `assets/legacy-names.json` 中找到它（原版文件名
  `mainbg-568h@2x.webp` 对应 `image/ui/phone-568-hd/mainbg.webp`），在那里覆盖。
* 或按原版文件名：把文件放到资源包根目录（`pack\mainbg-568h@2x.webp`）。游戏请求的是原版文件名，并先把它
  当作路径查找，所以根目录下的文件获胜。
* 字体页（`fontkt*`、`fontht*`）有一份生成的运行时副本 `generated/<路径>.wctex`，在同一层中游戏优先使用它：
  在资源包内替换字体页时，要同时删除对应的 `.wctex`（或重新构建）。模组没有这个问题。

**与原版资源包最重要的区别：游戏数据现在是 JSON。** 游戏读取的是下表右列的文件，而不是它们转换前的 XML：

| 原版文件 | 现在读取 |
|---|---|
| `armydef.xml` | `data/json/units/units.json` |
| `carddef.xml` | `data/json/cards/cards.json` |
| `commanderdef.xml` | `data/json/commanders/commanders.json` |
| `generalphotos.xml` | `data/json/commanders/portraits.json` |
| `battlelist.xml` | `data/json/campaigns/campaigns.json` |
| `conquestlist.xml` | `data/json/conquests/conquests.json` |
| `areatax1.xml` | `data/json/maps/1/areas.json` |
| `guioptions.xml` | `data/json/layout/options.json` |
| `Localizable.strings` 与各字符串表 | `data/json/text/<语言>/*.json` |
| 原先写在代码里的数值（地形、规则、勋章、AI） | `data/json/terrain/terrain.json`、`rules/rules.json`、`rules/medals.json`、`ai/profiles.json` |

原版 XML 文件仍在资源包中（`data/xml/`，未改动），但只有在对应的 JSON 文件不存在时游戏才会读取它；
`--verify-data` 会比较两者。**修改 `armydef.xml` 不会产生任何效果**：请改 `units.json`（字段说明见
`DATA_SCHEMA.md`）。剧本（`data/xml/scenarios/*.xml`）、教程脚本、纹理图集（图片旁边的 `.xml`）和二进制
地图文件保持原版格式，读取方式与以前相同。

**Android：修改 APK。** 解压 APK，修改 `assets/pack/` 下的文件，重新压缩，对齐（`zipalign -p 4`）并签名
（`apksigner sign --ks <你的密钥库>`）。重新签名的 APK 签名不同，所以要先卸载旧版本（这会删除它的存档；
请先把 `Android/data/com.wc2remake.csharp/files/` 复制出来）。模组文件夹不需要这些步骤，在 Android 上请优先
使用模组。

## 2. 模组文件夹（推荐）

一个模组就是一个包含 `mod.json` 的文件夹，其中的文件按资源包的方式排列（目录树，或放在根目录下的原版
文件名）。会被读取的文件夹：

| 平台 | 文件夹 |
|---|---|
| Windows | `%LOCALAPPDATA%\WC2CSharp\mods\<模组>\`、`WC2.exe` 旁边的 `mods\<模组>\`，开发版还会读取仓库的 `mods/` 文件夹（或只读取用 `--mods <文件夹>` 指定的文件夹） |
| Android | `Android/data/com.wc2remake.csharp/files/mods/<模组>/`（可通过 USB 访问）以及 `files/documents/mods/` |

`--no-mods` 让桌面版不加载任何模组启动；`--all-mods` 还会加载标记为 `"enabled": false` 的模组。
`mods/example-balance` 是一个可用的示例（发布时处于关闭状态）。

**mod.json**

```json
{
  "id": "my-mod",
  "name": "My mod",
  "version": "1.0",
  "author": "you",
  "description": "What the mod does.",
  "priority": 0,
  "enabled": true
}
```

`id` 是模组的名字（文件夹模组会替换同 id 的内置模组）；模组按 `priority` 从小到大（相同时按 id）应用，
所以 priority 越高越优先；`enabled: false` 关闭该模组。

**示例**

让所有国家的坦克更强（只写修改的字段）：

```json
// mods/my-mod/data/json/units/units.json
{ "countries": { "others": { "tank": { "strength": 120, "maxAttack": 10 } } } }
```

修改一条英文文本：

```json
// mods/my-mod/data/json/text/en/stringtable_en.json
{ "strings": { "Tank": "Medium Tank" } }
```

新的主菜单背景，按原版文件名或按路径（两种都可以）：

```
mods/my-mod/mainbg-568h@2x.webp
mods/my-mod/image/ui/phone-568-hd/mainbg.webp
```

修改一个征服剧本：把资源包中的 `data/xml/scenarios/conquest_1.xml` 复制为 `mods/my-mod/conquest_1.xml`
（根目录下的原版文件名）或 `mods/my-mod/data/xml/scenarios/conquest_1.xml`（相同路径），然后编辑这份副本。

让标准 AI 更重视防守（修改某个 AI 配置中的一个数值）：

```json
// mods/my-mod/data/json/ai/profiles.json
{ "profiles": { "standard": { "defendCapital": 4.0 } } }
```

**新增一个征服**：需要它的剧本 XML；`data/json/scenarios/index.json` 中的一项（`"file"` 指明 XML 的位置）；
`data/json/conquests/conquests.json` 中的一项（`centerX`、`centerY`、`flags`，以及选项 `fogOfWar` 和
`leftRightWrap`）；它的文本（`data/json/text/<语言>/stringtable_<语言>.json` 中的 `conquest name N`、
`conquest country name N <id>` 等）；以及按钮图片 `button_conquestN@2x.png`。`mods/map-natural-earth`
为征服 10 完成了以上所有内容，是一个完整的示例。用暂停菜单中的 **Save** 按钮导出的战局（`game.json`，
即 `DATA_SCHEMA.md` 中的 JSON 剧本格式）可以作为 `data/json/scenarios/<名字>.json` 放进模组，之后游戏会读取
它而不是同名的 XML。

**新增一张地图**：地图几何数据是二进制的（`area<id>.bin`、`adjion<id>.bin`、`areamark<id>.raw`、地图
贴图块、区域图集）。`WC2.Tools build-map` 可以从 GeoJSON 生成全部这些文件并输出为模组（见
`MAP_PIPELINE.md`）；新地图使用下一个空闲的地图 id。

**检查模组**

```powershell
dotnet run --project Hosts/Desktop -- --verify-data --mods <folder that contains your mod>
```

（参数为包含你的模组的文件夹）会逐字段列出模组相对原版数据修改了哪些定义，以及所有模式校验问题。游戏日志
（`--log FILE`）会列出每个已加载的模组及其包含的文件数。

## 3. 内置模组

`assets/build.json` 的 `builtInMods` 列出的模组文件夹，会被 `WC2.Tools build-pack` 复制到资源包的
`mods/<id>/` 下（APK 中也一样）。游戏像加载文件夹模组一样加载它们，因此资源包自身的文件保持不变，内容也
可以关闭。世界地图就是这样发布的（`mods/map-natural-earth`，内置、默认启用）。关闭方法：在它的
`mod.json` 中设置 `"enabled": false` 并重新构建；或者在模组文件夹中放一个 id 为 `map-natural-earth`、
`"enabled": false` 的文件夹模组（文件夹模组会替换同 id 的内置模组）。

## 限制

* 地图几何数据和剧本的地区掩码是二进制的；新地图由 `WC2.Tools build-map` 生成。
* 新的地区类型（`data/json/terrain/terrain.json`）完全由数据驱动；新的设施或建筑种类则不是（其行为在代码
  中），但它们的数值是数据。
* 新国家需要国旗和将领；游戏目前认识 31 个国家（`data/json/countries/countries.json`）。
* 游戏内还没有模组列表；通过 `mod.json` 中的 `enabled` 开关模组。
