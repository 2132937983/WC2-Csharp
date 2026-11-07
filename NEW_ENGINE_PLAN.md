# Engine and asset structure: where we are, and a plan for `newEngine`
**Language / 语言:** [English](#english) | [中文](#chinese)

<a id="english"></a>

Branch `original` holds the faithful restoration (commit `804ffdb`) and stays frozen as the
behavioural reference. Branch `newEngine` carries the work below. This document has four parts:

0. Progress on `newEngine` (what was done, how it was checked, what is next).
1. How the engine worked at the start (written before the work; paths then were `WC2/Engine`,
   `WC2/Game`, `WC2/assets`).
2. How the assets were organised, and what a running game actually loads.
3. The proposal and the decisions.

---

## Part 0. Progress on `newEngine` (status 2026-11-06)

Decisions: visuals may change; the rules code keeps its original names; the original files stay
untouched (they were moved and renamed, never re-encoded) and runtime formats are generated in a build
step; unreachable and dormant files stay in the APK; mods are a goal; Android and Windows only; game
data is JSON; definitions are data, special behaviour stays in code selected by type, trait or id.
Everything is committed on `newEngine`, step by step; every step ran the whole suite before its commit.

### Done

| Step | What | How it was checked |
|---|---|---|
| 0. Safety net | `--seed`, `--fixed-step`, `--docs`; script commands `dump`, `autoplay`, `settle`, `dumpafter`, `hittest`, `texstats`, `startconquest`; golden rule-state dumps; reference screenshots (`WC2.Tools compare-image`, 62 small JPEGs); the asset checker (`validate`); unit tests (`Tests/WC2.Tests`, 38); the hidden-control hit test | goldens replay byte for byte; the hit test found 14 reachable hidden buttons before the fix, 0 after |
| 1. Asset tree | `assets/` by purpose (`audio/music\|sfx\|ui`, `image/ui\|army/<kind>\|map/tiles\|regions\|flags\|markers\|effects\|fonts\|battlebg`, `data/json`, `data/xml`, `data/binary`, `scripts`), variants as folders (`common`, `hd`, `tablet`, `phone-568-hd`, ..., `<lang>`); `legacy-names.json` -> `generated/asset_index.json`; one asset layer (mods -> assets -> original names); Android reads the APK in place, extraction only as a fallback; `ASSET_LAYOUT.md`, `MODDING.md` | goldens from the package, the tree itself and the APK; `validate`: every original reachable, every name the data mentions present |
| 2. Data | JSON for units, cards, commanders, portraits, campaigns, conquests, terrain, war medals, rules, countries, audio, layout profiles, the options layout, the map area table, texts, AI profiles; scenarios and tutorial scripts stay the original XML with a generated index; a JSON Schema per file kind, checked whenever a file loads (an invalid mod's changes are dropped); `DATA_SCHEMA.md` | `--verify-data`: 19 data types identical to the original XML or built-in tables, every file valid |
| 3. Structure | projects `Engine`, `Data`, `Game`, `Hosts/Desktop`, `Hosts/Android`, `Tools`, `Tests` (the engine references nothing of the game, data only the engine, the game no host); one `Layout` facade fed by the layout profile (135 scattered checks); resources released when screens close (regression `resource_cycle`) | builds (0 warnings), suite |
| 3. Turn processing | hop distances cached per area, an allocation-free BFS, pooled particle systems, the AI's search buffers reused; `PerfStats` | 16 AI rounds: allocation per round 10-29 MB -> 3-6 MB, update time per round 80 -> 51 ms (round 1) and 36 -> 21 ms (round 8), AI decisions 44 -> 27 ms (round 1); goldens identical |
| 3. Rendering | ETC1 tiles as RGB565 where the GPU lacks ETC (Android uploads them natively); area overlays as RGBA5551 (converted when loading, the package does not grow); the Kai/Hei glyph pages as Alpha8 (16 MB each instead of 64), tinted by the batcher after a one-time probe of how the GPU samples Alpha8; battle backgrounds loaded only when used; frame statistics | texture memory: menu 97 -> 63 MB, conquest start 288 -> 179 MB (desktop; 389 MB before any of this); Android about 142 MB expected; menu 16 and conquest 71 draw calls per frame; reference screenshots unchanged |
| 4. No purchases | the medal store, the buy button, purchase and price hooks and the purchase grant are gone | `NoPurchaseTests` (sources, manifest, assemblies) |
| 5. Hit testing | touches outside the logical screen dropped at the input entry, hit tests clipped to the screen, drawing clipped by a scissor | `hit_test` (phone), `hit_test_wide` (1600x600 window), `ipad_hit_test` (1000x1000 window), probes in five more scripts, `HitTestTests` |
| 6. AI | every number of the AI in `data/json/ai/profiles.json`: classic (the original, reproduces every golden), standard (default: defends threatened capitals, ports, airfields, cities and home areas, reinforces them, never strips a threatened capital, weighs whole enemy stacks, buys cards more readily), hard | `ai_defends_marseille_*` (standard holds Marseille after Italy's first assault, classic loses it), `ai_standard_autoplay`; 16 rounds of conquest 1: key areas lost 24/73 instead of 29/73, capitals held 8/8 instead of 7/8 |
| 7. Map | `WC2.Tools build-map`: one world base map for every era from Natural Earth (provinces, towns, cities, ports, airfields) and CShapes 2.0 (the borders of 1910, 1914, 1936, 1939, 1945, 1949; today from Natural Earth): provinces split along every era's borders, merge only within the same units in every era and are cut into cells of a target size per world zone, so a scenario of any era only assigns owners (`assets/data/maps/natural-earth`: 7 389 land provinces, 1 125 sea zones of irregular shape; the whole width, wrapping, north to the Russian polar islands); a playable mod (`mods/map-natural-earth`: map 2 drawn from vertex buffers, 8 514 areas, 171 harbours, conquest 10 WORLD 1939 with `fogOfWar` false and `leftRightWrap` true), built into the package; `MAP_PIPELINE.md` | `MapPackTests` (units by era, historical splits, polar coverage, the seam, capitals of every era, the mesh covering every region exactly, the pipeline on a synthetic world), `world_menu`, `world_autoplay` (seam, zoom, markers, four rounds, naval count), `BuiltInModTests` |
| 8. Verification | Windows and Android build with 0 warnings; `Tests/run-tests.ps1`: 56 unit tests, audio, data, assets and 23 scripts all pass; the APK rebuilt (131.1 -> 142.0 MB, the world map mod included) and six golden games replayed from it byte for byte with `--assets <apk>` (conquest, campaign, save and load, the standard AI, both world map scripts) | see the commit log |
| 9. Map and camera (second round) | zooming out stops when the map fills the screen (no black bars); conquests.json `fogOfWar` and `leftRightWrap` (a wrapping map draws every visible copy, picks areas across the seam, moves, arrows, ranges and distances take the short way); fog of war hides the armies the player's alliance cannot see; sea zones missing from a map's area table load as sea (land units board transports instead of walking onto the sea); `MODDING.md` explains the asset layers, editing the package directly and mods; every explanatory document in English and Chinese | classic goldens unchanged, `CameraTests`, `world_autoplay` (seam and zoom shots) |
| 10. Provinces, mesh, navy (third round) | the world map at province detail (7 389 land provinces; 1939 Austria about 15, Poland about 70); the map drawn from vertex buffers (`map2.mesh`: every border traced, simplified and smoothed once, triangulated; owner colours and national borders follow ownership; 2 draw calls for the map, textures 215 -> 104 MB); units, flags and buildings scaled to fit small provinces, zoom up to 3, flag markers of constant size when zoomed out, flags only where armies stand; harbours are sea zones as in the original (ships are built there, the AI plans its navy from them), coastal starting units carry transport ships; bounded hop distance cache | `MapPackTests` (mesh coverage), `world_autoplay` (zoom and marker shots, `world_autoplay.expect`: harbours, transports and ships at sea after four AI rounds) |

### Not done yet

* A device test of the APK: the checklist in `DIFFERENCES_THREE_WAY.md` section 5, plus reading the
  APK in place, the extraction fallback, ETC upload, the Alpha8 probe on a phone GPU, the mod folder.
* The generated map: translated menu texts (English everywhere for now), scenarios for the other eras (the base map has them; each needs a `scenarios` entry), balance by play, a device test of the mesh (900 000 triangles when fully zoomed out).
* An engine host and game session object owning the subsystems; typed events; nullable reference
  types; a versioned save format.
* Layout: the facade replaced the checks; the layout numbers themselves (positions per profile) are
  still in the code.
* Rendering: no reordering by atlas (71 draw calls on the conquest map, all texture changes in
  drawing order); asynchronous texture decoding on the loading screen.
* TrueType text (Japanese, Traditional Chinese and Korean texts miss glyphs in every bundled font),
  the battle animation, higher resolution art.

### Where to look

* Engine: `Engine/IO/AssetSystem.cs`, `ModManager.cs`, `JsonData.cs`, `JsonSchema.cs`;
  `Engine/Graphics/ecGraphics.cs` (formats, Alpha8, clipping), `Layout.cs`; `Engine/Core/PerfStats.cs`.
* Data: `assets/data/json/` and its `schemas/`; `Data/` (paths, terrain, rules, layout profiles,
  audio, countries, AI profiles, map packs); readers that need game types in `Game/Data/`.
* Build and tools: `Tools/` (`PackBuilder`, `PackValidator`, `RuntimeFormats`, `ImageCompare`,
  `MapPipeline`, `MapClassicExport`), `assets/build.json`, `Tools/map/`.
* Tests: `Tests/` (`README.md`, `run-tests.ps1`, scripts, `golden/`, `Unit/`, `mods/`).
* Docs: `ASSET_LAYOUT.md`, `DATA_SCHEMA.md`, `MODDING.md`, `MAP_PIPELINE.md`, `PORTING_GUIDE.md`.

---

## Part 1. The engine today

### 1.1 Layers

```
Host (MonoGame)              WC2/Host/WC2Game.cs, WC2/Program.cs, WC2.Android/MainActivity.cs
  | window, mouse/touch, keys, life cycle, layout choice
  v
Native entry points          WC2/Game/Supports/ecGame.cs
  | ecGameInit / ecGameUpdate / ecGameRender / ecTouch* / CallNativeExit / nativePause / nativeResume
  | (the C API that the Java activity used to call into libeasytech.so)
  v
Engine (port of libeasytech) WC2/Engine      25 files, 5 610 lines
  ^
Game (port of the remake)    WC2/Game        104 files, 29 157 lines
```

* Everything lives in one namespace, `WC2`. `GlobalUsings.cs` imports nine static classes
  (`Globals`, `CStr`, `GameGlobals`, `FontGlobals`, `StringTableGlobals`, `ToolBox`, ...) so the ported
  code can call C-style free functions and globals (`GetPath`, `Sprintf`, `g_Scene`, `g_Font2`) unqualified.
* The engine barely depends on the game. The only real references are the Kai/Hei font fallback in
  `ecUniFont` and the string-table globals that `ecStringTable` defines. Splitting engine and game into
  separate assemblies is therefore cheap.
* The Android project compiles the same source files by linking them (`Compile Include="..\WC2\Engine\**"`),
  plus its own activity.

### 1.2 One frame

* **Update** (`WC2Game.Update`): poll input and translate it to touch calls, then `ecGameUpdate`:
  commander auto-save check, `CStateManager.Update` (current state, at most one state switch),
  `GUIManager.Update` (widget tree, children first), `GUIMotionManager.ProcMotion` (tweens),
  `CCSoundBox.UpdateSound`. Skipped entirely while the game is paused or waiting. Frame step is capped
  at 50 ms.
* **Render** (`ecGameRender`): `RenderBegin`, the current state's `Render` (the map scene, in world
  coordinates through `SetViewPoint`), reset the view, `GUIManager.Render` (widget tree, fade, overlay),
  darkening while paused, `RenderEnd` (flush).

### 1.3 Coordinates and resolution

* A **logical screen** is chosen once at start from the aspect ratio: 480, 534, 568 or 640 x 320 for
  phones, 1024 x 768 for 4:3 tablets. All GUI code works in these units.
* The window shows the logical screen with one uniform scale and black bars; touches are converted
  back. A 20:9 phone therefore gets 640 x 320 with bars at the sides.
* Almost all art is 2x ("@2x" files, "_hd" atlases). A texture's `xC` of 2 halves its size into logical
  units. The `hd` argument of `LoadRes`/`Init` decides per use whether coordinates are halved; it is a
  display decision, not a file property. The tablet layout loads some of the same 2x atlases with
  `hd = false`, so they appear twice as large there.
* The map uses world units. The camera is `SetViewPoint(x, y, scale)`.

### 1.4 Rendering

* `ecGraphics` keeps one CPU vertex array (12 000 vertices) and draws it with MonoGame's `BasicEffect`.
  It flushes when the texture or blend mode changes. Blend modes are alpha, additive and multiply.
  Helpers draw rectangles, lines, circles, rings and full-screen fades.
* **Texture loading.** A name goes through `ResolveTextureFile`: try `<name>@2x`, then the plain name,
  each with the extension fallback png, webp, pkm, pvr, jpg. The file is decoded on the CPU (SkiaSharp
  for PNG/WebP, our own ETC1 decoder for PKM) and uploaded as uncompressed RGBA. Textures are cached by
  path with a reference count and released by explicit `FreeTexture` calls.
* **Atlases.** `ecTextureRes` reads the atlas XML format
  (`<Texture name="ui1_hd.webp"/>` plus `<Image name x y w h refx refy/>`). Image names are global
  within one table and the first definition wins. All widgets share one table,
  `GUIElement.s_TextureRes`. `CGameRes` owns the game tables (army, flags, cards, effects, battle
  backgrounds).
* **Sprites.** `ecImage` is a texture sub-rectangle with a hot spot, per-vertex colours, flips and a
  blend mode.

### 1.5 Text and strings

* `ecUniFont` reads the game's binary `.fnt` (glyph count, height, 12-byte glyph records) plus a PNG
  page of the same name. Fonts are global objects (`g_Font1`..`g_Font7`, `g_Num*`, Kai and Hei), chosen
  per language and layout in `InitGlobalFonts`.
* `ecText` does layout, alignment, truncation and UTF-8 decoding.
* `ecStringTable` reads Apple plist XML. `Localizable.strings` inside `<lang>.lproj` names the language
  code and the string-table file.
* `ecLabelText`, which the original drew with the Android system font, has no backend and falls back
  to a bitmap font.

### 1.6 GUI

* A retained tree of `GUIElement`s with three flags (draws itself, subtree shown, enabled). Events go
  to children first, then to the element. This routing was verified against the binary.
* Widgets: button, button with text, radio button, image, text, scroll bar, scroll list, paged list,
  level selector. `GUIManager` is the root and owns fades and the overlay. `GUIMotionManager` runs up to
  16 tweens and reports arrivals as events.
* Screens talk to each other with integer codes: `Event.GUI` carries a type (button, scroll bar,
  warning, fade in/out, other) and an `int`. About 70 numeric `OTHER`/`case` codes appear in the GUI code.
* 52 GUI files: 19 menu screens, 27 in-game panels, loading, states, stubs. Every screen builds its
  layout in code from literal rectangles (177 `new GUIRect(...)` calls) and picks layout variants with
  `if` chains.

### 1.7 States, audio, effects, files, game state

* `CStateManager` holds six fixed states (logo, menu, match, load, game, edit) and switches at most once
  per update.
* `CCSoundBox` plays WAV effects through MonoGame `SoundEffect` and decodes each MP3 fully into memory
  (MP3Sharp, NLayer as fallback).
* `ecEffect` is the particle system driven by `effect_*.xml`. `CFightText` draws the floating damage
  numbers.
* `GetPath` looks in the language folder, then the asset root. `GetDocumentPath` points at the save
  folder. `ecFile` reads and writes little-endian binary. `TiXml` is a TinyXML-shaped facade over
  `System.Xml.Linq`. Saves are packed binary records; `settings.cfg` is 40 bytes.
* Game state is a set of global objects (`g_Scene`, `g_GameManager`, `g_Commander`, `g_GameRes`,
  `g_GameSettings`, `g_SoundRes`, `g_FightTextMgr`, `g_PlayerManager`, `g_NationInfo`) plus engine
  singletons (`ecGraphics`, `GUIManager`, `GUIMotionManager`, `CStateManager`, `CCSoundBox`,
  `ecEffectManager`, `ecEffectResManager`, `CObjectDef`).

### 1.8 Platforms

* Desktop copies the asset folder next to the executable and maps the mouse to touches.
* Android ships the assets inside the APK and copies all 136 MB to the app's files folder on first start,
  because every loader uses plain file paths.

### 1.9 What this design costs us

| Property | Consequence |
|---|---|
| File names are the API: about 770 asset-name literals in 61 game files, plus 162 `Sprintf` calls, many building names | Moving or renaming any file means editing code |
| Variant choice is spread out: 276 layout checks (`widthMode == 3`, `screenW <= 568`, `_iPad`, `-568h`) in 46 files | A new layout, for example a real 20:9 width, means touching all of them and producing new files |
| Every texture is uploaded as uncompressed RGBA | About 390 MB of textures in a conquest, see section 2.5 |
| Plain file IO everywhere (about 30 direct `File.*` sites outside `ecFile`) | Android must extract 136 MB first; there is no place to add a mod folder or a packed archive |
| Global objects and singletons with implicit init order | Hard to test rules in isolation, to replay a game, or to restart cleanly |
| Manual resource lifetime (`FreeTexture`/`Release*` in 29 game files) | Leaks and double releases are easy to introduce when screens change |
| Integer event codes, `Nullable` disabled, 13 warnings suppressed project-wide | Refactoring is risky without compiler help |
| Random streams seeded from the clock | A game cannot be reproduced for a test or a bug report |

None of this is a bug today; it is the shape of the original that we copied on purpose.

---

## Part 2. The assets today

### 2.1 Inventory

1 111 files, 136.8 MB on disk. "RGBA MB" is the size once decoded to uncompressed RGBA, which is what
the GPU holds today.

| Category | Files | Disk MB | RGBA MB | Formats |
|---|---|---|---|---|
| Map background tiles (`map1_1..112@2x`, 1024 px, 500 logical units each) | 112 | 58.7 | 470 | pkm 112 |
| Map zone overlay atlases (`m1_zoneN_1/2`, area images) | 118 | 9.0 | 238 | webp 59, xml 59 |
| Minimaps | 8 | 4.2 | 34 | pkm 8 |
| Map data (areas, adjacency, area mark grid, tax) | 4 | 1.2 | 0 | bin, raw, xml |
| Battle backgrounds (2048 px; no consumer since the battle animation is not ported) | 18 | 14.2 | 113 | pkm 9, xml 9 |
| Unit animation sets (`infantry_de_hd.xml/.png/.bin`; no consumer) | 150 | 5.4 | 23 | bin, png, xml |
| Kai/Hei CJK fonts (4096 px pages) | 8 | 15.2 | 168 | fnt, png |
| Other bitmap fonts | 204 | 4.3 | 51 | fnt, png |
| Localised atlases (button texts, menu, battle names) | 58 | 3.3 | 35 | xml, png, webp |
| Per-layout screen backgrounds (`-568h`, `-640h`, `_iPad`) | 40 | 4.3 | 85 | webp, png |
| Shared UI atlases and loose images | 84 | 9.0 | 125 | png, webp, xml |
| General portraits | 17 | 1.6 | 18 | webp |
| Effects | 18 | 0.2 | 1 | xml, png |
| Scenarios (battles, conquests, tutorial scripts) | 98 | 1.4 | 2 | xml, bin |
| Definitions (armydef, carddef, commanderdef, lists, options layout) | 16 | 0.1 | 0 | xml |
| String tables | 12 | 0.8 | 0 | xml |
| Music | 5 | 2.9 | - | mp3 |
| Sound effects | 27 | 0.7 | - | wav |
| Language folders (`*.lproj`) | 114 | 0.5 | 7 | fnt, png, strings, xml |
| **Total** | **1 111** | **136.8** | **1 370** | |

997 files sit flat in one folder; the rest are in six `*.lproj` language folders.

### 2.2 The naming scheme is the variant system

There is no manifest. A file's name carries its variant, and code picks the variant by building the name.

| Tag | Meaning | Who chooses it |
|---|---|---|
| `@2x` | 2x pixel art | the resolver, automatically |
| `_hd` | atlas or font for 2x art | code, explicitly |
| `_iPad` | tablet art | code (`widthMode == 3`) |
| `-568h`, `-640h` | wide phone backgrounds | code (`screenW <= 480/568/640`) |
| `-512h`, `-534h`, `-568h`, `-640h` | tutorial script per phone width | code |
| `_568hd`, `_640hd`, `_hd` | battle backgrounds per width | code |
| `_cn _en _ja _kr _ru _tw` | language | code, from the `language` string |
| `<lang>.lproj/` | language folder | the resolver, automatically |
| `_de _am _gb _ne ...` | country | code (army art, unit animation sets) |
| numbers | index, map id, zone id | code, through `Sprintf` |

Atlas XML files name their texture, fonts imply their PNG page, `generalphotos.xml` names portrait
files, and `Localizable.strings` names the string table.

### 2.3 Formats

* PKM is ETC1, 4 bits per pixel, opaque only. It is used for the big opaque images (map tiles, minimaps,
  battle backgrounds). We decode it on the CPU to RGBA, which multiplies its GPU size by 8.
* WebP and PNG carry everything with alpha: UI, zone overlays, fonts, portraits.
* Fonts are 2x bitmap fonts per language and size. The two Chinese fonts are 4096 x 4096 pages.
* Music is MPEG 2.5 Layer III at 8 kHz (low quality, needs MP3Sharp). Effects are 11 kHz PCM WAV.
* Data is XML (definitions, scenarios, atlases, string tables) plus small binary files (map geometry,
  adjacency, area mark grid, scenario area masks, unit animation).

### 2.4 What a running game actually loads

Method: a throwaway copy of the game, built outside the repository, logged every asset file the path
resolver returned. It ran the 12 regression scripts (Chinese, 568 x 320, plus the tablet tutorial), a
tablet menu tour, and menu tours at 480 and 640 width in English.

* **254 of 1 111 files were loaded, 50.8 MB of 136.8 MB.**
* Most of the rest is reachable but was not exercised. The scripts play European scenarios, so only 24
  of 112 map tiles and 24 of 118 zone files were needed; other scenarios, languages and layouts load
  their own files.
* **351 files (11.8 MB) cannot be loaded by any current code path:**

| Group | Files | MB |
|---|---|---|
| Unit animation sets and their motion/position tables (battle animation not ported) | 152 | 5.4 |
| Non-HD font variants (the content scale is always 2 now) | 76 | 5.4 |
| "New game" panel fonts in the language folders (the panel is a stub) | 96 | 0.5 |
| Editor, in-app purchase, service and legacy files | 27 | 0.4 |

* **Loaded but never drawn:** every game loads a pair of 2048 px battle backgrounds, because
  `CGameRes.Load` still prepares them for the battle animation that neither port plays.

### 2.5 Memory today

The same throwaway build logged the decoded size of all textures held in the cache:

| Moment | Live textures | RGBA MB |
|---|---|---|
| Main menu, Chinese | 21 | 164 |
| Main menu, English | 21 | 161 |
| Campaign battle (9 map tiles) | 48 | 310 |
| Conquest (24 map tiles) | 66 | 389 |
| Tablet tutorial | 62 | 318 |

Where it goes:

* **Kai/Hei font pages: 134 MB in every language.** Each is 4096 x 4096 RGBA. Languages without their
  own Kai/Hei fonts fall back to the Chinese pages, so English pays the same. The glyphs are white with
  the shape in alpha, so alpha-only storage would hold the same pixels in 34 MB.
* **Map tiles: 4.2 MB each decoded, 0.5 MB each as native ETC1.** A conquest needs 24 (101 MB, or 13 MB
  as ETC1). The eight minimaps (34 MB) are the same format.
* **Battle backgrounds: 34 MB** at 568 width, never drawn.
* **Army atlas: 33 MB** (4096 x 2024).

Native ETC1 for the map tiles, alpha-only font pages and not loading the unused battle backgrounds
would take the conquest case from about 390 MB to about 170 MB with no visible change, before touching
the UI atlases.

---

## Part 3. Proposal for `newEngine`

### 3.1 Principles

1. `original` stays frozen. It is the reference for behaviour, screenshots and audits.
2. Every step on `newEngine` keeps the regression suite green and adds tests where it changes behaviour.
3. Rules code (combat, AI, countries, turns) keeps its names, so the audits, the decompiled pseudo-C and
   the remake stay comparable. Modernisation targets the engine, the asset access and the screen code.
4. Old asset names keep working while the tree is reorganised, so the restructure can land before the
   call sites are cleaned up.
5. Each workstream delivers something measurable on its own: memory, start-up time, fewer files touched
   per feature, testability.

### 3.2 Workstreams

**A. Safety net (first, small).**
Seedable random streams with a `--seed` option, so a game can be replayed. Golden replay tests for the
rules: run a scripted battle with a fixed seed and compare the end state. A screenshot comparison mode
for the smoke scripts against `original`. An asset validator that resolves every name the code and the
data files ask for and reports missing or unreferenced files.

**B. Asset access layer (the key enabler).**
One `AssetSystem` that every loader goes through: `Open`, `Exists`, `ReadAllBytes`, `Enumerate`.
Behind it sit pluggable sources: a folder (desktop, development), the APK through the Android asset
manager or `TitleContainer` (no first-start extraction), and an optional overlay folder whose files
win (mods, quick art tests). A generated `manifest.json` lists every file with its logical name,
qualifiers, size and hash, so existence checks are a dictionary lookup instead of disk probes. The
manifest also maps every old flat name to its new path, so the game code keeps working unchanged while
the tree moves. About 30 direct `File.*` call sites move behind this API; `GetPath` becomes a thin
compatibility wrapper.

**C. Asset tree restructure.**
A migration script moves files into folders by domain and replaces name suffixes with folder
qualifiers, then writes the manifest and alias table. Proposed layout:

```
assets/
  manifest.json            generated: logical name, qualifiers, legacy alias, size, hash
  data/defs/               armydef, carddef, commanderdef, battlelist, conquestlist, generalphotos, guioptions
  data/scenarios/          battle_*, conquest_* (xml + area mask), tutorial scripts
  maps/1/                  area.bin, adjion.bin, areamark.raw, areatax.xml
  maps/1/tiles/            background tiles
  maps/1/zones/            zone overlay atlases
  maps/1/minimap/
  gfx/ui/                  ui1, ui2, mui, title, selbattle, selcountry, cardtex, flags, general ...
  gfx/ui/phone-568/        per-layout backgrounds (mainbg, bankbg, buycardbg, board_*, victorybg_*)
  gfx/ui/phone-640/
  gfx/ui/tablet/
  gfx/units/               army atlas, unit bases and marks
  gfx/effects/             eff + effect_*.xml
  fonts/                   shared fonts (font1, num*, fps)
  audio/music/  audio/sfx/
  lang/<code>/             string tables, Localizable.strings, text/menu/name atlases, language fonts
  dormant/                 battle backgrounds and unit animation sets (kept, not packaged until used)
  legacy/                  non-HD leftovers and files nothing reads (kept in git, not packaged)
```

The resolver then picks variants by qualifiers (language, layout, density) the way Android picks
resources, instead of 276 hand-written checks. The `hd` argument stays a per-use display decision
(see 1.3), because the tablet layout relies on it.

**D. Texture memory.**
Three changes with no visible effect come first: upload PKM files as native ETC1 where the GPU supports
it instead of decoding them (MonoGame 3.8.6.1 exposes `SurfaceFormat.RgbEtc1`, `Rgb8Etc2`,
`Rgba8Etc2`, `Dxt1` and `Astc`), store the font pages alpha-only, and stop loading the battle
backgrounds until something draws them. Section 2.5 puts that at about 390 MB down to about 170 MB in
a conquest. Next, an offline step that produces compressed textures per platform (ETC2 or ASTC with
alpha on Android, BC on desktop) from the untouched originals. Decoding moves to a worker thread during
the loading screen, with only the GPU upload on the main thread. Not loading the Chinese font pages for
other languages would save more but changes how English text looks, so it belongs to decision 1.

**E. Code structure.**
* Split into projects: `WC2.Engine` (library), `WC2.Game` (library), `WC2.Desktop` (host),
  `WC2.Android` (host), `WC2.Tools` (migration, validation, decoders, audio test), `WC2.Tests`.
  The Android project then references libraries instead of linking source files.
* Namespaces per layer, with the global static imports kept for the game library during the transition.
* An `EngineHost` object that owns the subsystems and their init and shutdown order. `Instance()`
  accessors stay as forwarders, so call sites change gradually. A `GameSession` object that owns
  `g_Scene`, `g_GameManager` and friends, so a test or a "new game" gets a clean state.
* Resource scopes per state and per screen. Textures and atlases acquired through a scope are released
  when it ends, replacing manual `FreeTexture` calls.
* Typed events: enums per screen instead of numeric `OTHER` codes. The routing rules stay as verified.
* Enable nullable reference types and lift the warning suppressions folder by folder.

**F. Layout.**
A `LayoutProfile` (phone 480, 534, 568, 640, tablet, and later a wide profile) with helpers such as
`Pick(phone480, phone568, phone640, tablet)` replaces the scattered checks. After that, screens can move
one by one to data-driven layouts, continuing the remake's `guioptions.xml` approach. This is what makes
new aspect ratios and UI mods practical.

**G. Saves and settings.**
A versioned save container with an importer for the current binary saves and `settings.cfg`, so the
format can grow without the 40-byte and packed-record limits.

**H. Content, later and optional.**
TrueType text for labels (restores the original's system-font labels and allows any language), the
battle animation (needs a port from the pseudo-C; its assets are already in `dormant/`), and higher
resolution art.

### 3.3 Suggested order

| Step | Workstreams | Size | Visible result |
|---|---|---|---|
| 1 | A | small | seeds, replays, asset validator, screenshot comparison |
| 2 | B | medium | no first-start extraction on Android, single IO path, overlay folder |
| 3 | C | small with tooling | new folder tree, manifest, old names still work, dead files out of the APK |
| 4 | D (native ETC1, async decode) | small to medium | large memory drop on Android, faster loads |
| 5 | E (projects, host, scopes) | medium | engine and game as libraries, tests can run without a window |
| 6 | F | medium to large | variant logic in one place, new layouts possible |
| 7 | E (typed events, nullable), G | ongoing | safer refactoring, extensible saves |
| 8 | H | large, optional | new content |

Steps 1 to 4 change no gameplay and no visuals; the regression suite and screenshot comparison against
`original` cover them. From step 6 on, visuals can change if you want them to.

### 3.4 Decisions needed from you

1. **Fidelity.** Must `newEngine` stay pixel-identical to `original` (screenshot comparison as a hard
   gate), or are visual improvements allowed after step 4?
2. **Naming.** Keep the original/remake names in the rules code (recommended for traceability), or
   rename the whole code base to .NET conventions?
3. **Asset sources.** Keep the original files byte-identical as the source of truth and generate runtime
   formats in a build step (recommended), or convert files in place?
4. **Dead and dormant files.** Move non-HD leftovers and unread files to `legacy/`, and the battle
   animation assets to `dormant/`, out of the APK? Both stay in git.
5. **Modding.** Is an overlay folder for mods a goal? It decides whether the manifest needs a load order
   and conflict rules.
6. **Platforms.** Android and Windows only, or also Linux, macOS, iOS? This affects the texture formats
   to generate.
7. **Data formats.** Keep XML for definitions and scenarios, compatible with the remake's data, or move
   to JSON?

---

<a id="chinese"></a>

# 引擎与资源结构：现状，以及 `newEngine` 的计划

分支 `original` 保存忠实还原的版本（提交 `804ffdb`），并保持冻结，作为行为参考。分支 `newEngine` 承载下文所述的工作。本文档分为四个部分：

0. `newEngine` 上的进展（做了什么、如何检验、下一步做什么）。
1. 开始时引擎是如何工作的（写于工作开始之前；当时的路径为 `WC2/Engine`、`WC2/Game`、`WC2/assets`）。
2. 资源是如何组织的，以及运行中的游戏实际加载了什么。
3. 提案与决定。

---

## 第 0 部分：`newEngine` 上的进展（截至 2026-11-06 的状态）

已作出的决定：画面可以改变；规则代码保留原有命名；原版文件保持不动（只做了移动和重命名，从未重新编码），运行时格式在构建步骤中生成；不可达的文件和休眠文件仍保留在 APK 中；支持模组（mod）是目标之一；只支持 Android 和 Windows；游戏数据采用 JSON；定义属于数据，特殊行为仍留在代码中，按类型、特性（trait）或 id 选择。所有工作都已在 `newEngine` 上逐步提交；每一步在提交前都运行了整套测试。

### 已完成

| 步骤 | 内容 | 检验方式 |
|---|---|---|
| 0. 安全网 | `--seed`、`--fixed-step`、`--docs`；测试脚本命令 `dump`、`autoplay`、`settle`、`dumpafter`、`hittest`、`texstats`、`startconquest`；规则状态的基准（golden）转储；参考截图（`WC2.Tools compare-image`，62 张小尺寸 JPEG）；资源检查器（`validate`）；单元测试（`Tests/WC2.Tests`，38 个）；隐藏控件的点击测试（hit test） | 基准逐字节回放一致；点击测试在修复前发现 14 个可触及的隐藏按钮，修复后为 0 个 |
| 1. 资源树 | `assets/` 按用途划分（`audio/music\|sfx\|ui`、`image/ui\|army/<kind>\|map/tiles\|regions\|flags\|markers\|effects\|fonts\|battlebg`、`data/json`、`data/xml`、`data/binary`、`scripts`），变体以文件夹形式存放（`common`、`hd`、`tablet`、`phone-568-hd`、...、`<lang>`）；`legacy-names.json` -> `generated/asset_index.json`；单一资源层（模组 -> 资源 -> 原版名称）；Android 直接在 APK 内读取，解压只作为后备方案；`ASSET_LAYOUT.md`、`MODDING.md` | 分别从游戏资源包、资源树本身和 APK 回放基准；`validate`：每个原版文件都可达，数据中提到的每个名称都存在 |
| 2. 数据 | 单位、卡牌、将领、肖像、战役、征服、地形、战争勋章、规则、国家、音频、布局配置、选项布局、地图地区表、文本、AI 配置均改用 JSON；剧本和教程脚本仍保留原版 XML，并附带一个生成的索引；每种文件各有一个 JSON Schema，每次加载文件时都会校验（无效模组的更改会被丢弃）；`DATA_SCHEMA.md` | `--verify-data`：19 种数据类型与原版 XML 或内置表完全一致，每个文件都有效 |
| 3. 结构 | 项目 `Engine`、`Data`、`Game`、`Hosts/Desktop`、`Hosts/Android`、`Tools`、`Tests`（引擎不引用游戏的任何部分，数据只引用引擎，游戏不引用任何宿主程序）；由布局配置驱动的单一 `Layout` 外观（facade）（取代了 135 处分散的判断）；界面关闭时释放资源（回归测试 `resource_cycle`） | 构建通过（0 个警告），测试套件通过 |
| 3. 回合处理 | 按地区缓存跳数距离、无内存分配的 BFS、池化的粒子系统、复用 AI 的搜索缓冲区；`PerfStats` | 16 个 AI 回合：每回合内存分配 10-29 MB -> 3-6 MB，每回合更新时间 80 -> 51 ms（第 1 回合）和 36 -> 21 ms（第 8 回合），AI 决策 44 -> 27 ms（第 1 回合）；基准完全一致 |
| 3. 渲染 | 在 GPU 不支持 ETC 时，ETC1 地图贴图块以 RGB565 存储（Android 则原生上传）；地区叠加层采用 RGBA5551（加载时转换，游戏资源包不会变大）；楷体/黑体（Kai/Hei）字形页采用 Alpha8（每页 16 MB 而非 64 MB），在一次性探测 GPU 如何采样 Alpha8 之后由批处理器着色；战斗背景只在使用时才加载；帧统计 | 纹理内存：菜单 97 -> 63 MB，征服开局 288 -> 179 MB（桌面端；做这些优化之前为 389 MB）；Android 预计约 142 MB；每帧绘制调用：菜单 16 次，征服 71 次；参考截图不变 |
| 4. 移除内购 | 勋章商店、购买按钮、购买与价格钩子以及购买奖励发放均已移除 | `NoPurchaseTests`（源代码、清单、程序集） |
| 5. 点击测试 | 逻辑屏幕之外的触摸在输入入口处即被丢弃，点击测试被限制在屏幕范围内，绘制通过裁剪矩形（scissor）裁剪 | `hit_test`（手机）、`hit_test_wide`（1600x600 窗口）、`ipad_hit_test`（1000x1000 窗口），另外五个测试脚本中的探测，`HitTestTests` |
| 6. AI | AI 的所有数值都在 `data/json/ai/profiles.json` 中：classic（原版，能复现每一个基准）、standard（默认：防守受威胁的首都、港口、机场、城市和本土地区，并向其增援，从不抽空受威胁的首都，评估整个敌方部队堆叠，更乐于购买卡牌）、hard | `ai_defends_marseille_*`（standard 在意大利第一次进攻后守住马赛，classic 则失守），`ai_standard_autoplay`；征服 1 进行 16 个回合：失去的关键地区为 24/73 而非 29/73，守住的首都为 8/8 而非 7/8 |
| 7. 地图 | `WC2.Tools build-map`：根据 Natural Earth（省份、城镇、城市、港口、机场）和 CShapes 2.0（1910、1914、1936、1939、1945、1949 年的边界；今天的边界取自 Natural Earth）生成一张适用于所有时代的世界底图：省份沿每个时代的边界拆分，只与各时代单元都相同的部分合并，并按各世界区域的目标大小切分，因此任何时代的剧本只需分配归属（`assets/data/maps/natural-earth`：7 389 个陆地省份，1 125 个形状不规则的海区；覆盖整个宽度、左右循环、北至俄罗斯北极群岛）；一个可游玩的模组（`mods/map-natural-earth`：以顶点缓冲绘制的地图 2，8 514 个地区，171 个港口，征服 10 WORLD 1939，`fogOfWar` 为 false、`leftRightWrap` 为 true），内置于游戏资源包中；`MAP_PIPELINE.md` | `MapPackTests`（各时代的单元、历史边界拆分、北极覆盖、接缝、各时代的首都、网格精确覆盖每个区域、在合成世界上运行的生成流程），`world_menu`，`world_autoplay`（接缝、缩放、标记、四个回合、海军统计），`BuiltInModTests` |
| 8. 验证 | Windows 与 Android 构建均为 0 警告；`Tests/run-tests.ps1`：56 个单元测试、音频、数据、资源以及 23 个测试脚本全部通过；APK 已重新构建（131.1 -> 142.0 MB，包含世界地图模组），并用 `--assets <apk>` 从中逐字节一致地回放了六局基准游戏（征服、战役、存档与读档、standard AI、两个世界地图脚本） | 见提交记录 |
| 9. 地图与镜头（第二轮） | 缩小到地图填满屏幕时停止（没有黑边）；conquests.json 的 `fogOfWar` 和 `leftRightWrap`（循环地图绘制每个可见的副本，跨越接缝选取地区，移动、箭头、范围和距离都走近路）；战争迷雾隐藏玩家所在同盟看不到的部队；地图地区表中缺少的海区按海洋加载（陆军登上运输船，而不是走上海面）；`MODDING.md` 说明资源层、直接修改游戏资源包以及模组；所有说明文档均为中英双语 | 经典基准不变，`CameraTests`，`world_autoplay`（接缝与缩放截图） |
| 10. 省份、网格、海军（第三轮） | 省份级精度的世界地图（7 389 个陆地省份；1939 年奥地利约 15 个、波兰约 70 个）；地图由顶点缓冲绘制（`map2.mesh`：每条边界只追踪、简化和平滑一次，再三角化；归属颜色和国界随归属变化；地图只需 2 次绘制调用，纹理 215 -> 104 MB）；部队、旗帜和建筑缩小以适应小省份，最大放大 3 倍，缩小时显示固定大小的旗帜标记，只在有军队的地方插旗；港口与原版一样是海区（在那里造船，AI 据此规划海军），沿海的初始部队带有运输船；跳数距离缓存有上限 | `MapPackTests`（网格覆盖），`world_autoplay`（缩放与标记截图，`world_autoplay.expect`：四轮 AI 之后的港口、海上运输船和舰船） |

### 尚未完成

* APK 的真机测试：`DIFFERENCES_THREE_WAY.md` 第 5 节中的检查清单，以及直接在 APK 内读取、解压后备方案、ETC 上传、手机 GPU 上的 Alpha8 探测、模组文件夹。
* 生成的地图：菜单文本的翻译（目前所有语言都显示英文）、其他时代的剧本（底图中已包含这些时代，每个只需一个 `scenarios` 条目）、通过实际游玩来调整平衡、在真机上测试网格（最大缩小时约 90 万个三角形）。
* 拥有各子系统的引擎宿主对象和游戏会话对象；类型化事件；可空引用类型；带版本号的存档格式。
* 布局：外观（facade）已取代了那些判断；但布局数值本身（每个布局配置中的位置）仍写在代码里。
* 渲染：尚未按图集重新排序（征服地图上有 71 次绘制调用，所有纹理切换都按绘制顺序发生）；在加载界面上异步解码纹理。
* TrueType 文本（日文、繁体中文和韩文文本在所有自带字体中都缺少字形）、战斗动画、更高分辨率的美术资源。

### 查阅位置

* 引擎：`Engine/IO/AssetSystem.cs`、`ModManager.cs`、`JsonData.cs`、`JsonSchema.cs`；`Engine/Graphics/ecGraphics.cs`（格式、Alpha8、裁剪）、`Layout.cs`；`Engine/Core/PerfStats.cs`。
* 数据：`assets/data/json/` 及其 `schemas/`；`Data/`（路径、地形、规则、布局配置、音频、国家、AI 配置、地图资源包）；需要用到游戏类型的读取器位于 `Game/Data/`。
* 构建与工具：`Tools/`（`PackBuilder`、`PackValidator`、`RuntimeFormats`、`ImageCompare`、`MapPipeline`、`MapClassicExport`）、`assets/build.json`、`Tools/map/`。
* 测试：`Tests/`（`README.md`、`run-tests.ps1`、测试脚本、`golden/`、`Unit/`、`mods/`）。
* 文档：`ASSET_LAYOUT.md`、`DATA_SCHEMA.md`、`MODDING.md`、`MAP_PIPELINE.md`、`PORTING_GUIDE.md`。

---

## 第 1 部分：引擎现状

### 1.1 层次结构

```
Host (MonoGame)              WC2/Host/WC2Game.cs, WC2/Program.cs, WC2.Android/MainActivity.cs
  | window, mouse/touch, keys, life cycle, layout choice
  v
Native entry points          WC2/Game/Supports/ecGame.cs
  | ecGameInit / ecGameUpdate / ecGameRender / ecTouch* / CallNativeExit / nativePause / nativeResume
  | (the C API that the Java activity used to call into libeasytech.so)
  v
Engine (port of libeasytech) WC2/Engine      25 files, 5 610 lines
  ^
Game (port of the remake)    WC2/Game        104 files, 29 157 lines
```

* 所有代码都位于同一个命名空间 `WC2` 中。`GlobalUsings.cs` 导入了九个静态类（`Globals`、`CStr`、`GameGlobals`、`FontGlobals`、`StringTableGlobals`、`ToolBox` 等），使移植过来的代码可以不加限定地调用 C 风格的自由函数和全局变量（`GetPath`、`Sprintf`、`g_Scene`、`g_Font2`）。
* 引擎几乎不依赖游戏。真正的引用只有 `ecUniFont` 中的楷体/黑体字体回退，以及 `ecStringTable` 所定义的字符串表全局变量。因此，把引擎和游戏拆分为独立的程序集代价很小。
* Android 项目通过链接的方式（`Compile Include="..\WC2\Engine\**"`）编译同一批源文件，外加它自己的 activity。

### 1.2 一帧的流程

* **更新**（`WC2Game.Update`）：轮询输入并将其转换为触摸调用，然后执行 `ecGameUpdate`：将领自动存档检查、`CStateManager.Update`（当前状态，最多一次状态切换）、`GUIManager.Update`（控件树，子元素优先）、`GUIMotionManager.ProcMotion`（补间动画）、`CCSoundBox.UpdateSound`。游戏暂停或处于等待状态时，整个更新都会跳过。帧步长上限为 50 ms。
* **渲染**（`ecGameRender`）：`RenderBegin`，当前状态的 `Render`（地图场景，通过 `SetViewPoint` 使用世界坐标），重置视图，`GUIManager.Render`（控件树、淡入淡出、覆盖层），暂停时压暗画面，`RenderEnd`（刷新提交）。

### 1.3 坐标与分辨率

* 启动时根据宽高比一次性选定一个**逻辑屏幕**：手机为 480、534、568 或 640 x 320，4:3 平板为 1024 x 768。所有 GUI 代码都以这些单位工作。
* 窗口以统一的缩放比例显示逻辑屏幕，并加上黑边；触摸坐标会被反向换算回来。因此 20:9 的手机得到的是 640 x 320，两侧带黑边。
* 几乎所有美术资源都是 2x 的（"@2x" 文件、"_hd" 图集）。纹理的 `xC` 为 2 时，其尺寸换算为逻辑单位时会减半。`LoadRes`/`Init` 的 `hd` 参数在每次使用时决定坐标是否减半；这是一个显示层面的决定，而不是文件的属性。平板布局以 `hd = false` 加载部分相同的 2x 图集，因此它们在平板上显示为两倍大小。
* 地图使用世界单位。摄像机为 `SetViewPoint(x, y, scale)`。

### 1.4 渲染

* `ecGraphics` 维护一个 CPU 端的顶点数组（12 000 个顶点），并用 MonoGame 的 `BasicEffect` 绘制它。纹理或混合模式改变时会刷新提交。混合模式有 alpha、加法和乘法三种。辅助函数可以绘制矩形、线段、圆、圆环以及全屏淡入淡出。
* **纹理加载。** 名称会经过 `ResolveTextureFile` 解析：先尝试 `<name>@2x`，再尝试原名，每次都按 png、webp、pkm、pvr、jpg 的顺序回退扩展名。文件在 CPU 上解码（PNG/WebP 使用 SkiaSharp，PKM 使用我们自己的 ETC1 解码器），并以未压缩的 RGBA 上传。纹理按路径缓存，带有引用计数，并通过显式调用 `FreeTexture` 释放。
* **图集。** `ecTextureRes` 读取图集 XML 格式（`<Texture name="ui1_hd.webp"/>` 加上 `<Image name x y w h refx refy/>`）。图像名称在一张表内是全局的，以第一个定义为准。所有控件共享同一张表 `GUIElement.s_TextureRes`。`CGameRes` 拥有游戏所用的各张表（部队、旗帜、卡牌、特效、战斗背景）。
* **精灵。** `ecImage` 是纹理中的一个子矩形，带有热点、逐顶点颜色、翻转和混合模式。

### 1.5 文本与字符串

* `ecUniFont` 读取游戏的二进制 `.fnt` 文件（字形数量、高度、12 字节的字形记录）以及同名的 PNG 字形页。字体是全局对象（`g_Font1`..`g_Font7`、`g_Num*`、楷体和黑体），在 `InitGlobalFonts` 中按语言和布局选择。
* `ecText` 负责排版、对齐、截断和 UTF-8 解码。
* `ecStringTable` 读取 Apple plist XML。`<lang>.lproj` 中的 `Localizable.strings` 指明语言代码和字符串表文件。
* `ecLabelText` 在原版中使用 Android 系统字体绘制，在这里没有后端实现，回退为位图字体。

### 1.6 GUI

* 一棵保留模式的 `GUIElement` 树，每个元素有三个标志（绘制自身、显示子树、启用）。事件先发给子元素，再发给元素自身。这种路由方式已对照二进制文件验证过。
* 控件：按钮、带文字的按钮、单选按钮、图像、文本、滚动条、滚动列表、分页列表、关卡选择器。`GUIManager` 是根节点，负责淡入淡出和覆盖层。`GUIMotionManager` 最多同时运行 16 个补间动画，并以事件形式报告动画到达终点。
* 界面之间用整数代码通信：`Event.GUI` 携带一个类型（按钮、滚动条、警告、淡入/淡出、其他）和一个 `int`。GUI 代码中出现了约 70 个数字形式的 `OTHER`/`case` 代码。
* 52 个 GUI 文件：19 个菜单界面、27 个游戏内面板，以及加载、状态和桩（stub）文件。每个界面都在代码中用字面量矩形构建布局（177 处 `new GUIRect(...)` 调用），并通过 `if` 链选择布局变体。

### 1.7 状态、音频、特效、文件、游戏状态

* `CStateManager` 持有六个固定状态（标志画面（logo）、菜单（menu）、对局（match）、加载（load）、游戏（game）、编辑（edit）），每次更新最多切换一次。
* `CCSoundBox` 通过 MonoGame 的 `SoundEffect` 播放 WAV 音效，并把每个 MP3 完整解码到内存中（使用 MP3Sharp，NLayer 作为后备）。
* `ecEffect` 是由 `effect_*.xml` 驱动的粒子系统。`CFightText` 绘制飘动的伤害数字。
* `GetPath` 先在语言文件夹中查找，再在资源根目录中查找。`GetDocumentPath` 指向存档文件夹。`ecFile` 读写小端序的二进制数据。`TiXml` 是构建在 `System.Xml.Linq` 之上、接口形态仿照 TinyXML 的外观层。存档是紧凑打包的二进制记录；`settings.cfg` 为 40 字节。
* 游戏状态是一组全局对象（`g_Scene`、`g_GameManager`、`g_Commander`、`g_GameRes`、`g_GameSettings`、`g_SoundRes`、`g_FightTextMgr`、`g_PlayerManager`、`g_NationInfo`）加上引擎单例（`ecGraphics`、`GUIManager`、`GUIMotionManager`、`CStateManager`、`CCSoundBox`、`ecEffectManager`、`ecEffectResManager`、`CObjectDef`）。

### 1.8 平台

* 桌面端把资源文件夹复制到可执行文件旁边，并把鼠标映射为触摸。
* Android 把资源打包在 APK 中，并在首次启动时把全部 136 MB 复制到应用的 files 文件夹，因为每个加载器都使用普通的文件路径。

### 1.9 这种设计的代价

| 特性 | 后果 |
|---|---|
| 文件名就是 API：61 个游戏文件中约有 770 个资源名字面量，另有 162 处 `Sprintf` 调用，其中许多用于拼接名称 | 移动或重命名任何文件都意味着要修改代码 |
| 变体选择分散在各处：46 个文件中有 276 处布局判断（`widthMode == 3`、`screenW <= 568`、`_iPad`、`-568h`） | 新增一种布局，例如真正的 20:9 宽度，就意味着要改动所有这些地方并制作新文件 |
| 每个纹理都以未压缩的 RGBA 上传 | 一局征服中约有 390 MB 纹理，见 2.5 节 |
| 到处都是普通文件 IO（`ecFile` 之外约有 30 处直接调用 `File.*` 的地方） | Android 必须先解压 136 MB；没有地方可以加入模组文件夹或打包归档 |
| 全局对象和单例，初始化顺序是隐式的 | 难以孤立地测试规则、回放一局游戏或干净地重新开始 |
| 手动管理资源生命周期（29 个游戏文件中的 `FreeTexture`/`Release*`） | 切换界面时很容易引入泄漏和重复释放 |
| 整数事件代码，禁用了 `Nullable`，整个项目屏蔽了 13 个警告 | 没有编译器的帮助，重构风险很大 |
| 随机数流以时钟作为随机种子 | 无法为测试或缺陷报告复现一局游戏 |

这些都不是现在的缺陷；它们是我们有意照搬的原版结构。

---

## 第 2 部分：资源现状

### 2.1 清单

共 1 111 个文件，磁盘占用 136.8 MB。"RGBA MB" 是解码为未压缩 RGBA 之后的大小，也就是目前 GPU 中实际占用的大小。

| 类别 | 文件数 | 磁盘 MB | RGBA MB | 格式 |
|---|---|---|---|---|
| 地图背景贴图块（`map1_1..112@2x`，1024 px，每块 500 逻辑单位） | 112 | 58.7 | 470 | pkm 112 |
| 地图分区叠加层图集（`m1_zoneN_1/2`，地区图像） | 118 | 9.0 | 238 | webp 59, xml 59 |
| 小地图 | 8 | 4.2 | 34 | pkm 8 |
| 地图数据（地区、邻接关系、地区标记网格、税收） | 4 | 1.2 | 0 | bin, raw, xml |
| 战斗背景（2048 px；由于战斗动画未移植，没有任何使用方） | 18 | 14.2 | 113 | pkm 9, xml 9 |
| 单位动画集（`infantry_de_hd.xml/.png/.bin`；没有任何使用方） | 150 | 5.4 | 23 | bin, png, xml |
| 楷体/黑体 CJK 字体（4096 px 字形页） | 8 | 15.2 | 168 | fnt, png |
| 其他位图字体 | 204 | 4.3 | 51 | fnt, png |
| 本地化图集（按钮文字、菜单、战斗名称） | 58 | 3.3 | 35 | xml, png, webp |
| 按布局区分的界面背景（`-568h`、`-640h`、`_iPad`） | 40 | 4.3 | 85 | webp, png |
| 共享 UI 图集和零散图像 | 84 | 9.0 | 125 | png, webp, xml |
| 将领肖像 | 17 | 1.6 | 18 | webp |
| 特效 | 18 | 0.2 | 1 | xml, png |
| 剧本（战斗、征服、教程脚本） | 98 | 1.4 | 2 | xml, bin |
| 定义（armydef、carddef、commanderdef、列表、选项布局） | 16 | 0.1 | 0 | xml |
| 字符串表 | 12 | 0.8 | 0 | xml |
| 音乐 | 5 | 2.9 | - | mp3 |
| 音效 | 27 | 0.7 | - | wav |
| 语言文件夹（`*.lproj`） | 114 | 0.5 | 7 | fnt, png, strings, xml |
| **合计** | **1 111** | **136.8** | **1 370** | |

997 个文件平铺在同一个文件夹中；其余文件位于六个 `*.lproj` 语言文件夹中。

### 2.2 命名方案就是变体系统

没有清单文件。文件名本身携带变体信息，代码通过拼接名称来选择变体。

| 标记 | 含义 | 由谁选择 |
|---|---|---|
| `@2x` | 2x 像素美术 | 解析器，自动选择 |
| `_hd` | 用于 2x 美术的图集或字体 | 代码，显式选择 |
| `_iPad` | 平板美术 | 代码（`widthMode == 3`） |
| `-568h`、`-640h` | 宽屏手机背景 | 代码（`screenW <= 480/568/640`） |
| `-512h`、`-534h`、`-568h`、`-640h` | 按手机宽度区分的教程脚本 | 代码 |
| `_568hd`、`_640hd`、`_hd` | 按宽度区分的战斗背景 | 代码 |
| `_cn _en _ja _kr _ru _tw` | 语言 | 代码，依据 `language` 字符串 |
| `<lang>.lproj/` | 语言文件夹 | 解析器，自动选择 |
| `_de _am _gb _ne ...` | 国家 | 代码（部队美术、单位动画集） |
| 数字 | 索引、地图 id、分区 id | 代码，通过 `Sprintf` |

图集 XML 文件指明其纹理，字体隐含对应的 PNG 字形页，`generalphotos.xml` 指明肖像文件，`Localizable.strings` 指明字符串表。

### 2.3 格式

* PKM 即 ETC1，每像素 4 位，仅支持不透明图像。它用于大型不透明图像（地图贴图块、小地图、战斗背景）。我们在 CPU 上把它解码为 RGBA，这使它在 GPU 上的大小变为原来的 8 倍。
* WebP 和 PNG 承载所有带 alpha 的内容：UI、分区叠加层、字体、肖像。
* 字体是按语言和字号区分的 2x 位图字体。两种中文字体的字形页为 4096 x 4096。
* 音乐为 8 kHz 的 MPEG 2.5 Layer III（音质低，需要 MP3Sharp）。音效为 11 kHz 的 PCM WAV。
* 数据为 XML（定义、剧本、图集、字符串表）以及一些小型二进制文件（地图几何、邻接关系、地区标记网格、剧本地区掩码、单位动画）。

### 2.4 运行中的游戏实际加载了什么

方法：在仓库之外构建一个一次性的游戏副本，记录路径解析器返回的每一个资源文件。它运行了 12 个回归测试脚本（中文、568 x 320，外加平板教程）、一次平板菜单巡览，以及英文下 480 和 640 宽度的菜单巡览。

* **1 111 个文件中加载了 254 个，136.8 MB 中加载了 50.8 MB。**
* 其余文件大多是可达的，只是没有被运行到。测试脚本玩的是欧洲剧本，因此 112 个地图贴图块中只需要 24 个，118 个分区文件中只需要 24 个；其他剧本、语言和布局会加载各自的文件。
* **351 个文件（11.8 MB）无法通过当前任何代码路径加载：**

| 分组 | 文件数 | MB |
|---|---|---|
| 单位动画集及其动作/位置表（战斗动画未移植） | 152 | 5.4 |
| 非 HD 字体变体（内容缩放现在始终为 2） | 76 | 5.4 |
| 语言文件夹中的"新游戏"面板字体（该面板只是桩实现） | 96 | 0.5 |
| 编辑器、应用内购买、服务以及遗留文件 | 27 | 0.4 |

* **加载了却从未绘制：** 每局游戏都会加载一对 2048 px 的战斗背景，因为 `CGameRes.Load` 仍在为战斗动画准备它们，而两个移植版本都不播放该动画。

### 2.5 内存现状

同一个一次性构建还记录了缓存中所有纹理解码后的大小：

| 时刻 | 存活纹理数 | RGBA MB |
|---|---|---|
| 主菜单，中文 | 21 | 164 |
| 主菜单，英文 | 21 | 161 |
| 战役战斗（9 个地图贴图块） | 48 | 310 |
| 征服（24 个地图贴图块） | 66 | 389 |
| 平板教程 | 62 | 318 |

内存的去向：

* **楷体/黑体字形页：每种语言都占 134 MB。** 每页都是 4096 x 4096 的 RGBA。没有自己的楷体/黑体字体的语言会回退到中文字形页，因此英文的开销也一样。字形是白色的，形状存储在 alpha 通道中，所以只存储 alpha 就能以 34 MB 保存相同的像素。
* **地图贴图块：解码后每块 4.2 MB，作为原生 ETC1 时每块 0.5 MB。** 一局征服需要 24 块（101 MB，若用 ETC1 则为 13 MB）。八张小地图（34 MB）也是同一格式。
* **战斗背景：34 MB**（568 宽度下），从未绘制。
* **部队图集：33 MB**（4096 x 2024）。

地图贴图块使用原生 ETC1、字形页只存储 alpha、不加载未使用的战斗背景，这几项就能在尚未改动 UI 图集的情况下，把征服场景的纹理内存从约 390 MB 降到约 170 MB，且没有任何可见变化。

---

## 第 3 部分：`newEngine` 提案

### 3.1 原则

1. `original` 保持冻结。它是行为、截图和审计的参考。
2. `newEngine` 上的每一步都保持回归测试套件全部通过，并在改变行为的地方补充测试。
3. 规则代码（战斗、AI、国家、回合）保留原有命名，使审计记录、反编译得到的伪 C 代码和重制版之间仍可相互对照。现代化改造针对的是引擎、资源访问和界面代码。
4. 在重新组织资源树期间，旧的资源名称继续可用，因此重构可以在清理调用点之前落地。
5. 每个工作流都能独立交付可衡量的成果：内存、启动时间、每个功能需要改动的文件更少、可测试性。

### 3.2 工作流

**A. 安全网（最先进行，规模小）。**
可设定随机种子的随机数流，通过 `--seed` 选项指定，使一局游戏可以回放。针对规则的基准（golden）回放测试：用固定的随机种子运行一场脚本化的战斗，并比较最终状态。为冒烟测试脚本提供与 `original` 对比的截图比较模式。一个资源校验器，解析代码和数据文件所请求的每个名称，并报告缺失或未被引用的文件。

**B. 资源访问层（关键的基础能力）。**
所有加载器都通过同一个 `AssetSystem`：`Open`、`Exists`、`ReadAllBytes`、`Enumerate`。其背后是可插拔的数据源：一个文件夹（桌面端、开发环境）、通过 Android 资源管理器或 `TitleContainer` 访问的 APK（首次启动无需解压），以及一个可选的覆盖文件夹，其中的文件优先生效（模组、快速美术测试）。生成的 `manifest.json` 列出每个文件的逻辑名称、限定符、大小和哈希，因此存在性检查只是一次字典查找，而不是磁盘探测。该清单还把每个旧的扁平名称映射到新路径，因此在资源树迁移期间，游戏代码无需修改即可继续工作。约 30 处直接调用 `File.*` 的地方移到该 API 之后；`GetPath` 变成一个轻量的兼容性包装。

**C. 资源树重组。**
一个迁移脚本按领域把文件移入各个文件夹，用文件夹限定符替代名称后缀，然后写出清单和别名表。建议的布局：

```
assets/
  manifest.json            generated: logical name, qualifiers, legacy alias, size, hash
  data/defs/               armydef, carddef, commanderdef, battlelist, conquestlist, generalphotos, guioptions
  data/scenarios/          battle_*, conquest_* (xml + area mask), tutorial scripts
  maps/1/                  area.bin, adjion.bin, areamark.raw, areatax.xml
  maps/1/tiles/            background tiles
  maps/1/zones/            zone overlay atlases
  maps/1/minimap/
  gfx/ui/                  ui1, ui2, mui, title, selbattle, selcountry, cardtex, flags, general ...
  gfx/ui/phone-568/        per-layout backgrounds (mainbg, bankbg, buycardbg, board_*, victorybg_*)
  gfx/ui/phone-640/
  gfx/ui/tablet/
  gfx/units/               army atlas, unit bases and marks
  gfx/effects/             eff + effect_*.xml
  fonts/                   shared fonts (font1, num*, fps)
  audio/music/  audio/sfx/
  lang/<code>/             string tables, Localizable.strings, text/menu/name atlases, language fonts
  dormant/                 battle backgrounds and unit animation sets (kept, not packaged until used)
  legacy/                  non-HD leftovers and files nothing reads (kept in git, not packaged)
```

之后，解析器会像 Android 选择资源那样，按限定符（语言、布局、密度）选择变体，而不再依靠 276 处手写的判断。`hd` 参数仍然是每次使用时的显示决定（见 1.3），因为平板布局依赖于它。

**D. 纹理内存。**
首先是三项没有可见影响的改动：在 GPU 支持的情况下把 PKM 文件作为原生 ETC1 上传，而不是先解码（MonoGame 3.8.6.1 提供了 `SurfaceFormat.RgbEtc1`、`Rgb8Etc2`、`Rgba8Etc2`、`Dxt1` 和 `Astc`）；字形页只存储 alpha；在真正有东西需要绘制战斗背景之前不再加载它们。按 2.5 节的估算，这能让一局征服的纹理内存从约 390 MB 降到约 170 MB。接下来是一个离线步骤，从未经改动的原版文件生成各平台的压缩纹理（Android 上为带 alpha 的 ETC2 或 ASTC，桌面端为 BC）。解码在加载界面期间移到工作线程进行，主线程只负责 GPU 上传。对其他语言不加载中文字形页还能节省更多内存，但会改变英文文本的外观，因此属于决定 1 的范畴。

**E. 代码结构。**
* 拆分为多个项目：`WC2.Engine`（库）、`WC2.Game`（库）、`WC2.Desktop`（宿主程序）、`WC2.Android`（宿主程序）、`WC2.Tools`（迁移、校验、解码器、音频测试）、`WC2.Tests`。这样 Android 项目引用的就是库，而不是链接源文件。
* 每一层使用自己的命名空间，过渡期间游戏库保留全局静态导入。
* 一个 `EngineHost` 对象，拥有各子系统，并负责它们的初始化和关闭顺序。`Instance()` 访问器保留为转发器，使调用点可以逐步修改。一个 `GameSession` 对象，拥有 `g_Scene`、`g_GameManager` 等对象，使测试或"新游戏"能获得干净的状态。
* 按状态和界面划分的资源作用域。通过作用域获取的纹理和图集在作用域结束时释放，取代手动的 `FreeTexture` 调用。
* 类型化事件：每个界面使用枚举，取代数字形式的 `OTHER` 代码。路由规则保持已验证的方式不变。
* 启用可空引用类型，并逐个文件夹解除警告屏蔽。

**F. 布局。**
用一个 `LayoutProfile`（手机 480、534、568、640，平板，以及以后的宽屏配置）加上 `Pick(phone480, phone568, phone640, tablet)` 之类的辅助函数，取代分散各处的判断。之后，各界面可以逐个迁移到数据驱动的布局，延续重制版 `guioptions.xml` 的做法。正是这一点让新的宽高比和 UI 模组变得切实可行。

**G. 存档与设置。**
一个带版本号的存档容器，并附带针对当前二进制存档和 `settings.cfg` 的导入器，使格式可以扩展，不再受 40 字节和紧凑记录的限制。

**H. 内容（以后进行，可选）。**
标签改用 TrueType 文本（恢复原版使用系统字体的标签，并支持任意语言）、战斗动画（需要从伪 C 代码移植；其资源已在 `dormant/` 中），以及更高分辨率的美术资源。

### 3.3 建议顺序

| 步骤 | 工作流 | 规模 | 可见成果 |
|---|---|---|---|
| 1 | A | 小 | 随机种子、回放、资源校验器、截图比较 |
| 2 | B | 中 | Android 首次启动无需解压、单一 IO 路径、覆盖文件夹 |
| 3 | C | 小（借助工具） | 新的文件夹树、清单、旧名称依然可用、无用文件移出 APK |
| 4 | D（原生 ETC1、异步解码） | 小到中 | Android 上内存大幅下降、加载更快 |
| 5 | E（项目、宿主程序、作用域） | 中 | 引擎和游戏成为库，测试无需窗口即可运行 |
| 6 | F | 中到大 | 变体逻辑集中在一处，可以支持新布局 |
| 7 | E（类型化事件、可空引用）、G | 持续进行 | 更安全的重构、可扩展的存档 |
| 8 | H | 大，可选 | 新内容 |

第 1 到 4 步既不改变玩法，也不改变画面；回归测试套件以及与 `original` 的截图比较可以覆盖它们。从第 6 步开始，如果你希望，画面可以改变。

### 3.4 需要你作出的决定

1. **保真度。** `newEngine` 是否必须与 `original` 保持像素级一致（以截图比较作为硬性关卡），还是允许在第 4 步之后进行画面改进？
2. **命名。** 规则代码保留原版/重制版的命名（为了可追溯性，推荐），还是把整个代码库改为 .NET 命名约定？
3. **资源来源。** 保持原版文件逐字节不变，作为唯一可信来源，并在构建步骤中生成运行时格式（推荐），还是就地转换文件？
4. **无用文件和休眠文件。** 是否把非 HD 遗留文件和无人读取的文件移到 `legacy/`，把战斗动画资源移到 `dormant/`，并将它们移出 APK？两者都保留在 git 中。
5. **模组支持。** 是否把用于模组的覆盖文件夹作为目标？这决定了清单是否需要加载顺序和冲突规则。
6. **平台。** 只支持 Android 和 Windows，还是也支持 Linux、macOS、iOS？这会影响需要生成的纹理格式。
7. **数据格式。** 定义和剧本继续使用 XML，以兼容重制版的数据，还是改用 JSON？
