using WC2;

// Command line: WC2 [--ipad] [--width N] [--height N] [--assets PATH] [--lang xx]
//               [--exit-after SEC] [--screenshot PATH] [--screenshot-every SEC] [--script FILE] [--log FILE]
if (DebugTools.TryRun(args)) return;

int logicalW = 568, logicalH = 320;
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
        case "--script" when i + 1 < args.Length: SmokeTest.LoadScript(args[++i]); break;
        case "--log" when i + 1 < args.Length: Globals.LogFile = args[++i]; break;
    }
}

// --ipad only picks a 4:3 window; the layout itself follows the original nativeInit buckets
// (640/568/534/480 x 320 by aspect ratio, 1024x768 below 1.40625) with HD content, as on Android.
if (ipad && windowW == 1136 && windowH == 640) { windowW = 1024; windowH = 768; }
g_contenscalefactor = 2.0f;
ecGame.SelectLayout(windowW, windowH, out logicalW, out logicalH);

ecGame.ConfigurePaths(assets, lang);

using var game = new WC2Game(logicalW, logicalH, windowW, windowH);
game.Run();
