# Assets: sources, the runtime package and how the game reads it

## Sources (in git)

| Folder | Content | Rule |
|---|---|---|
| `WC2/assets/` | the 1 111 original files of the game, flat, as shipped | never modified |
| `WC2/content/` | authored files: the JSON game data (`docs/DATA.md`) | the source of truth for game data |
| `WC2/assets-layout.json` | where each original file goes in the package, and which runtime formats to generate | build configuration |

## The runtime package (generated)

`WC2.Tools build-pack` runs after every build of `WC2` (output `bin/<cfg>/net9.0/pack/`) and
`WC2.Android` (output `obj/pack/`, shipped as `assets/pack/` in the APK). It:

1. copies every original file to the package path given by the first matching rule of
   `assets-layout.json`, keeping its file name;
2. adds the files of `WC2/content/` at their own paths (they win over an original at the same path);
3. generates runtime formats next to their sources (`runtimeFormats` in the layout file);
4. writes `index.json`: every file with its size, and every original name (alias) with its path.

The build is incremental: unchanged files are not copied again, and files an earlier build produced
but this one does not are removed. `WC2.Tools validate --pack <dir> --source WC2/assets` checks that
every original is in the package unchanged and every alias resolves.

Package layout:

```
index.json
data/            JSON game data; data/original/ keeps the original XML tables
  ui/            options page layout
maps/1/          area table (JSON + original XML), geometry (bin/raw)
  tiles/ minimap/ zones/
maps/common/     map frame boxes
scenarios/       scenarios (JSON + original XML + area masks), tutorial/ scripts
lang/<code>/     everything language specific: string tables, Localizable.json, fonts, text atlases
fonts/           fonts without a language
gfx/ui/          UI atlases and images; phone-568/, phone-640/, tablet/ hold the layout variants
gfx/units/       army and flag atlases
gfx/portraits/   general portraits
gfx/effects/     particle effects
gfx/battle/      battle backgrounds and unit animation sets (battle animation, not played yet)
gfx/tutorial/    tutorial images
audio/music/ audio/sfx/
legacy/          leftovers of the original tool chain (store, editor, old lists)
```

Every original file is packaged, including the ones no code reads today.

## Runtime formats

| Format | Used for | Effect |
|---|---|---|
| `.wctex` RGBA4444 (deflate) | the two 4096 px Kai/Hei font pages | 32 MB instead of 64 MB each on the GPU, no PNG decode at start |

The game uses a generated file only when it comes from the same layer as its source, so a mod that
replaces the source image still wins.

## How the game reads assets

All reads go through `AssetSystem` (`WC2.Engine/IO/AssetSystem.cs`). Layers are mounted in order:
the base package, then the mods by priority. A name resolves, in order, to

1. the current language's variant (`zh_CN.lproj/<name>` alias, then `lang/<code>/<name>`),
2. the package path itself,
3. the original flat name through `index.json` (and the file names of mod files).

So the ported code keeps loading `"mainbg-568h.webp"` or `"stringtable_cn.xml"` by their original
names while the files live in the new tree. Saves and settings are plain files in the documents folder
and do not go through the asset system.

Sources:

* desktop: the `pack/` folder next to the program (`--assets <dir>` may point at another package, at
  a flat folder of original files, or at a `.zip` / `.apk`);
* Android: the APK itself (`assets/pack/`), read in place through the index, nothing is extracted;
* mods: folders (`docs/MODDING.md`).

`JsonData.LoadMerged` reads every layer's version of a data file and merges them.

## Textures

* PNG and WebP are decoded with SkiaSharp and uploaded as RGBA8.
* PKM (ETC1: map tiles, minimaps, battle backgrounds) is uploaded as it is when the GPU supports ETC2
  or ETC1 (every Android device; 0.5 MB per 1024 px tile instead of 4 MB), otherwise decoded on the
  CPU. The log says which (`Textures: native ETC upload ...`).
* `.wctex` files are uploaded as RGBA4444.
* `--texture-stats` logs the live texture memory every 120 frames.

Measured at the start of a conquest on the desktop (no ETC support there): 389 MB before these
changes, 288 MB after (font pages halved, battle backgrounds loaded only when used). On a device with
ETC support the map tiles and minimaps drop by a further ~118 MB.
