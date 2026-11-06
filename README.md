# World Conqueror 2 — C# port

A C# / MonoGame port of the C++ remake in `Reference/WC2-C` (which itself ran on the original
Android `libeasytech` binary). Everything, including the engine layer the binary used to provide
(renderer, GUI toolkit, bitmap fonts, atlases, particle effects, state machine), is now managed code.

```
WC2/                  Desktop game (MonoGame DesktopGL, net9.0)  →  dotnet run
WC2.Android/          Android package (MonoGame Android)         →  dotnet build -c Release
Reference/WC2-C/      Original C++ remake (read only)
Reference/decompiled/ Hex-Rays pseudo-C of the original libeasytech.so, one file per class
                      (see its README; the engine semantics taken from it are listed in WC2/PORTING_GUIDE.md)
```

## Run on the desktop

```powershell
cd WC2
dotnet run
```

Options: `--width 1280 --height 720` (window size, logical height is always 320),
`--ipad` (1024x768 tablet layout), `--lang zh_CN|English|ja|kr|ru|zh_TW`,
`--assets PATH`, `--log FILE`, `--exit-after SEC --screenshot FILE` (smoke tests),
`--script FILE` (scripted input, see `Host/SmokeTest.cs`).

Controls: left mouse = finger, mouse wheel = zoom, right mouse drag = second finger (pinch),
`Esc`/`Backspace` = Android back key, `F11` = full screen.

Saves and settings live in `%LOCALAPPDATA%\WC2CSharp`.

## Project layout

* `WC2/Engine` — re-implementation of the easytech engine API (`ecGraphics`, `ecImage`,
  `ecTextureRes`, `ecUniFont`/`ecText`, `GUIElement`/`GUIManager`/widgets, `CStateManager`,
  `CCSoundBox`, `ecEffect`, `TiXml` facade over System.Xml, ETC1/PKM decoder, SkiaSharp image loading).
* `WC2/Game` — the ported game code, one folder per original module
  (`BasicElements`, `MapScene`, `Render`, `Fights`, `AI`, `GameStages`, `GUI/Menus`, `GUI/InGame`, ...).
  Class, method and field names follow the C++ on purpose (see `WC2/PORTING_GUIDE.md`).
* `WC2/Host` — MonoGame `Game` host, input mapping, smoke-test driver.
* `WC2/assets` — the original game data (copied verbatim, loaded at runtime).

## Android

`WC2.Android` compiles the same sources with `MonoGame.Framework.Android` and ships the assets
inside the APK (they are extracted to the app folder on first start). Requirements: the .NET
Android workload, an Android SDK (platform 34+), JDK 17. The project points at `C:\Android` and
`C:\Program Files\Java\jdk-17.0.2` when they exist; override with
`-p:AndroidSdkDirectory=... -p:JavaSdkDirectory=...`.

```powershell
cd WC2.Android
dotnet build -c Release
# APK: bin\Release\net9.0-android\*-Signed.apk
# or: .\WC2.Android\build-apk.ps1   (checks the workload, builds Release, copies WC2-android-arm64.apk to the repo root)
```

The activity mirrors the original `Wc2Activity`: layout buckets from the screen aspect (640/568/534/480
x 320, 1024x768 on 4:3 tablets), `nativePause`/`nativeResume` on the life cycle (background save, music,
darkened frame), the back key through `CallNativeExit` (exit box when nothing handled it), immersive
full-screen window, the original launcher icons and localised app name. Package id `com.wc2remake.csharp`,
arm64-v8a only, debug-signed. None of this has been run on a real device yet; the device checklist is in
`Reference/decompiled/DIFFERENCES_THREE_WAY.md` section 5.

## Smoke tests

`WC2/tests/*.txt` are scripts for the built-in input driver; `WC2/tests/run-tests.ps1` runs the audio
self-test and then every script (scripts named `ipad_*` run in the 1024x768 tablet layout) after
`dotnet build`, and writes screenshots and logs to `WC2/tests/out`.

## Status / known gaps

* Not ported (same as the C++ remake): battle animation (the remake never starts it either),
  multiplayer, in-app purchases, service/promotion panels, map editor. The tutorial *is* ported (from the
  binary; the remake ran the original code for it).
* The game logic was audited against the decompiled original (`Reference/decompiled/audit`); the
  remake's accidental deviations were corrected, its deliberate redesigns kept.
* `Reference/decompiled/DIFFERENCES_THREE_WAY.md` is the full original vs C++ remake vs C# matrix with
  the prioritised action list; `Reference/decompiled/DIFFERENCES.md` is the shorter original vs C# list.
* Music is decoded from the original MP3 files with MP3Sharp. The tracks are MPEG 2.5 Layer III at
  8 kHz; NLayer (kept only as a fallback) mis-frames that format and produced garbled, clipped audio
  at twice the speed. `dotnet run -- --test-audio [outDir]` decodes every track, loads every effect
  and prints durations and levels; the regression runner executes it first.
