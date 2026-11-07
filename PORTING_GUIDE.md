# WC2 C++ → C# porting guide

**Language / 语言:** [English](#english) | [中文](#chinese)

<a id="english"></a>

Source: `E:\CSharpProject\WC2C#\Reference\WC2-C\app\src\main\cpp` (`code/` = rewritten game code,
`easytech/includes/easytech/*.h` = class layouts / method lists).
Target: `E:\CSharpProject\WC2C#\WC2` (MonoGame DesktopGL, net9.0). Build with `dotnet build` in that folder.

## Working on the newEngine branch

The port itself is done; these rules apply to changes made on top of it (see `NEW_ENGINE_PLAN.md`).

1. **Rules code keeps the original names** (classes, methods, fields of the game logic) so it stays
   comparable with the decompiled original, the remake and the audits.
2. **Assets only through `AssetSystem`** (`Engine/IO/AssetSystem.cs`): `AssetSystem.Exists/OpenRead/
   ReadAllBytes/ReadAllText`, or `GetPath(name)` handed to `ecFile` / `TiXmlDocument`. Never `File.*` on
   an asset: the package may be a folder, a zip, an APK or overlaid by mods. Real files (saves, settings,
   exports) use `GetDocumentPath`.
3. **Game data is JSON** (`docs/DATA.md`): read it with `JsonData.LoadMerged(path)` so mods merge, with
   the `JsonData` accessors (they parse numbers exactly like the original XML attributes). Paths live in
   `Data/DataPaths.cs`. A new data file needs a reader, a converter or built-in default, an entry in
   `Game/Data/DataMigration.cs` so `--verify-data` covers it, and a schema in
   `assets/data/json/schemas/` (mapped in `DataPaths.SchemaFor`).
4. **No terrain constants in rules**: ask `Terrain.Area(type)`, `Terrain.Construction(type)`,
   `Terrain.Installation(type)`, `Terrain.Rules` (`Data/TerrainDefs.cs`) instead of testing for
   `CAPITAL`, `PORT`, `FORT` or hard-coding healing, levels and AI area values.
5. **Original files are never edited.** The originals in `assets/` keep their bytes (they were only
   moved and renamed, `assets/legacy-names.json`); authored data goes to `assets/data/json/`, generated
   formats are listed in `assets/build.json` (ASSET_LAYOUT.md).
6. **Layering**: `WC2.Engine` must not use game types (it does not reference `WC2.Game` or `WC2.Data`, so this is a
   compile error). `Data` references only the engine; `Game` references both and never a host; the
   hosts (`Hosts/Desktop`, `Hosts/Android`) only wire input, output and the life cycle.
7. **Keep the goldens green.** `Tests/run-tests.ps1` must pass; record new goldens (`-Record`) only for
   an intended rule or data change, and say so in the commit.
8. **AI numbers are data.** Every number the AI decides with is a field of `AiProfile`
   (`Data/AiProfiles.cs`, `data/json/ai/profiles.json`); `classic` holds the original's numbers and
   must keep reproducing the goldens. New behaviour is a term whose weight is 0 in `classic`.
9. **Generated content comes from the tools.** The map pack and the world map mod are written by
   `WC2.Tools build-map` (`Tools/map/build-natural-earth.ps1`); change the pipeline or its
   configuration and regenerate, never edit the output by hand. The same for `generated/`.
10. **Input and drawing stay on the logical screen.** Touches outside it are dropped, hit tests and
   drawing are clipped to it; a control hidden by moving it off the screen is unreachable by design
   (`hittest` script command, `HitTestTests`).

## Golden rules (original porting phase; paths were `WC2/Engine`, `WC2/Game`, now `Engine`, `Game`)

1. **Port 1:1.** Keep the algorithm, constants, magic numbers, coordinates, string keys and comments
   (translate Chinese comments to short English). Do not redesign, "fix" gameplay or drop features
   unless the guide says the feature is out of scope (JNI, IAP, networking).
2. **Keep the C++ names.** Classes, methods and fields keep their original names and casing
   (`public float[] touchPos`, `void TurnBegin()`, `g_Scene`). Fields are public. This is on purpose.
3. **Single namespace `WC2`, no `using` needed** for engine types; `GlobalUsings.cs` imports
   `Globals`, `CStr`, `GameGlobals`, `FontGlobals`, `StringTableGlobals`, `ToolBox`,
   `ArmyDefFunctions`, `BattleScenario`, `ecTextExtensions` statically. Unscoped enum members
   (`INFANTRY`, `CAPITAL`, `FORT`, `LV_UP_WAV`, `LOAD`, ...) and constants (`MAX_ARMY_LEVEL`,
   `SERIES_MAX`, `VALID_CARD_COUNT`, `TIXML_SUCCESS`, ...) are available unqualified.
   Nested ones: `CGameManager.CAMPAIGN`, `CountryAction.MOVE_ACTION`, `ActionNode.CARD_NODE`,
   `Event.GUI`, `Event.Info.GUIInfo.BUTTON`, `Event.Info.TouchInfo.END`.
4. **Engine files are read-only** (`WC2/Engine/**`, `GlobalUsings.cs`, `Program.cs`, `Host/**`).
   If you need something the engine lacks, write a private helper in your own file and mention it in
   your final report. Never edit files owned by another agent.
5. **Replace every `throw new NotImplementedException(...)` stub** in the skeleton files you own with
   the real port. Skeleton files list the fields already; add missing ones, do not rename existing ones.
6. One C++ `.cpp` file → one `.cs` file with the same base name in the matching folder under
   `WC2/Game/...` (e.g. `code/fights/FightAssist.cpp` → `WC2/Game/Fights/FightAssist.cs`). Classes that
   are spread over several `.cpp` files are `partial` classes: declare `public sealed partial class CScene`
   in each `.cs` file. **Make the skeleton class `partial` when you split it** (you own the skeleton file).
7. Finish with `dotnet build`: zero errors in the files you own. Errors in other agents' files may exist
   while they work; report them, do not fix them.

## Type / idiom mapping

| C++ | C# |
| --- | --- |
| `char name[16]`, `std::string`, `const char*` | `string` (never null for fields; compare with `==`) |
| `std::vector<T>`, `std::list<T>`, `std::deque<T>` | `List<T>` |
| `std::map<K,V>` / `unordered_map` | `Dictionary<K,V>` (use `TryGetValue`, never indexer-insert on lookup) |
| iterator member (e.g. `aoeCardCurTargetId`) | `int` index into the list |
| `T* p` | reference `T p` (null allowed) |
| `T arr[N]`, `T arr[N][M]` | `T[] arr = new T[N]`, jagged `T[][]` |
| `struct` by value copied into containers | `class` + `Clone()`/`CopyFrom()` (AreaInfo, CountryAction, ArmyCombatProfile already have them) |
| `unsigned long color` (0xAABBGGRR) | `uint` |
| `*(int*)&country->r` | `country.ColorRGBA` |
| `sprintf(buf, "fmt", ...)` | `string s = Sprintf("fmt", ...)` (C printf semantics, `%d %s %02d %.1f`) |
| `strcmp(a,b)==0` / `!strcmp` | `a == b` |
| `atoi`, `atof` | `atoi(s)`, `atof(s)` (in `CStr`) |
| `GetRand() % n` | same (`GetRand()` is non-negative) |
| `lrand48()` | `lrand48()` |
| `new ecImage(attr)` ... `delete img` | `new ecImage(attr)` ... set to `null` |
| `ecGraphics::Instance()->FreeTexture(t)` | same |
| `delete guiElement` / `FreeChild` / `SafeFreeChild` | `guiElement.Dispose()` / `parent.FreeChild(e)` / `GUIManager.Instance().SafeFreeChild(e)` |
| `GUIElement::OnEvent(event)` inside an override | `base.OnEvent(evt)` |
| `this->flags & GUIElement::SHOWN` / `0x20000` | `(flags & SHOWN) != 0` (also `ENABLED` = 0x40000 in the C++, `ACTIVE` = 0x10000) |
| `(flags & 0x30000) != 0x30000` | `(flags & (ACTIVE\|SHOWN)) != (ACTIVE\|SHOWN)` |
| `Event e{}; e.type = ...; e.info.gui.ptr = ...` | `var e = new Event(); e.type = Event.GUI; e.info.gui.type = Event.Info.GUIInfo.OTHER; e.info.gui.ptr = this; e.info.gui.info = 5;` or `Event.Gui(Event.Info.GUIInfo.OTHER, this, 5)` |
| reading `event.info.gui.type` for a TOUCH event (union pun) | use `evt.info.touch.type` (BEGIN=0, MOVE=1, END=2) |
| `TiXmlDocument(GetPath(f))`, `LoadFile()`, `FirstChild("x")`, `FirstChildElement()`, `NextSiblingElement()`, `Attribute("a")` (null if missing), `QueryIntAttribute("a", &v)` | identical API on the `TiXml*` classes; `QueryIntAttribute(name, out int v)`; `Attribute(name)` returns `string` or `null`. `TiXmlDocument.LoadAsset(name)` is a shortcut. Elements compare with `==`. |
| `ecFile f; f.Open(GetDocumentPath(name), "rb"); f.Read(&s, sizeof s)` | `var f = new ecFile(); f.Open(GetDocumentPath(name), "rb"); f.ReadInt32()/ReadFloat()/ReadBool()/ReadFixedString(n)` ... `f.Close()` |
| `g_StringTable.GetString(key)` | same (returns the key itself when missing) |
| `CCSoundBox::GetInstance()->PlaySE("x.wav")`, `g_SoundRes.PlayCharSE(LV_UP_WAV)` | same |
| `ecEffectManager::Instance()->AddEffect("effect_exp.xml", true)->FireAt(x,y,0)` | same |
| `g_FightTextMgr.AddText(x, y, text, 0xFF0000FF)` | same |
| JNI (`getStaticMethodInfo`, `CallStaticVoidMethod`, `showWebsiteJNI`, `ecShowReview`) | `getStaticMethodInfo(...)` returns false — keep the `if`, drop the body, or call the `Globals` no-op |
| `g_PlayerManager.*`, multiplayer branches | keep the calls (they are no-ops) |
| `out` parameters `R(float) x` | `ref float x` / `out float x` (follow the skeleton signature) |

Save files: write explicit little-endian layouts with the `ecFile` helpers (`ReadInt32`,
`WriteInt32`, `ReadFixedString(32)`, `WriteFixedString(s, 32)`, `WriteBool`/`ReadBool` as one byte,
`WritePadding(n)`). Reproduce the C++ struct order; byte-exact compatibility with Android saves is
not required, but Save and Load in this port must match each other.

## Engine API cheat sheet (all in `WC2.Engine`)

* `ecGraphics.Instance()`: `screenW/screenH` (logical, e.g. 568x320), `widthMode` (3 = tablet),
  `LoadTexture(name)` (also `LoadWEBPTexture`/`LoadETCTexture`, all resolve `@2x` and png/webp/pkm
  automatically), `FreeTexture(t)`, `SetViewPoint(x,y,scale)`, `SetBlendMode(0|1|2)`,
  `RenderRect(x,y,w,h,color)`, `RenderCircle`, `RenderRing`, `Fade(alpha)`.
* `ecTexture`: `w`,`h` logical size, `xC` content scale, `pixelW/pixelH`.
* `ecImage`: ctors `(tex, x,y,w,h)`, `(tex, ecTextureRect)`, `(ecImageAttr)`; `Render(x,y)`,
  `Render(x,y,w,h)`, `RenderEx(x,y,angle,xScale,yScale)`, `RenderStretch`, `Render4V`, `Render4VC`,
  `SetColor(uint,-1)`, `SetAlpha(float,-1)`, `SetFlip`, `SetTextureRect`, `textureRect.refx/refy`
  are public and mutable, `width/height`.
* `ecTextureRes`: `LoadRes(xml, hd)`, `UnloadRes`, `GetImage(name)` → `ecImageAttr` or null,
  `GetImageByFormat("%s_%s.png", a, b)` → new `ecImage` or null, `NewImage(name)`.
* `GUIElement`: `rect`, `flags`, `parent`, `AddChild(child, bool last)`, `RemoveChild`, `FreeChild`,
  `FreeAllChild`, `MoveToFront`, `SetPos`, `Move`, `Center`, `GetAbsRect(out GUIRect)` /
  `GetAbsRect()`, `CheckInRect(x,y)`, `Show/Hide/SetVisible/SetEnable`, `IsShown`, overrides
  `OnEvent(Event)`, `OnRender()`, `OnUpdate(dt)`, `Dispose()` (destructor: release textures here,
  call `base.Dispose()`). Static `GUIElement.s_TextureRes` is the shared GUI atlas
  (`s_TextureRes.GetImage("menubox.png")`).
  Event routing: TOUCH/BACK events go down to children (top-most first); GUI events (`BUTTON`,
  `OTHER`, `SCROLL_BAR`, `FADE_*`) bubble up through parents to `GUIManager` → `currentState.OnEvent`.
  A panel that returns `true` for TOUCH swallows the touch (modal). Default `OnEvent` returns
  `false` for touches it does not handle.
* `GUIManager.Instance()`: `currentState`, `AddImage(name, rect, parent)`,
  `AddImage(textureName, texRect, rect, parent, pvr)`, `AddButton(normal, pressed, rect, parent, font)`,
  `AddScrollBar(rect, parent, normalImg, pressedImg, xBase, yBase, init, multiplier, horizontal)`,
  `LoadTextureRes(xml, hd)`, `UnloadTextureRes`, `FadeIn(info)`, `FadeOut(info, overlayOrNull)`,
  `fadeTimer`, `SafeFreeChild(e)`, `FreeChild`, `FreeAllChild`, `MoveToFront`.
* `GUIButton`: `Init(normal, pressed, rect, font)`, `SetBackground`, `SetTextImage`, `SetGlow`,
  `SetText`, `SetTextColor`, `SetTextOffset`, `SetTextAlign`, `SetAlpha`, fields `imageNotPresed`,
  `imagePressed`, `imageBackground`, `imageText`, `iPressedDown`, `alpha`, `greyEnable`, `soundBtn`,
  `textOffset`, `textHeight`, `text`. `GUIButtonEx` adds `SetImageText(a,b)`, `imageNotPressed`,
  `imagePressed`, `imagePos[2]`. `GUIRadioButton` adds `selected`.
* `GUIImage`: `Init(name, rect)` (atlas) / `Init(texName, texRect, rect, pvr, etc, webp)` (file),
  `image`, `SetAlpha`. `GUIScrollBar`: `GetScrollPos()`, `SetScrollPos(v)`.
* `GUIScrollList`, `GUIPagedList`, `GUILevelSel`, `GUIMotionManager`, `ecMultipleTouch`
  (`touch.Count`), `CTouchInertia` (skeleton, ported by agent E) are available.
* Text: `ecUniFont` globals `g_Font1..7`, `g_FontHt`, `g_FontKt`, `g_Num1..8`; `ecText`:
  `Init(font)`, `SetText`, `SetColor(uint)`, `SetAlpha`, `DrawText(x, y, align)` (0 left, 1 right,
  2 middle), `GetStringWidth(0,false)`, `GetHeight()`, `fontSize[0/1]`; `ecTextDrawScaled(text, x, y,
  align, scale)`; `ecLabelText` (`Init(font,w,h,spaceW,spaceH)`, `SetText`, `SetColor`, `DrawText`).
* States: `CBaseState` (`OnEnter/OnExit/Update/Render/OnEvent/TouchBegin/TouchMove/TouchEnd/
  BackPressed/ScrollWheel/EnterBackground/EnterForeground`), `CStateManager.Instance()`
  (`SetCurState(EState)`, `GetStatePtr(EState)` → cast to `CGameState` etc.).
* Audio: `CCSoundBox.GetInstance()` (`LoadMusic(name, null)`, `PlayMusic(loop)`, `UnloadMusic`,
  `SetMusicVolume`, `SetSEVolume`, `PlaySE`), `g_SoundRes.PlayCharSE(SND_EFFECT)`.
* Effects: `ecEffectManager.Instance()` (`AddEffect`, `CreateEffect`, `Update`, `Render`,
  `RemoveAll`), `ecEffect` (`FireAt`, `Fire`, `Stop`, `MoveTo`, `Update`, `Render`, `IsLive`).
* `g_FightTextMgr`: `Init`, `Release`, `AddText`, `Update`, `Render`.
* Paths: `GetPath(name)` (asset), `GetDocumentPath(name)` (save/user data), `ecFile.AssetExists`.

## Colors

Vertex colors are `0xAABBGGRR` (R in the lowest byte) — exactly the literals used in the C++.
`0xFF0000FF` = opaque red, `0xFF33313E` = dark grey, alpha in the top byte.

## Out of scope (keep the surrounding flow, make the body a no-op)

JNI/Java calls, in-app purchases, advertising, Game Center / multiplayer sessions, battle animation
(`GUIBattle`; the remake never starts it either). The tutorial is *not* out of scope any more:
`GUITutorails.cpp` is empty in the remake because the original binary code ran there, and
`Game/GUI/InGame/GUITutorails.cs` is a port of that binary code (`Reference/decompiled/x86_64/GUITutorails.c`).

## Engine semantics confirmed from the binary

`Reference/decompiled` holds Hex-Rays pseudo-C of the original `libeasytech.so` (see its README).
The engine layer in `WC2.Engine` follows it; the points below are the ones that differ from what a
reasonable guess would be, so do not "fix" them:

* **Event routing.** `GUIManager.PostEvent` → `GUIElement.PostEvent` walks the tree depth first
  (first child first) and calls an element's own `OnEvent` only when none of its children consumed
  the event. The base `OnEvent` bubbles GUI events to the parent and returns false for everything
  else. Whatever reaches the root goes to `currentState.OnEvent`, including unconsumed touches; only
  after that does the host call `CStateManager.TouchBegin/Move/End`.
* **Buttons** consume a touch BEGIN inside their rect, cancel the press when a MOVE leaves the rect,
  and post the BUTTON event on END *without* consuming it. A pressed button with no pressed picture
  draws nothing. Disabled tint is `greyValue * 110`, `greyValue` defaults to 1.
* **Flags.** `ACTIVE` (0x10000) is toggled by `SetVisible` and only hides the element's own drawing
  (children still render) and blocks events to the subtree; `SHOWN` (0x20000, `Show/Hide`) hides the
  subtree. Every element starts with rect (0, 0, 480, 320).
* **Order.** Children update before the parent (`Update`), render last→first, receive events
  first→last. `MoveToFront` searches the whole subtree.
* **Fades** run at 2.5/s. A finished fade out leaves `fading == 3`: the screen stays black and all
  input is swallowed until `FadeIn`. The overlay passed to `FadeOut` is drawn above the fade (it is not
  part of the tree) and released when the next fade in completes. Fade events carry `ptr = GUIManager`.
* **Motion manager.** 16 sequential slots, per axis speeds in pixels per frame (`SetMotion` splits a
  diagonal speed and truncates to integers), optional wait in frames. Activation flags: 2 = run from the
  target back to the start, 4 = loop, 8 = make `ProcMotion` return true. Every arriving motion posts
  `WARNING(ptr = element, info = motion index)` to the current state; `CGameState` waits for index 1.
  `ProcMotion` runs after `GUIManager.Update` in `ecGameUpdate`.
* **Scroll bar.** Knob drawn at `(x + xScroll, y + yScroll)`; along the axis the knob travels from
  `base/2` to `length - base/2`, `GetScrollPos = multiplier * scroll / (length - base)`; the other axis is
  centered. Orientation lives in flag bit 0.
* **Text.** `GUIText` anchors at the element's x (right aligned text ends there). `ecText.DrawText`
  truncates the half width to whole pixels for centered text. `GUIButton` text is drawn from
  `x + w/2 + offset` whatever the alignment. Missing glyphs are skipped without advance; a 4-byte UTF-8
  sequence stops the string.
* **Images.** `ecImage` defaults to blend mode 2 (alpha); 1 = additive, 3 = multiply.
  `Render4VC` is the growing attack arrow: the top edge slides from the bottom edge as visibility goes
  0→1, with a white→transparent (white = 0) or dark (white = 1) gradient.
* **Atlas.** `LoadRes` keeps the first definition of a duplicated image name; coordinates are halved
  when the texture is 2x.
* **Fight text.** Own font `num2(_hd).fnt`, rises 40 px/s, alpha fades 0.5/s and the text dies at 0.2.
* **Effects.** `ecEffectResManager` owns the effect atlas (`eff.xml`) and caches effect definitions;
  emitters follow the original particle system (emission rate track, life track with per key flags,
  byte colors × ratios, `settings type` point/area). `ecGraphics.widthMode` is 2 for the phone layout
  and 3 for the tablet (1 only for widths ≤ 320).
* **State manager.** One state switch per `Update`, at its start; requesting the current state is a
  no-op; `exist` is set/cleared around `OnEnter/OnExit`.
* **Audio.** The original only wraps Android's SoundPool/MediaPlayer (`CCSoundBox` is JNI glue, volumes
  are `value / 100`). The music files are MPEG 2.5 Layer III at 8 kHz: decode them with MP3Sharp
  (its PCM layout follows `Format`, mono streams arrive as duplicated stereo), not NLayer, which
  mis-frames MPEG 2.5. `--test-audio` compares every track and effect against the file headers.

## Platform entry points confirmed from the binary (`jni.c`, `global.c`)

* Layout: `ecGame.SelectLayout` = `nativeInit` buckets (aspect > 1.8875 → 640x320, > 1.721875 → 568,
  > 1.584375 → 534, < 1.40625 → 1024x768 tablet, else 480x320); `g_contenscalefactor` is 2 on every
  device, including the tablet layout (`--ipad` on the desktop only picks a 4:3 window).
* Frame step capped at 0.05 s (`nativeRender`).
* `ecGamePause`/`ecGameWaiting` set two flags; while either is set `ecGameUpdate` does nothing and
  `ecGameRender` draws `Fade(0.5)` over the frame. On the desktop, losing window focus calls the
  background hooks (state save, music pause) but never the pause flag, so smoke runs keep going.
* `nativePause` = `ecGameDidEnterBackground` (commander save + states) + `ecGamePause`; `nativeResume` =
  `ecGameWillEnterForeground` (volumes from settings, `ResumeMusic`, states) + `ecGameResume`. The Android
  activity drives them from `OnPause/OnResume` and sets `ecGame.HostHandlesLifecycle`.
* Back key: `ecBackPressed` = GUI BACK event, then `CStateManager.BackPressed`; `CallNativeExit` shows the
  MENU state's exit box when that returned false, whatever the current state. Desktop Esc/Backspace and the
  `back` script command go through `CallNativeExit`.
* `CLogoState`: white screen, centred logo, volumes applied, 2.1 s, `FadeOut(-1)`, 1 s, menu. The UI
  atlases and `settings.cfg` that the original loaded there are loaded in `ecGameInit`
  (`LoadGlobalGuiAtlases`, same names and hd flags as `CLogoState::OnEnter`).

## Game-side audit

`Reference/decompiled/audit/README.md` summarises the comparison of the game classes (combat, areas,
armies, scene, AI, countries, turn flow) with the original binary. No porting bugs were found; the
reverse-engineering mistakes of the remake that changed gameplay were corrected in the C# (listed
there), the remake's deliberate redesigns were kept. When touching those functions, keep the "original:"
comments that mark the corrected spots. `Reference/decompiled/DIFFERENCES_THREE_WAY.md` is the complete
original vs remake vs C# matrix (classes, data, platform, build, tests) with the remaining action list.

---

<a id="chinese"></a>

# WC2 C++ → C# 移植指南

源码：`E:\CSharpProject\WC2C#\Reference\WC2-C\app\src\main\cpp`（`code/` = 重写后的游戏代码，`easytech/includes/easytech/*.h` = 类布局 / 方法列表）。
目标：`E:\CSharpProject\WC2C#\WC2`（MonoGame DesktopGL，net9.0）。在该文件夹中执行 `dotnet build` 进行构建。

## 在 newEngine 分支上工作

移植本身已经完成；以下规则适用于在其基础上所做的修改（见 `NEW_ENGINE_PLAN.md`）。

1. **规则代码保留原有名称**（游戏逻辑的类、方法、字段），以便与反编译出的原版、重制版以及各项审计保持可比性。
2. **只通过 `AssetSystem` 访问资源**（`Engine/IO/AssetSystem.cs`）：使用 `AssetSystem.Exists/OpenRead/
   ReadAllBytes/ReadAllText`，或者把 `GetPath(name)` 交给 `ecFile` / `TiXmlDocument`。绝不要对资源使用 `File.*`：游戏资源包可能是一个文件夹、一个 zip、一个 APK，也可能被模组（mod）覆盖。真实文件（存档、设置、导出文件）使用 `GetDocumentPath`。
3. **游戏数据是 JSON**（`docs/DATA.md`）：用 `JsonData.LoadMerged(path)` 读取，这样模组才能合并进来；用 `JsonData` 的访问器读取字段（它们解析数字的方式与原版 XML 属性完全相同）。路径定义在 `Data/DataPaths.cs` 中。新增一个数据文件需要：一个读取器，一个转换器或内置默认值，在 `Game/Data/DataMigration.cs` 中添加一个条目以便 `--verify-data` 覆盖它，以及在 `assets/data/json/schemas/` 中添加一个 schema（在 `DataPaths.SchemaFor` 中映射）。
4. **规则中不出现地形常量**：查询 `Terrain.Area(type)`、`Terrain.Construction(type)`、`Terrain.Installation(type)`、`Terrain.Rules`（`Data/TerrainDefs.cs`），而不是判断 `CAPITAL`、`PORT`、`FORT`，也不要硬编码治疗量、等级和 AI 的地区估值。
5. **绝不编辑原版文件。** `assets/` 中的原版文件保持字节不变（它们只是被移动和重命名过，见 `assets/legacy-names.json`）；编写的数据放在 `assets/data/json/` 中，生成的格式列在 `assets/build.json` 中（见 ASSET_LAYOUT.md）。
6. **分层**：`WC2.Engine` 不得使用游戏类型（它不引用 `WC2.Game` 或 `WC2.Data`，因此这样做会产生编译错误）。`Data` 只引用引擎；`Game` 引用这两者，且绝不引用宿主程序；宿主程序（`Hosts/Desktop`、`Hosts/Android`）只负责接通输入、输出和生命周期。
7. **保持基准（golden）全部通过。** `Tests/run-tests.ps1` 必须通过；只有在有意修改规则或数据时才录制新的基准（`-Record`），并在提交信息中说明这一点。
8. **AI 数值是数据。** AI 决策时用到的每一个数值都是 `AiProfile` 的一个字段（`Data/AiProfiles.cs`、`data/json/ai/profiles.json`）；`classic` 保存原版的数值，并且必须能继续重现基准。新行为是一个在 `classic` 中权重为 0 的项。
9. **生成的内容来自工具。** 地图包和世界地图模组由 `WC2.Tools build-map`（`Tools/map/build-natural-earth.ps1`）写出；应修改流水线或其配置后重新生成，绝不要手工编辑输出。`generated/` 同理。
10. **输入和绘制都限定在逻辑屏幕内。** 落在逻辑屏幕之外的触摸会被丢弃，点击测试（hit test）和绘制都会被裁剪到逻辑屏幕范围内；通过移出屏幕来隐藏的控件按设计就是无法触及的（`hittest` 测试脚本命令、`HitTestTests`）。

## 黄金法则（最初的移植阶段；当时的路径是 `WC2/Engine`、`WC2/Game`，现在是 `Engine`、`Game`）

1. **1:1 移植。** 保留算法、常量、魔数、坐标、字符串键和注释（把中文注释翻译成简短的英文）。不要重新设计、不要"修正"玩法，也不要删减功能，除非本指南说明该功能不在范围内（JNI、IAP、联网）。
2. **保留 C++ 名称。** 类、方法和字段保留原有的名称和大小写（`public float[] touchPos`、`void TurnBegin()`、`g_Scene`）。字段都是 public 的。这是有意为之。
3. **单一命名空间 `WC2`，使用引擎类型无需 `using`**；`GlobalUsings.cs` 以静态方式导入了 `Globals`、`CStr`、`GameGlobals`、`FontGlobals`、`StringTableGlobals`、`ToolBox`、`ArmyDefFunctions`、`BattleScenario`、`ecTextExtensions`。无作用域的枚举成员（`INFANTRY`、`CAPITAL`、`FORT`、`LV_UP_WAV`、`LOAD` 等）和常量（`MAX_ARMY_LEVEL`、`SERIES_MAX`、`VALID_CARD_COUNT`、`TIXML_SUCCESS` 等）可以不加限定直接使用。嵌套的成员：`CGameManager.CAMPAIGN`、`CountryAction.MOVE_ACTION`、`ActionNode.CARD_NODE`、`Event.GUI`、`Event.Info.GUIInfo.BUTTON`、`Event.Info.TouchInfo.END`。
4. **引擎文件只读**（`WC2/Engine/**`、`GlobalUsings.cs`、`Program.cs`、`Host/**`）。如果需要引擎缺少的功能，就在你自己的文件中编写一个私有辅助方法，并在最终报告中提及。绝不编辑归其他代理（agent）负责的文件。
5. **把你负责的骨架文件中的每一个 `throw new NotImplementedException(...)` 桩代码**替换为真正的移植代码。骨架文件中已经列出了字段；补充缺失的字段，不要重命名已有的字段。
6. 一个 C++ `.cpp` 文件 → 一个基础名称相同的 `.cs` 文件，放在 `WC2/Game/...` 下对应的文件夹中（例如 `code/fights/FightAssist.cpp` → `WC2/Game/Fights/FightAssist.cs`）。分散在多个 `.cpp` 文件中的类是 `partial` 类：在每个 `.cs` 文件中都声明 `public sealed partial class CScene`。**拆分骨架类时要把它改为 `partial`**（骨架文件归你负责）。
7. 最后执行 `dotnet build`：你负责的文件中必须零错误。其他代理的文件在他们工作期间可能存在错误；报告这些错误，不要去修复。

## 类型 / 惯用法映射

| C++ | C# |
| --- | --- |
| `char name[16]`、`std::string`、`const char*` | `string`（字段永远不为 null；用 `==` 比较） |
| `std::vector<T>`、`std::list<T>`、`std::deque<T>` | `List<T>` |
| `std::map<K,V>` / `unordered_map` | `Dictionary<K,V>`（使用 `TryGetValue`，查找时绝不通过索引器插入） |
| 迭代器成员（例如 `aoeCardCurTargetId`） | 指向该列表的 `int` 索引 |
| `T* p` | 引用 `T p`（允许为 null） |
| `T arr[N]`、`T arr[N][M]` | `T[] arr = new T[N]`，交错数组 `T[][]` |
| 按值复制进容器的 `struct` | `class` + `Clone()`/`CopyFrom()`（AreaInfo、CountryAction、ArmyCombatProfile 已经具备） |
| `unsigned long color`（0xAABBGGRR） | `uint` |
| `*(int*)&country->r` | `country.ColorRGBA` |
| `sprintf(buf, "fmt", ...)` | `string s = Sprintf("fmt", ...)`（C printf 语义，`%d %s %02d %.1f`） |
| `strcmp(a,b)==0` / `!strcmp` | `a == b` |
| `atoi`、`atof` | `atoi(s)`、`atof(s)`（位于 `CStr` 中） |
| `GetRand() % n` | 相同（`GetRand()` 是非负的） |
| `lrand48()` | `lrand48()` |
| `new ecImage(attr)` ... `delete img` | `new ecImage(attr)` ... 置为 `null` |
| `ecGraphics::Instance()->FreeTexture(t)` | 相同 |
| `delete guiElement` / `FreeChild` / `SafeFreeChild` | `guiElement.Dispose()` / `parent.FreeChild(e)` / `GUIManager.Instance().SafeFreeChild(e)` |
| 重写方法内部的 `GUIElement::OnEvent(event)` | `base.OnEvent(evt)` |
| `this->flags & GUIElement::SHOWN` / `0x20000` | `(flags & SHOWN) != 0`（C++ 中还有 `ENABLED` = 0x40000，`ACTIVE` = 0x10000） |
| `(flags & 0x30000) != 0x30000` | `(flags & (ACTIVE\|SHOWN)) != (ACTIVE\|SHOWN)` |
| `Event e{}; e.type = ...; e.info.gui.ptr = ...` | `var e = new Event(); e.type = Event.GUI; e.info.gui.type = Event.Info.GUIInfo.OTHER; e.info.gui.ptr = this; e.info.gui.info = 5;` 或 `Event.Gui(Event.Info.GUIInfo.OTHER, this, 5)` |
| 对 TOUCH 事件读取 `event.info.gui.type`（联合体类型双关） | 使用 `evt.info.touch.type`（BEGIN=0，MOVE=1，END=2） |
| `TiXmlDocument(GetPath(f))`、`LoadFile()`、`FirstChild("x")`、`FirstChildElement()`、`NextSiblingElement()`、`Attribute("a")`（缺失时为 null）、`QueryIntAttribute("a", &v)` | `TiXml*` 类上的 API 完全相同；`QueryIntAttribute(name, out int v)`；`Attribute(name)` 返回 `string` 或 `null`。`TiXmlDocument.LoadAsset(name)` 是一个快捷方式。元素用 `==` 比较。 |
| `ecFile f; f.Open(GetDocumentPath(name), "rb"); f.Read(&s, sizeof s)` | `var f = new ecFile(); f.Open(GetDocumentPath(name), "rb"); f.ReadInt32()/ReadFloat()/ReadBool()/ReadFixedString(n)` ... `f.Close()` |
| `g_StringTable.GetString(key)` | 相同（缺失时返回键本身） |
| `CCSoundBox::GetInstance()->PlaySE("x.wav")`、`g_SoundRes.PlayCharSE(LV_UP_WAV)` | 相同 |
| `ecEffectManager::Instance()->AddEffect("effect_exp.xml", true)->FireAt(x,y,0)` | 相同 |
| `g_FightTextMgr.AddText(x, y, text, 0xFF0000FF)` | 相同 |
| JNI（`getStaticMethodInfo`、`CallStaticVoidMethod`、`showWebsiteJNI`、`ecShowReview`） | `getStaticMethodInfo(...)` 返回 false —— 保留 `if`，删去其主体，或者调用 `Globals` 中的空操作（no-op） |
| `g_PlayerManager.*`、多人游戏分支 | 保留这些调用（它们是空操作） |
| `out` 参数 `R(float) x` | `ref float x` / `out float x`（遵循骨架中的签名） |

存档文件：用 `ecFile` 的辅助方法写出显式的小端（little-endian）布局（`ReadInt32`、`WriteInt32`、`ReadFixedString(32)`、`WriteFixedString(s, 32)`、按一个字节读写的 `WriteBool`/`ReadBool`、`WritePadding(n)`）。按 C++ 结构体的字段顺序重现；不要求与 Android 存档逐字节兼容，但本移植中的保存（Save）和加载（Load）必须彼此一致。

## 引擎 API 速查表（全部位于 `WC2.Engine`）

* `ecGraphics.Instance()`：`screenW/screenH`（逻辑尺寸，例如 568x320）、`widthMode`（3 = 平板）、`LoadTexture(name)`（以及 `LoadWEBPTexture`/`LoadETCTexture`，它们都会自动解析 `@2x` 以及 png/webp/pkm）、`FreeTexture(t)`、`SetViewPoint(x,y,scale)`、`SetBlendMode(0|1|2)`、`RenderRect(x,y,w,h,color)`、`RenderCircle`、`RenderRing`、`Fade(alpha)`。
* `ecTexture`：`w`、`h` 为逻辑尺寸，`xC` 为内容缩放比例，`pixelW/pixelH`。
* `ecImage`：构造函数 `(tex, x,y,w,h)`、`(tex, ecTextureRect)`、`(ecImageAttr)`；`Render(x,y)`、`Render(x,y,w,h)`、`RenderEx(x,y,angle,xScale,yScale)`、`RenderStretch`、`Render4V`、`Render4VC`、`SetColor(uint,-1)`、`SetAlpha(float,-1)`、`SetFlip`、`SetTextureRect`；`textureRect.refx/refy` 是 public 且可修改的；`width/height`。
* `ecTextureRes`：`LoadRes(xml, hd)`、`UnloadRes`、`GetImage(name)` → `ecImageAttr` 或 null、`GetImageByFormat("%s_%s.png", a, b)` → 新的 `ecImage` 或 null、`NewImage(name)`。
* `GUIElement`：`rect`、`flags`、`parent`、`AddChild(child, bool last)`、`RemoveChild`、`FreeChild`、`FreeAllChild`、`MoveToFront`、`SetPos`、`Move`、`Center`、`GetAbsRect(out GUIRect)` / `GetAbsRect()`、`CheckInRect(x,y)`、`Show/Hide/SetVisible/SetEnable`、`IsShown`，可重写 `OnEvent(Event)`、`OnRender()`、`OnUpdate(dt)`、`Dispose()`（析构函数：在这里释放纹理，并调用 `base.Dispose()`）。静态字段 `GUIElement.s_TextureRes` 是共享的 GUI 图集（`s_TextureRes.GetImage("menubox.png")`）。
  事件路由：TOUCH/BACK 事件向下传递给子元素（最上层的优先）；GUI 事件（`BUTTON`、`OTHER`、`SCROLL_BAR`、`FADE_*`）经由各级父元素向上冒泡到 `GUIManager` → `currentState.OnEvent`。对 TOUCH 返回 `true` 的面板会吞掉该触摸（模态）。默认的 `OnEvent` 对它不处理的触摸返回 `false`。
* `GUIManager.Instance()`：`currentState`、`AddImage(name, rect, parent)`、`AddImage(textureName, texRect, rect, parent, pvr)`、`AddButton(normal, pressed, rect, parent, font)`、`AddScrollBar(rect, parent, normalImg, pressedImg, xBase, yBase, init, multiplier, horizontal)`、`LoadTextureRes(xml, hd)`、`UnloadTextureRes`、`FadeIn(info)`、`FadeOut(info, overlayOrNull)`、`fadeTimer`、`SafeFreeChild(e)`、`FreeChild`、`FreeAllChild`、`MoveToFront`。
* `GUIButton`：`Init(normal, pressed, rect, font)`、`SetBackground`、`SetTextImage`、`SetGlow`、`SetText`、`SetTextColor`、`SetTextOffset`、`SetTextAlign`、`SetAlpha`，字段 `imageNotPresed`、`imagePressed`、`imageBackground`、`imageText`、`iPressedDown`、`alpha`、`greyEnable`、`soundBtn`、`textOffset`、`textHeight`、`text`。`GUIButtonEx` 增加了 `SetImageText(a,b)`、`imageNotPressed`、`imagePressed`、`imagePos[2]`。`GUIRadioButton` 增加了 `selected`。
* `GUIImage`：`Init(name, rect)`（图集）/ `Init(texName, texRect, rect, pvr, etc, webp)`（文件）、`image`、`SetAlpha`。`GUIScrollBar`：`GetScrollPos()`、`SetScrollPos(v)`。
* `GUIScrollList`、`GUIPagedList`、`GUILevelSel`、`GUIMotionManager`、`ecMultipleTouch`（`touch.Count`）、`CTouchInertia`（骨架，由代理 E 移植）均可使用。
* 文本：`ecUniFont` 全局变量 `g_Font1..7`、`g_FontHt`、`g_FontKt`、`g_Num1..8`；`ecText`：`Init(font)`、`SetText`、`SetColor(uint)`、`SetAlpha`、`DrawText(x, y, align)`（0 左对齐，1 右对齐，2 居中）、`GetStringWidth(0,false)`、`GetHeight()`、`fontSize[0/1]`；`ecTextDrawScaled(text, x, y,
  align, scale)`；`ecLabelText`（`Init(font,w,h,spaceW,spaceH)`、`SetText`、`SetColor`、`DrawText`）。
* 状态：`CBaseState`（`OnEnter/OnExit/Update/Render/OnEvent/TouchBegin/TouchMove/TouchEnd/
  BackPressed/ScrollWheel/EnterBackground/EnterForeground`），`CStateManager.Instance()`（`SetCurState(EState)`、`GetStatePtr(EState)` → 强制转换为 `CGameState` 等）。
* 音频：`CCSoundBox.GetInstance()`（`LoadMusic(name, null)`、`PlayMusic(loop)`、`UnloadMusic`、`SetMusicVolume`、`SetSEVolume`、`PlaySE`），`g_SoundRes.PlayCharSE(SND_EFFECT)`。
* 特效：`ecEffectManager.Instance()`（`AddEffect`、`CreateEffect`、`Update`、`Render`、`RemoveAll`），`ecEffect`（`FireAt`、`Fire`、`Stop`、`MoveTo`、`Update`、`Render`、`IsLive`）。
* `g_FightTextMgr`：`Init`、`Release`、`AddText`、`Update`、`Render`。
* 路径：`GetPath(name)`（资源）、`GetDocumentPath(name)`（存档/用户数据）、`ecFile.AssetExists`。

## 颜色

顶点颜色的格式是 `0xAABBGGRR`（R 位于最低字节）—— 与 C++ 中使用的字面量完全一致。`0xFF0000FF` = 不透明的红色，`0xFF33313E` = 深灰色，alpha 位于最高字节。

## 不在范围内（保留外围流程，把函数体改为空操作）

JNI/Java 调用、应用内购买、广告、Game Center / 多人游戏会话、战斗动画（`GUIBattle`；重制版也从未启动它）。教程*不再*属于范围之外：`GUITutorails.cpp` 在重制版中是空的，因为在那里运行的是原版的二进制代码；而 `Game/GUI/InGame/GUITutorails.cs` 是对那段二进制代码的移植（`Reference/decompiled/x86_64/GUITutorails.c`）。

## 从二进制中确认的引擎语义

`Reference/decompiled` 存放着原版 `libeasytech.so` 的 Hex-Rays 伪 C 代码（见其中的 README）。`WC2.Engine` 中的引擎层遵循它；下面列出的是那些与合理猜测不同的地方，所以不要去"修正"它们：

* **事件路由。** `GUIManager.PostEvent` → `GUIElement.PostEvent` 以深度优先的方式遍历元素树（先遍历第一个子元素），只有当某个元素的所有子元素都没有消费该事件时，才调用该元素自身的 `OnEvent`。基类的 `OnEvent` 把 GUI 事件冒泡给父元素，对其他所有事件返回 false。到达根节点的任何事件都会交给 `currentState.OnEvent`，包括未被消费的触摸；在此之后，宿主程序才会调用 `CStateManager.TouchBegin/Move/End`。
* **按钮**会消费落在其矩形内的触摸 BEGIN，当 MOVE 离开矩形时取消按下状态，并在 END 时发送 BUTTON 事件，但*不*消费该事件。处于按下状态但没有按下图片的按钮什么也不绘制。禁用时的着色为 `greyValue * 110`，`greyValue` 默认为 1。
* **标志位。** `ACTIVE`（0x10000）由 `SetVisible` 切换，它只隐藏元素自身的绘制（子元素仍会渲染），并阻止事件传给该子树；`SHOWN`（0x20000，`Show/Hide`）隐藏整个子树。每个元素初始的 rect 都是 (0, 0, 480, 320)。
* **顺序。** 子元素先于父元素更新（`Update`），按从后到前（last→first）的顺序渲染，按从前到后（first→last）的顺序接收事件。`MoveToFront` 会搜索整个子树。
* **淡入淡出**以 2.5/s 的速度进行。淡出完成后会保持 `fading == 3`：屏幕保持黑色，所有输入都被吞掉，直到调用 `FadeIn`。传给 `FadeOut` 的覆盖层绘制在淡出遮罩之上（它不属于元素树），并在下一次淡入完成时释放。淡入淡出事件携带 `ptr = GUIManager`。
* **运动管理器。** 16 个顺序执行的槽位，按轴分别设定速度，单位为像素/帧（`SetMotion` 会把对角线速度拆分到各轴并截断为整数），可选的等待时间以帧为单位。激活标志：2 = 从目标点运行回起点，4 = 循环，8 = 使 `ProcMotion` 返回 true。每个到达终点的运动都会向当前状态发送 `WARNING(ptr = element, info = motion index)`；`CGameState` 等待的是索引 1。`ProcMotion` 在 `ecGameUpdate` 中于 `GUIManager.Update` 之后运行。
* **滚动条。** 滑块绘制在 `(x + xScroll, y + yScroll)`；沿滚动轴方向，滑块从 `base/2` 移动到 `length - base/2`，`GetScrollPos = multiplier * scroll / (length - base)`；另一个轴上居中。方向保存在标志位的 bit 0 中。
* **文本。** `GUIText` 以元素的 x 为锚点（右对齐的文本在此处结束）。对于居中的文本，`ecText.DrawText` 会把半宽截断为整像素。无论对齐方式如何，`GUIButton` 的文本都从 `x + w/2 + offset` 处开始绘制。缺失的字形会被跳过，且不前进；遇到 4 字节的 UTF-8 序列时字符串就此结束。
* **图像。** `ecImage` 默认使用混合模式 2（alpha）；1 = 加色混合（additive），3 = 正片叠底（multiply）。`Render4VC` 是逐渐伸长的进攻箭头：随着可见度从 0→1，上边缘从下边缘处滑出，并带有白色→透明（white = 0）或深色（white = 1）的渐变。
* **图集。** 对于重复的图像名，`LoadRes` 保留第一个定义；纹理为 2x 时坐标减半。
* **战斗飘字。** 使用自己的字体 `num2(_hd).fnt`，以 40 px/s 上升，alpha 以 0.5/s 递减，降到 0.2 时文字消失。
* **特效。** `ecEffectResManager` 拥有特效图集（`eff.xml`）并缓存特效定义；发射器遵循原版的粒子系统（发射速率轨道、带有逐关键帧标志的生命周期轨道、字节颜色 × 比例系数、`settings type` point/area）。`ecGraphics.widthMode` 在手机布局下为 2，在平板布局下为 3（仅当宽度 ≤ 320 时为 1）。
* **状态管理器。** 每次 `Update` 只切换一次状态，且在其开头进行；请求切换到当前状态是空操作；`exist` 在 `OnEnter/OnExit` 前后被设置/清除。
* **音频。** 原版只是封装了 Android 的 SoundPool/MediaPlayer（`CCSoundBox` 是 JNI 胶水代码，音量为 `value / 100`）。音乐文件是 8 kHz 的 MPEG 2.5 Layer III：用 MP3Sharp 解码（其 PCM 布局遵循 `Format`，单声道流会以复制成两份的立体声形式到达），而不要用 NLayer，它对 MPEG 2.5 的分帧有误。`--test-audio` 会把每一首音乐和每一个音效与文件头进行比对。

## 从二进制中确认的平台入口点（`jni.c`、`global.c`）

* 布局：`ecGame.SelectLayout` = `nativeInit` 的分档规则（宽高比 > 1.8875 → 640x320，> 1.721875 → 568，> 1.584375 → 534，< 1.40625 → 1024x768 平板，其余 → 480x320）；`g_contenscalefactor` 在所有设备上都是 2，包括平板布局（桌面端的 `--ipad` 只是选择一个 4:3 的窗口）。
* 帧步长上限为 0.05 s（`nativeRender`）。
* `ecGamePause`/`ecGameWaiting` 设置两个标志；只要其中任意一个被设置，`ecGameUpdate` 就什么也不做，而 `ecGameRender` 会在画面上绘制 `Fade(0.5)`。在桌面端，窗口失去焦点会调用进入后台的钩子（保存状态、暂停音乐），但绝不会设置暂停标志，因此冒烟测试会继续运行。
* `nativePause` = `ecGameDidEnterBackground`（保存将领 + 各状态的处理）+ `ecGamePause`；`nativeResume` = `ecGameWillEnterForeground`（从设置中读取音量、`ResumeMusic`、各状态的处理）+ `ecGameResume`。Android 的 activity 在 `OnPause/OnResume` 中驱动它们，并设置 `ecGame.HostHandlesLifecycle`。
* 返回键：`ecBackPressed` = GUI BACK 事件，然后是 `CStateManager.BackPressed`；当后者返回 false 时，无论当前处于哪个状态，`CallNativeExit` 都会显示 MENU 状态的退出对话框。桌面端的 Esc/Backspace 以及 `back` 测试脚本命令都经由 `CallNativeExit` 处理。
* `CLogoState`：白屏，居中的 logo，应用音量，2.1 s，`FadeOut(-1)`，1 s，进入菜单。原版在这里加载的 UI 图集和 `settings.cfg` 改为在 `ecGameInit` 中加载（`LoadGlobalGuiAtlases`，名称和 hd 标志与 `CLogoState::OnEnter` 中的相同）。

## 游戏侧审计

`Reference/decompiled/audit/README.md` 总结了游戏类（战斗、地区、部队、场景、AI、国家、回合流程）与原版二进制的对比结果。没有发现移植错误；重制版中那些改变了玩法的逆向工程错误已在 C# 中更正（列在该文件中），重制版有意做出的重新设计则予以保留。修改这些函数时，请保留标记已更正之处的 "original:" 注释。`Reference/decompiled/DIFFERENCES_THREE_WAY.md` 是完整的原版 vs 重制版 vs C# 对照矩阵（类、数据、平台、构建、测试），并附有剩余的待办事项列表。
