using System.Globalization;

namespace WC2;

/// <summary>
/// Offline checks of the display mapping and the touch router, without a window
/// (<c>WC2 --test-input</c>). Prints one line per layout and "FAIL ..." for every failed check;
/// tests/run-tests.ps1 runs it first.
/// </summary>
public static class InputSelfTest
{
    private static int s_checks, s_failures;

    private static void Check(bool ok, string what)
    {
        s_checks++;
        if (ok) return;
        s_failures++;
        Console.WriteLine("FAIL " + what);
    }

    public static void Run()
    {
        Layouts();
        Router();
        Console.WriteLine($"input self test: {s_checks} checks, {s_failures} failed");
    }

    /// <summary>Phones (with and without a notch on either side), tablets and letterboxed desktop windows.</summary>
    private static void Layouts()
    {
        var cases = new (string name, int w, int h, int l, int t, int r, int b)[]
        {
            ("phone 20:9", 2400, 1080, 0, 0, 0, 0),
            ("phone 20:9, notch left", 2400, 1080, 120, 0, 0, 0),
            ("phone 20:9, notch right (turned around)", 2400, 1080, 0, 0, 120, 0),
            ("phone 2:1, notch", 2160, 1080, 90, 0, 0, 0),
            ("phone 19.5:9, wide notch", 2340, 1080, 200, 0, 0, 0),
            ("phone 16:9", 1920, 1080, 0, 0, 0, 0),
            ("phone 3:2", 2160, 1440, 0, 0, 0, 0),
            ("tablet 16:10", 2560, 1600, 0, 0, 0, 0),
            ("tablet 4:3", 2048, 1536, 0, 0, 0, 0),
            ("desktop 1600x600", 1600, 600, 0, 0, 0, 0),
            ("desktop 1000x1000", 1000, 1000, 0, 0, 0, 0),
            ("odd size, uneven cutout", 2399, 1079, 33, 0, 7, 0),
        };
        var g = ecGraphics.Instance();
        foreach (var c in cases)
        {
            int ix = Math.Max(c.l, c.r), iy = Math.Max(c.t, c.b);
            ecGame.SelectLayout(c.w - 2 * ix, c.h - 2 * iy, out int lw, out int lh);
            g.Init(lw, lh, 0, c.w, c.h);
            g.Resize(c.w, c.h, c.l, c.t, c.r, c.b);
            float s = g.layoutToScreenMultiplier[0];
            float dw = lw * s, dh = lh * s;
            float left = g.offsetX, right = c.w - g.offsetX - dw, top = g.offsetY, bottom = c.h - g.offsetY - dh;
            string n = $"{c.name} ({c.w}x{c.h}, layout {lw}x{lh})";
            Check(MathF.Abs(left - right) <= 1.01f && MathF.Abs(top - bottom) <= 1.01f, $"{n}: centred (bars {left:0.#}/{right:0.#} x {top:0.#}/{bottom:0.#})");
            Check(left >= c.l && right >= c.r - 0.01f && top >= c.t && bottom >= c.b - 0.01f, $"{n}: clear of the cutout");
            Check(MathF.Abs(dw - (c.w - 2 * ix)) <= 1.01f || MathF.Abs(dh - (c.h - 2 * iy)) <= 1.01f, $"{n}: as large as the safe area allows");
            Check(g.screenClip.X == (int)g.offsetX && g.screenClip.Width >= (int)dw && g.screenClip.Right <= c.w, $"{n}: clip rectangle {g.screenClip}");
            g.LogicalToWindow(lw * 0.25f, lh * 0.75f, out float px, out float py);
            g.WindowToLogical(px, py, out float x, out float y);
            Check(MathF.Abs(x - lw * 0.25f) < 0.01f && MathF.Abs(y - lh * 0.75f) < 0.01f, $"{n}: touch and drawing transforms agree");
            if (left >= 1)
            {
                g.WindowToLogical(g.offsetX - 0.5f, c.h * 0.5f, out float bx, out float by);
                Check(!g.IsOnScreen(bx, by), $"{n}: the left bar is off the logical screen");
            }
            Console.WriteLine($"layout {n}: scale {s:0.###}, bars {left:0.#}/{right:0.#} x {top:0.#}/{bottom:0.#}");
        }
        // a size-only resize keeps the cutout
        g.Init(640, 320, 0, 2400, 1080);
        g.Resize(2400, 1080, 120, 0, 0, 0);
        g.Resize(2400, 1080);
        Check(g.safeInsets[0] == 120 && g.offsetX >= 120, "a resize without insets keeps the cutout");
    }

    private sealed class Recorder : ITouchSink
    {
        public readonly List<string> events = new();
        public readonly List<int> indices = new();
        public int backs;
        private void Add(string kind, float x, float y, int index)
        {
            events.Add(string.Create(CultureInfo.InvariantCulture, $"{kind} {x:0.#} {y:0.#}"));
            indices.Add(index);
        }
        public void Begin(float x, float y, int index) => Add("B", x, y, index);
        public void Move(float x, float y, int index) => Add("M", x, y, index);
        public void End(float x, float y, int index) => Add("E", x, y, index);
        public void Cancel(int index) { events.Add("C"); indices.Add(index); }
        public void Back() => backs++;
        public void Clear() { events.Clear(); indices.Clear(); backs = 0; }
        public string Sequence => string.Join(" | ", events);
    }

    /// <summary>Expected event text for a window pixel (same mapping and format as the recorder).</summary>
    private static string At(string kind, float px, float py)
    {
        ecGraphics.Instance().WindowToLogical(px, py, out float x, out float y);
        return string.Create(CultureInfo.InvariantCulture, $"{kind} {x:0.#} {y:0.#}");
    }

    private static string Seq(params string[] events) => string.Join(" | ", events);

    /// <summary>The router on a 420 dpi phone (2.625 px per dp: edge strip 63 px, slop 21 px, back 126 px).</summary>
    private static void Router()
    {
        var g = ecGraphics.Instance();
        g.Init(640, 320, 0, 2160, 1080);
        g.Resize(2160, 1080, 0, 0, 0, 0); // 2:1 phone, scale 3.375, no bars
        var rec = new Recorder();
        var r = new TouchRouter { Density = 2.625f, Sink = rec };
        double t = 0;
        r.Update(t);

        r.Begin(0, 1080, 540);
        r.End(0, 1080, 540);
        Check(rec.Sequence == Seq(At("B", 1080, 540), At("E", 1080, 540)) && rec.backs == 0, "tap in the middle reaches the game at once: " + rec.Sequence);
        int firstIndex = rec.indices[0];
        rec.Clear();
        r.Begin(0, 1080, 540);
        r.End(0, 1080, 540);
        Check(rec.indices[0] != firstIndex, "every gesture gets a fresh game index (Android reuses pointer ids)");

        rec.Clear();
        r.Begin(0, 10, 540);
        r.Move(0, 40, 545);
        r.Move(0, 150, 550);
        r.Move(0, 300, 560);
        r.End(0, 300, 560);
        Check(rec.events.Count == 0 && rec.backs == 1, "left edge swipe is back and the game sees nothing: " + rec.Sequence);

        rec.Clear();
        r.Begin(3, 2150, 300);
        r.Move(3, 2000, 310);
        r.End(3, 1900, 310);
        Check(rec.events.Count == 0 && rec.backs == 1, "right edge swipe is back: " + rec.Sequence);

        rec.Clear();
        r.Begin(0, 10, 540);
        r.Move(0, 60, 540);
        r.End(0, 100, 540);
        Check(rec.events.Count == 0 && rec.backs == 0, "a short edge swipe (90 px < 48 dp) is abandoned: " + rec.Sequence);

        rec.Clear();
        r.Begin(0, 20, 1000);
        r.End(0, 20, 1000);
        Check(rec.Sequence == Seq(At("B", 20, 1000), At("E", 20, 1000)) && rec.backs == 0, "a tap at the edge reaches the game: " + rec.Sequence);

        rec.Clear();
        r.Begin(0, 20, 300);
        r.Move(0, 22, 340);
        r.Move(0, 24, 600);
        r.End(0, 24, 600);
        Check(rec.Sequence == Seq(At("B", 20, 300), At("M", 22, 340), At("M", 24, 600), At("E", 24, 600)) && rec.backs == 0,
            "a drag along the edge is the game's, from its start point: " + rec.Sequence);

        rec.Clear();
        r.Begin(0, 2140, 500);
        r.Move(0, 2159, 500);
        r.Move(0, 2159, 520);
        r.End(0, 2159, 520);
        Check(rec.backs == 0 && rec.events.Count > 0 && rec.events[0] == At("B", 2140, 500), "a drag out of the edge is the game's: " + rec.Sequence);

        rec.Clear();
        t = 10;
        r.Update(t);
        r.Begin(0, 20, 500);
        t += 0.4;
        r.Update(t);
        r.Move(0, 300, 500);
        r.End(0, 300, 500);
        Check(rec.Sequence == Seq(At("B", 20, 500), At("M", 300, 500), At("E", 300, 500)) && rec.backs == 0,
            "an edge finger held still is the game's (slow drags, press and hold): " + rec.Sequence);

        rec.Clear();
        r.Begin(0, 20, 500);
        r.Begin(1, 1000, 500);
        r.Move(0, 200, 500);
        r.End(0, 200, 500);
        r.End(1, 1000, 500);
        Check(rec.events.Count == 5 && rec.events[0] == At("B", 20, 500) && rec.events[1] == At("B", 1000, 500) && rec.backs == 0,
            "a second finger hands the edge finger to the game (pinch): " + rec.Sequence);

        rec.Clear();
        r.Begin(0, 1000, 500);
        r.Move(0, 1100, 500);
        r.Cancel(0);
        Check(rec.Sequence == Seq(At("B", 1000, 500), At("M", 1100, 500), "C"), "a cancelled touch is cancelled in the game, never ended: " + rec.Sequence);

        rec.Clear();
        r.Begin(0, 10, 500);
        r.Move(0, 200, 500);
        r.Cancel(0);
        Check(rec.events.Count == 0 && rec.backs == 0, "a cancelled edge swipe does nothing: " + rec.Sequence);

        rec.Clear();
        r.Begin(0, 50, 540);
        Check(rec.events.Count == 0, "50 px is inside the 24 dp edge strip at 2.625 px/dp");
        r.End(0, 50, 540);
        r.Density = 1f;
        rec.Clear();
        r.Begin(0, 50, 540);
        Check(rec.events.Count == 1, "50 px is outside the 24 dp edge strip at 1 px/dp");
        r.End(0, 50, 540);
        rec.Clear();
        r.Begin(0, 10, 540);
        r.Move(0, 60, 540);
        r.End(0, 100, 540);
        Check(rec.backs == 1, "the 90 px swipe that is too short on the phone is back at 1 px/dp (48 px)");

        // letterboxed: 2400x1080 with the 640x320 layout has 120 px bars
        r.Density = 2.625f;
        g.Resize(2400, 1080, 0, 0, 0, 0);
        rec.Clear();
        r.Begin(0, 100, 540);
        r.End(0, 100, 540);
        g.WindowToLogical(100, 540, out float barX, out float barY);
        Check(rec.events.Count == 2 && !g.IsOnScreen(barX, barY), "a tap in the bar maps off the logical screen (the game's entry drops it): " + rec.Sequence);
        rec.Clear();
        r.Begin(0, 10, 540);
        r.Move(0, 200, 540);
        r.End(0, 250, 540);
        Check(rec.backs == 1 && rec.events.Count == 0, "an edge swipe that starts in the bar is back");

        rec.Clear();
        r.Begin(0, 1200, 540);
        r.CancelAll();
        Check(rec.Sequence == Seq(At("B", 1200, 540), "C") && r.ActiveCount == 0, "losing focus cancels every finger: " + rec.Sequence);
    }
}
