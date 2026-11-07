namespace WC2;

/// <summary>
/// Regression probe for hidden controls (script command <c>hittest LABEL</c>).
///
/// The original hides several controls by moving them off the screen (the campaign series buttons at
/// x = screen width, the in-game round, card and pause buttons just past the right or bottom edge,
/// the gold panel above the top edge until they slide in) or by hiding them. On a window or phone
/// whose aspect ratio differs from the layout, the area outside the logical screen is part of the
/// window (letterbox bars, the notch margin), so such a control must not react to a touch there.
/// The probe walks the whole GUI tree and, for every button:
/// <list type="bullet">
/// <item>if any part of it lies outside the logical screen, touches that part: at a real window pixel
/// through the touch router (the path Android touches take) when the window has bars there, at a
/// synthetic point through the input entry (<see cref="ecGame.ecTouchBegin"/>), and straight into the
/// GUI tree (bypassing the entry filter);</item>
/// <item>if it is hidden (itself or an ancestor not shown or not active), checks that it does not
/// accept input.</item>
/// </list>
/// Buttons do not fire while the probe runs (<see cref="GUIButton.ProbeMode"/>), so probing never
/// changes the game. Every button that would have fired is logged as "hittest failed".
/// </summary>
public static class HitTestProbe
{
    private const int ProbeIndex = 9;
    private const int ProbePointer = -77;

    public static int Run(string label)
    {
        var gfx = ecGraphics.Instance();
        float sw = gfx.screenW, sh = gfx.screenH;
        var buttons = new List<GUIButton>();
        Collect(GUIManager.Instance(), buttons);

        // logical area the window covers (larger than the screen when the window has bars)
        gfx.WindowToLogical(0, 0, out float lx0, out float ly0);
        gfx.WindowToLogical(gfx.layoutW, gfx.layoutH, out float lx1, out float ly1);

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
                if (!ShownChain(button))
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

                // a point a real window pixel reaches (letterbox bars), if this window has one there
                if (Intersect(part, lx0, ly0, lx1, ly1, out var reachable))
                {
                    float x = reachable.x + reachable.w * 0.5f, y = reachable.y + reachable.h * 0.5f;
                    gfx.LogicalToWindow(x, y, out float px, out float py);
                    probes++;
                    fired.Clear();
                    var router = TouchRouter.Instance();
                    router.Begin(ProbePointer, px, py);
                    router.End(ProbePointer, px, py);
                    if (fired.Contains(button))
                    {
                        failures++;
                        Log($"hittest failed [{label}]: off-screen {Describe(button, r)} fired at window pixel {px:0},{py:0} (touch router)");
                    }
                    button.ResetPress();
                }

                float sx = part.x + part.w * 0.5f, sy = part.y + part.h * 0.5f;
                // through the input entry (what the hosts and the scripts use)
                probes++;
                fired.Clear();
                ecGame.ecTouchBegin(sx, sy, ProbeIndex);
                ecGame.ecTouchEnd(sx, sy, ProbeIndex);
                if (fired.Contains(button))
                {
                    failures++;
                    Log($"hittest failed [{label}]: off-screen {Describe(button, r)} fired at {sx:0.#},{sy:0.#} (input entry)");
                }
                // straight into the GUI tree: the widgets themselves must refuse points off the screen
                probes++;
                fired.Clear();
                GUIManager.Instance().PostEvent(Event.Touch(Event.Info.TouchInfo.BEGIN, sx, sy, ProbeIndex));
                GUIManager.Instance().PostEvent(Event.Touch(Event.Info.TouchInfo.END, sx, sy, ProbeIndex));
                if (fired.Contains(button))
                {
                    failures++;
                    Log($"hittest failed [{label}]: off-screen {Describe(button, r)} fired at {sx:0.#},{sy:0.#} (GUI tree)");
                }
                button.ResetPress();
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
