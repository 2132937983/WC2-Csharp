# Three-way differences: original `libeasytech.so` vs C++ remake vs C# port

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
