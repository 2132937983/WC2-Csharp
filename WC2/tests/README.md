# Scripted smoke tests

Each `*.txt` file is a script for the built-in input driver (`WC2/Host/SmokeTest.cs`): coordinates are
logical screen units (568x320 phone layout) unless the command takes window pixels. `shot NAME` saves a
screenshot next to the `--screenshot` path. Run them all with `tests\run-tests.ps1` after `dotnet build`;
results go to `tests\out`.

Notes:

* Lines starting with `#` are comments. Commands: `wait S`, `click X Y`, `press X Y`, `move X Y`,
  `release X Y`, `drag X1 Y1 X2 Y2 [S]`, `back`, `wheel DELTA`, `shot NAME`, `log TEXT`, `exit`.
* Touch screen commands take window pixels and go through the touch router, as Android touches do:
  `tap PX PY`, `swipe PX1 PY1 PX2 PY2 [S]` (a swipe from a side edge is back), `touch down|move|up|cancel
  ID PX PY`. `insets L T R B` sets the display cutout the Android activity would report.
* Checks (they log "... failed", which fails the script): `layout LABEL` (logical screen centred, clear of
  the cutout, touches and drawing use one transform), `bars LABEL` (nothing drawn outside the logical
  screen), `hittest LABEL` (off-screen and hidden buttons do not react to touches), `ghost on|off` (white
  test buttons straddling every edge, for bars and hittest), `expectback N`, `mark` + `expectlog TEXT` /
  `expectnolog TEXT`, `camera mark|moved|still` (the map camera).
* A first line `#! ARGS` adds command line options for that script: window size (`--width`, `--height`),
  `--density PX_PER_DP` (touch screen density for the edge swipe) and `--insets L,T,R,B` (display cutout).
* `back` is the Android back key (`CallNativeExit`): a panel or state that handles it closes, otherwise
  the exit box appears on top of whatever is shown.
* Scripts named `ipad_*.txt` are started with `--ipad` by the runner, so their coordinates are in the
  1024x768 tablet layout.
* The runner first runs `--test-input` (layouts of phones, tablets and letterboxed windows with and without
  a notch, and the touch router; "FAIL" lines are problems) and the audio self-test (`--test-audio`, every
  music file decoded and every effect ok), then flags `failed`/`xception` lines in a script log and a
  missing "Music playing" line.
