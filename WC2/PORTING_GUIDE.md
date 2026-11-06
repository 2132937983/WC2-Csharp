# WC2 C++ → C# porting guide

Source: `E:\CSharpProject\WC2C#\Reference\WC2-C\app\src\main\cpp` (`code/` = rewritten game code,
`easytech/includes/easytech/*.h` = class layouts / method lists).
Target: `E:\CSharpProject\WC2C#\WC2` (MonoGame DesktopGL, net9.0). Build with `dotnet build` in that folder.

## Working on the newEngine branch

The port itself is done; these rules apply to changes made on top of it (see `NEW_ENGINE_PLAN.md`).

1. **Rules code keeps the original names** (classes, methods, fields of the game logic) so it stays
   comparable with the decompiled original, the remake and the audits.
2. **Assets only through `AssetSystem`** (`WC2.Engine/IO/AssetSystem.cs`): `AssetSystem.Exists/OpenRead/
   ReadAllBytes/ReadAllText`, or `GetPath(name)` handed to `ecFile` / `TiXmlDocument`. Never `File.*` on
   an asset: the package may be a folder, a zip, an APK or overlaid by mods. Real files (saves, settings,
   exports) use `GetDocumentPath`.
3. **Game data is JSON** (`docs/DATA.md`): read it with `JsonData.LoadMerged(path)` so mods merge, with
   the `JsonData` accessors (they parse numbers exactly like the original XML attributes). Paths live in
   `WC2.Game/Data/DataPaths.cs`. A new data file needs a reader, a converter or built-in default, and an
   entry in `WC2.Game/Data/DataMigration.cs` so `--verify-data` covers it.
4. **No terrain constants in rules**: ask `Terrain.Area(type)`, `Terrain.Construction(type)`,
   `Terrain.Installation(type)`, `Terrain.Rules` (`WC2.Game/Data/TerrainDefs.cs`) instead of testing for
   `CAPITAL`, `PORT`, `FORT` or hard-coding healing, levels and AI area values.
5. **Original files are never edited.** `WC2/assets` stays as shipped; authored data goes to
   `WC2/content`, package layout and generated formats to `WC2/assets-layout.json`.
6. **Layering**: `WC2.Engine` must not use game types (it does not reference `WC2.Game`, so this is a
   compile error). Engine code lives in `WC2.Engine/`, game code in `WC2.Game/`.
7. **Keep the goldens green.** `tests
un-tests.ps1` must pass; record new goldens (`-Record`) only for
   an intended rule or data change, and say so in the commit.

## Golden rules (original porting phase; paths were `WC2/Engine`, `WC2/Game`, now `WC2.Engine`, `WC2.Game`)

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
