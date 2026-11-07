using System.Collections.Concurrent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace WC2;

public enum HostTouchKind { Down, Move, Up, Cancel }

/// <summary>A raw touch from the platform: pointer id and window (back buffer) pixels.</summary>
public readonly record struct HostTouch(int Id, HostTouchKind Kind, float X, float Y);

/// <summary>
/// MonoGame host. Maps the window, touches and mouse onto the original touch/orientation API and
/// drives ecGameInit / ecGameUpdate / ecGameRender.
///
/// Display: the back buffer always covers the whole window (on Android MonoGame would letterbox it
/// to the preferred size and shift the viewport and the touches by that amount; the viewport offset
/// is lost after a GL context loss, which moved the picture to the left). The game centres the
/// logical screen itself (<see cref="ecGraphics.Resize(int, int, int, int, int, int)"/>), keeping
/// clear of the display cutout the host reports, and touches use the same transform.
/// </summary>
public sealed class WC2Game : Game
{
    /// <summary>
    /// Set by a host that feeds its own touches into <see cref="HostTouches"/> (Android, from the UI
    /// thread); TouchPanel, the mouse and the keyboard are not polled then.
    /// </summary>
    public static volatile bool HostDeliversTouches;
    public static readonly ConcurrentQueue<HostTouch> HostTouches = new();
    /// <summary>
    /// Unsafe window border in pixels {left, top, right, bottom} (the display cutout), published by the
    /// host as a new array whenever it changes; applied at the start of the next frame.
    /// </summary>
    public static volatile int[] HostSafeInsets = new int[4];
    private static int s_pendingBack;

    /// <summary>Back key or gesture from another thread (Android UI thread); handled on the game thread.</summary>
    public static void RequestBack() => Interlocked.Increment(ref s_pendingBack);

    private readonly GraphicsDeviceManager graphics;
    private readonly int logicalW;
    private readonly int logicalH;
    private bool initialized;

    private MouseState previousMouse;
    private KeyboardState previousKeyboard;
    private bool leftDown;
    private bool rightDown;
    private float pinchDistance;
    private const int LeftTouchIndex = 1;
    private const int RightTouchIndex = 2;
    private int previousScrollValue;
    private bool wasActive = true;
    private double elapsed;

    /// <summary>When &gt; 0 the game exits automatically after this many seconds (automated smoke tests).</summary>
    public static double ExitAfterSeconds;
    /// <summary>When set, a PNG of the back buffer is written here right before an automatic exit (and every ScreenshotEverySeconds).</summary>
    public static string ScreenshotPath;
    public static double ScreenshotEverySeconds;
    private double nextScreenshot;
    private bool exitRequested;
    private string pendingShot;

    public WC2Game(int logicalWidth, int logicalHeight, int windowWidth, int windowHeight)
    {
        logicalW = logicalWidth;
        logicalH = logicalHeight;
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = windowWidth,
            PreferredBackBufferHeight = windowHeight,
            SynchronizeWithVerticalRetrace = true,
            GraphicsProfile = GraphicsProfile.HiDef,
        };
        // the 4096 pixel atlases need HiDef; fall back to Reach on devices without it
        graphics.PreparingDeviceSettings += (sender, e) =>
        {
            try
            {
                if (!e.GraphicsDeviceInformation.Adapter.IsProfileSupported(GraphicsProfile.HiDef))
                    e.GraphicsDeviceInformation.GraphicsProfile = GraphicsProfile.Reach;
            }
            catch { }
        };
        IsMouseVisible = true;
        IsFixedTimeStep = false;
        Window.AllowUserResizing = true;
        Window.Title = "World Conqueror 2 (C# port)";
        OnExitRequested = () => Exit();
        OnFullScreenChanged = full =>
        {
            try { graphics.IsFullScreen = full; graphics.ApplyChanges(); } catch { }
        };
    }

    private static readonly int[] s_noInsets = new int[4];

    /// <summary>
    /// Keeps the renderer on the whole back buffer, once per update and per draw (window resized,
    /// Android view resized when the bars hide or the notch area is granted, GL context recreated,
    /// cutout insets changed). It is the only place the window size reaches the game.
    /// </summary>
    private void SyncDisplay()
    {
        if (OperatingSystem.IsAndroid()) KeepBackBufferOnWholeView();
        var pp = GraphicsDevice.PresentationParameters;
        int bw = pp.BackBufferWidth, bh = pp.BackBufferHeight;
        if (bw <= 0 || bh <= 0) return;
        // MonoGame's GraphicsDevice.Initialize (after a lost GL context) and its Android letterbox
        // move the viewport; the game's transform assumes it starts at the window corner
        var vp = GraphicsDevice.Viewport;
        if (vp.X != 0 || vp.Y != 0 || vp.Width != bw || vp.Height != bh)
            GraphicsDevice.Viewport = new Viewport(0, 0, bw, bh);
        var gfx = ecGraphics.Instance();
        var insets = HostSafeInsets ?? s_noInsets;
        if (bw == gfx.layoutW && bh == gfx.layoutH && insets[0] == gfx.safeInsets[0] && insets[1] == gfx.safeInsets[1]
            && insets[2] == gfx.safeInsets[2] && insets[3] == gfx.safeInsets[3])
            return;
        gfx.Resize(bw, bh, insets[0], insets[1], insets[2], insets[3]);
        Log($"Display: window {bw}x{bh}, cutout insets {insets[0]},{insets[1]},{insets[2]},{insets[3]}; " +
            $"logical {gfx.screenW}x{gfx.screenH} drawn at {gfx.screenClip.X},{gfx.screenClip.Y} size {gfx.screenClip.Width}x{gfx.screenClip.Height} (scale {gfx.layoutToScreenMultiplier[0]:0.###})");
    }

    /// <summary>
    /// Android: MonoGame fits the back buffer into the view with the preferred aspect ratio, centres it
    /// with a viewport offset and subtracts that offset from the touches. Keeping the preferred size
    /// equal to the view (DisplayMode is the view size there) removes that second letterbox.
    /// </summary>
    private void KeepBackBufferOnWholeView()
    {
        var mode = GraphicsDevice.DisplayMode;
        int vw = mode.Width, vh = mode.Height;
        if (vw <= 0 || vh <= 0) return;
        var pp = GraphicsDevice.PresentationParameters;
        if (graphics.PreferredBackBufferWidth == vw && graphics.PreferredBackBufferHeight == vh &&
            pp.BackBufferWidth == vw && pp.BackBufferHeight == vh && Window.ClientBounds.X == 0 && Window.ClientBounds.Y == 0)
            return;
        graphics.PreferredBackBufferWidth = vw;
        graphics.PreferredBackBufferHeight = vh;
        graphics.ApplyChanges();
    }

    protected override void Initialize()
    {
        base.Initialize();
        var gfx = ecGraphics.Instance();
        gfx.Attach(GraphicsDevice);
        try
        {
            var pp = GraphicsDevice.PresentationParameters;
            int bw = pp.BackBufferWidth > 0 ? pp.BackBufferWidth : graphics.PreferredBackBufferWidth;
            int bh = pp.BackBufferHeight > 0 ? pp.BackBufferHeight : graphics.PreferredBackBufferHeight;
            ecGame.ecGameInit(logicalW, logicalH, 0, bw, bh);
            SyncDisplay();
        }
        catch (Exception ex)
        {
            Log("ecGameInit failed: " + ex);
            throw;
        }
        initialized = true;
        SmokeTest.ScreenshotRequest = name =>
        {
            string dir = string.IsNullOrEmpty(ScreenshotPath) ? AppContext.BaseDirectory : Path.GetDirectoryName(ScreenshotPath) ?? ".";
            pendingShot = Path.Combine(dir, name.EndsWith(".png") ? name : name + ".png");
        };
        previousMouse = Mouse.GetState();
        previousKeyboard = Keyboard.GetState();
        previousScrollValue = previousMouse.ScrollWheelValue;
    }

    /// <summary>Mouse position (window client units) to logical coordinates, via back buffer pixels.</summary>
    private void ToLogical(int px, int py, out float x, out float y)
    {
        var bounds = Window.ClientBounds;
        var pp = GraphicsDevice.PresentationParameters;
        float sx = bounds.Width > 0 ? (float)pp.BackBufferWidth / bounds.Width : 1f;
        float sy = bounds.Height > 0 ? (float)pp.BackBufferHeight / bounds.Height : 1f;
        ecGraphics.Instance().WindowToLogical(px * sx, py * sy, out x, out y);
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (dt > 0.05f) dt = 0.05f; // original nativeRender: a frame never advances the game by more than 50 ms

        // desktop: window focus drives the background/foreground hooks (state save, music) but never
        // the pause flag, so an unfocused window keeps running; Android handles the life cycle itself
        if (!ecGame.HostHandlesLifecycle && IsActive != wasActive)
        {
            wasActive = IsActive;
            try
            {
                if (IsActive) ecGame.ecGameWillEnterForeground();
                else { ecGame.ecGameDidEnterBackground(); CCSoundBox.GetInstance().PauseMusic(); }
            }
            catch (Exception ex) { Log("background/foreground hook failed: " + ex.Message); }
        }

        elapsed += dt;
        try
        {
            SyncDisplay();
            var router = TouchRouter.Instance();
            router.Update(elapsed);
            // back from the Android UI thread (key, system gesture): run it here, never concurrently with the game
            for (int pending = Interlocked.Exchange(ref s_pendingBack, 0); pending > 0; pending--)
                ecGame.CallNativeExit();
            if (HostDeliversTouches) DrainHostTouches(router);
            else if (IsActive) PollInput();
            else if (router.ActiveCount > 0) router.CancelAll(); // focus lost with a finger down
            SmokeTest.Step(elapsed, dt);
            ecGame.ecGameUpdate(dt);
        }
        catch (Exception ex)
        {
            Log("Update failed: " + ex);
        }
        base.Update(gameTime);
    }

    /// <summary>Android: touches queued by the activity, in order, through the router.</summary>
    private static void DrainHostTouches(TouchRouter router)
    {
        while (HostTouches.TryDequeue(out var t))
        {
            switch (t.Kind)
            {
                case HostTouchKind.Down: router.Begin(t.Id, t.X, t.Y); break;
                case HostTouchKind.Move: router.Move(t.Id, t.X, t.Y); break;
                case HostTouchKind.Up: router.End(t.Id, t.X, t.Y); break;
                default: router.Cancel(t.Id); break;
            }
        }
    }

    private bool touchPanelConnected;

    /// <summary>Desktop touch screens: MonoGame's TouchPanel (back buffer pixels) through the router.</summary>
    private bool PollTouchPanel()
    {
        TouchCollection touches;
        try { touches = TouchPanel.GetState(); }
        catch { return false; }
        if (touches.Count == 0 && !touchPanelConnected) return false;
        touchPanelConnected = touchPanelConnected || touches.Count > 0;
        var router = TouchRouter.Instance();
        foreach (var t in touches)
        {
            switch (t.State)
            {
                case TouchLocationState.Pressed: router.Begin(t.Id, t.Position.X, t.Position.Y); break;
                case TouchLocationState.Moved: router.Move(t.Id, t.Position.X, t.Position.Y); break;
                case TouchLocationState.Released:
                case TouchLocationState.Invalid: router.End(t.Id, t.Position.X, t.Position.Y); break;
            }
        }
        return true;
    }

    private void PollInput()
    {
        if (PollTouchPanel()) { previousKeyboard = Keyboard.GetState(); return; }
        var mouse = Mouse.GetState();
        var keyboard = Keyboard.GetState();
        bool inside = Window.ClientBounds.Width <= 0 || new Rectangle(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height).Contains(mouse.Position);
        ToLogical(mouse.X, mouse.Y, out float lx, out float ly);

        // left button = first finger
        if (mouse.LeftButton == ButtonState.Pressed && !leftDown)
        {
            if (inside)
            {
                leftDown = true;
                ecGame.ecTouchBegin(lx, ly, LeftTouchIndex);
            }
        }
        else if (mouse.LeftButton == ButtonState.Pressed && leftDown)
        {
            if (mouse.X != previousMouse.X || mouse.Y != previousMouse.Y)
                ecGame.ecTouchMove(lx, ly, LeftTouchIndex);
        }
        else if (mouse.LeftButton == ButtonState.Released && leftDown)
        {
            leftDown = false;
            ecGame.ecTouchEnd(lx, ly, LeftTouchIndex);
        }

        // right button = second finger (pinch zoom emulation around the first finger)
        if (mouse.RightButton == ButtonState.Pressed && !rightDown && inside)
        {
            rightDown = true;
            ecGame.ecTouchBegin(lx + 60f, ly + 60f, RightTouchIndex);
            pinchDistance = 0f;
        }
        else if (mouse.RightButton == ButtonState.Pressed && rightDown)
        {
            if (mouse.X != previousMouse.X || mouse.Y != previousMouse.Y)
                ecGame.ecTouchMove(lx + 60f, ly + 60f, RightTouchIndex);
        }
        else if (mouse.RightButton == ButtonState.Released && rightDown)
        {
            rightDown = false;
            ecGame.ecTouchEnd(lx + 60f, ly + 60f, RightTouchIndex);
        }

        // wheel = zoom
        int scroll = mouse.ScrollWheelValue;
        if (scroll != previousScrollValue)
        {
            float delta = (scroll - previousScrollValue) / 120f;
            previousScrollValue = scroll;
            ecGame.ecScrollWheel(lx, delta, ly);
        }

        // keys
        // Esc and Backspace behave like the Android back key (CallNativeExit: unhandled -> exit box)
        if (keyboard.IsKeyDown(Keys.Escape) && !previousKeyboard.IsKeyDown(Keys.Escape))
            ecGame.CallNativeExit();
        if (keyboard.IsKeyDown(Keys.Back) && !previousKeyboard.IsKeyDown(Keys.Back))
            ecGame.CallNativeExit();
        if (keyboard.IsKeyDown(Keys.F11) && !previousKeyboard.IsKeyDown(Keys.F11))
            ecSetFullScreen(!graphics.IsFullScreen);
        foreach (var key in keyboard.GetPressedKeys())
        {
            if (!previousKeyboard.IsKeyDown(key)) ecGame.ecKeyDown((int)key);
        }

        previousMouse = mouse;
        previousKeyboard = keyboard;
    }

    protected override void Draw(GameTime gameTime)
    {
        try
        {
            SyncDisplay();
            ecGame.ecGameRender();
            LayoutProbe.AfterRender(GraphicsDevice);
        }
        catch (Exception ex)
        {
            Log("Render failed: " + ex);
            GraphicsDevice.Clear(Color.Black);
        }
        base.Draw(gameTime);

        if (pendingShot != null) { SaveScreenshot(pendingShot); pendingShot = null; }
        bool timeUp = ExitAfterSeconds > 0 && elapsed >= ExitAfterSeconds;
        bool periodic = ScreenshotEverySeconds > 0 && elapsed >= nextScreenshot;
        if (!string.IsNullOrEmpty(ScreenshotPath) && (timeUp || periodic))
        {
            nextScreenshot = elapsed + ScreenshotEverySeconds;
            string path = periodic && !timeUp ? Path.Combine(Path.GetDirectoryName(ScreenshotPath) ?? ".", Path.GetFileNameWithoutExtension(ScreenshotPath) + "_" + ((int)elapsed).ToString("D3") + ".png") : ScreenshotPath;
            SaveScreenshot(path);
        }
        if (timeUp && !exitRequested)
        {
            exitRequested = true;
            Log("Smoke test: exiting after " + elapsed.ToString("F1") + "s");
            Exit();
        }
    }

    private void SaveScreenshot(string path)
    {
        try
        {
            int w = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int h = GraphicsDevice.PresentationParameters.BackBufferHeight;
            var data = new Color[w * h];
            GraphicsDevice.GetBackBufferData(data);
            var bytes = new byte[w * h * 4];
            for (int i = 0; i < data.Length; i++)
            {
                bytes[i * 4] = data[i].R; bytes[i * 4 + 1] = data[i].G; bytes[i * 4 + 2] = data[i].B; bytes[i * 4 + 3] = 255;
            }
            using var bmp = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Opaque));
            System.Runtime.InteropServices.Marshal.Copy(bytes, 0, bmp.GetPixels(), bytes.Length);
            using var img = SkiaSharp.SKImage.FromBitmap(bmp);
            using var png = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using var fs = File.Create(path);
            png.SaveTo(fs);
            Log("Screenshot saved: " + path);
        }
        catch (Exception ex)
        {
            Log("Screenshot failed: " + ex.Message);
        }
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        try { ecGame.ecGameShutdownHook(); } catch { }
        base.OnExiting(sender, args);
    }
}
