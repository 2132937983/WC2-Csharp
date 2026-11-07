# Asset layout

**Language / 语言:** [English](#english) | [中文](#chinese)

<a id="english"></a>

Everything the game reads lives in one tree, `assets/`, sorted by purpose. The 1 111 files of the
original game are in it unchanged (moved and renamed, never re-encoded); the authored JSON data sits
next to them. The build turns the tree into the runtime package and generates the index that keeps
every original name working.

## The tree

```
assets/
  audio/
    music/                 battle1..4.mp3, defeat_music.mp3
    sfx/                   game sounds (fire, move, occupy, ...)
    ui/                    interface sounds (btn, cancel, select, pop, buy, ...)
  image/
    ui/<variant>/[<lang>/] interface atlases and images
    army/armour|infantry|artillery|navy/<variant>/   unit sprites of the battle animation (per country)
    army/atlas/<variant>/  the unit atlas the map uses (army.xml + army.webp)
    army/commander/<variant>/  general portraits and their atlas
    map/tiles/<variant>/   map background tiles (ETC1 .pkm)
    map/minimap/<variant>/ minimap tiles
    map/regions/<variant>/ area overlay atlases (m1_zone*, m1_conquest*)
    map/flags/<variant>/   flag atlas
    map/markers/<variant>/ frame boxes, fortification and anti-aircraft markers
    effects/<variant>/     particle effects
    fonts/<variant>/[<lang>/]  bitmap fonts (.fnt + pages)
    battlebg/<variant>/    battle backgrounds
  data/
    json/                  authored game data (DATA_SCHEMA.md): units/ cards/ commanders/ campaigns/
                           conquests/ terrain/ rules/ layout/ maps/ scenarios/ text/<lang>/ ...
    xml/                   the original XML data, untouched: armydef.xml, carddef.xml, ...,
                           scenarios/ (campaign battles, conquests, tutorial), text/ (string tables),
                           maps/1/ (area tax table), battle/ (animation definitions), legacy/
    binary/                original binary data: maps/1/ (area geometry), scenarios/ (area masks)
    maps/<id>/             generated map packs (MAP_PIPELINE.md): natural-earth (regions, adjacency, raster)
    strings/<lang>/        Localizable.strings, InfoPlist.strings
    legacy/                map.ini
  scripts/
    tutorial/              tutorial scripts
  build.json               build settings (not packaged)
  legacy-names.json        original name -> path in this tree (not packaged)
mods/                      mods mounted by development builds (MODDING.md)
generated/asset_index.json copy of the generated index, kept current by every build (for review)
```

### Variants are folders, not name suffixes

The original encoded variants in file names (`ui1_hd.webp`, `mainbg-568h@2x.webp`,
`button_start_cn_iPad.png`). In the tree the suffix is gone and the variant is a folder:

| Folder | Original suffix | Meaning |
|---|---|---|
| `common` | none | base art (the 480x320 SD set, and files without variants) |
| `hd` | `_hd`, `@2x` | double resolution (every Android phone uses it) |
| `tablet` | `_iPad` | 1024x768 tablet layout |
| `tablet-hd` | `_iPad_hd`, `_iPad@2x` | tablet layout, double resolution |
| `phone-568`, `phone-568-hd` | `-568h`, `-568h@2x`, `_568hd` | 568x320 phone layout |
| `phone-640`, `phone-640-hd` | `-640h`, `-640h@2x`, `_640hd` | 640x320 phone layout |
| `<lang>` below the variant | `_cn`, `_en`, `_ja`, `_kr`/`_ko`, `_ru`, `_tw`, or an `X.lproj` folder | language |

Example: `zh_CN.lproj/font_text_newgame_1_cn_hd.png` is
`image/fonts/hd/cn/font_text_newgame_1.png`. A file in a language folder whose suffix names another
language keeps that suffix (`ru.lproj/font_text_newgame_1_en.png` ->
`image/fonts/common/ru/font_text_newgame_1_en.png`), so no two originals share a path. Country codes
of unit sprites are not variants (`tank_de_hd.png` -> `image/army/armour/hd/tank_de.png`).

## Original names keep working

`assets/legacy-names.json` maps every original name (its path in the original flat folder, language
folders included) to its path in the tree. The ported code still asks for `"mainbg-568h.webp"`,
`"stringtable_cn.xml"` or `"zh_CN.lproj/Localizable.strings"`; the asset layer finds the file through
the index. Add an entry only when a file the code requests by an old name moves; new files need none.

## Build: the runtime package

`WC2.Tools build-pack` runs after every build of the desktop host (output
`Hosts/Desktop/bin/<cfg>/net9.0/pack/`) and of the Android host (output `Hosts/Android/obj/pack/`,
shipped as `assets/pack/` in the APK). It:

1. copies the tree to the package (same paths; `build.json`, `legacy-names.json` and Markdown files
   stay out), incrementally;
2. generates runtime formats under `generated/` (see below);
3. packages the mods listed in `build.json` `builtInMods` under `mods/<id>/` (the game mounts them as
   mods; `--no-mods` leaves them out);
4. writes `generated/asset_index.json`: every file with its size, variant, language and original
   name(s), and the full original name -> path map. The desktop build also writes a copy to
   `generated/asset_index.json` in the repository root, so a review shows what changed.

Files an earlier build produced but this one does not are removed from the package.

### Runtime formats

`build.json` `runtimeFormats` lists generated files. The originals are never replaced; the game uses a
generated file only when it comes from the same layer as its source, so a mod that replaces the source
image still wins.

| Format | Source | Output | Effect |
|---|---|---|---|
| `rgba4444` | `image/fonts/hd/cn/fontht.png`, `fontkt.png` (4096 px glyph pages) | `generated/image/fonts/hd/cn/*.wctex` | 32 MB instead of 64 MB each on the GPU, no PNG decode at start |

## How the game reads assets (the asset layer)

Every read goes through `AssetSystem` (`Engine/IO/AssetSystem.cs`): textures, sounds, fonts, XML,
JSON, string tables. Layers are mounted in order: the base package, then the mods by priority. A name
resolves, in this order:

1. **mods**: a path in a mod (`image/ui/hd/ui1.webp`), or a file a mod puts at its root under an
   original name (`ui1_hd.webp`);
2. **assets**: the path in the base package;
3. **generated index**: an original name, through `generated/asset_index.json` (for a bare name the
   current language folder first: `zh_CN.lproj/<name>`, then `<name>`). The resulting path again comes
   from the top-most layer that has it, so a mod replacing that path wins.

JSON data files are not just replaced: `JsonData.LoadMerged` merges every layer's version (MODDING.md).

Sources of the base package:

* **Android**: the APK itself (`assets/pack/`), read in place through the index; nothing is extracted.
  Only when reading in place fails (checked at start-up) is the package copied out once to
  `files/pack-<install time>/` and read from there.
* **Desktop**: the `pack/` folder next to the program. `--assets <dir>` may point at another package,
  at the repository's `assets/` tree itself (runs without a build, using `legacy-names.json`; no
  generated formats), at a flat folder of original files (as shipped by the original game), or at a
  `.zip` / `.apk` file.

## Checks

`WC2.Tools validate --pack <package> --source assets` (run by `Tests/run-tests.ps1`):

* the index and the files agree (every listed file exists with its size, every file is listed);
* every original name points to an existing file;
* every asset name the data mentions (JSON and XML data, scripts, atlas and font descriptors) resolves:
  by path, by original name, by original name plus the variant suffixes the game adds itself, or as a
  sprite of an atlas. `build.json` `knownMissingReferences` lists the three gaps of the original data;
* with `--source`, the whole tree is in the package, unchanged.

## Textures

* PNG and WebP are decoded with SkiaSharp and uploaded as RGBA8.
* PKM (ETC1: map tiles, minimaps, battle backgrounds) is uploaded as it is when the GPU supports ETC2
  or ETC1 (Android), otherwise decoded on the CPU. The log says which.
* `.wctex` files are uploaded as RGBA4444.
* `--texture-stats` logs the live texture memory every 120 frames.

## Nothing is left out

Every original file is packaged, including the ones no code reads today (the battle animation sets,
the store and editor leftovers under `data/xml/legacy/`). They stay in git and in the APK.

---

<a id="chinese"></a>

# 资源布局

游戏读取的所有内容都位于同一棵目录树 `assets/` 中，按用途分类存放。原版游戏的 1 111 个文件原封不动地放在其中（只是移动和重命名，从未重新编码）；编写的 JSON 数据就放在它们旁边。构建过程会把这棵目录树转换为运行时游戏资源包，并生成一份索引，让每一个原版名称都继续可用。

## 目录树

```
assets/
  audio/
    music/                 battle1..4.mp3, defeat_music.mp3
    sfx/                   游戏音效（fire、move、occupy……）
    ui/                    界面音效（btn、cancel、select、pop、buy……）
  image/
    ui/<variant>/[<lang>/] 界面图集和图片
    army/armour|infantry|artillery|navy/<variant>/   战斗动画中的单位精灵图（按国家区分）
    army/atlas/<variant>/  地图使用的单位图集（army.xml + army.webp）
    army/commander/<variant>/  将领头像及其图集
    map/tiles/<variant>/   地图背景贴图块（ETC1 .pkm）
    map/minimap/<variant>/ 小地图贴图块
    map/regions/<variant>/ 地区叠加层图集（m1_zone*、m1_conquest*）
    map/flags/<variant>/   旗帜图集
    map/markers/<variant>/ 边框、防御工事和防空标记
    effects/<variant>/     粒子特效
    fonts/<variant>/[<lang>/]  位图字体（.fnt + 字形页）
    battlebg/<variant>/    战斗背景
  data/
    json/                  编写的游戏数据（DATA_SCHEMA.md）：units/ cards/ commanders/ campaigns/
                           conquests/ terrain/ rules/ layout/ maps/ scenarios/ text/<lang>/ ...
    xml/                   原版 XML 数据，未作改动：armydef.xml、carddef.xml、……、
                           scenarios/（战役战斗、征服、教程）、text/（字符串表）、
                           maps/1/（地区税收表）、battle/（动画定义）、legacy/
    binary/                原版二进制数据：maps/1/（地区几何形状）、scenarios/（地区遮罩）
    maps/<id>/             生成的地图包（MAP_PIPELINE.md）：natural-earth（区域、邻接关系、栅格）
    strings/<lang>/        Localizable.strings、InfoPlist.strings
    legacy/                map.ini
  scripts/
    tutorial/              教程脚本
  build.json               构建设置（不打包）
  legacy-names.json        原版名称 -> 在本目录树中的路径（不打包）
mods/                      开发版构建所挂载的模组（MODDING.md）
generated/asset_index.json 生成的索引的副本，每次构建都会更新（供审阅用）
```

### 变体是文件夹，而不是名称后缀

原版把变体编码在文件名中（`ui1_hd.webp`、`mainbg-568h@2x.webp`、`button_start_cn_iPad.png`）。在本目录树中，后缀被去掉了，变体变成了一个文件夹：

| 文件夹 | 原版后缀 | 含义 |
|---|---|---|
| `common` | 无 | 基础美术资源（480x320 的 SD 资源集，以及没有变体的文件） |
| `hd` | `_hd`、`@2x` | 双倍分辨率（所有 Android 手机都使用它） |
| `tablet` | `_iPad` | 1024x768 平板布局 |
| `tablet-hd` | `_iPad_hd`、`_iPad@2x` | 平板布局，双倍分辨率 |
| `phone-568`、`phone-568-hd` | `-568h`、`-568h@2x`、`_568hd` | 568x320 手机布局 |
| `phone-640`、`phone-640-hd` | `-640h`、`-640h@2x`、`_640hd` | 640x320 手机布局 |
| 变体之下的 `<lang>` | `_cn`、`_en`、`_ja`、`_kr`/`_ko`、`_ru`、`_tw`，或者 `X.lproj` 文件夹 | 语言 |

示例：`zh_CN.lproj/font_text_newgame_1_cn_hd.png` 对应 `image/fonts/hd/cn/font_text_newgame_1.png`。如果语言文件夹中某个文件的后缀指明的是另一种语言，则保留该后缀（`ru.lproj/font_text_newgame_1_en.png` -> `image/fonts/common/ru/font_text_newgame_1_en.png`），这样就不会有两个原版文件共用同一个路径。单位精灵图中的国家代码不是变体（`tank_de_hd.png` -> `image/army/armour/hd/tank_de.png`）。

## 原版名称继续可用

`assets/legacy-names.json` 把每一个原版名称（即它在原版扁平文件夹中的路径，包括语言文件夹）映射到它在本目录树中的路径。移植后的代码仍然请求 `"mainbg-568h.webp"`、`"stringtable_cn.xml"` 或 `"zh_CN.lproj/Localizable.strings"`；资源层会通过索引找到对应的文件。只有当代码以旧名称请求的某个文件被移动时，才需要添加条目；新文件不需要。

## 构建：运行时游戏资源包

`WC2.Tools build-pack` 会在每次构建桌面宿主程序（输出到 `Hosts/Desktop/bin/<cfg>/net9.0/pack/`）和 Android 宿主程序（输出到 `Hosts/Android/obj/pack/`，在 APK 中以 `assets/pack/` 的形式发布）之后运行。它会：

1. 以增量方式把目录树复制到游戏资源包中（路径保持不变；`build.json`、`legacy-names.json` 和 Markdown 文件不包含在内）；
2. 在 `generated/` 下生成运行时格式（见下文）；
3. 把 `build.json` 的 `builtInMods` 中列出的模组打包到 `mods/<id>/` 下（游戏把它们作为模组挂载；`--no-mods` 会把它们排除在外）；
4. 写出 `generated/asset_index.json`：其中包含每个文件的大小、变体、语言和原版名称，以及完整的原版名称 -> 路径映射。桌面构建还会在仓库根目录的 `generated/asset_index.json` 写一份副本，以便在审阅时能看到发生了哪些变化。

早先的构建生成过、但本次构建不再生成的文件，会从游戏资源包中删除。

### 运行时格式

`build.json` 的 `runtimeFormats` 列出了要生成的文件。原版文件从不会被替换；只有当生成文件与其源文件来自同一资源层时，游戏才会使用该生成文件，因此替换了源图片的模组仍然优先生效。

| 格式 | 源文件 | 输出 | 效果 |
|---|---|---|---|
| `rgba4444` | `image/fonts/hd/cn/fontht.png`、`fontkt.png`（4096 px 的字形页） | `generated/image/fonts/hd/cn/*.wctex` | 每个在 GPU 上占用 32 MB 而不是 64 MB，启动时无需解码 PNG |

## 游戏如何读取资源（资源层）

每一次读取都经过 `AssetSystem`（`Engine/IO/AssetSystem.cs`）：纹理、声音、字体、XML、JSON、字符串表。各层按顺序挂载：先是基础游戏资源包，然后是按优先级排列的各个模组。一个名称按以下顺序解析：

1. **模组**：模组中的某个路径（`image/ui/hd/ui1.webp`），或者模组以原版名称放在其根目录下的文件（`ui1_hd.webp`）；
2. **资源**：基础游戏资源包中的路径；
3. **生成的索引**：通过 `generated/asset_index.json` 解析原版名称（对于不带目录的名称，先查当前语言文件夹：`zh_CN.lproj/<name>`，然后才是 `<name>`）。解析得到的路径同样取自拥有该路径的最上层，因此替换了该路径的模组优先生效。

JSON 数据文件不只是被简单替换：`JsonData.LoadMerged` 会把每一层中的版本合并起来（见 MODDING.md）。

基础游戏资源包的来源：

* **Android**：APK 本身（`assets/pack/`），通过索引原地读取；不解压任何内容。只有当原地读取失败时（在启动时检查），才会把游戏资源包一次性复制到 `files/pack-<install time>/`，并从那里读取。
* **桌面端**：程序旁边的 `pack/` 文件夹。`--assets <dir>` 可以指向另一个游戏资源包；也可以直接指向仓库中的 `assets/` 目录树（无需构建即可运行，使用 `legacy-names.json`；没有生成的格式）；或者指向一个由原版文件组成的扁平文件夹（与原版游戏发布时的形式相同）；又或者指向一个 `.zip` / `.apk` 文件。

## 检查

`WC2.Tools validate --pack <package> --source assets`（由 `Tests/run-tests.ps1` 运行）：

* 索引与文件一致（列出的每个文件都存在且大小相符，每个文件都已列出）；
* 每个原版名称都指向一个存在的文件；
* 数据中提到的每个资源名称（JSON 和 XML 数据、脚本、图集和字体描述文件）都能解析：按路径、按原版名称、按原版名称加上游戏自行添加的变体后缀，或者作为某个图集中的精灵图。`build.json` 的 `knownMissingReferences` 列出了原版数据中的三处缺失；
* 使用 `--source` 时，整棵目录树都在游戏资源包中，且未被改动。

## 纹理

* PNG 和 WebP 用 SkiaSharp 解码，并以 RGBA8 格式上传。
* PKM（ETC1：地图贴图块、小地图、战斗背景）在 GPU 支持 ETC2 或 ETC1 时（Android）按原样上传，否则在 CPU 上解码。日志中会说明采用的是哪一种方式。
* `.wctex` 文件以 RGBA4444 格式上传。
* `--texture-stats` 每 120 帧在日志中记录一次当前的纹理内存占用。

## 没有遗漏任何内容

每一个原版文件都会被打包，包括目前没有任何代码读取的文件（战斗动画资源集、`data/xml/legacy/` 下商店和编辑器的残留文件）。它们保留在 git 和 APK 中。
