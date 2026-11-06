# Tests

`run-tests.ps1` (after `dotnet build` in `WC2`) runs, in this order:

1. **audio**: `--test-audio` decodes every music file and loads every sound effect.
2. **data**: `--verify-data` loads every JSON data file and the original XML it was converted from and
   compares the definitions field by field. A deliberate data change shows up here; that is intended
   (the game data is meant to match the original until someone changes it on purpose).
3. **scripts**: every `*.txt` here, in deterministic mode: `--seed 20261106 --fixed-step` and a fresh
   documents folder per script (`out/<script>/docs`). Each `dump NAME` writes the rule state
   (countries, areas, units, commander) as JSON; the runner compares it byte for byte with
   `golden/<script>/NAME.json`.

```powershell
.\tests\run-tests.ps1                 # everything
.\tests\run-tests.ps1 -Filter conquest
.\tests\run-tests.ps1 -Record         # store this run's dumps as the new goldens
.\tests\run-tests.ps1 -RealTime       # real clock, no goldens (the old smoke-test behaviour)
```

Record new goldens only when a rule change is intended, and say so in the commit. Screenshots and
logs go to `out/`.

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
| `shot NAME` | screenshot |
| `log TEXT` | write to the log |
| `exit` | quit |

Lines starting with `#` are comments.

## What the scripts cover

| Script | Covers |
|---|---|
| `campaign_autoplay` | the first campaign battle played by the AI to its end |
| `conquest_autoplay` | conquest 1 played by the AI for 16 rounds |
| `campaign_13_rounds`, `conquest_7_rounds`, `conquest_ai_turns` | round flow with fixed waits |
| `attack_box`, `card_shop`, `commander_and_options`, `pause_options_save`, `save_and_load` | screens and flows |
| `tutorial`, `ipad_tutorial` | the tutorial on the phone and tablet layouts |
| `campaign_start`, `conquest_start` | starting both modes |
