# World Conqueror 2 — C# port

A C# / MonoGame port of the C++ remake in `Reference/WC2-C` (which itself ran on the original
Android `libeasytech` binary). Everything, including the engine layer the binary used to provide
(renderer, GUI toolkit, bitmap fonts, atlases, particle effects, state machine), is now managed code.

Branches: `original` is the faithful restoration and stays frozen as the reference; `newEngine`
carries the modernisation described in `NEW_ENGINE_PLAN.md` (JSON game data, data-driven terrain,
mods, a generated asset package, deterministic tests).

```
WC2.slnx              solution with all projects
WC2/                  Desktop program (MonoGame DesktopGL, net9.0)  →  dotnet run
  assets/             the original game files, never modified
  content/            authored game data (JSON), laid over the originals in the package
  assets-layout.json  package layout and generated runtime formats
  tests/              deterministic regression suite and goldens
WC2.Engine/           engine library (no reference to the game)
WC2.Game/             game library (rules, AI, scene, screens, data layer, shared host)
WC2.Android/          Android package (MonoGame Android)         →  dotnet build -c Release
WC2.Tools/            build-pack: original files + content → runtime package (runs with every build)
docs/                 DATA.md (JSON data reference), MODDING.md, ASSETS.md (package and asset system)
samples/mods/         an example mod
Reference/WC2-C/      Original C++ remake (read only, not in git)
Reference/decompiled/ Hex-Rays pseudo-C of the original libeasytech.so (not in git)
```

## Run on the desktop

```powershell
cd WC2
dotnet run
```

Options: `--width 1280 --height 720` (window size; the logical layout follows the aspect ratio like
the original: 480/534/568/640 x 320, 1024 x 768 below 4:3), `--ipad` (a 4:3 window),
`--lang zh_CN|English|ja|kr|ru|zh_TW`, `--assets PATH` (a package, a flat folder of the original
files, or a `.zip`/`.apk`), `--mods DIR`, `--no-mods`, `--docs DIR` (saves and settings),
`--log FILE`, `--texture-stats`.
Test options: `--seed N --fixed-step` (replayable run), `--exit-after SEC --screenshot FILE`,
`--script FILE` (scripted input, see `WC2/tests/README.md`).
Tools: `--verify-data [--mods DIR]`, `--convert-data`, `--test-audio`, `--test-xml`, `--test-fonts`,
`--decode-pkm`.

Controls: left mouse = finger, mouse wheel = zoom, right mouse drag = second finger (pinch),
`Esc`/`Backspace` = Android back key, `F11` = full screen.

Saves, settings and mods live in `%LOCALAPPDATA%\WC2CSharp` (`mods\` for mods).

## Project layout

* `WC2.Engine` — re-implementation of the easytech engine API (`ecGraphics`, `ecImage`,
  `ecTextureRes`, `ecUniFont`/`ecText`, `GUIElement`/`GUIManager`/widgets, `CStateManager`,
  `CCSoundBox`, `ecEffect`, `TiXml` facade over System.Xml, ETC1/PKM decoder, SkiaSharp image loading),
  plus `IO/AssetSystem` (layered asset access), `IO/ModManager`, `IO/JsonData` (JSON layering).
  It has no reference to the game; the compiler enforces the layering.
* `WC2.Game` — the ported game code, one folder per original module
  (`BasicElements`, `MapScene`, `Render`, `Fights`, `AI`, `GameStages`, `GUI/Menus`, `GUI/InGame`, ...).
  Class, method and field names of the rules follow the C++ on purpose (see `WC2/PORTING_GUIDE.md`).
  `Game/Data` holds the terrain definitions, data paths and the XML→JSON migration and verification.
  `Host/` holds the MonoGame `Game` host and the script driver shared by desktop and Android.
* `WC2` — the desktop program: `Program.cs` (options) and `Host/DebugTools.cs` (tool commands).
* `WC2.Android` compiles the engine and game sources directly against MonoGame's Android build.

## Game data and mods

All game data is JSON (`docs/DATA.md`): units, cards, commanders, terrain, campaigns, conquests,
scenarios, tutorial scripts, texts, the options layout and the per-area map table. Terrain rules that
were constants in the code (area types, buildings, installations, AI area values) and the unit and
commander level tables are data now. Mods are folders that add files or merge single entries into the
data (`docs/MODDING.md`, example in `samples/mods/example-balance`).

## Android

`WC2.Android` compiles the same sources with `MonoGame.Framework.Android`. The generated package ships
inside the APK as `assets/pack/` and is read in place through its index; nothing is extracted on first
start (an extraction left by an earlier build is deleted). Mods go to
`Android/data/com.wc2remake.csharp/files/mods/`. Requirements: the .NET Android workload, an Android SDK
(platform 34+), JDK 17. The project points at `C:\Android` and `C:\Program Files\Java\jdk-17.0.2` when
they exist; override with `-p:AndroidSdkDirectory=... -p:JavaSdkDirectory=...`.

```powershell
cd WC2.Android
dotnet build -c Release
# APK: bin\Release\net9.0-android\*-Signed.apk
# or: .\WC2.Android\build-apk.ps1   (checks the workload, builds Release, copies WC2-android-arm64.apk to the repo root)
```

The activity mirrors the original `Wc2Activity`: layout buckets from the screen aspect, `nativePause` /
`nativeResume` on the life cycle, the back key through `CallNativeExit`, immersive full-screen window,
the original launcher icons and localised app name. Map tiles are uploaded as ETC textures where the
GPU supports it. Package id `com.wc2remake.csharp`, arm64-v8a only, debug-signed. None of this has been
run on a real device yet; the desktop build can run straight from the APK (`--assets <apk>`), which the
tests use to check its contents. The device checklist is in `DIFFERENCES_THREE_WAY.md` section 5.

## Tests

`WC2/tests/run-tests.ps1` (after `dotnet build`) runs the audio self-test, the data verification
(every JSON file loads into the same definitions as the original XML) and every script in
deterministic mode (fixed seed, fixed time step, fresh documents folder), and compares the game-state
dumps with `WC2/tests/golden`. Two scripts let the AI play whole games (`autoplay`). The suite takes
about two minutes. Details in `WC2/tests/README.md`.

## Status / known gaps

* Not ported (same as the C++ remake): battle animation (the remake never starts it either),
  multiplayer, in-app purchases, service/promotion panels, map editor. The tutorial *is* ported (from the
  binary; the remake ran the original code for it).
* The game logic was audited against the decompiled original; the remake's accidental deviations were
  corrected, its deliberate redesigns kept. `DIFFERENCES_THREE_WAY.md` is the original vs C++ remake vs
  C# matrix.
* Music is decoded from the original MP3 files with MP3Sharp (the tracks are MPEG 2.5 Layer III at
  8 kHz, which NLayer mis-frames).
* Text in Japanese, Traditional Chinese and Korean misses glyphs: neither the remake's Kai/Hei fonts
  nor the original bitmap fonts cover those texts (the original drew them with the Android system font).
