# Three-way differences: original `libeasytech.so` vs C++ remake vs C# port

**Language / 语言:** [English](#english) | [中文](#chinese)

<a id="english"></a>

State as of 2026-11-05 (evening), after the fixes listed in section 6.A were applied, the regression
suite (audio self-test + 12 scripted runs) was rerun and the APK rebuilt.

The three artefacts:

* **Original** — the Android game library `libeasytech.so` (engine and game in one binary), decompiled
  to pseudo-C in `x86_64/` and `arm64/` (one `.c` per class, `index.tsv`, `vtables.txt`).
* **C++ remake** — `Reference/WC2-C`. Its own code is `app/src/main/cpp/code/**` (106 files). It links
  against the *original binary* through a lazy binder (`easytech/src/<abi>/libeasytech.a`): every
  function the remake does not define in `code/**` still runs as original machine code inside the remake
  when something calls it. The remake defines only the game classes plus six engine overrides (`ecText`,
  `ecUniFont`, `ecTextureRes::GetImage*`, `ecGraphics::RenderCircle/RenderRing`, `GUILevelSel`,
  `GUIScrollList`, `GUIPagedList`, `GUIRadioButton`).
* **C# port** — `WC2/` (MonoGame DesktopGL) and `WC2.Android/`. `WC2/Game/**` was ported from the
  remake's `code/**`; `WC2/Engine/**` re-implements the binary's engine layer. Nothing can fall back to
  the binary: whatever the C# does not contain does not exist.

Detailed evidence lives in the reports this document integrates; rows below cite them instead of
repeating their tables:

| Report | Covers |
|---|---|
| `audit/threeway_gamecode.md` | every remake source file vs the C# (names, literals, flow numbers), every original class the remake runs from the binary, remake-only additions and dev hooks |
| `audit/threeway_data.md` | asset trees, every data file and schema, the data-driven rules introduced by the remake |
| `audit/threeway_platform.md` | JNI entry points, Android life cycle, manifest, packaging, build scripts, tooling, tests, device-only checks |
| `audit/Fights.md`, `AreasArmies.md`, `Scene.md`, `AI.md`, `CountriesTurns.md`, `audit/README.md` | gameplay formulas: original vs remake vs C#, what was corrected and what was kept |
| `DIFFERENCES.md` | original vs C# (engine layer, platform layer, remaining deliberate deviations) |
| `WC2/PORTING_GUIDE.md` "Engine semantics confirmed from the binary" | the engine comparison |

Path shorthand: `D\` = `Reference\decompiled\x86_64\`, `R\` = `Reference\WC2-C\app\src\main\cpp\code\`,
`G\` = `WC2\Game\`, `E\` = `WC2\Engine\`, `A\` = `WC2.Android\`. Pseudo-C functions are cited by their
`// ===== _ZN...` header or by `Class::Method` inside the named `.c` file.

Verdicts: `identical` (same behaviour in all three, or in the two that have it), `intentional redesign`
(the remake changed it on purpose; the row says whether the C# kept it), `missing in C++ remake`,
`missing in C#` (sub-category: intentionally unported / accidentally missing / platform-replaced,
behaviour-equivalent / unverified), `bug in C#` (the C# differed from both), `unverified`. Rows marked
**fixed 2026-11-05** were `bug in C#` or `missing in C#` when the analysis started and are now closed.

---

## 1. Three-way matrix

### 1.1 Runtime architecture and engine layer

| Item | Original | C++ remake | C# port | Verdict | Evidence |
|---|---|---|---|---|---|
| Code base | one native library, 4 139 functions in 146 class files | own `code/**` (30 k lines) + the patched original binary through the binder; weak stubs for every original symbol, a strong definition in `code/**` wins | managed re-implementation: `G\` (~29 k lines, 105 files), `E\` (~5.6 k lines, 25 files), host ~0.8 k | intentional redesign (both) | `index.tsv`; `R\..\CMakeLists.txt:145-205`; `original_easytech.h:10-19`; file counts |
| Engine classes (`GUIElement/Manager/MotionManager/Button/ButtonEx/Image/Text/ScrollBar`, `ecGraphics`, `ecImage`, `ecTextureRes`, `ecEffect*`, `CFightText`, `CCSoundBox`, `CSoundRes`, `ecMultipleTouch`, `ecLabelText`, `ecStringTable`, `ecFile`, `CStateManager`, TinyXML) | binary | binary (binder), except the six overrides named above | `E\**`, aligned statement by statement with the pseudo-C (child-first event routing, button semantics, ACTIVE/SHOWN bits, fades 2.5/s with the black hold, 16 motion slots and WARNING events, scroll bar geometry, text anchoring, blend modes, atlas first-definition-wins, particle system, fight text) | identical, with the deliberate deviations listed in `DIFFERENCES.md` §2 | `PORTING_GUIDE.md` "Engine semantics confirmed from the binary"; `threeway_gamecode.md` §2.1 last row |
| Remake engine overrides: `ecText` (4-byte UTF-8, no truncation of centred text), `ecUniFont`, `ecTextureRes::GetImage/GetImageByFormat`, `RenderCircle` 64-bit vertex fix, `RenderRing`, `GUILevelSel`, `GUIScrollList`, `GUIPagedList`, `GUIRadioButton` | `ecText` truncates centred text to `int`; 3-byte UTF-8; `RenderCircle` broken on 64-bit; no ring, no lists | `R\supports\texts\*.cpp`, `R\gui\basics\ecGraphics.cpp`, `R\gui\GUI*List.cpp` | `E\Text\ecText.cs`, `ecUniFont.cs`, `E\Graphics\ecGraphics.cs:295-341`, `E\GUI\GUIListWidgets.cs`, `GUIButton.cs` | identical to the remake (intentional redesign, C# kept); centred-text truncation follows the original (`(int)(width*0.5f)`) | `threeway_gamecode.md` §1.5, §1.9; `DIFFERENCES.md` §2 |
| Rendering backend | OpenGL ES 1.1 fixed pipeline (`GLESv1_CM`), 4000-entry vertex buffer, orientation modes 2/3 rotate | same (binary) | MonoGame `BasicEffect`, same batching and blend modes; orientation modes 2/3 not implemented (Android passes mode 0) | platform-replaced, behaviour-equivalent | `DIFFERENCES.md` §1; `D\jni.c:878` (`ecGameInit(...,0,...)`) |
| Screen fitting | X and Y scaled independently (stretch) | same | uniform scale + black letterbox bars, touch offsets compensate | intentional redesign in C# (kept) | `E\Graphics\ecGraphics.cs:107-113`; `DIFFERENCES.md` §1 |
| Logical layout selection | `nativeInit`: aspect > 1.8875 → 640x320, > 1.721875 → 568, > 1.584375 → 534, < 1.40625 → 1024x768 tablet, else 480x320; `g_contenscalefactor = 2` always | same (binary) | was `clamp(round(320*w/h),480,640)` and never the tablet layout on Android, `--ipad` used content scale 1 (which points at non-shipped non-HD files); **fixed 2026-11-05**: `ecGame.SelectLayout` reproduces the buckets, scale 2 everywhere, Android and desktop share it | bug in C# → fixed | `D\jni.c:842-882`; `G\Supports\ecGame.cs` `SelectLayout`; `WC2\Program.cs`; `A\MainActivity.cs`; verified: `tests\ipad_tutorial.txt` screenshots |
| Tablet UI atlases | `CLogoState::OnEnter`: `ui_iPad.xml`, `ui1_hd.xml` (hd) or `ui1.xml`, `ui2_hd.xml` (not hd), `text_%s_iPad_hd.xml` (hd), `text_%s_iPad.xml`, `image_newgame_hd.xml` | same (binary) | was `ui_iPad`, `text_%s_iPad`, `ui1_hd`, `ui2_hd` (hd); **fixed 2026-11-05** to the original list (plus an English fallback for the language tables) | bug in C# (minor) → fixed | `D\CLogoState.c:104-166`; `G\Supports\ecGame.cs` `LoadGlobalGuiAtlases` |
| Frame step | `nativeRender`: dt capped at 0.05 s | same | was 0.1 s; **fixed 2026-11-05** | bug in C# → fixed | `D\jni.c:926-942`; `WC2\Host\WC2Game.cs` `Update` |
| Pause / waiting flags | `ecGamePause/Resume/Waiting` set two flags; `ecGameUpdate` is skipped and `ecGameRender` darkens the frame (`Fade(0.5)`) while either is set | same | one flag that only skipped the update; **fixed 2026-11-05**: two flags, update skipped, `Fade(0.5)` drawn; desktop focus loss deliberately does *not* set the pause flag (an unfocused window keeps running, which the smoke tests rely on) | bug in C# → fixed (desktop focus = deliberate deviation) | `D\global.c` `_Z11ecGamePausev`, `_Z12ecGameUpdatef`, `_Z12ecGameRenderv`; `G\Supports\ecGame.cs` |
| Back key | `CallNativeExit`: `ecBackPressed()` (GUI BACK event, then `CStateManager::BackPressed`); when false, `CMenuState::ShowExit` on the MENU state object whatever the current state | same | showed the exit box only when the current state *was* the menu, otherwise the activity finished / the desktop exited; **fixed 2026-11-05**: `ecGame.CallNativeExit` ports the Java entry point; Android back, desktop Esc/Backspace and the `back` script command use it | bug in C# → fixed | `D\jni.c:908-924`; `D\global.c` `_Z13ecBackPressedv`; `G\Supports\ecGame.cs`; verified: `tests\out\back\K1_exitbox.png` |
| Background / foreground | `nativePause` = `ecGameDidEnterBackground` (`CCommander::Save` + states' `EnterBackground`) + `ecGamePause`; `nativeResume` = `ecGameWillEnterForeground` (re-apply music/SE volume, `ResumeMusic`, `EnterForeground`) + `ecGameResume` | same; `CCommander::Save` emptied (persistence moved to `NewSave` on medal change) | hooks existed but were only driven by `Game.IsActive` polling (never observed while an Android activity is paused) and did not re-apply volumes; **fixed 2026-11-05**: `nativePause/nativeResume` ported (idempotent), called from `MainActivity.OnPause/OnResume`; volumes re-applied; `Save()` stays empty as in the remake | bug in C# → fixed (device check pending) | `D\jni.c:892-906`; `D\global.c` `_Z24ecGameDidEnterBackgroundv`, `_Z25ecGameWillEnterForegroundv`; `A\MainActivity.cs`; `threeway_platform.md` §5 |
| Logo state | white screen, centred `logo(_iPad).webp`, settings + UI atlases loaded, volumes applied, 2.1 s, `FadeOut(-1)`, 1 s, `CPlayerManager::Init`, MENU | binary | was a free re-implementation (black, tap to skip, 2.0 s); **fixed 2026-11-05**: port of the pseudo-C (atlas/settings loading lives in `ecGameInit`) | bug in C# → fixed | `D\CLogoState.c`; `G\GUI\States\CLogoState.cs` |
| Textures | libpng (power-of-two padding, RGB565 option), WebP, PVRTC, ETC1 | + `func_AutoPicFormat.cpp` extension fallback chain around the binary loaders | SkiaSharp (PNG/WebP/JPG) + own ETC1 decoder; `ResolveTextureFile` tries `@2x` first then png/webp/pkm/pvr/jpg; PVRTC unsupported (none shipped) | platform-replaced, behaviour-equivalent | `E\Graphics\ecGraphics.cs:400-441`; `R\supports\func_AutoPicFormat.cpp`; `DIFFERENCES.md` §1 |
| Audio | Java `ecSound` (SoundPool) / `ecMusic` (MediaPlayer) through `*JNI` | same | MonoGame `SoundEffect` (OpenAL); MP3 decoded in memory with MP3Sharp (NLayer fallback; NLayer alone mis-framed the MPEG 2.5 8 kHz tracks → the "horn" noise); `--test-audio` self-test | platform-replaced, behaviour-equivalent (verified by sample correlation 1.000 against a Media Foundation decode) | `E\Audio\CCSoundBox.cs`; `WC2\Host\DebugTools.cs`; `README.md` audio note |
| Java-rendered label text (`CreateTextureWithString`) | system font via JNI | same | not available: `ecLabelText` falls back to the bitmap text font with word wrapping | platform-replaced (minor visual difference) | `DIFFERENCES.md` §1 |
| XML parser | TinyXML (strict) | same | facade over `System.Xml.Linq` tolerant of the two malformed shipped lines (`carddef.xml:4,7`) | platform-replaced, behaviour-equivalent | `threeway_data.md` §2.2 |
| Save files (`game%d.sav`, `conquest%d.sav`, `commander.sav`, `settings.cfg`) | packed C structs (header 196, country 1300, area 200, army 44 bytes; commander 95 words, magic 1161909081) | same layout via the binary; `settings.cfg` grown to 40 bytes, `CCommander::Save` empty + `NewSave` | explicit little-endian layout with the same sizes; the area record caps units at 4 where the original can write 5; settings and commander as in the remake | platform-replaced; compatibility with files written by the real app **unverified** | `G\GameStages\SaveIO.cs`; `DIFFERENCES.md` §1 "Saves"; `threeway_platform.md` §6 |
| Random streams | seeded from `rand()` without `srand` (deterministic per process) | `lrand48()` without `srand48` (deterministic) | `System.Random` seeded from time (different every launch) | bug in C# in the strict sense; **knowingly kept** (determinism of the originals is accidental; smoke runs do not depend on it) | `D\global.c:1616-1618`; `R\supports\ecGame.cpp:108-109`; `E\Core\Globals.cs:69-83` |
| Asset lookup order | root first, then `<LangDir>/file` | same | language folder first, then root | platform-replaced, behaviour-equivalent (no file exists in both places, verified on the APK) | `D\global.c:64-89`; `E\Core\Globals.cs` `GetPath` |
| `game.xml` dev export path | `CGameManager::SaveBattle` exists, never called | pause-box Save calls `SaveBattle("game.xml")`; `GetPath("game.xml")` redirected to `/storage/emulated/0/Download/` | export kept; path went into the asset root; **fixed 2026-11-05**: redirected to the document folder (`%LOCALAPPDATA%\WC2CSharp` / `files/documents`) | platform-replaced | `R\supports\ToolBox.cpp:131-141`; `E\Core\Globals.cs` `GetPath`; `G\GUI\InGame\CGameStateEvents.cs:131-137` |
| Logging | `__android_log_print` tag `WC2` (13 sites) | none of its own | `Globals.Log` → console, debugger, optional `--log FILE`; every host callback guarded | intentional redesign in C# | `threeway_platform.md` §7 |

### 1.2 States and screens

| Item | Original | C++ remake | C# port | Verdict | Evidence |
|---|---|---|---|---|---|
| `CLogoState` | 9 fns | binary, reached | faithful port (see 1.1) | fixed | `threeway_gamecode.md` §2.1 |
| `CMenuState`, `GUIMainMenu`, `GUIExitWarning`, `GUILockedWarning`, `GUISelBattle`, `GUIBattleList/Item`, `GUIEnd`, `GUICommander` family (5 classes), `GUICountryList/Item`, `GUISelCountry` | original screens | re-implemented; conquest expansion (`GUIConquestList/Intro/CountryIntro` new, `ActualConquestMap`), unlock-all campaigns | same names, same resource names, string keys, `case`/`FadeOut`/`OTHER` numbers | identical to the remake (intentional redesign where the remake changed the screen) | `threeway_gamecode.md` §1.7 (names n/n, literals clean) |
| `CLoadState`, `GUILoading` | original | 11 + 5 fns; EDIT switch commented out | same | identical | `threeway_gamecode.md` §1.7 |
| `CGameState` (events + life cycle), in-game panels `GUIAIProgress`, `GUIAttackBox`, `GUIBank`, `GUIBattleIntro`, `GUIBegin`, `GUIDefeated` (+`autoCloseTimer`), `GUIDialogue`, `GUIGold`, `GUIMedal`, `GUIPauseBox`, `GUIResult`, `GUITax`, `GUITech`, `GUIVictory`, `GUIWarning`, `GUISelArmy/ArmyItem`, cards (`GUIBuyCard`, `GUICard`, `GUICardList`, `GUICardTab`, `GUISmallCard`), `GUISave/SaveItem` | original | re-implemented (`OnExit` delegated to the binary) | same; `OnExit` sequence re-implemented from `D\CGameState.c` | identical | `threeway_gamecode.md` §1.6 |
| `GUIOptions` | fixed layout, 5 settings | XML layout `guioptions.xml`, options `newConquestLoading`, `aiHosting`, paged list | same | intentional redesign, C# kept | `R\gui\ingame\GUIOptions.cpp:496-700`; `G\GUI\InGame\GUIOptions.cs:273-470` |
| `GUITutorails` (tutorial overlay, 13 commands, 6 script files) | 13 fns, 5 767 B | **binary, reached** (`GUITutorails.cpp` is empty; created by `CGameStateLifeCycle.cpp:217-224`, started by the menu's tutorial button) | was an empty 15-line element (tutorial started, nothing happened); **ported 2026-11-05** from `D\GUITutorails.c` (script by width, SEL_AREA..EXIT, hand/mask/guider/dialog, WAIT ended by a tap, EXIT → `FadeOut(9)`), verified by `tests\tutorial.txt` and `tests\ipad_tutorial.txt` | was missing in C# (accidentally missing) → ported | `G\GUI\InGame\GUITutorails.cs`; `tests\out\T1..T4_tutorial.png` |
| `GUIBattle` + `CBattleScene` + `CBattleUnit` + `ecLibrary/ecLayer/ecFrame/ecElement` (battle animation, ~22 KB) | played when `g_GameSettings.battleAnimation` | **not reached**: the remake's `CGameState::StartBattale` resolves the fight instantly and never calls `GUIBattle::BattleStart` (flag documented as unused); panel still created, option still shown, motion data still parsed by the binary | stub panel, loaders empty, option kept and dead | **missing in C++ remake**, inherited by C# | `R\fights\FightLogics.cpp:10-23`; `R\gui\ingame\CGameStateLifeCycle.cpp:202-214`; `G\GUI\Stubs\NetworkIapStubs.cs:137-161`; `G\BasicElements\CObjectDef.cs:361-368` |
| `GUIIap` + `GUIProductPrice` (medal shop), `GUIService`, `GUINewGame` (promotion, 13 KB of JNI-backed UI) | original | binary, reached (`OTHER 13/20/27`), Java backends absent | one-line stub panels, entry points kept | missing in C#: intentionally unported | `G\GUI\Stubs\NetworkIapStubs.cs:40-90`; `PORTING_GUIDE.md` "Out of scope" |
| Multiplayer: `CPlayerManager` (67 fns), `CPlayer`, `ecSessionInterface`, `ecGameKit`, `Packet*`, `GUIHostList/HostItem/ConnectHostState/MatchSetup/CorpsButton/CorpsInfo/ActionInfo`, `CMatchState` | original | binary, reached through `g_PlayerManager.*`; no match can form without the Java session layer | no-op data classes and stub panels; menu entries kept | missing in C#: intentionally unported | `G\Supports\CPlayerManager.cs`; `threeway_gamecode.md` §2.1 |
| `CEditState` (map editor) | 14 fns | binary, **dead** (the only switch is commented out) | stub returning to MENU | missing in C++ remake (unreachable) and C# | `R\gui\loading\CLoadState.cpp:172-179` |
| `CheckMD5` + `MD5` (asset integrity inside `ecFile`) | 24 KB | binary, transitive | none | missing in C#: intentionally unported (anti-tamper) | `D\ecFile.c:340-380`; `D\CheckMD5.c` |

### 1.3 Gameplay rules

Every C# function is a statement-for-statement port of the remake (975 remake functions found by name
in the C#, literals and flow numbers equal; `threeway_gamecode.md` "How the comparison was made"). The
rows below therefore state the remake's relation to the original and whether the C# follows the remake
or was corrected towards the original on 2026-11-05 (`audit/README.md` lists both sets in full).

| Rule group | Original | C++ remake | C# port | Verdict | Evidence |
|---|---|---|---|---|---|
| Dice count thresholds, no dice when damage ≥ hp, attacker rolls first, emptied-area order, integer radar halving, integer bombing fall-off, medal roll thresholds/stream/area, no medal below 20 damage | original rule | reverse-engineering mistakes | corrected to the original (`original:` comments) | identical (after correction) | `Fights.md` B1-B14; `audit/README.md` "corrected" |
| Counter ranges, damage multipliers (×1.4 → data 130 %/150 %), experience table `{90,150,200,300}` without navy factor, carrier-strike experience and instant kill at infinite rate, fort second counter by trait | hard-coded tables | data-driven (`armydef.xml` profiles) | kept | intentional redesign, C# kept | `Fights.md`; `threeway_data.md` §3 |
| Cross-entrenchment movement | only tank / heavy tank keep movement | trait on six more units (artillery, rocket, destroyer, cruiser, battleship, carrier) | data and legacy defaults corrected to the original | identical (after correction); the only asset edit of the port | `Fights.md` B4; `WC2\assets\armydef.xml` lines 83-169 |
| Sea movement clamp, airport healing level, last-area conquest, complaint condition, movement cost of sea moves, reduced-industry branch, loaded units' max hp, move-to-front order, drafting pixel snap, `army` defeat survival, `AirstrikeRadius`, card payment, defeated-country test | original rule | mistakes | corrected | identical (after correction) | `AreasArmies.md`, `CountriesTurns.md`, `audit/README.md` |
| Encirclement only next to capitals/large cities, up to four ships per sea area, `BeConquestedBy` gives land to the conqueror, income cap 99999, medal top-up, neutral countries flagged defeated at start, `core` defeat type, tech level unclamped, units start with movement 0, dialogue defaults | original rule | redesign | kept | intentional redesign, C# kept | `AreasArmies.md`, `CountriesTurns.md` B1/B12 |
| Camera (zoom limit, 82 px margin, dt easing), data-driven selection circles and the two-hop artillery rule, carrier strikes through the payload, bomber jitter stream, `CScene.Update` only animates running areas, `InitAreaImage` generalised | original | redesign | kept | intentional redesign, C# kept | `Scene.md` |
| AI: +40 player-target bonus keyed on the target, `sortArmyToFront`, draft order by card price, unit-type bonus → `AddAiDamageTendency`, ALLY targets skipped, cooldown on every card, draft placement with artillery and airport penalty | original | redesign | kept | intentional redesign, C# kept | `AI.md` B1-B13 |
| AI: signed `getMaxId`, `currentCountryIndex` reset, `getMedal` thresholds and stream, `getAiPercent` counting, `TypeAdd` table, integer coefficient cast, 0x3000 score formula, card bit masks, `calcAreaArmyHp(area,false)`, random bands, bomber → air-strike fallback, industry weights, tactics bands | original | mistakes | corrected | identical (after correction) | `AI.md`; `audit/README.md` |
| Scenario alliance letters | `a/b/c` → 1/2/3, anything else NEUTRAL | 26 letters, `'d'` → NEUTRAL, **`'n'` → 14** (a 14th belligerent alliance; 15 shipped neutral countries in `battle_allies7/8`, `battle_axis2/3`, `battle_nato1/2`, `battle_wto3`, `conquest_4/7` took turns and could be attacked) | inherited the defect; **fixed 2026-11-05**: `a/b/c` → 1/2/3, everything else NEUTRAL (the remake's writer also emits `"n"` for neutral, so exports round-trip again) | bug in C++ remake inherited → fixed in C# | `D\CGameManager.c` `LoadBattle`; `D\GetBattleBelligerentList.c:71-84`; `R\gameStagesBehaviors\LoadBattleFromXml.cpp:21-29`; `G\GameStages\LoadBattleFromXml.cs` `ParseAlliance` |
| Unlock-all campaigns (`GetNumPlayedBatttles` returns `SERIES_MAX` = 20) | returns `campaignProgress[series]` | dev/cheat choice | kept | intentional redesign, C# kept (one-line revert if the progression is wanted) | `R\supports\CampaignSize.cpp:155-161`; `G\BasicElements\CCommander.cs:290` |
| Commander auto-save on medal change (anti file-cheat), `GUIDefeated.autoCloseTimer`, `SkipMode`, `CombatSpatialIndex`, `CanContainArmy`, `calcAreaArmyHp`, elite infantry (id 15, card 28), real bomber (id 16, data only), card tabs, per-country card prices/tech | absent / fixed | remake additions | all present | identical to the remake | `threeway_gamecode.md` §3 |

### 1.4 Data files and schemas

| Item | Original | C++ remake | C# port | Verdict | Evidence |
|---|---|---|---|---|---|
| Asset tree | APK assets (older generation listed by `CheckMD5`: 681 paths, 427 of them not shipped) | 1 111 files, HD/webp/pkm set | same 1 111 files, byte-identical except `armydef.xml` (six `cross-entrenchment` tokens removed) | identical (one intentional data correction) | `threeway_data.md` §1 (hash pass) |
| `armydef.xml` | 15 fixed types, per-country lists, `strength/movement/minatk/maxatk` only | `army-types`, combat profiles (`attack/counter/range/damage/result`), traits, `weapon-armor-rules`, `unit-type`, `navy`, `image`, `country-independent`, `legacy-combat-defaults`, per-type `others` fallback | same parser (`ArmyDefLoader/Parsing/CombatRules/Store.cs`) | intentional redesign, C# kept; the shipped file cannot be read by the original | `threeway_data.md` §2.1; `D\CObjectDef.c` `LoadArmyDef` |
| `carddef.xml` | flat `<cards><card>`, 28 slots, `round`/`tech` default 1 | `<country call>` groups, any id, `relatedArmyId`, `maxTimes`, defaults 0 | same | intentional redesign, C# kept | `threeway_data.md` §2.2 |
| Scenario XML (`battle_*.xml`, `conquest_*.xml`) | `LoadBattle` | `LoadBattleScenarioData` pre-parse + live loader; `core` defeat; movement 0; dialogue defaults | same; alliance letters now as the original (above) | identical except the listed redesigns | `threeway_data.md` §2.3 |
| `battlelist.xml`, `conquestlist.xml`, `commanderdef.xml`, `generalphotos.xml` | read | same attributes/defaults | same | identical | `threeway_data.md` §2.4 |
| `motiondef.xml`, `unitposdef.xml` (battle animation data) | parsed | parsed by the binary (`CObjectDef::LoadUnitMotions/LoadUnitPositons`), never consumed | empty loaders | missing in C#: intentionally unported (consumer missing in the remake too) | `R\basicElements\CObjectDef.cpp:403-409`; `G\BasicElements\CObjectDef.cs:361-368` |
| Map data (`area%d.bin`, `adjion%d.bin`, `areamark%d.raw`, `areatax%d.xml`, `areasenable` bins, zone atlases, background tiles) | map 1 hard-coded | generalised to `MapID`, zone retry pass | same | identical / intentional redesign kept | `threeway_data.md` §2.5 |
| `m1_conquest1.xml`, `m1_conquest2.xml` | loaded in conquest 1 / 2 | never loaded | never loaded | missing in C++ remake, behaviour-equivalent (the files duplicate zone images; the retry pass covers them) | `threeway_data.md` §2.5 |
| Unit art lookup (`army_hd.xml`) | nine countries, `_ne` fallback, four base colours | 34-country probe, `others` fallback, custom `image`, grey base tinted by `GetAllianceColor` | same | intentional redesign, C# kept (atlas edited accordingly by the remake) | `threeway_data.md` §2.6 |
| Battle-background atlases | HD ≤480 `_hd` (hd), ≤568 `_568hd` (hd), else `_640hd`; tablet `_iPad` | ≤568 branch asks for `_568h` (does not exist), hd flags differ | inherited; **fixed 2026-11-05** to the original names/flags | bug in C++ remake inherited → fixed (no consumer today) | `D\CGameRes.c:3925-3960`; `G\Render\CGameRes.cs` `LoadRes("battlebg…")` |
| `settings.cfg` | 32 bytes, speed clamp 0..5 | 40 bytes (+`useNewConquestLoading`, `aiHosting`), clamp 0..4, old files accepted | same | intentional redesign, C# kept | `threeway_data.md` §2.7 |
| Strings, fonts | `Localizable.strings`, `stringtable_*`, font set | + Kai/Hei fonts, `num7`, `g_introTextScale` | same + `EnsureFallbackFonts` | intentional redesign, C# kept | `threeway_data.md` §2.7 |
| `guioptions.xml` | n/a | options layout | same attributes | intentional redesign, C# kept | `threeway_data.md` §2.7 |
| `map.ini`, `editres.xml`, `conquest.xml`, `tutorails.xml/.bin`, `iap_extra.xml`, `armydef-bak.xml`, `resolutionColumns.xml` | editor / legacy / tutorial / IAP / debug | unread or binary | unread (tutorial scripts now read) | identical / intentionally unported | `threeway_data.md` §2.8 |

### 1.5 Platform, packaging, build, tooling

| Item | Original | C++ remake | C# port | Verdict | Evidence |
|---|---|---|---|---|---|
| ABIs | arm64-v8a, armeabi-v7a, x86, x86_64 libraries | packages armeabi-v7a, x86, arm64-v8a | arm64-v8a only (desktop: any CPU) | missing in C#: deliberate narrowing (knowingly left) | `threeway_platform.md` §1 |
| SDK levels, package id, version | compile 28, `com.easytech.wc2` 15 / 1.3.8 | min 19 / target 28, same id and version | min 23 / target 35, `com.wc2remake.csharp`, versionCode 1; display version was 1.0 while `getVersionName()` returned 1.3.8 → **fixed 2026-11-05**: display version 1.3.8 | intentional redesign (new id, modern target); version string now consistent | `A\WC2.Android.csproj` |
| Permissions | BILLING, network, storage | identical manifest | none (nothing needs them) | intentional redesign, C# kept minimal | `threeway_platform.md` §2 |
| Activity / theme / system UI | `Wc2Activity`, `Theme.*.NoTitleBar.Fullscreen`, `hideSystemUI` immersive sticky, notch meta-data | same | had no theme and no window flags; **fixed 2026-11-05**: `Theme.Black.NoTitleBar.Fullscreen`, immersive-sticky flags re-applied on focus, `LayoutInDisplayCutoutMode.ShortEdges`, `KeepScreenOn` (port addition) | missing in C# (accidentally missing) → fixed (device check pending) | `A\MainActivity.cs` `ApplyWindowFlags/HideSystemUi` |
| Life cycle hooks | `onPause/onResume` → `nativePause/nativeResume` | same | **fixed 2026-11-05** (see 1.1) | fixed (device check pending) | `A\MainActivity.cs` |
| Launcher icon and label | 5 densities × 2 icons, 6 string locales | same | 104-byte placeholder icon, hard-coded label; **fixed 2026-11-05**: the original icons copied (`appicon`, `appicon_round`), `@string/app_name` in en/ja/ko/ru/zh-rCN/zh-rTW | missing in C# (accidentally missing) → fixed | `A\Resources\mipmap-*`, `A\Resources\values*\strings.xml`; APK listing (ten icons, `resources.arsc` 2 272 bytes) |
| Asset access | read in place from the APK | same | extracted once to `files/game` (136 MB) before the first frame | platform-replaced, behaviour-equivalent; first-start cost unverified on a device | `A\MainActivity.cs` `AssetExtractor` |
| Document folder | Java-provided path | same | `files/documents` / `%LOCALAPPDATA%\WC2CSharp` | platform-replaced | `threeway_platform.md` §6 |
| Language selection | Java locale → `LangDir` | same | UI culture → `zh_TW/zh_CN/ja/kr/ru/English`, `--lang` override | platform-replaced, behaviour-equivalent (exact Java branches unverified) | `G\Supports\ecGame.cs:31-45` |
| Signing | vendor key | debug key | debug key (`AndroidKeyStore=false`) | identical state (not store-ready) | `threeway_platform.md` §8 |
| Build script / pinned tool chain | n/a | `build.ps1`/`build.bat` with dependency checks, SHA-pinned Gradle 7.3.3, AGP 7.2.2, NDK r16b | had floating `MonoGame.Framework.* 3.8.*` and no script; **fixed 2026-11-05**: versions pinned to 3.8.6.1, `A\build-apk.ps1` (workload check, build, copy to the repo root) | missing in C# (accidentally missing) → fixed; no CI in any of the three | `A\build-apk.ps1`; both `.csproj` |
| Tests | none | none (junit/espresso declared, no sources) | desktop: audio self-test + 12 scripted runs (`tests\run-tests.ps1`), screenshots; nothing on Android | missing in original and remake; C# desktop-only (Android unverified) | `WC2\tests\` |
| Debug tooling | none | dead Java debug activities, `font_tool.py` | `--decode-pkm --test-xml --test-fonts --test-audio`, script driver, `--log`; no font builder | intentional redesign; `font_tool.py` knowingly not ported | `threeway_platform.md` §7 |
| JNI-backed features (`AddMedal`, `PurchaseSuccess`, `nativeSetProductPrice`, `ecPromotion` new-tip, `MainMenuLoadedJNI`, `ecMailServiceJNI`, `GetDataFromServerJNI`, `isAndroidEmulator`, `ecGetUUID`) | Java | binary (Java side dead) | no-ops / `false` | missing in C#: intentionally unported | `E\Core\Globals.cs:105-136`; `threeway_platform.md` §9 |
| `ecShowReview`, `ecAccelerometer`, `ecSetIdleTimerDisabled`, `ecIs/SetFullScreen` | empty or uncalled exports | same | no-ops; `ecSetFullScreen` bound to F11 on desktop | identical (no-op everywhere) | `threeway_platform.md` §4, §9 |

---

## 2. C++ remake → C# port: what the C# lacks or does differently

Everything the remake implements in `code/**` exists in the C# under the same name with the same
constants (`threeway_gamecode.md` §1). The differences are:

**2.1 Original classes the remake runs from the binary and the C# replaces**

| Item | State in C# | Category |
|---|---|---|
| `GUITutorails` | ported 2026-11-05 from the pseudo-C, verified on desktop (phone and tablet layout) | closed |
| `CLogoState` | ported 2026-11-05 | closed |
| `GUIIap`, `GUIProductPrice`, `GUIService`, `GUINewGame` | stub panels ("not available") | intentionally unported |
| `CPlayerManager`, `CPlayer`, session/packet classes, lobby and corps panels, `GUIActionInfo`, `CMatchState` | no-ops and stub panels, flow kept | intentionally unported |
| `CObjectDef::LoadUnitMotions/LoadUnitPositons/Get*/Release*` | empty | intentionally unported (no consumer in the remake either) |
| `CheckMD5`, `MD5` | absent | intentionally unported |
| `CEditState` | stub | unreachable in the remake as well |
| `CActionAI::getNumArmyMove`, `CFight::GetAttackAdd/GetDefendAdd`, `CScene::RenderAreas*`, `CGameManager::Init/NextBattle`, `GUISelCountry::SelBattle`, `CCountry::GenCardTargetList` | `=> 0` / absent | identical in effect (1-5 byte stubs or dead in the release binary) |

**2.2 Remake source that has no C# counterpart on purpose**

`CMenuStateBridge.cpp` (C++ ABI glue), `easytech_global.cpp` (empty), `func_AutoPicFormat.cpp`
(replaced by `ResolveTextureFile`), `GetActualMap` (cast helper), `killAllEcImages` (delete helper),
`CGameRes::RenderArmyWithDef` and `SetOptionLevelFromTouch` (both `#if 0`), `CScene::RenderSeaAreas`
(debug overlay, never called), the two `printf` lines in `MapExtension.cpp`, the commented
`GameButtonDI` button, `ShowDiplomaticDialog`, the `com_easytech_wc2_TestActivity.h` hooks and the
commented EDIT switch in `CLoadState.cpp` (`threeway_gamecode.md` §3).

**2.3 Resources**

The 1 111 game assets are identical except the `armydef.xml` correction. The remake's Android
resources (icons, six `strings.xml`, styles) were missing in the C# project and are now copied
(icons, `app_name`) or expressed as the activity theme. Not carried over: `patch.aar` (a "cloud save"
mod activity that the remake's manifest does not reference), `resolutionColumns.xml`-driven Java debug
dialogs (excluded from the remake build), `raw/sub.srt`, `public.xml`/`ids.txt` stable ids (only needed
by the prebuilt `easytech.jar`).

**2.4 Data-driven rules**

Every rule the remake moved into data is consumed by the C# with the same semantics
(`threeway_data.md` §3: traits, profiles, damage/weapon rules, result rules, AI bonus, navy flag,
defeat types, card tables, dialogue defaults, options layout, conquest numbering). Two places were
changed *towards the original*: the `cross-entrenchment` trait data and legacy default, and the
alliance letter rule (`'n'` neutral).

**2.5 Dev hooks**

* `GetPath("game.xml")` → Download folder: replaced by the document folder (fixed today).
* `SaveBattle("game.xml")` from the pause box: kept.
* Commander auto-save on medal change: kept.
* Unlock-all campaigns: kept.
* `RenderSeaAreas`, `printf`, `GameButtonDI`, JNI test hooks: dead in the remake, not ported.

**2.6 Build scripts, packaging, tests**

| Remake | C# | State |
|---|---|---|
| `build.ps1`/`build.bat` with JDK/SDK discovery, dependency checks, offline/clean switches; docs `BUILD.en.md`/`BUILD.zh-CN.md` | `WC2.Android\build-apk.ps1` (added today), `README.md` Android section | closed (smaller scope: no clean/offline switches) |
| Gradle wrapper SHA-pinned, AGP/NDK pinned | NuGet versions pinned (`MonoGame.Framework.* 3.8.6.1`, `SkiaSharp 3.119.2`, `NLayer 1.16.0`, `MP3Sharp 1.0.5`); no lock file | closed (lock file optional) |
| three ABIs | arm64 only | knowingly left |
| `applicationId com.easytech.wc2`, version 15 / 1.3.8 | `com.wc2remake.csharp`, 1 / 1.3.8 | intentional (does not clash with the real game) |
| manifest permissions, `max_aspect`, notch meta-data | none / cutout mode set in code | intentional / closed |
| no tests | desktop regression suite, no Android tests | C# ahead; Android unverified |

**2.7 Engine fixes of the remake and their C# status**

`ecText` 4-byte UTF-8 (kept), `RenderCircle` 64-bit fix (not needed in managed code, same vertices),
`RenderRing` (kept), `AutoPicFormat` fallback (kept as `ResolveTextureFile`), `ecUniFont` record layout
(kept), `ecTextureRes::GetImage` behaviour (kept), `GUILevelSel/ScrollList/PagedList/RadioButton` (kept),
`CampaignSize` 20 per series (kept), anti-file-cheat auto-save (kept).

---

## 3. Original → C++ remake: what the remake dropped or redesigned, and what the C# did with it

| Original feature | Remake | C# |
|---|---|---|
| Battle animation (`GUIBattle`, `CBattleScene`, motion data) | dropped (never started; option and data loading left in place) | same as the remake; would have to be ported from the pseudo-C (no C++ source exists) |
| Tutorial | kept (binary code) | ported today |
| Logo screen | kept (binary) | ported today |
| Map editor | unreachable | stub |
| IAP shop, service, promotion, review prompt, new-tip badge | binary UI without a working Java side | stubs / no-ops |
| Multiplayer | binary, non-functional | no-ops |
| Asset integrity check | still runs inside the binary | absent |
| Progress-based campaign unlock | all campaigns unlocked | kept (unlock-all) |
| Hard-coded combat tables, counter table, selection circles, two-hop rule, carrier strike type 4 | data-driven profiles in `armydef.xml` | kept |
| ×1.4 panzer/infantry multiplier | 130 % / 150 % data rules | kept |
| Experience thresholds and navy factor | changed table, factor removed | kept |
| One flat card table | per-country card prices/tech, draft order by price, elite infantry card | kept |
| 15 fixed unit ids, nine-country art | registry with dynamic ids, 34-country art probe, custom images | kept |
| a/b/c alliances, else neutral | 26 letters (`'n'` became alliance 14) | corrected to the original rule |
| land/army defeat | + `core` | kept |
| Tech level clamp, sea start movement, dialogue uninitialised | unclamped, 0, defaults | kept |
| Encirclement, ships per sea area, `BeConquestedBy`, income cap, medal payment, neutral turn handling | redesigned | kept |
| Camera limits and margin | freed | kept |
| AI target bonus, draft order, unit-type bonus table, ally scoring, cooldown checks, placement | redesigned | kept |
| Eight fixed conquests with flag preview | scanned conquests, real-map preview, intro panels | kept |
| Fixed options page | XML layout + two new options | kept |
| 32-byte settings, speed clamp 5 | 40 bytes, clamp 4 | kept |
| Fonts | + Kai/Hei, `num7` | kept (+ fallback) |
| `CCommander::Save` on every change | emptied; `NewSave` on medal change | kept |
| Zone atlas loading for map 1 | generalised, retry pass, conquest overlays dropped | kept |
| Battle-background atlas names | one wrong name | corrected to the original |
| Dice/roll/medal/movement/AI details listed in `audit/README.md` | reverse-engineering mistakes | corrected to the original |

---

## 4. C# port: missing advantages and content, by category

**Intentionally unported** (documented in `README.md` "Status / known gaps" and `PORTING_GUIDE.md`
"Out of scope"): in-app medal shop and prices, customer-service mail and data-from-server, promotion /
"more games" panel and its web links, review prompt, multiplayer (session layer, lobby, corps, match
state, UUID), map editor, asset-integrity check, battle-animation data loaders (and the animation itself,
which the remake also lacks), 32-bit ARM and x86 packages, `BILLING`/network/storage permissions, the
remake's `font_tool.py`, the remake's dead Java debug activities and `patch.aar`.

**Accidentally missing — found by this analysis and closed on 2026-11-05**: tutorial overlay; faithful
logo screen; layout buckets and tablet layout on Android; HD content for the tablet layout; tablet UI
atlas list; frame-step cap; pause/waiting flags with the darkened frame; back-key semantics
(`CallNativeExit`); foreground volume re-application; Android `onPause/onResume` hooks; immersive
full-screen window, cutout handling; launcher icons and localised label; display version; neutral
alliance letter; `game.xml` export location; battle-background atlas names; pinned package versions;
build script. Still open in this category: none known.

**Platform-replaced, behaviour-equivalent**: rendering backend, screen fitting (letterbox instead of
stretch), texture loading and format fallback, audio stack, XML parser, save-file IO, asset extraction
to the files folder, document folder, language selection, asset lookup order, touch id assignment,
exit/URL hooks, logging, `game.xml` location.

**Unverified** (needs a device or files from the real game): Android life cycle hooks and background
save, music in the background, exit finishing the activity, immersive bars on notch/20:9 screens, layout
per device class, first-start extraction time, back/gesture navigation on API 33+, pinch zoom through
`TouchPanel`, locale mapping, loading `settings.cfg`/`commander.sav`/`game6.sav` written by the real
app (area record unit cap 4 vs 5), GLES performance and start-up with the 22.6 MB assembly blob,
whether the remake itself packages `modlibeasytech.so` for arm64, the consumer of the Java shake sensor.

---

## 5. Unverified items and device-test checklist

Could not be established from the sources (all three artefacts):

* The exact Java-side behaviour of `Wc2Activity` (`easytech.jar` bytecode was not decompiled): the
  document directory it passes, the `ko.lproj` vs `kr.lproj` branch, the shake-sensor consumer, whether
  `MediaPlayer` is paused in `onPause` independently of the native hooks.
* Whether the remake's Gradle build really packages `modlibeasytech.so` for arm64 (`CMakeLists.txt`
  comment without a copy step).
* Compatibility of C# saves with files written by the original app (never exchanged).

Device-test checklist for the APK (`WC2-android-arm64.apk`, package `com.wc2remake.csharp`,
versionName 1.3.8):

1. Launcher shows the original icon and the localised name (device in zh-CN/ja/ko/ru).
2. First start: extraction time of the 136 MB asset set, free-space behaviour, second start is instant.
3. Status/navigation bars hidden during play; swipe reveals them transiently; notch/cut-out areas used
   (short edges); no letterbox surprise on 20:9.
4. Layout: 16:9 → 568x320, 16:10 → 534x320, 20:9 → 640x320, 4:3 tablet → 1024x768 with the iPad
   atlases (compare with the original on the same device class).
5. Home → return during a battle: `game6.sav`/`conquest6.sav` appears in `files/documents`, music
   stops and resumes, volumes unchanged, the frame is darkened while paused.
6. Exit box → confirm finishes the activity; `settings.cfg` written on exit.
7. Back key in every state (logo, menu, load, battle, pause box, tutorial), hardware and gesture back
   on API 33+.
8. Pinch zoom and two-finger pan on the map; tap precision at the letterbox edges.
9. Tutorial on a phone and on a tablet (script selection by width).
10. Screen stays on during long AI turns (`KeepScreenOn`).
11. Copy a `settings.cfg` (32 bytes), `commander.sav` and `game6.sav` from the real game into
    `files/documents` and load them.
12. Frame rate on a low-end arm64 device with the 4096-pixel atlases; cold start time.
13. Install on an armeabi-v7a-only or x86 device fails by design.

---

## 6. Prioritised action list

### 6.A Fixed in the C# on 2026-11-05 (this analysis)

| # | Fix | Files | Evidence |
|---|---|---|---|
| 1 | Tutorial overlay ported from the binary | `G\GUI\InGame\GUITutorails.cs`, `tests\tutorial.txt`, `tests\ipad_tutorial.txt` | `D\GUITutorails.c`; screenshots `T1..T4`, `P1..P2` |
| 2 | Logo state made faithful | `G\GUI\States\CLogoState.cs` | `D\CLogoState.c` |
| 3 | Alliance letter `'n'` (and every non-a/b/c letter) = NEUTRAL | `G\GameStages\LoadBattleFromXml.cs` | `D\CGameManager.c`, `D\GetBattleBelligerentList.c:71-84` |
| 4 | Layout buckets + tablet layout, HD content everywhere | `G\Supports\ecGame.cs` `SelectLayout`, `WC2\Program.cs`, `A\MainActivity.cs` | `D\jni.c:842-882` |
| 5 | Tablet UI atlas list | `G\Supports\ecGame.cs` `LoadGlobalGuiAtlases` | `D\CLogoState.c:104-166` |
| 6 | Frame step cap 0.05 s | `WC2\Host\WC2Game.cs` | `D\jni.c:926-942` |
| 7 | Pause/waiting flags skip the update and darken the frame | `G\Supports\ecGame.cs` | `D\global.c` `ecGameUpdate/ecGameRender` |
| 8 | Back key = `CallNativeExit` (exit box from any state) | `G\Supports\ecGame.cs`, `WC2\Host\WC2Game.cs`, `WC2\Host\SmokeTest.cs`, `A\MainActivity.cs` | `D\jni.c:908-924` |
| 9 | Foreground re-applies volumes; background calls the commander save; `nativePause/nativeResume` ported and driven by the activity | `G\Supports\ecGame.cs`, `A\MainActivity.cs` | `D\global.c:1192-1251`, `D\jni.c:892-906` |
| 10 | Immersive full-screen theme and flags, cutout mode, keep-screen-on | `A\MainActivity.cs` | remake `res\values*\styles.xml`, `Wc2Activity` strings |
| 11 | Launcher icons (5 densities, round), localised `app_name` (6 locales), display version 1.3.8 | `A\Resources\**`, `A\Properties\AndroidManifest.xml`, `A\WC2.Android.csproj` | remake `res\**` |
| 12 | `game.xml` export in the document folder | `E\Core\Globals.cs` `GetPath` | `R\supports\ToolBox.cpp:131-141` |
| 13 | Battle-background atlas names and hd flags | `G\Render\CGameRes.cs` | `D\CGameRes.c:3925-3960` |
| 14 | Pinned MonoGame versions, APK build script, tablet regression script | both `.csproj`, `A\build-apk.ps1`, `tests\run-tests.ps1` | — |

Verification after the fixes: `dotnet build` clean (0 errors) for `WC2` and `WC2.Android`;
`tests\run-tests.ps1` = audio self-test (5 tracks decoded, 27 effects ok) + 12 scripts
(`attack_box`, `campaign_13_rounds`, `campaign_start`, `card_shop`, `commander_and_options`,
`conquest_7_rounds`, `conquest_ai_turns`, `conquest_start`, `ipad_tutorial`, `pause_options_save`,
`save_and_load`, `tutorial`) all `ok`; APK rebuilt and copied to `WC2-android-arm64.apk`.

### 6.B Fix next in the C# (desktop-verifiable, not done)

1. **Save-file compatibility with the real app**: compare `SaveIO.cs` against a `game6.sav` written by
   the original; raise the area record's unit cap from 4 to 5 if the original can write 5
   (`DIFFERENCES.md` §1 "Saves").
2. **First-start asset extraction**: show progress or read assets straight from the APK (the original
   and the remake read in place); 136 MB copy on the UI thread today (`A\MainActivity.cs`).
3. **Release signing config** (`AndroidKeyStore`, a project keystore) before any distribution.
4. **Battle animation**: the only gameplay content missing from both ports. Port `GUIBattle`,
   `CBattleScene`, `CBattleUnit`, `ecLibrary/ecLayer/ecFrame/ecElement` and the two `CObjectDef` loaders
   from the pseudo-C; re-enable `StartBattale`'s animation flag. Large (≈22 KB of binary code) and
   needs its own audit; decide first whether the option should stay.
5. Optional one-liners if the original behaviour is preferred: campaign unlock by progress
   (`CCommander.cs:290`), deterministic RNG seeding (`Globals.cs:69-83`).

### 6.C Test on a real Android device

Section 5 checklist, items 1-13; the life cycle, immersive and layout fixes of 6.A were only
compile-checked and desktop-checked.

### 6.D Knowingly left unported

In-app purchases and prices, service/mail/server, promotion panel and web links, review prompt,
multiplayer, map editor, asset-integrity check, 32-bit/x86 packages, the remake's font tool and dead
Java debug UI; the remake's deliberate gameplay redesigns listed in section 3 are kept (each can be
reverted individually with the evidence in the audit reports).

---

<a id="chinese"></a>

# 三方差异：原版 `libeasytech.so`、C++ 重制版与 C# 移植版

截至 2026-11-05（晚间）的状态：第 6.A 节所列修复已应用，回归测试套件（音频自检 + 12 个脚本化运行）已重新运行，
APK 已重新构建。

三个对比对象：

* **原版** — Android 游戏库 `libeasytech.so`（引擎与游戏位于同一个二进制文件中），已反编译为伪 C 代码，
  位于 `x86_64/` 和 `arm64/`（每个类一个 `.c`，另有 `index.tsv`、`vtables.txt`）。
* **C++ 重制版** — `Reference/WC2-C`。其自有代码为 `app/src/main/cpp/code/**`（106 个文件）。它通过一个延迟绑定器
  （lazy binder，`easytech/src/<abi>/libeasytech.a`）链接到*原版二进制文件*：凡是重制版没有在 `code/**` 中定义的函数，
  一旦被调用，仍以原版机器码的形式在重制版内部运行。重制版只定义了游戏类以及六处引擎覆盖（`ecText`、
  `ecUniFont`、`ecTextureRes::GetImage*`、`ecGraphics::RenderCircle/RenderRing`、`GUILevelSel`、
  `GUIScrollList`、`GUIPagedList`、`GUIRadioButton`）。
* **C# 移植版** — `WC2/`（MonoGame DesktopGL）和 `WC2.Android/`。`WC2/Game/**` 移植自重制版的 `code/**`；
  `WC2/Engine/**` 重新实现了二进制文件中的引擎层。任何东西都无法回退到二进制文件：C# 中没有的东西就是不存在。

详细证据见本文档所整合的各份报告；下文各行直接引用这些报告，而不重复其中的表格：

| 报告 | 涵盖内容 |
|---|---|
| `audit/threeway_gamecode.md` | 重制版每个源文件与 C# 的对比（名称、字面量、流程编号），重制版从二进制文件中运行的每个原版类，重制版独有的新增内容和开发钩子 |
| `audit/threeway_data.md` | 资源树、每个数据文件及其 schema、重制版引入的数据驱动规则 |
| `audit/threeway_platform.md` | JNI 入口点、Android 生命周期、清单（manifest）、打包、构建脚本、工具、测试、只能在设备上进行的检查 |
| `audit/Fights.md`, `AreasArmies.md`, `Scene.md`, `AI.md`, `CountriesTurns.md`, `audit/README.md` | 玩法公式：原版 vs 重制版 vs C#，哪些已修正、哪些被保留 |
| `DIFFERENCES.md` | 原版 vs C#（引擎层、平台层、其余有意保留的偏差） |
| `WC2/PORTING_GUIDE.md` "Engine semantics confirmed from the binary"（经二进制确认的引擎语义）一节 | 引擎对比 |

路径简写：`D\` = `Reference\decompiled\x86_64\`，`R\` = `Reference\WC2-C\app\src\main\cpp\code\`，
`G\` = `WC2\Game\`，`E\` = `WC2\Engine\`，`A\` = `WC2.Android\`。伪 C 函数通过其
`// ===== _ZN...` 头部注释引用，或以所在 `.c` 文件中的 `Class::Method` 形式引用。

结论分类：`identical`（相同：三者行为一致，或在具备该功能的两者中行为一致）、`intentional redesign`
（有意重新设计：重制版有意做了改动；该行会说明 C# 是否保留）、`missing in C++ remake`（C++ 重制版缺失）、
`missing in C#`（C# 缺失；子类别：有意不移植 / 意外缺失 / 平台替换、行为等价 / 未验证）、`bug in C#`
（C# 缺陷：C# 与另外两者都不同）、`unverified`（未验证）。标记为 **已于 2026-11-05 修复** 的行在分析开始时
属于 `bug in C#` 或 `missing in C#`，现已关闭。

---

## 1. 三方对照矩阵

### 1.1 运行时架构与引擎层

| 项目 | 原版 | C++ 重制版 | C# 移植版 | 结论 | 证据 |
|---|---|---|---|---|---|
| 代码库 | 一个原生库，146 个类文件中共 4 139 个函数 | 自有 `code/**`（30 k 行）+ 通过绑定器链接的、打过补丁的原版二进制文件；每个原版符号都有弱桩（weak stub），`code/**` 中的强定义优先 | 托管代码重新实现：`G\`（约 29 k 行，105 个文件），`E\`（约 5.6 k 行，25 个文件），宿主程序约 0.8 k 行 | 有意重新设计（两者皆是） | `index.tsv`；`R\..\CMakeLists.txt:145-205`；`original_easytech.h:10-19`；文件计数 |
| 引擎类（`GUIElement/Manager/MotionManager/Button/ButtonEx/Image/Text/ScrollBar`、`ecGraphics`、`ecImage`、`ecTextureRes`、`ecEffect*`、`CFightText`、`CCSoundBox`、`CSoundRes`、`ecMultipleTouch`、`ecLabelText`、`ecStringTable`、`ecFile`、`CStateManager`、TinyXML） | 二进制文件 | 二进制文件（经绑定器），上文所述六处覆盖除外 | `E\**`，逐条语句与伪 C 代码对齐（子元素优先的事件路由、按钮语义、ACTIVE/SHOWN 位、2.5/s 的淡入淡出及黑屏保持、16 个动作槽位与 WARNING 事件、滚动条几何、文本锚定、混合模式、图集“首次定义优先”、粒子系统、战斗文字） | 相同，有意偏差见 `DIFFERENCES.md` §2 | `PORTING_GUIDE.md` "Engine semantics confirmed from the binary"；`threeway_gamecode.md` §2.1 最后一行 |
| 重制版的引擎覆盖：`ecText`（4 字节 UTF-8，不截断居中文本）、`ecUniFont`、`ecTextureRes::GetImage/GetImageByFormat`、`RenderCircle` 64 位顶点修复、`RenderRing`、`GUILevelSel`、`GUIScrollList`、`GUIPagedList`、`GUIRadioButton` | `ecText` 将居中文本截断为 `int`；3 字节 UTF-8；`RenderCircle` 在 64 位上损坏；没有圆环，没有列表 | `R\supports\texts\*.cpp`、`R\gui\basics\ecGraphics.cpp`、`R\gui\GUI*List.cpp` | `E\Text\ecText.cs`、`ecUniFont.cs`、`E\Graphics\ecGraphics.cs:295-341`、`E\GUI\GUIListWidgets.cs`、`GUIButton.cs` | 与重制版相同（有意重新设计，C# 保留）；居中文本截断遵循原版（`(int)(width*0.5f)`） | `threeway_gamecode.md` §1.5、§1.9；`DIFFERENCES.md` §2 |
| 渲染后端 | OpenGL ES 1.1 固定管线（`GLESv1_CM`），4000 项顶点缓冲区，方向模式 2/3 会旋转 | 相同（二进制文件） | MonoGame `BasicEffect`，批处理和混合模式相同；未实现方向模式 2/3（Android 传入模式 0） | 平台替换，行为等价 | `DIFFERENCES.md` §1；`D\jni.c:878`（`ecGameInit(...,0,...)`） |
| 屏幕适配 | X 和 Y 独立缩放（拉伸） | 相同 | 等比缩放 + 黑色信箱边（letterbox），触摸偏移做相应补偿 | C# 中的有意重新设计（保留） | `E\Graphics\ecGraphics.cs:107-113`；`DIFFERENCES.md` §1 |
| 逻辑布局选择 | `nativeInit`：宽高比 > 1.8875 → 640x320，> 1.721875 → 568，> 1.584375 → 534，< 1.40625 → 1024x768 平板，否则 480x320；`g_contenscalefactor = 2` 恒定 | 相同（二进制文件） | 原为 `clamp(round(320*w/h),480,640)`，在 Android 上从不使用平板布局，`--ipad` 使用内容缩放 1（指向未随包发布的非 HD 文件）；**已于 2026-11-05 修复**：`ecGame.SelectLayout` 重现这些档位，各处缩放均为 2，Android 与桌面共用 | C# 缺陷 → 已修复 | `D\jni.c:842-882`；`G\Supports\ecGame.cs` `SelectLayout`；`WC2\Program.cs`；`A\MainActivity.cs`；已验证：`tests\ipad_tutorial.txt` 截图 |
| 平板 UI 图集 | `CLogoState::OnEnter`：`ui_iPad.xml`、`ui1_hd.xml`（hd）或 `ui1.xml`、`ui2_hd.xml`（非 hd）、`text_%s_iPad_hd.xml`（hd）、`text_%s_iPad.xml`、`image_newgame_hd.xml` | 相同（二进制文件） | 原为 `ui_iPad`、`text_%s_iPad`、`ui1_hd`、`ui2_hd`（hd）；**已于 2026-11-05 修复**为原版列表（另为语言表增加了英文回退） | C# 缺陷（次要）→ 已修复 | `D\CLogoState.c:104-166`；`G\Supports\ecGame.cs` `LoadGlobalGuiAtlases` |
| 帧步长 | `nativeRender`：dt 上限为 0.05 s | 相同 | 原为 0.1 s；**已于 2026-11-05 修复** | C# 缺陷 → 已修复 | `D\jni.c:926-942`；`WC2\Host\WC2Game.cs` `Update` |
| 暂停 / 等待标志 | `ecGamePause/Resume/Waiting` 设置两个标志；任一标志被设置时，跳过 `ecGameUpdate`，并由 `ecGameRender` 将画面压暗（`Fade(0.5)`） | 相同 | 只有一个标志，且只跳过更新；**已于 2026-11-05 修复**：两个标志，跳过更新，绘制 `Fade(0.5)`；桌面端窗口失去焦点时有意*不*设置暂停标志（未获得焦点的窗口继续运行，冒烟测试依赖这一点） | C# 缺陷 → 已修复（桌面焦点 = 有意偏差） | `D\global.c` `_Z11ecGamePausev`、`_Z12ecGameUpdatef`、`_Z12ecGameRenderv`；`G\Supports\ecGame.cs` |
| 返回键 | `CallNativeExit`：`ecBackPressed()`（先发 GUI BACK 事件，再调用 `CStateManager::BackPressed`）；返回 false 时，无论当前处于哪个状态，都在 MENU 状态对象上调用 `CMenuState::ShowExit` | 相同 | 仅当当前状态*就是*菜单时才显示退出框，否则结束 Activity / 退出桌面程序；**已于 2026-11-05 修复**：`ecGame.CallNativeExit` 移植了该 Java 入口点；Android 返回键、桌面 Esc/Backspace 以及测试脚本命令 `back` 都使用它 | C# 缺陷 → 已修复 | `D\jni.c:908-924`；`D\global.c` `_Z13ecBackPressedv`；`G\Supports\ecGame.cs`；已验证：`tests\out\back\K1_exitbox.png` |
| 后台 / 前台 | `nativePause` = `ecGameDidEnterBackground`（`CCommander::Save` + 各状态的 `EnterBackground`）+ `ecGamePause`；`nativeResume` = `ecGameWillEnterForeground`（重新应用音乐/音效音量、`ResumeMusic`、`EnterForeground`）+ `ecGameResume` | 相同；`CCommander::Save` 被清空（持久化改为在勋章变化时执行 `NewSave`） | 钩子存在，但只由轮询 `Game.IsActive` 驱动（Android Activity 暂停期间永远观察不到），且不重新应用音量；**已于 2026-11-05 修复**：移植了 `nativePause/nativeResume`（幂等），由 `MainActivity.OnPause/OnResume` 调用；重新应用音量；`Save()` 与重制版一样保持为空 | C# 缺陷 → 已修复（待设备检查） | `D\jni.c:892-906`；`D\global.c` `_Z24ecGameDidEnterBackgroundv`、`_Z25ecGameWillEnterForegroundv`；`A\MainActivity.cs`；`threeway_platform.md` §5 |
| Logo 状态 | 白屏，居中显示 `logo(_iPad).webp`，加载设置与 UI 图集，应用音量，2.1 s，`FadeOut(-1)`，1 s，`CPlayerManager::Init`，进入 MENU | 二进制文件 | 原为自由发挥的重新实现（黑屏、点击跳过、2.0 s）；**已于 2026-11-05 修复**：按伪 C 代码移植（图集/设置的加载位于 `ecGameInit` 中） | C# 缺陷 → 已修复 | `D\CLogoState.c`；`G\GUI\States\CLogoState.cs` |
| 纹理 | libpng（补齐到 2 的幂、RGB565 选项）、WebP、PVRTC、ETC1 | + 围绕二进制加载器的 `func_AutoPicFormat.cpp` 扩展名回退链 | SkiaSharp（PNG/WebP/JPG）+ 自研 ETC1 解码器；`ResolveTextureFile` 先尝试 `@2x`，再依次尝试 png/webp/pkm/pvr/jpg；不支持 PVRTC（未随包发布该格式的文件） | 平台替换，行为等价 | `E\Graphics\ecGraphics.cs:400-441`；`R\supports\func_AutoPicFormat.cpp`；`DIFFERENCES.md` §1 |
| 音频 | Java `ecSound`（SoundPool）/ `ecMusic`（MediaPlayer），经由 `*JNI` | 相同 | MonoGame `SoundEffect`（OpenAL）；MP3 由 MP3Sharp 在内存中解码（NLayer 作为回退；单用 NLayer 会把 MPEG 2.5 8 kHz 音轨分帧错误 → 产生“喇叭声”噪音）；`--test-audio` 自检 | 平台替换，行为等价（通过与 Media Foundation 解码结果的样本相关性 1.000 验证） | `E\Audio\CCSoundBox.cs`；`WC2\Host\DebugTools.cs`；`README.md` 中的音频说明 |
| Java 渲染的标签文字（`CreateTextureWithString`） | 通过 JNI 使用系统字体 | 相同 | 不可用：`ecLabelText` 回退到带自动换行的位图文字字体 | 平台替换（细微的视觉差异） | `DIFFERENCES.md` §1 |
| XML 解析器 | TinyXML（严格） | 相同 | 基于 `System.Xml.Linq` 的外观层，可容忍随包发布文件中两行格式错误的内容（`carddef.xml:4,7`） | 平台替换，行为等价 | `threeway_data.md` §2.2 |
| 存档文件（`game%d.sav`、`conquest%d.sav`、`commander.sav`、`settings.cfg`） | 打包的 C 结构体（头部 196、国家 1300、地区 200、部队 44 字节；将领 95 个字（word），魔数 1161909081） | 经由二进制文件采用相同布局；`settings.cfg` 扩大到 40 字节，`CCommander::Save` 为空 + `NewSave` | 显式小端布局，大小相同；地区记录将单位数上限设为 4，而原版可以写入 5 个；设置和将领与重制版一致 | 平台替换；与真实应用所写文件的兼容性**未验证** | `G\GameStages\SaveIO.cs`；`DIFFERENCES.md` §1 "Saves"；`threeway_platform.md` §6 |
| 随机数流 | 由 `rand()` 生成随机种子，未调用 `srand`（每个进程内是确定性的） | `lrand48()`，未调用 `srand48`（确定性） | `System.Random` 以时间作为随机种子（每次启动都不同） | 严格意义上属于 C# 缺陷；**有意保留**（原版与重制版的确定性是偶然的；冒烟运行不依赖于它） | `D\global.c:1616-1618`；`R\supports\ecGame.cpp:108-109`；`E\Core\Globals.cs:69-83` |
| 资源查找顺序 | 先根目录，再 `<LangDir>/file` | 相同 | 先语言文件夹，再根目录 | 平台替换，行为等价（没有文件同时存在于两处，已在 APK 上验证） | `D\global.c:64-89`；`E\Core\Globals.cs` `GetPath` |
| `game.xml` 开发导出路径 | `CGameManager::SaveBattle` 存在，但从未被调用 | 暂停框中的“保存”调用 `SaveBattle("game.xml")`；`GetPath("game.xml")` 被重定向到 `/storage/emulated/0/Download/` | 保留导出；路径落在了资源根目录；**已于 2026-11-05 修复**：重定向到文档文件夹（`%LOCALAPPDATA%\WC2CSharp` / `files/documents`） | 平台替换 | `R\supports\ToolBox.cpp:131-141`；`E\Core\Globals.cs` `GetPath`；`G\GUI\InGame\CGameStateEvents.cs:131-137` |
| 日志 | `__android_log_print`，标签 `WC2`（13 处） | 没有自己的日志 | `Globals.Log` → 控制台、调试器、可选的 `--log FILE`；每个宿主程序回调都有异常保护 | C# 中的有意重新设计 | `threeway_platform.md` §7 |

### 1.2 状态与界面

| 项目 | 原版 | C++ 重制版 | C# 移植版 | 结论 | 证据 |
|---|---|---|---|---|---|
| `CLogoState` | 9 个函数 | 二进制文件，会被执行到 | 忠实移植（见 1.1） | 已修复 | `threeway_gamecode.md` §2.1 |
| `CMenuState`、`GUIMainMenu`、`GUIExitWarning`、`GUILockedWarning`、`GUISelBattle`、`GUIBattleList/Item`、`GUIEnd`、`GUICommander` 系列（5 个类）、`GUICountryList/Item`、`GUISelCountry` | 原版界面 | 重新实现；征服模式扩展（新增 `GUIConquestList/Intro/CountryIntro`，`ActualConquestMap`），解锁全部战役 | 名称相同，资源名、字符串键、`case`/`FadeOut`/`OTHER` 编号均相同 | 与重制版相同（重制版改动了界面之处属于有意重新设计） | `threeway_gamecode.md` §1.7（名称 n/n 一致，字面量无差异） |
| `CLoadState`、`GUILoading` | 原版 | 11 + 5 个函数；EDIT 切换被注释掉 | 相同 | 相同 | `threeway_gamecode.md` §1.7 |
| `CGameState`（事件 + 生命周期），游戏内面板 `GUIAIProgress`、`GUIAttackBox`、`GUIBank`、`GUIBattleIntro`、`GUIBegin`、`GUIDefeated`（+`autoCloseTimer`）、`GUIDialogue`、`GUIGold`、`GUIMedal`、`GUIPauseBox`、`GUIResult`、`GUITax`、`GUITech`、`GUIVictory`、`GUIWarning`、`GUISelArmy/ArmyItem`，卡牌（`GUIBuyCard`、`GUICard`、`GUICardList`、`GUICardTab`、`GUISmallCard`），`GUISave/SaveItem` | 原版 | 重新实现（`OnExit` 委托给二进制文件） | 相同；`OnExit` 序列根据 `D\CGameState.c` 重新实现 | 相同 | `threeway_gamecode.md` §1.6 |
| `GUIOptions` | 固定布局，5 项设置 | XML 布局 `guioptions.xml`，选项 `newConquestLoading`、`aiHosting`，分页列表 | 相同 | 有意重新设计，C# 保留 | `R\gui\ingame\GUIOptions.cpp:496-700`；`G\GUI\InGame\GUIOptions.cs:273-470` |
| `GUITutorails`（教程覆盖层，13 条命令，6 个教程脚本文件） | 13 个函数，5 767 B | **二进制文件，会被执行到**（`GUITutorails.cpp` 为空；由 `CGameStateLifeCycle.cpp:217-224` 创建，由菜单的教程按钮启动） | 原为一个只有 15 行的空元素（教程启动后什么都不发生）；**已于 2026-11-05 移植**自 `D\GUITutorails.c`（按宽度选择脚本，SEL_AREA..EXIT，手指/遮罩/引导员/对话框，WAIT 以一次点击结束，EXIT → `FadeOut(9)`），由 `tests\tutorial.txt` 和 `tests\ipad_tutorial.txt` 验证 | 原为 C# 缺失（意外缺失）→ 已移植 | `G\GUI\InGame\GUITutorails.cs`；`tests\out\T1..T4_tutorial.png` |
| `GUIBattle` + `CBattleScene` + `CBattleUnit` + `ecLibrary/ecLayer/ecFrame/ecElement`（战斗动画，约 22 KB） | 当 `g_GameSettings.battleAnimation` 为真时播放 | **不会被执行到**：重制版的 `CGameState::StartBattale` 立即结算战斗，从不调用 `GUIBattle::BattleStart`（该标志被注明为未使用）；面板仍会创建，选项仍会显示，动作数据仍由二进制文件解析 | 桩面板，加载器为空，选项保留但不起作用 | **C++ 重制版缺失**，C# 继承了这一点 | `R\fights\FightLogics.cpp:10-23`；`R\gui\ingame\CGameStateLifeCycle.cpp:202-214`；`G\GUI\Stubs\NetworkIapStubs.cs:137-161`；`G\BasicElements\CObjectDef.cs:361-368` |
| `GUIIap` + `GUIProductPrice`（勋章商店）、`GUIService`、`GUINewGame`（推广，13 KB 依赖 JNI 的 UI） | 原版 | 二进制文件，会被执行到（`OTHER 13/20/27`），Java 后端缺失 | 单行桩面板，保留入口点 | C# 缺失：有意不移植 | `G\GUI\Stubs\NetworkIapStubs.cs:40-90`；`PORTING_GUIDE.md` "Out of scope" |
| 多人游戏：`CPlayerManager`（67 个函数）、`CPlayer`、`ecSessionInterface`、`ecGameKit`、`Packet*`、`GUIHostList/HostItem/ConnectHostState/MatchSetup/CorpsButton/CorpsInfo/ActionInfo`、`CMatchState` | 原版 | 二进制文件，通过 `g_PlayerManager.*` 被执行到；没有 Java 会话层就无法组成对局 | 空操作的数据类和桩面板；保留菜单入口 | C# 缺失：有意不移植 | `G\Supports\CPlayerManager.cs`；`threeway_gamecode.md` §2.1 |
| `CEditState`（地图编辑器） | 14 个函数 | 二进制文件，**死代码**（唯一的切换入口被注释掉） | 返回 MENU 的桩 | C++ 重制版（不可达）和 C# 均缺失 | `R\gui\loading\CLoadState.cpp:172-179` |
| `CheckMD5` + `MD5`（`ecFile` 内的资源完整性校验） | 24 KB | 二进制文件，间接调用 | 无 | C# 缺失：有意不移植（防篡改） | `D\ecFile.c:340-380`；`D\CheckMD5.c` |

### 1.3 玩法规则

C# 中的每个函数都是对重制版的逐语句移植（在 C# 中按名称找到了 975 个重制版函数，字面量和流程编号均相同；
见 `threeway_gamecode.md` "How the comparison was made"）。因此，下面各行说明的是重制版与原版的关系，以及 C# 是
遵循重制版，还是在 2026-11-05 被修正为原版的做法（`audit/README.md` 完整列出了这两组）。

| 规则组 | 原版 | C++ 重制版 | C# 移植版 | 结论 | 证据 |
|---|---|---|---|---|---|
| 骰子数量阈值、伤害 ≥ hp 时不掷骰、攻击方先掷、被清空地区的处理顺序、雷达减半按整数计算、轰炸衰减按整数计算、勋章掷骰阈值/随机流/地区、伤害低于 20 时不得勋章 | 原版规则 | 逆向工程错误 | 修正为原版（带 `original:` 注释） | 相同（修正后） | `Fights.md` B1-B14；`audit/README.md` "corrected" |
| 反击射程、伤害倍率（×1.4 → 数据 130 %/150 %）、不含海军系数的经验表 `{90,150,200,300}`、航母打击的经验以及无限速率下的即时击杀、按特性决定的要塞第二次反击 | 硬编码的表 | 数据驱动（`armydef.xml` 中的配置） | 保留 | 有意重新设计，C# 保留 | `Fights.md`；`threeway_data.md` §3 |
| 跨堑壕移动 | 只有坦克 / 重型坦克保留移动力 | 另有六种单位具备该特性（火炮、火箭炮、驱逐舰、巡洋舰、战列舰、航母） | 数据及旧版默认值修正为原版 | 相同（修正后）；移植版唯一的资源修改 | `Fights.md` B4；`WC2\assets\armydef.xml` 第 83-169 行 |
| 海上移动限制、机场治疗等级、最后一个地区的征服、抱怨条件、海上移动的移动力消耗、工业降低分支、装载单位的最大 hp、移到前排的顺序、征兵像素对齐、`army` 失败类型下的存活判定、`AirstrikeRadius`、卡牌支付、国家战败判定 | 原版规则 | 错误 | 已修正 | 相同（修正后） | `AreasArmies.md`、`CountriesTurns.md`、`audit/README.md` |
| 包围仅在首都/大城市旁生效、每个海上地区最多四艘舰船、`BeConquestedBy` 将土地交给征服者、收入上限 99999、勋章补足、中立国在开局时被标记为战败、`core` 失败类型、科技等级不受限制、单位以移动力 0 开局、对话默认值 | 原版规则 | 重新设计 | 保留 | 有意重新设计，C# 保留 | `AreasArmies.md`、`CountriesTurns.md` B1/B12 |
| 镜头（缩放限制、82 px 边距、dt 缓动）、数据驱动的选择圈与火炮两跳规则、通过载荷进行的航母打击、轰炸机抖动随机流、`CScene.Update` 只为运行中的地区播放动画、`InitAreaImage` 泛化 | 原版 | 重新设计 | 保留 | 有意重新设计，C# 保留 | `Scene.md` |
| AI：以目标为键的 +40 玩家目标加成、`sortArmyToFront`、按卡牌价格决定征兵顺序、单位类型加成 → `AddAiDamageTendency`、跳过 ALLY 目标、每张卡牌都有冷却、考虑火炮和机场惩罚的征兵布置 | 原版 | 重新设计 | 保留 | 有意重新设计，C# 保留 | `AI.md` B1-B13 |
| AI：有符号的 `getMaxId`、`currentCountryIndex` 重置、`getMedal` 阈值与随机流、`getAiPercent` 计数、`TypeAdd` 表、系数的整数强制转换、0x3000 评分公式、卡牌位掩码、`calcAreaArmyHp(area,false)`、随机区间、轰炸机 → 空袭回退、工业权重、战术区间 | 原版 | 错误 | 已修正 | 相同（修正后） | `AI.md`；`audit/README.md` |
| 剧本联盟字母 | `a/b/c` → 1/2/3，其他一律 NEUTRAL | 26 个字母，`'d'` → NEUTRAL，**`'n'` → 14**（第 14 个交战联盟；`battle_allies7/8`、`battle_axis2/3`、`battle_nato1/2`、`battle_wto3`、`conquest_4/7` 中随包发布的 15 个中立国家会轮流行动并且可以被攻击） | 继承了该缺陷；**已于 2026-11-05 修复**：`a/b/c` → 1/2/3，其他一律 NEUTRAL（重制版的写出器同样会为中立输出 `"n"`，因此导出文件又能正确往返） | C++ 重制版缺陷被继承 → 已在 C# 中修复 | `D\CGameManager.c` `LoadBattle`；`D\GetBattleBelligerentList.c:71-84`；`R\gameStagesBehaviors\LoadBattleFromXml.cpp:21-29`；`G\GameStages\LoadBattleFromXml.cs` `ParseAlliance` |
| 解锁全部战役（`GetNumPlayedBatttles` 返回 `SERIES_MAX` = 20） | 返回 `campaignProgress[series]` | 开发/作弊性质的选择 | 保留 | 有意重新设计，C# 保留（如需按进度解锁，改回只需一行） | `R\supports\CampaignSize.cpp:155-161`；`G\BasicElements\CCommander.cs:290` |
| 勋章变化时自动保存将领（防文件作弊）、`GUIDefeated.autoCloseTimer`、`SkipMode`、`CombatSpatialIndex`、`CanContainArmy`、`calcAreaArmyHp`、精英步兵（id 15，卡牌 28）、真实轰炸机（id 16，仅数据）、卡牌标签页、按国家区分的卡牌价格/科技 | 无 / 固定 | 重制版新增 | 全部具备 | 与重制版相同 | `threeway_gamecode.md` §3 |

### 1.4 数据文件与 schema

| 项目 | 原版 | C++ 重制版 | C# 移植版 | 结论 | 证据 |
|---|---|---|---|---|---|
| 资源树 | APK 资源（`CheckMD5` 列出的是较旧的一代：681 条路径，其中 427 条未随包发布） | 1 111 个文件，HD/webp/pkm 套件 | 相同的 1 111 个文件，除 `armydef.xml`（删除了六个 `cross-entrenchment` 标记）外逐字节相同 | 相同（一处有意的数据修正） | `threeway_data.md` §1（哈希比对） |
| `armydef.xml` | 15 种固定类型，按国家的列表，只有 `strength/movement/minatk/maxatk` | `army-types`、战斗配置（`attack/counter/range/damage/result`）、特性、`weapon-armor-rules`、`unit-type`、`navy`、`image`、`country-independent`、`legacy-combat-defaults`、按类型的 `others` 回退 | 相同的解析器（`ArmyDefLoader/Parsing/CombatRules/Store.cs`） | 有意重新设计，C# 保留；随包发布的文件无法被原版读取 | `threeway_data.md` §2.1；`D\CObjectDef.c` `LoadArmyDef` |
| `carddef.xml` | 扁平的 `<cards><card>`，28 个槽位，`round`/`tech` 默认为 1 | `<country call>` 分组，任意 id，`relatedArmyId`，`maxTimes`，默认为 0 | 相同 | 有意重新设计，C# 保留 | `threeway_data.md` §2.2 |
| 剧本 XML（`battle_*.xml`、`conquest_*.xml`） | `LoadBattle` | `LoadBattleScenarioData` 预解析 + 实时加载器；`core` 失败类型；移动力 0；对话默认值 | 相同；联盟字母现已与原版一致（见上文） | 除所列的重新设计外相同 | `threeway_data.md` §2.3 |
| `battlelist.xml`、`conquestlist.xml`、`commanderdef.xml`、`generalphotos.xml` | 读取 | 属性/默认值相同 | 相同 | 相同 | `threeway_data.md` §2.4 |
| `motiondef.xml`、`unitposdef.xml`（战斗动画数据） | 解析 | 由二进制文件解析（`CObjectDef::LoadUnitMotions/LoadUnitPositons`），从不使用 | 空加载器 | C# 缺失：有意不移植（重制版中同样缺少使用方） | `R\basicElements\CObjectDef.cpp:403-409`；`G\BasicElements\CObjectDef.cs:361-368` |
| 地图数据（`area%d.bin`、`adjion%d.bin`、`areamark%d.raw`、`areatax%d.xml`、`areasenable` bin 文件、分区（zone）图集、背景地图贴图块） | 地图 1 硬编码 | 泛化为 `MapID`，增加分区重试流程 | 相同 | 相同 / 有意重新设计并保留 | `threeway_data.md` §2.5 |
| `m1_conquest1.xml`、`m1_conquest2.xml` | 在征服 1 / 2 中加载 | 从不加载 | 从不加载 | C++ 重制版缺失，行为等价（这些文件与分区图像重复；重试流程覆盖了它们） | `threeway_data.md` §2.5 |
| 单位美术查找（`army_hd.xml`） | 九个国家，`_ne` 回退，四种底色 | 34 个国家的探测，`others` 回退，自定义 `image`，灰色底色由 `GetAllianceColor` 着色 | 相同 | 有意重新设计，C# 保留（重制版相应修改了图集） | `threeway_data.md` §2.6 |
| 战斗背景图集 | HD ≤480 `_hd`（hd），≤568 `_568hd`（hd），否则 `_640hd`；平板 `_iPad` | ≤568 分支请求 `_568h`（不存在），hd 标志不同 | 继承；**已于 2026-11-05 修复**为原版的名称/标志 | C++ 重制版缺陷被继承 → 已修复（目前没有使用方） | `D\CGameRes.c:3925-3960`；`G\Render\CGameRes.cs` `LoadRes("battlebg…")` |
| `settings.cfg` | 32 字节，速度限制为 0..5 | 40 字节（+`useNewConquestLoading`、`aiHosting`），限制为 0..4，接受旧文件 | 相同 | 有意重新设计，C# 保留 | `threeway_data.md` §2.7 |
| 字符串、字体 | `Localizable.strings`、`stringtable_*`、字体集 | + Kai/Hei（楷体/黑体）字体，`num7`，`g_introTextScale` | 相同 + `EnsureFallbackFonts` | 有意重新设计，C# 保留 | `threeway_data.md` §2.7 |
| `guioptions.xml` | 不适用 | 选项布局 | 属性相同 | 有意重新设计，C# 保留 | `threeway_data.md` §2.7 |
| `map.ini`、`editres.xml`、`conquest.xml`、`tutorails.xml/.bin`、`iap_extra.xml`、`armydef-bak.xml`、`resolutionColumns.xml` | 编辑器 / 旧版 / 教程 / IAP / 调试 | 未读取，或由二进制文件读取 | 未读取（教程脚本现已读取） | 相同 / 有意不移植 | `threeway_data.md` §2.8 |

### 1.5 平台、打包、构建、工具

| 项目 | 原版 | C++ 重制版 | C# 移植版 | 结论 | 证据 |
|---|---|---|---|---|---|
| ABI | arm64-v8a、armeabi-v7a、x86、x86_64 库 | 打包 armeabi-v7a、x86、arm64-v8a | 仅 arm64-v8a（桌面：任意 CPU） | C# 缺失：有意收窄（明知而保留） | `threeway_platform.md` §1 |
| SDK 级别、包 id、版本 | compile 28，`com.easytech.wc2` 15 / 1.3.8 | min 19 / target 28，id 与版本相同 | min 23 / target 35，`com.wc2remake.csharp`，versionCode 1；显示版本原为 1.0，而 `getVersionName()` 返回 1.3.8 → **已于 2026-11-05 修复**：显示版本为 1.3.8 | 有意重新设计（新 id，较新的 target）；版本字符串现已一致 | `A\WC2.Android.csproj` |
| 权限 | BILLING、网络、存储 | 清单相同 | 无（没有任何功能需要） | 有意重新设计，C# 保持最小化 | `threeway_platform.md` §2 |
| Activity / 主题 / 系统 UI | `Wc2Activity`、`Theme.*.NoTitleBar.Fullscreen`、`hideSystemUI` 沉浸式粘性模式、刘海 meta-data | 相同 | 原先没有主题，也没有窗口标志；**已于 2026-11-05 修复**：`Theme.Black.NoTitleBar.Fullscreen`，获得焦点时重新应用沉浸式粘性标志，`LayoutInDisplayCutoutMode.ShortEdges`，`KeepScreenOn`（移植版新增） | C# 缺失（意外缺失）→ 已修复（待设备检查） | `A\MainActivity.cs` `ApplyWindowFlags/HideSystemUi` |
| 生命周期钩子 | `onPause/onResume` → `nativePause/nativeResume` | 相同 | **已于 2026-11-05 修复**（见 1.1） | 已修复（待设备检查） | `A\MainActivity.cs` |
| 启动器图标与标签 | 5 种密度 × 2 个图标，6 种语言区域的字符串 | 相同 | 104 字节的占位图标，硬编码的标签；**已于 2026-11-05 修复**：复制了原版图标（`appicon`、`appicon_round`），`@string/app_name` 提供 en/ja/ko/ru/zh-rCN/zh-rTW | C# 缺失（意外缺失）→ 已修复 | `A\Resources\mipmap-*`、`A\Resources\values*\strings.xml`；APK 文件清单（十个图标，`resources.arsc` 2 272 字节） |
| 资源访问 | 直接从 APK 中原地读取 | 相同 | 在第一帧之前一次性解压到 `files/game`（136 MB） | 平台替换，行为等价；首次启动的开销未在设备上验证 | `A\MainActivity.cs` `AssetExtractor` |
| 文档文件夹 | Java 提供的路径 | 相同 | `files/documents` / `%LOCALAPPDATA%\WC2CSharp` | 平台替换 | `threeway_platform.md` §6 |
| 语言选择 | Java 区域设置 → `LangDir` | 相同 | UI 区域性 → `zh_TW/zh_CN/ja/kr/ru/English`，可用 `--lang` 覆盖 | 平台替换，行为等价（Java 中的确切分支未验证） | `G\Supports\ecGame.cs:31-45` |
| 签名 | 厂商密钥 | 调试密钥 | 调试密钥（`AndroidKeyStore=false`） | 状态相同（无法上架商店） | `threeway_platform.md` §8 |
| 构建脚本 / 固定版本的工具链 | 不适用 | `build.ps1`/`build.bat`，带依赖检查，SHA 固定的 Gradle 7.3.3，AGP 7.2.2，NDK r16b | 原先使用浮动版本 `MonoGame.Framework.* 3.8.*` 且没有脚本；**已于 2026-11-05 修复**：版本固定为 3.8.6.1，新增 `A\build-apk.ps1`（工作负载检查、构建、复制到仓库根目录） | C# 缺失（意外缺失）→ 已修复；三者均无 CI | `A\build-apk.ps1`；两个 `.csproj` |
| 测试 | 无 | 无（声明了 junit/espresso，但没有源码） | 桌面：音频自检 + 12 个脚本化运行（`tests\run-tests.ps1`），截图；Android 上没有 | 原版与重制版缺失；C# 仅限桌面（Android 未验证） | `WC2\tests\` |
| 调试工具 | 无 | 已失效的 Java 调试 Activity，`font_tool.py` | `--decode-pkm --test-xml --test-fonts --test-audio`，测试脚本驱动器，`--log`；没有字体生成器 | 有意重新设计；`font_tool.py` 有意未移植 | `threeway_platform.md` §7 |
| 依赖 JNI 的功能（`AddMedal`、`PurchaseSuccess`、`nativeSetProductPrice`、`ecPromotion` 新提示、`MainMenuLoadedJNI`、`ecMailServiceJNI`、`GetDataFromServerJNI`、`isAndroidEmulator`、`ecGetUUID`） | Java | 二进制文件（Java 端已失效） | 空操作 / `false` | C# 缺失：有意不移植 | `E\Core\Globals.cs:105-136`；`threeway_platform.md` §9 |
| `ecShowReview`、`ecAccelerometer`、`ecSetIdleTimerDisabled`、`ecIs/SetFullScreen` | 空的或未被调用的导出函数 | 相同 | 空操作；桌面上 `ecSetFullScreen` 绑定到 F11 | 相同（各处均为空操作） | `threeway_platform.md` §4、§9 |

---

## 2. C++ 重制版 → C# 移植版：C# 缺少的内容或做法不同之处

重制版在 `code/**` 中实现的一切都以相同的名称、相同的常量存在于 C# 中（`threeway_gamecode.md` §1）。差异如下：

**2.1 重制版从二进制文件运行、由 C# 替代的原版类**

| 项目 | C# 中的状态 | 类别 |
|---|---|---|
| `GUITutorails` | 2026-11-05 根据伪 C 代码移植，已在桌面上验证（手机与平板布局） | 已关闭 |
| `CLogoState` | 2026-11-05 移植 | 已关闭 |
| `GUIIap`、`GUIProductPrice`、`GUIService`、`GUINewGame` | 桩面板（"not available"，不可用） | 有意不移植 |
| `CPlayerManager`、`CPlayer`、会话/数据包类、大厅与军团面板、`GUIActionInfo`、`CMatchState` | 空操作和桩面板，流程保留 | 有意不移植 |
| `CObjectDef::LoadUnitMotions/LoadUnitPositons/Get*/Release*` | 空 | 有意不移植（重制版中同样没有使用方） |
| `CheckMD5`、`MD5` | 不存在 | 有意不移植 |
| `CEditState` | 桩 | 在重制版中同样不可达 |
| `CActionAI::getNumArmyMove`、`CFight::GetAttackAdd/GetDefendAdd`、`CScene::RenderAreas*`、`CGameManager::Init/NextBattle`、`GUISelCountry::SelBattle`、`CCountry::GenCardTargetList` | `=> 0` / 不存在 | 效果相同（在发布版二进制文件中是 1-5 字节的桩或死代码） |

**2.2 有意没有 C# 对应物的重制版源码**

`CMenuStateBridge.cpp`（C++ ABI 胶水代码）、`easytech_global.cpp`（空）、`func_AutoPicFormat.cpp`
（由 `ResolveTextureFile` 替代）、`GetActualMap`（类型转换辅助函数）、`killAllEcImages`（删除辅助函数）、
`CGameRes::RenderArmyWithDef` 和 `SetOptionLevelFromTouch`（均为 `#if 0`）、`CScene::RenderSeaAreas`
（调试覆盖层，从未被调用）、`MapExtension.cpp` 中的两行 `printf`、被注释掉的
`GameButtonDI` 按钮、`ShowDiplomaticDialog`、`com_easytech_wc2_TestActivity.h` 钩子，以及
`CLoadState.cpp` 中被注释掉的 EDIT 切换（`threeway_gamecode.md` §3）。

**2.3 资源**

1 111 个游戏资源除 `armydef.xml` 的修正外完全相同。重制版的 Android 资源（图标、六个 `strings.xml`、样式）
原先在 C# 项目中缺失，现已被复制（图标、`app_name`）或以 Activity 主题的形式表达。未迁移的有：`patch.aar`
（一个“云存档”模组（mod）Activity，重制版的清单并未引用它）、由 `resolutionColumns.xml` 驱动的 Java 调试
对话框（已从重制版构建中排除）、`raw/sub.srt`、`public.xml`/`ids.txt` 稳定 id（只有预编译的
`easytech.jar` 需要）。

**2.4 数据驱动规则**

重制版移入数据中的每条规则，C# 都以相同的语义使用（`threeway_data.md` §3：特性、配置、伤害/武器规则、
结果规则、AI 加成、海军标志、失败类型、卡牌表、对话默认值、选项布局、征服编号）。有两处被*朝原版方向*
修改：`cross-entrenchment` 特性数据及旧版默认值，以及联盟字母规则（`'n'` 为中立）。

**2.5 开发钩子**

* `GetPath("game.xml")` → Download 文件夹：替换为文档文件夹（今日已修复）。
* 暂停框中的 `SaveBattle("game.xml")`：保留。
* 勋章变化时自动保存将领：保留。
* 解锁全部战役：保留。
* `RenderSeaAreas`、`printf`、`GameButtonDI`、JNI 测试钩子：在重制版中是死代码，未移植。

**2.6 构建脚本、打包、测试**

| 重制版 | C# | 状态 |
|---|---|---|
| `build.ps1`/`build.bat`，带 JDK/SDK 自动查找、依赖检查、离线/清理开关；文档 `BUILD.en.md`/`BUILD.zh-CN.md` | `WC2.Android\build-apk.ps1`（今日新增），`README.md` 的 Android 一节 | 已关闭（范围较小：没有清理/离线开关） |
| Gradle wrapper 经 SHA 固定，AGP/NDK 版本固定 | NuGet 版本固定（`MonoGame.Framework.* 3.8.6.1`、`SkiaSharp 3.119.2`、`NLayer 1.16.0`、`MP3Sharp 1.0.5`）；没有锁文件 | 已关闭（锁文件可选） |
| 三种 ABI | 仅 arm64 | 明知而保留 |
| `applicationId com.easytech.wc2`，版本 15 / 1.3.8 | `com.wc2remake.csharp`，1 / 1.3.8 | 有意为之（不与真实游戏冲突） |
| 清单权限、`max_aspect`、刘海 meta-data | 无 / 在代码中设置 cutout 模式 | 有意为之 / 已关闭 |
| 没有测试 | 桌面回归测试套件，没有 Android 测试 | C# 领先；Android 未验证 |

**2.7 重制版的引擎修复及其在 C# 中的状态**

`ecText` 4 字节 UTF-8（保留），`RenderCircle` 64 位修复（托管代码中不需要，顶点相同），
`RenderRing`（保留），`AutoPicFormat` 回退（以 `ResolveTextureFile` 的形式保留），`ecUniFont` 记录布局
（保留），`ecTextureRes::GetImage` 行为（保留），`GUILevelSel/ScrollList/PagedList/RadioButton`（保留），
`CampaignSize` 每个系列 20 场（保留），防文件作弊自动保存（保留）。

---

## 3. 原版 → C++ 重制版：重制版删除或重新设计了什么，以及 C# 如何处理

| 原版功能 | 重制版 | C# |
|---|---|---|
| 战斗动画（`GUIBattle`、`CBattleScene`、动作数据） | 删除（从不启动；选项和数据加载留在原处） | 与重制版相同；需要根据伪 C 代码移植（不存在 C++ 源码） |
| 教程 | 保留（二进制代码） | 今日已移植 |
| Logo 界面 | 保留（二进制） | 今日已移植 |
| 地图编辑器 | 不可达 | 桩 |
| IAP 商店、客服、推广、评价提示、新提示角标 | 二进制 UI，没有可工作的 Java 端 | 桩 / 空操作 |
| 多人游戏 | 二进制，无法使用 | 空操作 |
| 资源完整性校验 | 仍在二进制文件内运行 | 不存在 |
| 基于进度的战役解锁 | 所有战役均已解锁 | 保留（全部解锁） |
| 硬编码的战斗表、反击表、选择圈、两跳规则、航母打击类型 4 | `armydef.xml` 中数据驱动的配置 | 保留 |
| ×1.4 装甲/步兵倍率 | 130 % / 150 % 数据规则 | 保留 |
| 经验阈值与海军系数 | 表已更改，系数被移除 | 保留 |
| 单一扁平卡牌表 | 按国家区分的卡牌价格/科技，按价格决定的征兵顺序，精英步兵卡牌 | 保留 |
| 15 个固定单位 id，九国美术 | 采用动态 id 的注册表，34 国美术探测，自定义图像 | 保留 |
| a/b/c 联盟，其余为中立 | 26 个字母（`'n'` 变成了联盟 14） | 修正为原版规则 |
| land/army 失败类型 | + `core` | 保留 |
| 科技等级限制、海上单位开局移动力、对话未初始化 | 不受限制、0、默认值 | 保留 |
| 包围、每个海上地区的舰船数、`BeConquestedBy`、收入上限、勋章支付、中立国回合处理 | 重新设计 | 保留 |
| 镜头限制与边距 | 放开 | 保留 |
| AI 目标加成、征兵顺序、单位类型加成表、盟友评分、冷却检查、布置 | 重新设计 | 保留 |
| 八个固定征服（带旗帜预览） | 扫描得到的征服、真实地图预览、介绍面板 | 保留 |
| 固定的选项页 | XML 布局 + 两个新选项 | 保留 |
| 32 字节设置，速度限制 5 | 40 字节，限制 4 | 保留 |
| 字体 | + Kai/Hei、`num7` | 保留（+ 回退） |
| 每次变化都执行 `CCommander::Save` | 清空；勋章变化时执行 `NewSave` | 保留 |
| 地图 1 的分区图集加载 | 泛化，增加重试流程，删除征服覆盖层 | 保留 |
| 战斗背景图集名称 | 一个名称错误 | 修正为原版 |
| `audit/README.md` 中列出的骰子/掷骰/勋章/移动/AI 细节 | 逆向工程错误 | 修正为原版 |

---

## 4. C# 移植版：缺失的优势与内容（按类别）

**有意不移植**（记录于 `README.md` "Status / known gaps" 和 `PORTING_GUIDE.md` "Out of scope"）：应用内
勋章商店及价格、客服邮件与从服务器获取数据、推广 /“更多游戏”面板及其网页链接、评价提示、多人游戏（会话层、
大厅、军团、对局状态、UUID）、地图编辑器、资源完整性校验、战斗动画数据加载器（以及动画本身，重制版同样
没有）、32 位 ARM 和 x86 安装包、`BILLING`/网络/存储权限、重制版的 `font_tool.py`、重制版已失效的 Java
调试 Activity 以及 `patch.aar`。

**意外缺失——由本次分析发现并于 2026-11-05 关闭**：教程覆盖层；忠实的 Logo 界面；Android 上的布局档位和
平板布局；平板布局的 HD 内容；平板 UI 图集列表；帧步长上限；带画面压暗效果的暂停/等待标志；返回键语义
（`CallNativeExit`）；回到前台时重新应用音量；Android `onPause/onResume` 钩子；沉浸式全屏窗口、刘海
（cutout）处理；启动器图标和本地化标签；显示版本；中立联盟字母；`game.xml` 导出位置；战斗背景图集名称；
固定的包版本；构建脚本。此类别中仍未关闭的：目前已知无。

**平台替换，行为等价**：渲染后端、屏幕适配（以信箱边替代拉伸）、纹理加载与格式回退、音频栈、XML 解析器、
存档文件 IO、将资源解压到 files 文件夹、文档文件夹、语言选择、资源查找顺序、触摸 id 分配、退出/URL 钩子、
日志、`game.xml` 位置。

**未验证**（需要设备或真实游戏的文件）：Android 生命周期钩子与后台保存、后台时的音乐、退出时结束 Activity、
刘海屏/20:9 屏幕上的沉浸式系统栏、各设备类别的布局、首次启动的解压时间、API 33+ 上的返回/手势导航、通过
`TouchPanel` 实现的双指缩放、区域设置映射、加载由真实应用写入的 `settings.cfg`/`commander.sav`/`game6.sav`
（地区记录的单位上限 4 vs 5）、GLES 性能以及带着 22.6 MB 程序集 blob 的启动、重制版本身是否为 arm64 打包了
`modlibeasytech.so`、Java 摇晃传感器的使用方。

---

## 5. 未验证项与设备测试清单

无法从源码（全部三个对比对象）中确定的事项：

* `Wc2Activity` 在 Java 端的确切行为（未反编译 `easytech.jar` 字节码）：它传入的文档目录、`ko.lproj` 与
  `kr.lproj` 分支、摇晃传感器的使用方、`MediaPlayer` 是否会在 `onPause` 中独立于原生钩子被暂停。
* 重制版的 Gradle 构建是否真的为 arm64 打包了 `modlibeasytech.so`（`CMakeLists.txt` 中有相关注释，
  但没有复制步骤）。
* C# 存档与原版应用所写文件的兼容性（从未互相交换过）。

APK 的设备测试清单（`WC2-android-arm64.apk`，包名 `com.wc2remake.csharp`，
versionName 1.3.8）：

1. 启动器显示原版图标和本地化名称（设备语言为 zh-CN/ja/ko/ru）。
2. 首次启动：136 MB 资源集的解压时间、存储空间不足时的表现，第二次启动应立即完成。
3. 游戏过程中隐藏状态栏/导航栏；滑动时短暂显示；使用刘海/挖孔区域
   （short edges）；20:9 屏幕上没有意外的信箱边。
4. 布局：16:9 → 568x320，16:10 → 534x320，20:9 → 640x320，4:3 平板 → 1024x768 并使用 iPad
   图集（在同类设备上与原版对比）。
5. 战斗中按 Home → 返回：`game6.sav`/`conquest6.sav` 出现在 `files/documents` 中，音乐
   停止后恢复，音量不变，暂停期间画面变暗。
6. 退出框 → 确认后结束 Activity；退出时写入 `settings.cfg`。
7. 每个状态（logo、菜单、加载、战斗、暂停框、教程）下的返回键，API 33+ 上的硬件返回与手势返回。
8. 地图上的双指缩放与双指平移；信箱边边缘处的点击精度。
9. 在手机和平板上运行教程（按宽度选择脚本）。
10. AI 回合较长时屏幕保持常亮（`KeepScreenOn`）。
11. 将真实游戏中的 `settings.cfg`（32 字节）、`commander.sav` 和 `game6.sav` 复制到
    `files/documents` 并加载它们。
12. 在低端 arm64 设备上使用 4096 像素图集时的帧率；冷启动时间。
13. 在仅支持 armeabi-v7a 或 x86 的设备上安装会失败，这是设计如此。

---

## 6. 按优先级排列的行动清单

### 6.A 已于 2026-11-05 在 C# 中修复（本次分析）

| # | 修复 | 文件 | 证据 |
|---|---|---|---|
| 1 | 从二进制文件移植教程覆盖层 | `G\GUI\InGame\GUITutorails.cs`、`tests\tutorial.txt`、`tests\ipad_tutorial.txt` | `D\GUITutorails.c`；截图 `T1..T4`、`P1..P2` |
| 2 | Logo 状态改为忠实实现 | `G\GUI\States\CLogoState.cs` | `D\CLogoState.c` |
| 3 | 联盟字母 `'n'`（以及所有非 a/b/c 的字母）= NEUTRAL | `G\GameStages\LoadBattleFromXml.cs` | `D\CGameManager.c`、`D\GetBattleBelligerentList.c:71-84` |
| 4 | 布局档位 + 平板布局，各处均使用 HD 内容 | `G\Supports\ecGame.cs` `SelectLayout`、`WC2\Program.cs`、`A\MainActivity.cs` | `D\jni.c:842-882` |
| 5 | 平板 UI 图集列表 | `G\Supports\ecGame.cs` `LoadGlobalGuiAtlases` | `D\CLogoState.c:104-166` |
| 6 | 帧步长上限 0.05 s | `WC2\Host\WC2Game.cs` | `D\jni.c:926-942` |
| 7 | 暂停/等待标志跳过更新并压暗画面 | `G\Supports\ecGame.cs` | `D\global.c` `ecGameUpdate/ecGameRender` |
| 8 | 返回键 = `CallNativeExit`（任何状态下都弹出退出框） | `G\Supports\ecGame.cs`、`WC2\Host\WC2Game.cs`、`WC2\Host\SmokeTest.cs`、`A\MainActivity.cs` | `D\jni.c:908-924` |
| 9 | 回到前台时重新应用音量；进入后台时调用将领保存；移植 `nativePause/nativeResume` 并由 Activity 驱动 | `G\Supports\ecGame.cs`、`A\MainActivity.cs` | `D\global.c:1192-1251`、`D\jni.c:892-906` |
| 10 | 沉浸式全屏主题与标志、cutout 模式、保持屏幕常亮 | `A\MainActivity.cs` | 重制版的 `res\values*\styles.xml`、`Wc2Activity` 字符串 |
| 11 | 启动器图标（5 种密度，含圆形）、本地化的 `app_name`（6 种语言区域）、显示版本 1.3.8 | `A\Resources\**`、`A\Properties\AndroidManifest.xml`、`A\WC2.Android.csproj` | 重制版的 `res\**` |
| 12 | `game.xml` 导出到文档文件夹 | `E\Core\Globals.cs` `GetPath` | `R\supports\ToolBox.cpp:131-141` |
| 13 | 战斗背景图集名称与 hd 标志 | `G\Render\CGameRes.cs` | `D\CGameRes.c:3925-3960` |
| 14 | 固定 MonoGame 版本、APK 构建脚本、平板回归测试脚本 | 两个 `.csproj`、`A\build-apk.ps1`、`tests\run-tests.ps1` | — |

修复后的验证：`WC2` 和 `WC2.Android` 的 `dotnet build` 干净通过（0 个错误）；
`tests\run-tests.ps1` = 音频自检（5 条音轨已解码，27 个音效正常）+ 12 个测试脚本
（`attack_box`、`campaign_13_rounds`、`campaign_start`、`card_shop`、`commander_and_options`、
`conquest_7_rounds`、`conquest_ai_turns`、`conquest_start`、`ipad_tutorial`、`pause_options_save`、
`save_and_load`、`tutorial`）全部 `ok`；APK 已重新构建并复制为 `WC2-android-arm64.apk`。

### 6.B 接下来在 C# 中修复（可在桌面验证，尚未完成）

1. **与真实应用的存档兼容性**：将 `SaveIO.cs` 与原版写出的 `game6.sav` 进行对比；如果原版能写入 5 个单位，
   则将地区记录的单位上限从 4 提高到 5（`DIFFERENCES.md` §1 "Saves"）。
2. **首次启动的资源解压**：显示进度，或直接从 APK 读取资源（原版和重制版都是原地读取）；目前在 UI 线程上
   复制 136 MB（`A\MainActivity.cs`）。
3. **发布签名配置**（`AndroidKeyStore`、项目密钥库），须在任何分发之前完成。
4. **战斗动画**：两个移植版中唯一缺失的玩法内容。根据伪 C 代码移植 `GUIBattle`、
   `CBattleScene`、`CBattleUnit`、`ecLibrary/ecLayer/ecFrame/ecElement` 以及两个 `CObjectDef` 加载器；
   重新启用 `StartBattale` 的动画标志。工作量大（约 22 KB 二进制代码），需要单独审计；应先决定是否保留该选项。
5. 如果更倾向原版行为，可选的单行改动：按进度解锁战役
   （`CCommander.cs:290`）、确定性的 RNG 随机种子（`Globals.cs:69-83`）。

### 6.C 在真实 Android 设备上测试

第 5 节清单的第 1-13 项；6.A 中的生命周期、沉浸式和布局修复仅经过编译检查和桌面检查。

### 6.D 有意保留不移植

应用内购买及价格、客服/邮件/服务器、推广面板与网页链接、评价提示、多人游戏、地图编辑器、资源完整性校验、
32 位/x86 安装包、重制版的字体工具以及已失效的 Java 调试 UI；第 3 节所列重制版有意做出的玩法重新设计均予以
保留（每一项都可以依据审计报告中的证据单独回退）。
