using WC2;

// Command line: WC2 [--ipad] [--width N] [--height N] [--assets PATH] [--lang xx]
//               [--exit-after SEC] [--screenshot PATH] [--screenshot-every SEC] [--script FILE] [--log FILE]
//               [--seed N] [--fixed-step] [--docs DIR] [--mods DIR] [--no-mods]
//   --seed N      replayable run: every random seed is drawn from N
//   --fixed-step  every frame advances the game by exactly 1/60 s and the loop runs unthrottled
//   --docs DIR    saves, settings and the commander file live in DIR instead of the user folder
//   --mods DIR    load mods from DIR as well (mods are always read from <documents>/mods)
//   --no-mods     start with the base package only
//   --assets DIR  a generated package, the assets/ tree, a flat folder of the originals, or a .zip/.apk
//   --all-mods    also load mods marked "enabled": false
//   --ai NAME     AI profile (data/json/ai/profiles.json: classic, standard, hard)
//   --scenario FILE [--country ID]  start a conquest with that scenario right after the menu appears
//                 (e.g. conquest_10.xml from the map-natural-earth mod, MAP_PIPELINE.md)
int logicalW = 568, logicalH = 320;
string startScenario = null, startCountry = "de";
bool scriptGiven = false;
int windowW = 1136, windowH = 640;
string assets = null;
string lang = null;
bool ipad = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--ipad": ipad = true; break;
        case "--width" when i + 1 < args.Length: int.TryParse(args[++i], out windowW); break;
        case "--height" when i + 1 < args.Length: int.TryParse(args[++i], out windowH); break;
        case "--assets" when i + 1 < args.Length: assets = args[++i]; break;
        case "--lang" when i + 1 < args.Length: lang = args[++i]; break;
        case "--exit-after" when i + 1 < args.Length: double.TryParse(args[++i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out WC2Game.ExitAfterSeconds); break;
        case "--screenshot" when i + 1 < args.Length: WC2Game.ScreenshotPath = args[++i]; break;
        case "--screenshot-every" when i + 1 < args.Length: double.TryParse(args[++i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out WC2Game.ScreenshotEverySeconds); break;
        case "--script" when i + 1 < args.Length: SmokeTest.LoadScript(args[++i]); scriptGiven = true; break;
        case "--log" when i + 1 < args.Length: Globals.LogFile = args[++i]; break;
        case "--seed" when i + 1 < args.Length: if (int.TryParse(args[++i], out int seed)) SetSeedOverride(seed); break;
        case "--fixed-step": WC2Game.FixedStep = true; break;
        case "--docs" when i + 1 < args.Length: ecGame.DocumentsOverride = Path.GetFullPath(args[++i]); break;
        case "--mods" when i + 1 < args.Length: ecGame.ModsFolderOverride = Path.GetFullPath(args[++i]); break;
        case "--no-mods": ecGame.ModsDisabled = true; break;
        case "--all-mods": ModManager.IncludeDisabled = true; break;
        case "--ai" when i + 1 < args.Length: AiProfiles.Override = args[++i]; break;
        case "--scenario" when i + 1 < args.Length: startScenario = args[++i]; break;
        case "--country" when i + 1 < args.Length: startCountry = args[++i]; break;
        case "--texture-stats": ecGraphics.TextureStatsEvery = 120; break;
    }
}

// debug commands (--test-audio, --convert-data, --verify-data, ...) run after the options above,
// so --docs and --mods apply to them as well
if (DebugTools.TryRun(args)) return;

// --ipad only picks a 4:3 window; the layout itself follows the original nativeInit buckets
// (640/568/534/480 x 320 by aspect ratio, 1024x768 below 1.40625) with HD content, as on Android.
if (ipad && windowW == 1136 && windowH == 640) { windowW = 1024; windowH = 768; }
g_contenscalefactor = 2.0f;
ecGame.ConfigurePaths(assets, lang);   // first: the layout profiles are data
if (startScenario != null && !scriptGiven)
    SmokeTest.LoadLines(new[] { "wait 4.5", $"startconquest {startScenario} {startCountry}" });
ecGame.SelectLayout(windowW, windowH, out logicalW, out logicalH);

using var game = new WC2Game(logicalW, logicalH, windowW, windowH);
game.Run();
