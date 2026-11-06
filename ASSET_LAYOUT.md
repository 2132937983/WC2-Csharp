# Asset layout

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

`WC2.Tools build-pack` runs after every build of `WC2` (output `WC2/bin/<cfg>/net9.0/pack/`) and of
`WC2.Android` (output `WC2.Android/obj/pack/`, shipped as `assets/pack/` in the APK). It:

1. copies the tree to the package (same paths; `build.json`, `legacy-names.json` and Markdown files
   stay out), incrementally;
2. generates runtime formats under `generated/` (see below);
3. writes `generated/asset_index.json`: every file with its size, variant, language and original
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

Every read goes through `AssetSystem` (`WC2.Engine/IO/AssetSystem.cs`): textures, sounds, fonts, XML,
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

`WC2.Tools validate --pack <package> --source assets` (run by `WC2/tests/run-tests.ps1`):

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
