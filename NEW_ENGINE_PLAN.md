# Engine and asset structure: where we are, and a plan for `newEngine`

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
| 0. Safety net | `--seed`, `--fixed-step`, `--docs`; script commands `dump`, `autoplay`, `settle`, `dumpafter`, `hittest`, `texstats`, `startconquest`; golden rule-state dumps; reference screenshots (`WC2.Tools compare-image`, 63 small JPEGs); the asset checker (`validate`); unit tests (`Tests/WC2.Tests`, 35); the hidden-control hit test | goldens replay byte for byte; the hit test found 14 reachable hidden buttons before the fix, 0 after |
| 1. Asset tree | `assets/` by purpose (`audio/music\|sfx\|ui`, `image/ui\|army/<kind>\|map/tiles\|regions\|flags\|markers\|effects\|fonts\|battlebg`, `data/json`, `data/xml`, `data/binary`, `scripts`), variants as folders (`common`, `hd`, `tablet`, `phone-568-hd`, ..., `<lang>`); `legacy-names.json` -> `generated/asset_index.json`; one asset layer (mods -> assets -> original names); Android reads the APK in place, extraction only as a fallback; `ASSET_LAYOUT.md`, `MODDING.md` | goldens from the package, the tree itself and the APK; `validate`: every original reachable, every name the data mentions present |
| 2. Data | JSON for units, cards, commanders, portraits, campaigns, conquests, terrain, war medals, rules, countries, audio, layout profiles, the options layout, the map area table, texts, AI profiles; scenarios and tutorial scripts stay the original XML with a generated index; a JSON Schema per file kind, checked whenever a file loads (an invalid mod's changes are dropped); `DATA_SCHEMA.md` | `--verify-data`: 19 data types identical to the original XML or built-in tables, every file valid |
| 3. Structure | projects `Engine`, `Data`, `Game`, `Hosts/Desktop`, `Hosts/Android`, `Tools`, `Tests` (the engine references nothing of the game, data only the engine, the game no host); one `Layout` facade fed by the layout profile (135 scattered checks); resources released when screens close (regression `resource_cycle`) | builds (0 warnings), suite |
| 3. Turn processing | hop distances cached per area, an allocation-free BFS, pooled particle systems, the AI's search buffers reused; `PerfStats` | 16 AI rounds: allocation per round 10-29 MB -> 3-6 MB, update time per round 80 -> 51 ms (round 1) and 36 -> 21 ms (round 8), AI decisions 44 -> 27 ms (round 1); goldens identical |
| 3. Rendering | ETC1 tiles as RGB565 where the GPU lacks ETC (Android uploads them natively); area overlays as RGBA5551 (converted when loading, the package does not grow); the Kai/Hei glyph pages as Alpha8 (16 MB each instead of 64), tinted by the batcher after a one-time probe of how the GPU samples Alpha8; battle backgrounds loaded only when used; frame statistics | texture memory: menu 97 -> 63 MB, conquest start 288 -> 179 MB (desktop; 389 MB before any of this); Android about 142 MB expected; menu 16 and conquest 71 draw calls per frame; reference screenshots unchanged |
| 4. No purchases | the medal store, the buy button, purchase and price hooks and the purchase grant are gone | `NoPurchaseTests` (sources, manifest, assemblies) |
| 5. Hit testing | touches outside the logical screen dropped at the input entry, hit tests clipped to the screen, drawing clipped by a scissor | `hit_test` (phone), `hit_test_wide` (1600x600 window), `ipad_hit_test` (1000x1000 window), probes in five more scripts, `HitTestTests` |
| 6. AI | every number of the AI in `data/json/ai/profiles.json`: classic (the original, reproduces every golden), standard (default: defends threatened capitals, ports, airfields, cities and home areas, reinforces them, never strips a threatened capital, weighs whole enemy stacks, buys cards more readily), hard | `ai_defends_marseille_*` (standard holds Marseille after Italy's first assault, classic loses it), `ai_standard_autoplay`; 16 rounds of conquest 1: key areas lost 24/73 instead of 29/73, capitals held 8/8 instead of 7/8 |
| 7. Map | `WC2.Tools build-map`: Natural Earth GeoJSON -> regions, adjacency, terrain types, ownership, capitals, cities, ports, airfields (`assets/data/maps/natural-earth`: 966 land regions in 167 countries, 380 sea zones), and a playable mod (`mods/map-natural-earth`: map 2 in the scene's formats with a 31-country world conquest, conquest 10 in the menu with its preview); `MAP_PIPELINE.md` | `MapPackTests` (coverage southern Greenland to southern Madagascar and Darwin, capitals, Marseille, the pipeline on a synthetic world), `world_menu` (menu, preview, game), `world_autoplay` (four rounds on the world map) |
| 8. Verification | Windows and Android build with 0 warnings; `Tests/run-tests.ps1`: unit tests, audio, data, assets and 27 scripts all pass; the APK rebuilt (133.9 MB) and replayed against the goldens with `--assets <apk>` | see the commit log |

### Not done yet

* A device test of the APK: the checklist in `DIFFERENCES_THREE_WAY.md` section 5, plus reading the
  APK in place, the extraction fallback, ETC upload, the Alpha8 probe on a phone GPU, the mod folder.
* The generated map: smoother region shapes, fleets, translated menu texts, balance by play.
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
