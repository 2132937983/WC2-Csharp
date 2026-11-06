using System.Globalization;

namespace WC2;

/// <summary>
/// Minimal scripted input driver for unattended testing. Script lines (one command per line):
///   wait SECONDS            - pause before the next command
///   click X Y               - tap at logical coordinates
///   press X Y               - finger down
///   move X Y                - finger move
///   release X Y             - finger up
///   drag X1 Y1 X2 Y2 [SEC]  - press, move in steps, release
///   back                    - back key
///   wheel DELTA             - mouse wheel
///   shot NAME               - save a screenshot next to --screenshot
///   autoplay ROUNDS         - play N rounds without input (presses the round button, closes boxes)
///   settle                  - close boxes and finish AI moves until the human side can act
///   dump NAME               - write the rule state as JSON (golden tests)
///   hittest LABEL           - off-screen and hidden buttons must not react to touches (logs failures)
///   texstats LABEL          - log the live texture memory
///   dumpafter COUNTRY NAME  - write the rule state when COUNTRY's turn ends (before the next acts)
///   log TEXT                - write to the log
///   exit                    - quit
/// Lines starting with # are comments.
/// </summary>
public static class SmokeTest
{
    private sealed class Command
    {
        public string name;
        public string[] args;
    }

    private static readonly List<Command> s_commands = new();
    private static int s_index;
    private static double s_waitUntil;
    private static bool s_loaded;
    private static double s_startDelay = 0.5;

    // drag state
    private static bool s_dragging;
    private static double s_dragStart, s_dragDuration;
    private static float s_dragX1, s_dragY1, s_dragX2, s_dragY2;

    public static void LoadScript(string path)
    {
        try
        {
            foreach (var raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                s_commands.Add(new Command { name = parts[0].ToLowerInvariant(), args = parts.Skip(1).ToArray() });
            }
            s_loaded = s_commands.Count > 0;
            Log("Smoke script loaded: " + s_commands.Count + " commands");
        }
        catch (Exception ex)
        {
            Log("Smoke script failed to load: " + ex.Message);
        }
    }

    private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    public static void Step(double elapsed, float dt)
    {
        if (!s_loaded) return;
        if (elapsed < s_startDelay) return;

        if (s_dragging)
        {
            double t = Math.Min(1.0, (elapsed - s_dragStart) / Math.Max(0.01, s_dragDuration));
            float x = s_dragX1 + (s_dragX2 - s_dragX1) * (float)t;
            float y = s_dragY1 + (s_dragY2 - s_dragY1) * (float)t;
            ecGame.ecTouchMove(x, y, 1);
            if (t >= 1.0)
            {
                ecGame.ecTouchEnd(s_dragX2, s_dragY2, 1);
                s_dragging = false;
            }
            return;
        }

        if (GameTestDriver.Active)
        {
            GameTestDriver.Step(dt);
            return;
        }

        if (elapsed < s_waitUntil) return;
        while (s_index < s_commands.Count && elapsed >= s_waitUntil && !s_dragging && !GameTestDriver.Active)
        {
            var c = s_commands[s_index++];
            try { Execute(c, elapsed); }
            catch (Exception ex) { Log("Smoke command failed (" + c.name + "): " + ex.Message); }
        }
    }

    private static void Execute(Command c, double elapsed)
    {
        switch (c.name)
        {
            case "wait":
                s_waitUntil = elapsed + F(c.args[0]);
                break;
            case "click":
            {
                float x = F(c.args[0]), y = F(c.args[1]);
                Log($"Smoke: click {x},{y}");
                ecGame.ecTouchBegin(x, y, 1);
                ecGame.ecTouchEnd(x, y, 1);
                break;
            }
            case "press":
                ecGame.ecTouchBegin(F(c.args[0]), F(c.args[1]), 1);
                break;
            case "move":
                ecGame.ecTouchMove(F(c.args[0]), F(c.args[1]), 1);
                break;
            case "release":
                ecGame.ecTouchEnd(F(c.args[0]), F(c.args[1]), 1);
                break;
            case "drag":
                s_dragX1 = F(c.args[0]); s_dragY1 = F(c.args[1]); s_dragX2 = F(c.args[2]); s_dragY2 = F(c.args[3]);
                s_dragDuration = c.args.Length > 4 ? F(c.args[4]) : 0.5;
                s_dragStart = elapsed;
                s_dragging = true;
                Log($"Smoke: drag {s_dragX1},{s_dragY1} -> {s_dragX2},{s_dragY2}");
                ecGame.ecTouchBegin(s_dragX1, s_dragY1, 1);
                break;
            case "back":
                Log("Smoke: back");
                ecGame.CallNativeExit();
                break;
            case "wheel":
                ecGame.ecScrollWheel(0f, F(c.args[0]), 0f);
                break;
            case "shot":
                RequestScreenshot(c.args.Length > 0 ? c.args[0] : "shot");
                // the screenshot is taken by the next Draw: give it a frame before continuing
                s_waitUntil = elapsed + 0.15;
                break;
            case "autoplay":
                // plays N rounds without input (round button, closes boxes); the script continues afterwards
                GameTestDriver.StartAutoplay(c.args.Length > 0 ? (int)F(c.args[0]) : 1);
                break;
            case "settle":
                // close pending boxes and let the AI finish until the human side can act (ends no turn)
                GameTestDriver.StartSettle();
                break;
            case "dump":
            {
                // rule state as JSON next to the screenshots (golden tests compare these files)
                string name = c.args.Length > 0 ? c.args[0] : "state";
                if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) name += ".json";
                GameStateDigest.Write(Path.Combine(OutputDirectory ?? ".", name));
                break;
            }
            case "dumpafter":
            {
                // dumpafter COUNTRY NAME: write the rule state when that country's turn ends (AI tests)
                string name = c.args.Length > 1 ? c.args[1] : "after_" + c.args[0];
                if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) name += ".json";
                GameTestDriver.DumpAfterTurn(c.args[0], Path.Combine(OutputDirectory ?? ".", name));
                break;
            }
            case "hittest":
                // touches every off-screen or hidden button; logs "hittest failed" for each one that reacts
                HitTestProbe.Run(c.args.Length > 0 ? c.args[0] : "hittest");
                break;
            case "texstats":
                // live texture memory now (resource release checks)
                Log($"Smoke: texstats {(c.args.Length > 0 ? c.args[0] : "")}: {ecGraphics.Instance().TextureStats()}");
                Log($"Smoke: frame {(c.args.Length > 0 ? c.args[0] : "")}: {ecGraphics.Instance().lastFrameDrawCalls} draw calls, {ecGraphics.Instance().lastFrameTextureSwitches} texture switches");
                break;
            case "log":
                Log("Smoke: " + string.Join(' ', c.args));
                break;
            case "exit":
                OnExitRequested?.Invoke();
                break;
            default:
                Log("Smoke: unknown command " + c.name);
                break;
        }
    }

    /// <summary>Folder for "dump" output; the host sets it to the screenshot folder.</summary>
    public static string OutputDirectory;

    /// <summary>Set by the host; takes a file name without directory.</summary>
    public static Action<string> ScreenshotRequest;

    private static void RequestScreenshot(string name) => ScreenshotRequest?.Invoke(name);
}
