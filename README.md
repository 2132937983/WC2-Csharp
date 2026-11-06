# World Conqueror 2 — C# port

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

Next to the classic map, `WC2.Tools build-map` generates maps from Natural Earth geography
(`MAP_PIPELINE.md`): a data pack (`assets/data/maps/natural-earth`) and a playable world conquest as a
mod (`mods/map-natural-earth`: conquest 10 "WORLD (NATURAL EARTH)" in the conquest list).

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
