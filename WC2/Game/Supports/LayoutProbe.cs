using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WC2;

/// <summary>
/// Display checks for the test scripts:
/// <list type="bullet">
/// <item><c>layout LABEL</c> (<see cref="Run"/>): the logical screen is centred on the window (bars differ by
/// at most a pixel), clear of the display cutout, as large as the safe area allows; the device viewport
/// and back buffer are the whole window; a touch at the window pixel of a logical point lands on it.</item>
/// <item><c>bars LABEL</c> (<see cref="RequestBarsCheck"/>): after the next frame every window pixel outside
/// the logical screen is black, so nothing placed off the screen shows.</item>
/// <item><c>ghost on|off</c> (<see cref="Ghost"/>): white test buttons straddling every edge and one far
/// away, to check that off-screen parts are neither drawn (bars) nor clickable (hittest).</item>
/// </list>
/// Problems are logged as "layout failed" / "bars failed", which fail the script.
/// </summary>
public static class LayoutProbe
{
    public static int Run(string label)
    {
        var g = ecGraphics.Instance();
        int failures = 0;
        void Fail(string message) { failures++; Log($"layout failed [{label}]: {message}"); }

        float scale = g.layoutToScreenMultiplier[0];
        float drawW = g.screenW * scale, drawH = g.screenH * scale;
        float left = g.offsetX, right = g.layoutW - g.offsetX - drawW;
        float top = g.offsetY, bottom = g.layoutH - g.offsetY - drawH;
        if (MathF.Abs(left - right) > 1.01f) Fail($"not centred horizontally: bars {left:0.#} / {right:0.#} px");
        if (MathF.Abs(top - bottom) > 1.01f) Fail($"not centred vertically: bars {top:0.#} / {bottom:0.#} px");
        if (left < 0 || right < -0.01f || top < 0 || bottom < -0.01f) Fail("larger than the window");
        var ins = g.safeInsets;
        if (left < ins[0] || right < ins[2] - 0.01f || top < ins[1] || bottom < ins[3] - 0.01f)
            Fail($"overlaps the display cutout (insets {ins[0]},{ins[1]},{ins[2]},{ins[3]})");
        int insetX = Math.Max(ins[0], ins[2]), insetY = Math.Max(ins[1], ins[3]);
        if (MathF.Abs(drawW - (g.layoutW - 2 * insetX)) > 1.01f && MathF.Abs(drawH - (g.layoutH - 2 * insetY)) > 1.01f)
            Fail($"smaller than the safe area allows ({drawW:0.#}x{drawH:0.#} in {g.layoutW - 2 * insetX}x{g.layoutH - 2 * insetY})");
        if (g.screenClip.X != (int)g.offsetX || g.screenClip.Y != (int)g.offsetY ||
            Math.Abs(g.screenClip.Width - drawW) > 1.01f || Math.Abs(g.screenClip.Height - drawH) > 1.01f)
            Fail($"clip rectangle {g.screenClip} does not match the picture");

        var device = g.device;
        if (device != null)
        {
            var pp = device.PresentationParameters;
            if (pp.BackBufferWidth != g.layoutW || pp.BackBufferHeight != g.layoutH)
                Fail($"back buffer {pp.BackBufferWidth}x{pp.BackBufferHeight} is not the window {g.layoutW}x{g.layoutH}");
            var vp = device.Viewport;
            if (vp.X != 0 || vp.Y != 0 || vp.Width != g.layoutW || vp.Height != g.layoutH)
                Fail($"viewport {vp.X},{vp.Y} {vp.Width}x{vp.Height} is not the whole window");
        }

        // touches and drawing use one transform
        foreach (var (x, y) in new[] { (0f, 0f), (g.screenW * 0.5f, g.screenH * 0.5f), (g.screenW - 1f, g.screenH - 1f) })
        {
            g.LogicalToWindow(x, y, out float px, out float py);
            g.WindowToLogical(px, py, out float rx, out float ry);
            if (MathF.Abs(rx - x) > 0.01f || MathF.Abs(ry - y) > 0.01f) Fail($"round trip {x},{y} -> {px},{py} -> {rx},{ry}");
        }
        // the first window pixel outside the picture on each side is off the logical screen
        float midX = g.offsetX + drawW * 0.5f, midY = g.offsetY + drawH * 0.5f;
        CheckOff(g, left >= 1, g.offsetX - 0.5f, midY, "left bar", Fail);
        CheckOff(g, right >= 1, g.offsetX + drawW + 0.5f, midY, "right bar", Fail);
        CheckOff(g, top >= 1, midX, g.offsetY - 0.5f, "top bar", Fail);
        CheckOff(g, bottom >= 1, midX, g.offsetY + drawH + 0.5f, "bottom bar", Fail);

        Log($"layout [{label}]: window {g.layoutW}x{g.layoutH}, cutout insets {ins[0]},{ins[1]},{ins[2]},{ins[3]}, logical {g.screenW}x{g.screenH} " +
            $"scale {scale:0.###}, bars left {left:0.#} right {right:0.#} top {top:0.#} bottom {bottom:0.#}; {failures} failures");
        return failures;
    }

    private static void CheckOff(ecGraphics g, bool hasBar, float px, float py, string where, Action<string> fail)
    {
        if (!hasBar) return;
        g.WindowToLogical(px, py, out float x, out float y);
        if (g.IsOnScreen(x, y)) fail($"{where} pixel {px},{py} maps onto the screen ({x:0.##},{y:0.##})");
    }

    // ---- bars ----------------------------------------------------------------------------

    private static string s_barsLabel;

    public static void RequestBarsCheck(string label) => s_barsLabel = label;

    /// <summary>Called by the host after each frame is rendered; runs a requested bars check.</summary>
    public static void AfterRender(GraphicsDevice device)
    {
        if (s_barsLabel == null || device == null) return;
        string label = s_barsLabel;
        s_barsLabel = null;
        var g = ecGraphics.Instance();
        int w = device.PresentationParameters.BackBufferWidth, h = device.PresentationParameters.BackBufferHeight;
        var data = new Color[w * h];
        device.GetBackBufferData(data);
        var clip = g.screenClip;
        int outside = 0, lit = 0, insideLit = 0;
        int firstX = -1, firstY = -1;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var c = data[y * w + x];
                bool dark = c.R <= 8 && c.G <= 8 && c.B <= 8;
                if (clip.Contains(x, y)) { if (!dark) insideLit++; continue; }
                outside++;
                if (dark) continue;
                if (lit++ == 0) { firstX = x; firstY = y; }
            }
        }
        if (lit > 0)
            Log($"bars failed [{label}]: {lit} of {outside} window pixels outside the logical screen are not black (first at {firstX},{firstY})");
        else
            Log($"bars [{label}]: {outside} window pixels outside the logical screen, all black; {insideLit} lit pixels on the screen");
        if (insideLit == 0) Log($"bars failed [{label}]: the logical screen is black (nothing drawn?)");
    }

    // ---- ghost buttons ---------------------------------------------------------------------

    /// <summary>A plain white button: easy to spot in the bars if clipping fails.</summary>
    private sealed class GhostButton : GUIButton
    {
        public override void OnRender()
        {
            GetAbsRect(out var r);
            ecGraphics.Instance().RenderRect(r.x, r.y, r.w, r.h, 0xFFFFFFFFu);
        }
    }

    private static readonly List<GUIButton> s_ghosts = new();

    public static void Ghost(bool on)
    {
        var manager = GUIManager.Instance();
        foreach (var ghost in s_ghosts)
            if (!ghost.IsDisposed) manager.FreeChild(ghost);
        s_ghosts.Clear();
        if (!on) return;
        var g = ecGraphics.Instance();
        float sw = g.screenW, sh = g.screenH;
        var rects = new[]
        {
            new GUIRect(sw - 24f, sh * 0.5f - 20f, 64f, 40f),      // straddles the right edge
            new GUIRect(-40f, sh * 0.5f - 20f, 64f, 40f),          // straddles the left edge
            new GUIRect(sw * 0.5f - 32f, -24f, 64f, 40f),          // straddles the top edge
            new GUIRect(sw * 0.5f - 32f, sh - 16f, 64f, 40f),      // straddles the bottom edge
            new GUIRect(sw + 4f, 10f, 64f, 40f),                   // just past the right edge (the original's way to hide)
            new GUIRect(100000f, 100f, 64f, 40f),                  // far away
        };
        foreach (var r in rects)
        {
            var ghost = new GhostButton { rect = r };
            manager.AddChild(ghost, false);
            s_ghosts.Add(ghost);
        }
        Log($"ghost: {s_ghosts.Count} test buttons at the screen edges");
    }
}
