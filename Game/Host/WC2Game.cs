using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace WC2;

/// <summary>
/// MonoGame host. Maps the window and mouse onto the original touch/orientation API and
/// drives ecGameInit / ecGameUpdate / ecGameRender.
/// </summary>
public sealed class WC2Game : Game
{
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

    /// <summary>
    /// Deterministic test mode (<c>--fixed-step</c>): every update advances the game by exactly 1/60 s
    /// and the loop runs without vsync, so scripted runs repeat exactly and finish faster.
    /// </summary>
    public static bool FixedStep;
    private const float FixedStepSeconds = 1f / 60f;

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
            SynchronizeWithVerticalRetrace = !FixedStep,
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
        Window.ClientSizeChanged += OnClientSizeChanged;
        OnExitRequested = () => Exit();
        OnFullScreenChanged = full =>
        {
            try { graphics.IsFullScreen = full; graphics.ApplyChanges(); } catch { }
        };
    }

    private void OnClientSizeChanged(object sender, EventArgs e)
    {
        if (!initialized) return;
        var bounds = Window.ClientBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        ecGraphics.Instance().Resize(bounds.Width, bounds.Height);
    }

    protected override void Initialize()
    {
        base.Initialize();
        var gfx = ecGraphics.Instance();
        gfx.Attach(GraphicsDevice);
        try
        {
            ecGame.ecGameInit(logicalW, logicalH, 0, graphics.PreferredBackBufferWidth, graphics.PreferredBackBufferHeight);
        }
        catch (Exception ex)
        {
            Log("ecGameInit failed: " + ex);
            throw;
        }
        initialized = true;
        SmokeTest.OutputDirectory = string.IsNullOrEmpty(ScreenshotPath) ? AppContext.BaseDirectory : Path.GetDirectoryName(Path.GetFullPath(ScreenshotPath));
        SmokeTest.ScreenshotRequest = name =>
        {
            string dir = string.IsNullOrEmpty(ScreenshotPath) ? AppContext.BaseDirectory : Path.GetDirectoryName(ScreenshotPath) ?? ".";
            pendingShot = Path.Combine(dir, name.EndsWith(".png") ? name : name + ".png");
        };
        HitTestProbe.WindowToLogical = (px, py) => { ToLogical(px, py, out float lx, out float ly); return (lx, ly); };
        HitTestProbe.WindowSize = () => (GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight);
        previousMouse = Mouse.GetState();
        previousKeyboard = Keyboard.GetState();
        previousScrollValue = previousMouse.ScrollWheelValue;
    }

    private void ToLogical(int px, int py, out float x, out float y)
    {
        var gfx = ecGraphics.Instance();
        x = (px - gfx.offsetX) / Math.Max(0.0001f, gfx.layoutToScreenMultiplier[0]);
        y = (py - gfx.offsetY) / Math.Max(0.0001f, gfx.layoutToScreenMultiplier[1]);
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = FixedStep ? FixedStepSeconds : (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (dt > 0.05f) dt = 0.05f; // original nativeRender: a frame never advances the game by more than 50 ms

        // desktop: window focus drives the background/foreground hooks (state save, music) but never
        // the pause flag, so an unfocused window keeps running; Android handles the life cycle itself
        if (!ecGame.HostHandlesLifecycle && !FixedStep && IsActive != wasActive)
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
            if (IsActive && !FixedStep) PollInput();
            SmokeTest.Step(elapsed, dt);
            ecGame.ecGameUpdate(dt);
        }
        catch (Exception ex)
        {
            Log("Update failed: " + ex);
        }
        base.Update(gameTime);
    }

    private readonly Dictionary<int, int> touchIds = new();
    private int nextTouchIndex = 1;
    private bool touchPanelConnected;

    /// <summary>Real touch screens (Android) feed the touch API; the mouse path stays for desktop.</summary>
    private bool PollTouchPanel()
    {
        TouchCollection touches;
        try { touches = TouchPanel.GetState(); }
        catch { return false; }
        if (touches.Count == 0 && !touchPanelConnected) return false;
        touchPanelConnected = touchPanelConnected || touches.Count > 0;
        foreach (var t in touches)
        {
            ToLogical((int)t.Position.X, (int)t.Position.Y, out float lx, out float ly);
            switch (t.State)
            {
                case TouchLocationState.Pressed:
                    if (!touchIds.ContainsKey(t.Id))
                    {
                        int index = nextTouchIndex++;
                        if (nextTouchIndex > 1000) nextTouchIndex = 1;
                        touchIds[t.Id] = index;
                        ecGame.ecTouchBegin(lx, ly, index);
                    }
                    break;
                case TouchLocationState.Moved:
                    if (touchIds.TryGetValue(t.Id, out int moveIndex)) ecGame.ecTouchMove(lx, ly, moveIndex);
                    break;
                case TouchLocationState.Released:
                case TouchLocationState.Invalid:
                    if (touchIds.Remove(t.Id, out int endIndex)) ecGame.ecTouchEnd(lx, ly, endIndex);
                    break;
            }
        }
        return true;
    }

    private void PollInput()
    {
        if (PollTouchPanel()) { previousKeyboard = Keyboard.GetState(); return; }
        var mouse = Mouse.GetState();
        var keyboard = Keyboard.GetState();
        bool inside = GraphicsDevice.Viewport.Bounds.Contains(mouse.Position);
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
            ecGame.ecGameRender();
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
