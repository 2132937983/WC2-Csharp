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
///   tap PX PY               - touch screen tap at window pixels (through the touch router, like Android)
///   swipe PX1 PY1 PX2 PY2 [SEC] - touch screen drag at window pixels (edge swipes mean back)
///   touch down|move|up|cancel ID PX PY - one raw touch event at window pixels
///   insets L T R B          - display cutout of the window in pixels (as the Android activity reports it)
///   back                    - back key
///   expectback N            - the game has had N back actions so far (key, gesture, script)
///   layout LABEL            - logical screen centred, clear of the cutout, same transform for touches
///   bars LABEL              - after the next frame, nothing is drawn outside the logical screen
///   hittest LABEL           - off-screen and hidden buttons must not react to touches
///   ghost on|off            - white test buttons straddling the screen edges (for bars / hittest)
///   mark                    - start collecting log lines for expectlog / expectnolog
///   expectlog TEXT          - some log line since the mark contains TEXT
///   expectnolog TEXT        - no log line since the mark contains TEXT
///   camera mark|moved|still - remember the map camera / check that it moved or stayed
///   wheel DELTA             - mouse wheel
///   shot NAME               - save a screenshot next to --screenshot
///   log TEXT                - write to the log
///   exit                    - quit
/// Lines starting with # are comments ("#! ARGS" on the first line gives the runner extra command line options).
/// Checks log "... failed" lines, which fail the script in tests/run-tests.ps1.
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

    // drag state (logical coordinates, or window pixels through the touch router for swipe)
    private static bool s_dragging;
    private static bool s_dragTouch;
    private static double s_dragStart, s_dragDuration;
    private static float s_dragX1, s_dragY1, s_dragX2, s_dragY2;
    private const int ScriptPointer = 1;

    // log lines since the last "mark"
    private static readonly List<string> s_marked = new();
    private static bool s_marking;
    private static float s_cameraX, s_cameraY, s_cameraScale;

    private static void OnLog(string line)
    {
        if (!s_marking || line.StartsWith("Smoke:", StringComparison.Ordinal)) return;
        lock (s_marked) s_marked.Add(line);
    }

    private static bool Marked(string text)
    {
        lock (s_marked) return s_marked.Any(l => l.Contains(text, StringComparison.Ordinal));
    }

    public static void LoadScript(string path)
    {
        Globals.LogListener += OnLog;
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
            if (s_dragTouch) TouchRouter.Instance().Move(ScriptPointer, x, y);
            else ecGame.ecTouchMove(x, y, 1);
            if (t >= 1.0)
            {
                if (s_dragTouch) TouchRouter.Instance().End(ScriptPointer, s_dragX2, s_dragY2);
                else ecGame.ecTouchEnd(s_dragX2, s_dragY2, 1);
                s_dragging = false;
            }
            return;
        }

        if (elapsed < s_waitUntil) return;
        while (s_index < s_commands.Count && elapsed >= s_waitUntil && !s_dragging)
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
            case "swipe":
                s_dragX1 = F(c.args[0]); s_dragY1 = F(c.args[1]); s_dragX2 = F(c.args[2]); s_dragY2 = F(c.args[3]);
                s_dragDuration = c.args.Length > 4 ? F(c.args[4]) : (c.name == "swipe" ? 0.25 : 0.5);
                s_dragStart = elapsed;
                s_dragging = true;
                s_dragTouch = c.name == "swipe";
                Log($"Smoke: {c.name} {s_dragX1},{s_dragY1} -> {s_dragX2},{s_dragY2}");
                if (s_dragTouch) TouchRouter.Instance().Begin(ScriptPointer, s_dragX1, s_dragY1);
                else ecGame.ecTouchBegin(s_dragX1, s_dragY1, 1);
                break;
            case "tap":
            {
                float px = F(c.args[0]), py = F(c.args[1]);
                Log($"Smoke: tap at window pixel {px},{py}");
                TouchRouter.Instance().Begin(ScriptPointer, px, py);
                TouchRouter.Instance().End(ScriptPointer, px, py);
                break;
            }
            case "touch":
            {
                string kind = c.args[0].ToLowerInvariant();
                int id = int.Parse(c.args[1], CultureInfo.InvariantCulture);
                float px = c.args.Length > 2 ? F(c.args[2]) : 0f, py = c.args.Length > 3 ? F(c.args[3]) : 0f;
                var router = TouchRouter.Instance();
                switch (kind)
                {
                    case "down": router.Begin(id, px, py); break;
                    case "move": router.Move(id, px, py); break;
                    case "up": router.End(id, px, py); break;
                    case "cancel": router.Cancel(id); break;
                    default: Log("Smoke: unknown touch event " + kind); break;
                }
                break;
            }
            case "insets":
                WC2Game.HostSafeInsets = new[] { (int)F(c.args[0]), (int)F(c.args[1]), (int)F(c.args[2]), (int)F(c.args[3]) };
                Log($"Smoke: cutout insets {string.Join(',', c.args)}");
                // applied at the start of the next frame
                s_waitUntil = elapsed + 0.1;
                break;
            case "back":
                Log("Smoke: back");
                ecGame.CallNativeExit();
                break;
            case "expectback":
            {
                int expected = int.Parse(c.args[0], CultureInfo.InvariantCulture);
                if (ecGame.BackCount != expected) Log($"expectback failed: {expected} back actions expected, {ecGame.BackCount} happened");
                else Log($"Smoke: {expected} back actions as expected");
                break;
            }
            case "layout":
                LayoutProbe.Run(c.args.Length > 0 ? c.args[0] : "layout");
                break;
            case "bars":
                LayoutProbe.RequestBarsCheck(c.args.Length > 0 ? c.args[0] : "bars");
                s_waitUntil = elapsed + 0.15;
                break;
            case "hittest":
                HitTestProbe.Run(c.args.Length > 0 ? c.args[0] : "hittest");
                break;
            case "ghost":
                LayoutProbe.Ghost(c.args.Length == 0 || c.args[0] != "off");
                break;
            case "mark":
                lock (s_marked) s_marked.Clear();
                s_marking = true;
                break;
            case "expectlog":
            case "expectnolog":
            {
                string text = string.Join(' ', c.args);
                bool found = Marked(text);
                if (found != (c.name == "expectlog")) Log($"{c.name} failed: \"{text}\" {(found ? "was logged" : "was not logged")} since the mark");
                else Log($"Smoke: {c.name} \"{text}\" ok");
                break;
            }
            case "camera":
            {
                var cam = g_Scene.camera;
                float x = cam.centerPos[0], y = cam.centerPos[1], scale = cam.scale;
                string what = c.args.Length > 0 ? c.args[0] : "mark";
                bool moved = MathF.Abs(x - s_cameraX) > 0.5f || MathF.Abs(y - s_cameraY) > 0.5f || MathF.Abs(scale - s_cameraScale) > 0.001f;
                if (what == "moved" && !moved) Log($"camera failed: the map did not move ({x:0.#},{y:0.#} x{scale:0.###})");
                else if (what == "still" && moved) Log($"camera failed: the map moved from {s_cameraX:0.#},{s_cameraY:0.#} to {x:0.#},{y:0.#}");
                else Log($"Smoke: camera {what} at {x:0.#},{y:0.#} x{scale:0.###}");
                s_cameraX = x; s_cameraY = y; s_cameraScale = scale;
                break;
            }
            case "wheel":
                ecGame.ecScrollWheel(0f, F(c.args[0]), 0f);
                break;
            case "shot":
                RequestScreenshot(c.args.Length > 0 ? c.args[0] : "shot");
                // the screenshot is taken by the next Draw: give it a frame before continuing
                s_waitUntil = elapsed + 0.15;
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

    /// <summary>Set by the host; takes a file name without directory.</summary>
    public static Action<string> ScreenshotRequest;

    private static void RequestScreenshot(string name) => ScreenshotRequest?.Invoke(name);
}
