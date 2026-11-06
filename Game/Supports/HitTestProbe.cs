namespace WC2;

/// <summary>
/// Regression probe for hidden controls (script command <c>hittest LABEL</c>).
///
/// The original hides several controls by moving them off the screen (the in-game round, card and
/// pause buttons start just past the right or bottom edge, the gold panel above the top edge) or by
/// hiding them. On a window or phone whose aspect ratio differs from the layout, the area outside the
/// logical screen is part of the window (letterbox bars), so such a control must not react to a touch
/// there. The probe walks the whole GUI tree and, for every button:
/// <list type="bullet">
/// <item>if any part of it lies outside the logical screen, touches that part through the normal
/// input entry (<see cref="ecGame.ecTouchBegin"/> / <see cref="ecGame.ecTouchEnd"/>), once at a point a
/// real window pixel maps to (when the current window has bars there) and once at a synthetic point,
/// and once more straight into the GUI tree (bypassing the entry filter);</item>
/// <item>if it is hidden (itself or an ancestor not shown or not active), checks that it does not
/// accept input.</item>
/// </list>
/// Buttons do not fire while the probe runs (<see cref="GUIButton.ProbeMode"/>), so probing never
/// changes the game. Every button that would have fired is logged as "hittest failed".
/// </summary>
public static class HitTestProbe
{
    /// <summary>Maps a window pixel to logical coordinates; set by the host (null without a window).</summary>
    public static Func<int, int, (float x, float y)> WindowToLogical;
    /// <summary>Window size in pixels; set by the host.</summary>
    public static Func<(int w, int h)> WindowSize;

    public static int Run(string label)
    {
        var gfx = ecGraphics.Instance();
        float sw = gfx.screenW, sh = gfx.screenH;
        var buttons = new List<GUIButton>();
        Collect(GUIManager.Instance(), buttons);

        // logical area the window covers (wider than the screen when the window has bars)
        float lx0 = 0, ly0 = 0, lx1 = sw, ly1 = sh;
        if (WindowToLogical != null && WindowSize != null)
        {
            var (ww, wh) = WindowSize();
            (lx0, ly0) = WindowToLogical(0, 0);
            (lx1, ly1) = WindowToLogical(ww, wh);
        }

        int offScreen = 0, hidden = 0, probes = 0, failures = 0;
        var fired = new List<GUIButton>();
        Action<GUIButton> onFire = b => fired.Add(b);
        GUIButton.ProbeMode = true;
        GUIButton.Probed += onFire;
        try
        {
            foreach (var button in buttons)
            {
                if (button.IsDisposed) continue;
                var r = button.GetAbsRect();
                bool shownChain = ShownChain(button);
                if (!shownChain)
                {
                    hidden++;
                    if (button.AcceptsInputAt(r.x + r.w * 0.5f, r.y + r.h * 0.5f))
                    {
                        failures++;
                        Log($"hittest failed [{label}]: hidden {Describe(button, r)} accepts input");
                    }
                    continue;
                }
                if (!OutsidePart(r, 0, 0, sw, sh, out var part)) continue;
                offScreen++;

                var points = new List<(float x, float y, string how)>();
                // a point a real window pixel can reach (letterbox bars), if this window has one
                if (Intersect(part, lx0, ly0, lx1, ly1, out var reachable))
                    points.Add((reachable.x + reachable.w * 0.5f, reachable.y + reachable.h * 0.5f, "window"));
                points.Add((part.x + part.w * 0.5f, part.y + part.h * 0.5f, "synthetic"));

                foreach (var (x, y, how) in points)
                {
                    // through the input entry (what the host and the scripts use)
                    probes++;
                    fired.Clear();
                    ecGame.ecTouchBegin(x, y, 9);
                    ecGame.ecTouchEnd(x, y, 9);
                    if (fired.Contains(button))
                    {
                        failures++;
                        Log($"hittest failed [{label}]: off-screen {Describe(button, r)} fired at {x:0.#},{y:0.#} ({how}, input entry)");
                    }
                    // straight into the GUI tree: the widgets themselves must refuse points off the screen
                    probes++;
                    fired.Clear();
                    GUIManager.Instance().PostEvent(Event.Touch(Event.Info.TouchInfo.BEGIN, x, y, 9));
                    GUIManager.Instance().PostEvent(Event.Touch(Event.Info.TouchInfo.END, x, y, 9));
                    if (fired.Contains(button))
                    {
                        failures++;
                        Log($"hittest failed [{label}]: off-screen {Describe(button, r)} fired at {x:0.#},{y:0.#} ({how}, GUI tree)");
                    }
                    button.ResetPress();
                }
            }
        }
        finally
        {
            GUIButton.Probed -= onFire;
            GUIButton.ProbeMode = false;
        }
        Log($"hittest [{label}]: screen {sw}x{sh}, window covers {lx0:0.#},{ly0:0.#} .. {lx1:0.#},{ly1:0.#}; {buttons.Count} buttons, {offScreen} off screen, {hidden} hidden, {probes} probes, {failures} failures");
        return failures;
    }

    private static void Collect(GUIElement e, List<GUIButton> into)
    {
        if (e is GUIButton b) into.Add(b);
        for (var c = e.firstChild; c != null; c = c.nextSibling) Collect(c, into);
    }

    private static bool ShownChain(GUIElement e)
    {
        for (var p = e; p != null; p = p.parent)
            if ((p.flags & (GUIElement.ACTIVE | GUIElement.SHOWN)) != (GUIElement.ACTIVE | GUIElement.SHOWN)) return false;
        return true;
    }

    private static string Describe(GUIButton b, GUIRect r) =>
        $"{b.GetType().Name} in {b.parent?.GetType().Name ?? "?"} at {r.x:0.#},{r.y:0.#} {r.w:0.#}x{r.h:0.#}";

    /// <summary>The largest part of <paramref name="r"/> outside the screen rectangle, if any.</summary>
    private static bool OutsidePart(GUIRect r, float x0, float y0, float x1, float y1, out GUIRect part)
    {
        GUIRect found = default;
        float best = 0;
        void Consider(float ax, float ay, float bx, float by)
        {
            if (bx <= ax || by <= ay) return;
            float area = (bx - ax) * (by - ay);
            if (area > best) { best = area; found = new GUIRect(ax, ay, bx - ax, by - ay); }
        }
        float rx1 = r.x + r.w, ry1 = r.y + r.h;
        Consider(r.x, r.y, Math.Min(rx1, x0), ry1);          // left of the screen
        Consider(Math.Max(r.x, x1), r.y, rx1, ry1);          // right
        Consider(r.x, r.y, rx1, Math.Min(ry1, y0));          // above
        Consider(r.x, Math.Max(r.y, y1), rx1, ry1);          // below
        part = found;
        return best > 0;
    }

    private static bool Intersect(GUIRect r, float x0, float y0, float x1, float y1, out GUIRect result)
    {
        float ax = Math.Max(r.x, x0), ay = Math.Max(r.y, y0);
        float bx = Math.Min(r.x + r.w, x1), by = Math.Min(r.y + r.h, y1);
        result = new GUIRect(ax, ay, bx - ax, by - ay);
        return bx - ax > 0.5f && by - ay > 0.5f;
    }
}
