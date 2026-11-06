# Tests

`run-tests.ps1` (after `dotnet build Hosts/Desktop` in the repository root) runs, in this order:

0. **unit**: `dotnet test Tests` — the in-process unit tests in `Unit/` (schema validator, every data
   file against its schema, invalid mod data falls back, asset layer resolution order, original names,
   variant folders, layout buckets, hit testing rules, the IAP removal, AI evaluation, map pipeline).
1. **audio**: `--test-audio` decodes every music file and loads every sound effect.
2. **data**: `--verify-data` checks every JSON data file against its schema, then loads every type from
   the JSON and from the original XML (or built-in table) it was converted from and compares the
   definitions field by field. A deliberate data change shows up here; that is intended
   (the game data is meant to match the original until someone changes it on purpose).
3. **assets**: `WC2.Tools validate` checks the built package: every original is reachable by its old
   name, every index entry exists, every asset the data names exists, the whole tree is packaged.
4. **scripts**: every `*.txt` here, in deterministic mode: `--seed 20261106 --fixed-step` and a fresh
   documents folder per script (`out/<script>/docs`). Each `dump NAME` writes the rule state
   (countries, areas, units, commander) as JSON; the runner compares it byte for byte with
   `golden/<script>/NAME.json`. Each `shot NAME` is compared with `golden/<script>/NAME.jpg`
   (`WC2.Tools compare-image`: scaled to the reference size, a pixel counts as changed when a channel
   differs by more than 40, the shot fails above 1 % changed pixels; a `NAME.diff.png` marks them).
   A script whose first line is `#! ARGS` gets ARGS on the command line (window size).

```powershell
.\Tests\run-tests.ps1                 # everything
.\Tests\run-tests.ps1 -Filter conquest
.\Tests\run-tests.ps1 -Record         # store this run's dumps as the new goldens
.\Tests\run-tests.ps1 -RecordShots    # store this run's screenshots as the new references
.\Tests\run-tests.ps1 -RealTime       # real clock, no goldens (the old smoke-test behaviour)
```

Record new goldens only when a rule change is intended, and new reference screenshots only when a
visual change is intended; say so in the commit. Screenshots and logs go to `out/`.

## Script commands

Coordinates are logical screen units (568 x 320 phone layout; scripts named `ipad_*.txt` run with
`--ipad` in the 1024 x 768 tablet layout). Times are game seconds.

| Command | Effect |
|---|---|
| `wait S` | wait S seconds |
| `click X Y`, `press X Y`, `move X Y`, `release X Y` | touch input |
| `drag X1 Y1 X2 Y2 [S]` | press, move in steps, release |
| `back` | the Android back key (`CallNativeExit`: a panel or state that handles it closes, otherwise the exit box appears) |
| `wheel DELTA` | mouse wheel |
| `autoplay ROUNDS` | play N rounds without input: ends the human turn (the AI hosting option plays it), fast-forwards AI turns and closes boxes; stops early when the battle ends |
| `settle` | close boxes and finish AI moves until the human side can act (ends no turn) |
| `dump NAME` | write the rule state as `NAME.json` next to the screenshots |
| `hittest LABEL` | every button off the screen is touched there (at a window pixel in the letterbox bars when the window has them, and at a synthetic point) through the input entry and straight through the GUI tree; every hidden button must refuse input. Buttons do not fire while probing; each one that would have fires a `hittest failed` log line |
| `dumpafter COUNTRY NAME` | write the rule state as `NAME.json` the moment COUNTRY's turn ends (its own decisions, before the next country acts; AI tests) |
| `startconquest FILE COUNTRY` | start a conquest with any scenario file as COUNTRY (the generated world map: `conquest_10.xml`) |
| `texstats LABEL` | log the live texture memory and the last frame's draw calls; lines whose labels differ only by a trailing number must report the same memory (resources are released when screens close) |
| `shot NAME` | screenshot |
| `log TEXT` | write to the log |
| `exit` | quit |

Lines starting with `#` are comments. A first line `#! ARGS` adds command line options (`$TESTS` is this
folder). Scripts run with `--no-mods --ai classic` unless their `#!` line says otherwise (the goldens
were recorded with the original AI).

A script `X.txt` may have `X.expect`: lines `DUMP AREA owner COUNTRY` or `DUMP AREA units>=N` checked
against the dumps (the AI tests).

## What the scripts cover

| Script | Covers |
|---|---|
| `campaign_autoplay` | the first campaign battle played by the AI to its end |
| `conquest_autoplay` | conquest 1 played by the AI for 16 rounds |
| `campaign_13_rounds`, `conquest_7_rounds`, `conquest_ai_turns` | round flow with fixed waits |
| `attack_box`, `card_shop`, `commander_and_options`, `pause_options_save`, `save_and_load` | screens and flows |
| `tutorial`, `ipad_tutorial` | the tutorial on the phone and tablet layouts |
| `campaign_start`, `conquest_start` | starting both modes |
| `hit_test`, `hit_test_wide`, `ipad_hit_test` | hidden and off-screen controls refuse touches: phone layout, a 1600x600 window (640x320 layout, bars left and right), a 1000x1000 window (tablet layout, bars above and below); `card_shop`, `commander_and_options`, `conquest_start`, `pause_options_save`, `tutorial` probe their screens too |
| `resource_cycle` | menu -> conquest -> menu twice: the same texture memory on every visit |
| `ai_defends_marseille_classic`, `ai_defends_marseille_standard` | the test mod `mods/ai-marseille` (an Italian stack next to a weakly held Marseille): the standard AI reinforces and holds Marseille (`.expect`), the classic AI loses it |
| `ai_standard_autoplay` | conquest 1 played by the standard AI for 16 rounds |
| `world_menu`, `world_autoplay` | the generated world map (`mods/map-natural-earth`, MAP_PIPELINE.md): through the conquest menu (list, preview, game), and played by the standard AI for four rounds |

## Unit tests (`Unit/`, `dotnet test Tests`)

| File | Covers |
|---|---|
| `SchemaAndDataTests.cs` | the schema validator; every data file against its schema; invalid mod data falls back to the game's data; the asset layer's resolution order; original names; variant folders; layout buckets |
| `HitTestTests.cs` | points off the screen never hit; only the visible part of an edge control hits; hidden and disabled controls refuse input |
| `NoPurchaseTests.cs` | no purchase code, billing permission, purchase types or buy button |
| `AiProfileTests.cs` | classic equals the original numbers; inheritance; the command line override |
| `MapPackTests.cs` | the Natural Earth pack (consistency, coverage, capitals, Marseille); the pipeline and the classic export on a synthetic world |
| `BuiltInModTests.cs` | mods built into the package mount and merge like folder mods, a folder mod replaces one with the same id, the world map is built in |
