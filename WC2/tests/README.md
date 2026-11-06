# Scripted smoke tests

Each `*.txt` file is a script for the built-in input driver (`WC2/Host/SmokeTest.cs`): coordinates are
logical screen units (568x320 phone layout). `shot NAME` saves a screenshot next to the `--screenshot`
path. Run them all with `tests\run-tests.ps1` after `dotnet build`; results go to `tests\out`.

Notes:

* Lines starting with `#` are comments. Commands: `wait S`, `click X Y`, `press X Y`, `move X Y`,
  `release X Y`, `drag X1 Y1 X2 Y2 [S]`, `back`, `wheel DELTA`, `shot NAME`, `log TEXT`, `exit`.
* `back` is the Android back key (`CallNativeExit`): a panel or state that handles it closes, otherwise
  the exit box appears on top of whatever is shown.
* Scripts named `ipad_*.txt` are started with `--ipad` by the runner, so their coordinates are in the
  1024x768 tablet layout.
* The runner flags `failed`/`xception` lines in a log and a missing "Music playing" line; the audio
  self-test (`--test-audio`) runs first and must report every music file decoded and every effect ok.
