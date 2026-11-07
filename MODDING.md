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

**A new map**: map geometry is binary (`area<id>.bin`, `adjion<id>.bin`, `areamark<id>.raw`, and either
tiles plus zone atlases or one `map<id>.mesh`, which the game draws from vertex buffers). `WC2.Tools
build-map` generates all of it from GeoJSON as a mod (`MAP_PIPELINE.md`); a new map takes the next free map
id. A mesh map's `areas.json` also carries display settings (zoom, unit fitting, markers; DATA_SCHEMA.md).

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

## 4. A total conversion: build-scenario

A conquest with its own countries, factions and wars needs no code. `samples/mods/world-1962` is a complete
example: an original alternate 1962 on the world map, 65 countries, ten factions and relations that change
with the rounds. It is a sample, not part of the game: nothing under `samples/` is packaged or loaded
unless you ask for it.

**Playing the sample**

* Windows: copy `samples/mods/world-1962` into `%LOCALAPPDATA%\WC2CSharp\mods\` (or into `mods\` next to
  `WC2.exe`), or start a development build with `dotnet run --project Hosts/Desktop -- --mods samples/mods`.
* Android: copy the folder to `Android/data/com.wc2remake.csharp/files/mods/world-1962/`.
* Then **Conquest**, the 11th entry, *1962 DIVIDED WORLD*. It needs the world map (`map-natural-earth`, built
  in). To switch it off, delete the folder or set `"enabled": false` in its `mod.json`.

**What the mod contains** (all generated, about 1 MB)

| File | What |
|---|---|
| `data/json/scenarios/conquest_11.json` | the scenario: factions, relations, countries, every area's owner, cities, units |
| `conquest_11.bin` | the area switch file (every area takes part) |
| `data/json/conquests/conquests.json` | the conquest list entry (camera, `fogOfWar`, `leftRightWrap`) |
| `data/json/countries/countries.json` | the ids of the new countries |
| `flag_<id>.png`, `button_<id>@2x.png`, `sflag_<id>@2x.png` | each new country's flag, list button and small preview flag |
| `button_conquest11@2x.png` | the conquest list button |
| `data/json/text/<lang>/stringtable_<lang>.json` (and `_iPad`) | names, intros, faction names, announcements (English and Chinese; English in the other languages) |
| `source/world-1962.json` | the rules everything above is built from |

**Building it**: `dotnet run --project Tools -- build-scenario --config samples/mods/world-1962/source/world-1962.json`
writes the mod next to its `source/` folder (`--out DIR` writes elsewhere). Change the rules, build again,
restart the game.

**The rules file** (JSON; comments and trailing commas allowed)

* `mod` (the `mod.json` fields), `conquest` (the number; 11 is the first free one), `map` (`id`, the map
  pack and its area table), `texts` (`name`, `age`, `intro`), `camera` and `button` (lon, lat),
  `fogOfWar`, `leftRightWrap`, `economy`: money = (`moneyBase` + `moneyPerTax` x taxes) x the country's
  `economy`, industry likewise with `industryBase` and `industryPerTax`. Every text is per language
  (`{"en": ..., "cn": ...}`); English stands in for a missing language.
* `defaults`: what a country does not say itself: `commander`, `techLevel`, `taxFactor`, `fleets`, `units`
  per kind of area (`capital`, `largeCity`, `city`, `port`), `intro`, `victory`.
* `factions`: `id`, `name`, `color` (unit bases), `leader`, `tags`.
* `relations`: as in the scenario (`DATA_SCHEMA.md`, "Factions and relations"); the `announce` texts become
  string table keys.
* `countries`, in turn order: `id`; `art` uses the game's own flag and units of an existing country (`de`,
  `ja`, `tw`, ...), otherwise `flag` draws one: a design `{"bands": [colours], "vertical": false, "weights":
  [...], "overlays": [...]}` with overlays `rect`, `cross`, `saltire`, `disc`, `ring`, `star`, `triangle`,
  `crescent`, `diamond`, `checker`, `border`, or a PNG next to the rules. Then `faction` (or `alliance`),
  `tags`, `name`, `intro`, `victory`, `color`, `commander`, `techLevel`, `capital` (lon, lat: the nearest
  owned capital area gets a fort and the capital garrison), `garrison` (extra infantry in capitals and
  large cities), `fleets` (destroyers along the coast), `economy`.
* `owners`: rules in order, a later rule winning. A region matches when every condition holds:
  `countries` (today's country code), `provinces` (ISO 3166-2 codes; `"AM-"` takes all of a country),
  `era` (`{"1939": ["gw:255"]}`: the unit holding it in that era of the map pack), `lon` and `lat`
  (`[min, max]`), `near` (`[lon, lat, km]`), `regions` (ids) and `not` (a rule that must not match).
  `"to": null` leaves regions to nobody.
* `armies`: extra units on the owned area nearest a point (fronts).

**New countries without the tool**: an id the game does not know needs (1) an entry in
`data/json/countries/countries.json` and (2) one picture, `flag_<id>.png`: the plain flag, any size wider
than tall (150 x 102 has the original's proportions). The game puts it on a pole on the map, scales it for
the boxes, and shows it in the conquest list unless the mod also has `button_<id>@2x.png` (151 x 105) and
`sflag_<id>@2x.png` (66 x 43). Such a country's units use the common art and its AI badge is a common one;
in the scenario its `name` (the art id) is that id.

Everything here is data, so a mod can name countries, draw borders and bring pictures as its author wants.
Use pictures you may use: the sample draws its flags from simple designs (national flags and new ones).

## Limits

* Map geometry and scenario area masks are binary; new maps come from `WC2.Tools build-map`.
* New area types (`data/json/terrain/terrain.json`) are fully data driven; new installation or building
  kinds are not (their behaviour is code), but their numbers are data.
* New countries need only an id and a flag picture (section 4); their units use the common art and their AI
  badge is a common one. Factions beyond the original three sides and timed war and peace are scenario data
  (`DATA_SCHEMA.md`, "Factions and relations"); there is no diplomacy screen to change them during a game.
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

**新增一张地图**：地图几何数据是二进制的（`area<id>.bin`、`adjion<id>.bin`、`areamark<id>.raw`，以及
地图贴图块加区域图集，或者一个由游戏用顶点缓冲绘制的 `map<id>.mesh`）。`WC2.Tools build-map` 可以从
GeoJSON 生成全部这些文件并输出为模组（见 `MAP_PIPELINE.md`）；新地图使用下一个空闲的地图 id。网格地图的
`areas.json` 还带有显示设置（缩放、部队缩放、标记；见 DATA_SCHEMA.md）。

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

## 4. 全面改造：build-scenario

一场拥有自己的国家、阵营和战争的征服无需编写代码。`samples/mods/world-1962` 是一个完整的示例：世界地图上
一个原创的 1962 年架空历史，65 个国家、10 个阵营，以及随回合变化的外交关系。它只是示例，不属于游戏本身：
`samples/` 下的任何内容都不会被打包或加载，除非你主动要求。

**游玩示例**

* Windows：把 `samples/mods/world-1962` 复制到 `%LOCALAPPDATA%\WC2CSharp\mods\`（或 `WC2.exe` 旁边的
  `mods\`），或者用 `dotnet run --project Hosts/Desktop -- --mods samples/mods` 启动开发版。
* Android：把该文件夹复制到 `Android/data/com.wc2remake.csharp/files/mods/world-1962/`。
* 然后进入 **征服**，第 11 项 *1962 DIVIDED WORLD*。它需要世界地图（`map-natural-earth`，内置）。要关闭它，
  删除该文件夹，或在其 `mod.json` 中设置 `"enabled": false`。

**模组包含的内容**（全部自动生成，约 1 MB）

| 文件 | 内容 |
|---|---|
| `data/json/scenarios/conquest_11.json` | 剧本：阵营、外交关系、国家、每个地区的归属、城市、部队 |
| `conquest_11.bin` | 地区开关文件（所有地区都参与） |
| `data/json/conquests/conquests.json` | 征服列表条目（镜头、`fogOfWar`、`leftRightWrap`） |
| `data/json/countries/countries.json` | 新国家的 id |
| `flag_<id>.png`、`button_<id>@2x.png`、`sflag_<id>@2x.png` | 每个新国家的国旗、列表按钮和预览小旗 |
| `button_conquest11@2x.png` | 征服列表按钮 |
| `data/json/text/<语言>/stringtable_<语言>.json`（及 `_iPad`） | 国名、简介、阵营名、公告（英文和中文；其他语言使用英文） |
| `source/world-1962.json` | 以上所有内容据以生成的规则 |

**构建**：`dotnet run --project Tools -- build-scenario --config samples/mods/world-1962/source/world-1962.json`
会把模组写到 `source/` 文件夹旁边（`--out DIR` 写到别处）。修改规则、重新构建、重启游戏即可。

**规则文件**（JSON；允许注释和末尾逗号）

* `mod`（`mod.json` 的字段）、`conquest`（编号；11 是第一个空闲编号）、`map`（`id`、地图包及其地区表）、
  `texts`（`name`、`age`、`intro`）、`camera` 和 `button`（经度、纬度）、`fogOfWar`、`leftRightWrap`、
  `economy`：金钱 =（`moneyBase` + `moneyPerTax` x 税收）x 该国的 `economy`，工业同理使用 `industryBase`
  和 `industryPerTax`。所有文本都按语言给出（`{"en": ..., "cn": ...}`）；缺少的语言使用英文。
* `defaults`：国家自己没有指定的内容：`commander`、`techLevel`、`taxFactor`、`fleets`、按地区类型
  （`capital`、`largeCity`、`city`、`port`）给出的 `units`、`intro`、`victory`。
* `factions`：`id`、`name`、`color`（单位底座颜色）、`leader`、`tags`。
* `relations`：与剧本中相同（`DATA_SCHEMA.md`，“阵营与外交关系”）；`announce` 文本会变成字符串表的键。
* `countries`，按回合顺序：`id`；`art` 使用游戏中某个已有国家自己的国旗和单位（`de`、`ja`、`tw` ……），
  否则由 `flag` 绘制国旗：一个设计 `{"bands": [颜色], "vertical": false, "weights": [...], "overlays": [...]}`，
  叠加图形有 `rect`、`cross`、`saltire`、`disc`、`ring`、`star`、`triangle`、`crescent`、`diamond`、
  `checker`、`border`，或者规则文件旁边的一张 PNG。然后是 `faction`（或 `alliance`）、`tags`、`name`、
  `intro`、`victory`、`color`、`commander`、`techLevel`、`capital`（经度、纬度：最近的己方首都地区获得要塞
  和首都守军）、`garrison`（首都和大城市额外的步兵）、`fleets`（沿海的驱逐舰）、`economy`。
* `owners`：按顺序排列的规则，后面的规则优先。当所有条件都满足时，一个区域与规则匹配：`countries`（今天的
  国家代码）、`provinces`（ISO 3166-2 代码；`"AM-"` 表示整个国家）、`era`（`{"1939": ["gw:255"]}`：地图包
  在该时代中拥有它的政治单元）、`lon` 和 `lat`（`[最小, 最大]`）、`near`（`[经度, 纬度, 公里]`）、`regions`
  （id）以及 `not`（一条不能匹配的规则）。`"to": null` 让区域不属于任何国家。
* `armies`：在离某点最近的己方地区放置额外部队（前线）。

**不用工具添加新国家**：游戏不认识的 id 需要（1）在 `data/json/countries/countries.json` 中登记，以及（2）一张
图片 `flag_<id>.png`：普通的国旗，任意尺寸、宽大于高（150 x 102 与原版比例相同）。游戏会把它挂到地图上的旗杆
上，为各种对话框缩放它，并在征服列表中显示它，除非模组还提供了 `button_<id>@2x.png`（151 x 105）和
`sflag_<id>@2x.png`（66 x 43）。这样的国家的部队使用通用美术，其 AI 徽章也是通用徽章；在剧本中它的 `name`
（美术 id）就是这个 id。

这里的一切都是数据，所以模组可以按作者的意愿命名国家、划定边界、提供图片。请使用你有权使用的图片：本示例的
国旗是由简单的设计绘制的（各国国旗以及新设计的旗帜）。

## 限制

* 地图几何数据和剧本的地区掩码是二进制的；新地图由 `WC2.Tools build-map` 生成。
* 新的地区类型（`data/json/terrain/terrain.json`）完全由数据驱动；新的设施或建筑种类则不是（其行为在代码
  中），但它们的数值是数据。
* 新国家只需要一个 id 和一张国旗图片（第 4 节）；它们的部队使用通用美术，AI 徽章也是通用徽章。超出原版三方的
  阵营以及定时的战争与和平都是剧本数据（`DATA_SCHEMA.md`，“阵营与外交关系”）；游戏中没有可以改变它们的外交界面。
* 游戏内还没有模组列表；通过 `mod.json` 中的 `enabled` 开关模组。
