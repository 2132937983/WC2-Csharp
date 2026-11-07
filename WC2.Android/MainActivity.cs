using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;

namespace WC2;

/// <summary>
/// Android launcher. Mirrors what the original Wc2Activity / ecRenderer did around the native
/// library: layout selection from the screen aspect (nativeInit), nativePause / nativeResume on
/// the activity life cycle, CallNativeExit on the back key, and a full-screen immersive window.
///
/// Display and input (port additions):
/// <list type="bullet">
/// <item>The layout bucket follows the real full screen (the window covers the notch and the hidden
/// navigation bar), minus the display cutout; the cutout insets are published to the game on every
/// insets change, and the game keeps the picture centred and clear of them.</item>
/// <item>Touches are taken at the activity (window pixels, every pointer, ACTION_CANCEL included) and
/// queued for the game thread, where the touch router maps them with the renderer's transform and
/// recognizes the edge swipe for back.</item>
/// <item>The system back gesture is turned off along the side edges (it cancelled drags starting
/// there); the router's edge swipe replaces it. The back key and the system back (gesture navigation,
/// predictive back) are taken before MonoGame's view, which swallowed KEYCODE_BACK, and run on the
/// game thread.</item>
/// </list>
/// </summary>
[Activity(
    Label = "@string/app_name",
    MainLauncher = true,
    Icon = "@mipmap/appicon",
    RoundIcon = "@mipmap/appicon_round",
    Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
    AlwaysRetainTaskState = true,
    LaunchMode = LaunchMode.SingleInstance,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout | ConfigChanges.UiMode)]
public class MainActivity : AndroidGameActivity
{
    private WC2Game game;
    private View view;
    private Java.Lang.Object backCallback;
    private readonly int[] viewLocation = new int[2];
    private const int FlagCanceled = 0x20;
    private float density = 1f;
    /// <summary>Width of the system back-gesture zones (left, right) in pixels, from the window insets.</summary>
    private int gestureLeft, gestureRight;

    protected override void OnCreate(Bundle bundle)
    {
        base.OnCreate(bundle);

        string assetsDir = Path.Combine(FilesDir.AbsolutePath, "game");
        string docsDir = Path.Combine(FilesDir.AbsolutePath, "documents");
        try { AssetExtractor.EnsureExtracted(Assets, "game", assetsDir, PackageManager.GetPackageInfo(PackageName, 0).VersionCode.ToString()); }
        catch (Exception ex) { Globals.Log("Asset extraction failed: " + ex); }

        // the window covers the notch: set before the window is measured or attached
        ApplyWindowFlags();

        // original nativeInit: four phone buckets and the 1024x768 tablet layout, HD content on every device.
        // The buckets follow the real full screen (Resources.DisplayMetrics leaves out the navigation bar
        // and the notch, which the immersive window covers) minus the display cutout.
        RealScreenSize(out int w, out int h);
        int[] cutout = StartupCutout();
        int insetX = Math.Max(cutout[0], cutout[2]), insetY = Math.Max(cutout[1], cutout[3]);
        density = Resources.DisplayMetrics.Density;
        Globals.g_contenscalefactor = 2.0f;
        ecGame.SelectLayout(w - 2 * insetX, h - 2 * insetY, out int logicalW, out int logicalH);

        ecGame.ConfigurePaths(assetsDir, null);
        Globals.SetDocumentPath(docsDir);
        ecGame.HostHandlesLifecycle = true; // OnPause/OnResume below replace the window-focus polling
        Globals.Log($"Android screen {w}x{h}, density {density:0.###}, cutout {cutout[0]},{cutout[1]},{cutout[2]},{cutout[3]}, layout {logicalW}x{logicalH}");

        WC2Game.HostSafeInsets = cutout;
        WC2Game.HostDeliversTouches = true;
        TouchRouter.Instance().Density = density;

        game = new WC2Game(logicalW, logicalH, w, h);
        view = game.Services.GetService(typeof(View)) as View;
        SetContentView(view);
        if (view != null)
        {
            view.SetOnApplyWindowInsetsListener(new InsetsListener(this));
            view.LayoutChange += (sender, e) => UpdateGestureExclusion();
        }
        HideSystemUi();
        RegisterBackCallback();
        game.Run();
    }

    /// <summary>The whole screen in landscape (long side first), as the immersive window covers it.</summary>
    private void RealScreenSize(out int w, out int h)
    {
        int a, b;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            var bounds = WindowManager.CurrentWindowMetrics.Bounds;
            a = bounds.Width(); b = bounds.Height();
        }
        else
        {
            var size = new Android.Graphics.Point();
#pragma warning disable CA1422 // DefaultDisplay / GetRealSize: the API level 30+ path is above
            WindowManager.DefaultDisplay.GetRealSize(size);
#pragma warning restore CA1422
            a = size.X; b = size.Y;
        }
        if (a <= 0 || b <= 0)
        {
            var metrics = Resources.DisplayMetrics;
            a = metrics.WidthPixels; b = metrics.HeightPixels;
        }
        w = Math.Max(a, b);
        h = Math.Min(a, b);
    }

    /// <summary>
    /// Display cutout known before the window exists (API 29+), made symmetric and turned to landscape;
    /// the insets listener reports the exact values once the window is attached.
    /// </summary>
    private int[] StartupCutout()
    {
        int l = 0, t = 0, r = 0, b = 0;
        bool portrait = false;
        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                var metrics = WindowManager.CurrentWindowMetrics;
                var insets = metrics.WindowInsets.GetInsetsIgnoringVisibility(WindowInsets.Type.DisplayCutout());
                l = insets.Left; t = insets.Top; r = insets.Right; b = insets.Bottom;
                portrait = metrics.Bounds.Width() < metrics.Bounds.Height();
            }
            else if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
#pragma warning disable CA1422 // DefaultDisplay: the API level 30+ path is above
                var display = WindowManager.DefaultDisplay;
                var cut = display.Cutout;
                if (cut != null) { l = cut.SafeInsetLeft; t = cut.SafeInsetTop; r = cut.SafeInsetRight; b = cut.SafeInsetBottom; }
                var size = new Android.Graphics.Point();
                display.GetRealSize(size);
#pragma warning restore CA1422
                portrait = size.X < size.Y;
            }
        }
        catch (Exception ex) { Globals.Log("Cutout query failed: " + ex.Message); }
        int x = portrait ? Math.Max(t, b) : Math.Max(l, r);
        int y = portrait ? Math.Max(l, r) : Math.Max(t, b);
        return new[] { x, y, x, y };
    }

    /// <summary>Window insets changed (attached, turned around, bars shown or hidden): publish the cutout, refresh the edge exclusion.</summary>
    private sealed class InsetsListener : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        private readonly MainActivity activity;
        public InsetsListener(MainActivity activity) { this.activity = activity; }

        public WindowInsets OnApplyWindowInsets(View v, WindowInsets insets)
        {
            try { activity.OnInsets(insets); }
            catch (Exception ex) { Globals.Log("Insets failed: " + ex.Message); }
            return v.OnApplyWindowInsets(insets);
        }
    }

    private void OnInsets(WindowInsets insets)
    {
        if (insets == null) return;
        int l = 0, t = 0, r = 0, b = 0;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
        {
            var cut = insets.DisplayCutout;
            if (cut != null) { l = cut.SafeInsetLeft; t = cut.SafeInsetTop; r = cut.SafeInsetRight; b = cut.SafeInsetBottom; }
        }
        var old = WC2Game.HostSafeInsets;
        if (old == null || old[0] != l || old[1] != t || old[2] != r || old[3] != b)
        {
            WC2Game.HostSafeInsets = new[] { l, t, r, b };
            Globals.Log($"Android cutout insets {l},{t},{r},{b}");
        }
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
#pragma warning disable CA1422 // SystemGestureInsets: the replacement GetInsets(Type.SystemGestures()) needs API level 30
            var gestures = insets.SystemGestureInsets;
#pragma warning restore CA1422
            gestureLeft = gestures.Left;
            gestureRight = gestures.Right;
            TouchRouter.Instance().SystemEdgePx = Math.Max(gestureLeft, gestureRight);
        }
        UpdateGestureExclusion();
    }

    /// <summary>
    /// API 29+: the system back gesture is turned off along the full height of both side edges, so a drag
    /// that starts at the edge reaches the game instead of being cancelled; the touch router recognizes
    /// the edge swipe for back. (The 200 dp limit on exclusions does not apply while the navigation bar
    /// is stickily hidden, as it is here.)
    /// </summary>
    private void UpdateGestureExclusion()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Q || view == null) return;
        int w = view.Width, h = view.Height;
        if (w <= 0 || h <= 0) return;
        int edge = (int)MathF.Ceiling(Math.Max(TouchRouter.EdgeZoneDp * density, Math.Max(gestureLeft, gestureRight)));
        edge = Math.Min(edge, w / 2);
        view.SystemGestureExclusionRects = new List<Android.Graphics.Rect> { new(0, 0, edge, h), new(w - edge, 0, w, h) };
    }

    /// <summary>
    /// All touches go to the game through the queue (window pixels relative to the game view, which
    /// fills the window). MonoGame's view never sees them: its TouchPanel turned ACTION_CANCEL into a
    /// release (a click) and subtracted its own letterbox offset.
    /// </summary>
    public override bool DispatchTouchEvent(MotionEvent e)
    {
        if (e == null) return false;
        float ox = 0f, oy = 0f;
        if (view != null)
        {
            view.GetLocationInWindow(viewLocation);
            ox = viewLocation[0];
            oy = viewLocation[1];
        }
        var queue = WC2Game.HostTouches;
        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
            case MotionEventActions.PointerDown:
            {
                int i = e.ActionIndex;
                queue.Enqueue(new HostTouch(e.GetPointerId(i), HostTouchKind.Down, e.GetX(i) - ox, e.GetY(i) - oy));
                break;
            }
            case MotionEventActions.Move:
                for (int i = 0; i < e.PointerCount; i++)
                    queue.Enqueue(new HostTouch(e.GetPointerId(i), HostTouchKind.Move, e.GetX(i) - ox, e.GetY(i) - oy));
                break;
            case MotionEventActions.Up:
            case MotionEventActions.PointerUp:
            {
                int i = e.ActionIndex;
                // MotionEvent.FLAG_CANCELED (API 33): this pointer was cancelled (palm rejection), not lifted
                var kind = ((int)e.Flags & FlagCanceled) != 0 ? HostTouchKind.Cancel : HostTouchKind.Up;
                queue.Enqueue(new HostTouch(e.GetPointerId(i), kind, e.GetX(i) - ox, e.GetY(i) - oy));
                break;
            }
            case MotionEventActions.Cancel:
                for (int i = 0; i < e.PointerCount; i++)
                    queue.Enqueue(new HostTouch(e.GetPointerId(i), HostTouchKind.Cancel, 0f, 0f));
                break;
        }
        return true;
    }

    /// <summary>
    /// Back key (and Escape on a keyboard): handled here, before MonoGame's view, whose OnKeyDown
    /// consumed KEYCODE_BACK so that OnBackPressed never ran. One back per key release, on the game thread.
    /// </summary>
    public override bool DispatchKeyEvent(KeyEvent e)
    {
        if (e != null && (e.KeyCode == Keycode.Back || e.KeyCode == Keycode.Escape))
        {
            if (e.Action == KeyEventActions.Up && !e.IsCanceled) WC2Game.RequestBack();
            return true;
        }
        return base.DispatchKeyEvent(e);
    }

    /// <summary>
    /// API 33+: when the system routes back through the dispatcher (predictive back: targetSdk 36, or the
    /// manifest flag) the key event above never comes. Without that opt-in Android ignores this callback.
    /// </summary>
    private void RegisterBackCallback()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu) return;
        try
        {
            var callback = new BackCallback();
            OnBackInvokedDispatcher.RegisterOnBackInvokedCallback(Android.Window.IOnBackInvokedDispatcher.PriorityDefault, callback);
            backCallback = callback;
        }
        catch (Exception ex) { Globals.Log("Back callback failed: " + ex.Message); }
    }

    private sealed class BackCallback : Java.Lang.Object, Android.Window.IOnBackInvokedCallback
    {
        public void OnBackInvoked() => WC2Game.RequestBack();
    }

    // Wc2Activity.onPause -> nativePause (background save + pause flag); onResume -> nativeResume
    protected override void OnPause()
    {
        base.OnPause();
        try { ecGame.nativePause(); }
        catch (Exception ex) { Globals.Log("nativePause failed: " + ex.Message); }
    }

    protected override void OnResume()
    {
        try { ecGame.nativeResume(); }
        catch (Exception ex) { Globals.Log("nativeResume failed: " + ex.Message); }
        base.OnResume();
        HideSystemUi();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus) HideSystemUi(); // sticky immersive mode: re-hide the bars after a swipe
    }

#pragma warning disable CA1422, CS0672 // OnBackPressed is deprecated (API 33); kept for any path that still calls it
    public override void OnBackPressed()
    {
        // original CallNativeExit: the game handles the key, or the exit box appears; the activity never
        // finishes here. Queued: this runs on the UI thread, the game on its own thread.
        WC2Game.RequestBack();
    }
#pragma warning restore CA1422, CS0672

    private void ApplyWindowFlags()
    {
        try
        {
            // original theme Theme.*.NoTitleBar.Fullscreen; KeepScreenOn is a port addition (long AI turns)
            Window?.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.P && Window != null)
            {
                var attrs = Window.Attributes;
                attrs.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges; // the original declared notch support
                Window.Attributes = attrs;
            }
        }
        catch (Exception ex) { Globals.Log("Window flags failed: " + ex.Message); }
    }

    private void HideSystemUi()
    {
        try
        {
            var decor = Window?.DecorView;
            if (decor == null) return;
#pragma warning disable CA1422 // SystemUiVisibility is deprecated but still honoured on every supported API level
            decor.SystemUiVisibility = (StatusBarVisibility)(
                SystemUiFlags.ImmersiveSticky | SystemUiFlags.LayoutStable | SystemUiFlags.LayoutHideNavigation |
                SystemUiFlags.LayoutFullscreen | SystemUiFlags.HideNavigation | SystemUiFlags.Fullscreen);
#pragma warning restore CA1422
        }
        catch (Exception ex) { Globals.Log("Immersive mode failed: " + ex.Message); }
    }
}

/// <summary>Copies the APK assets to a real folder once, so the engine can use plain file IO.</summary>
public static class AssetExtractor
{
    public static void EnsureExtracted(Android.Content.Res.AssetManager assets, string sourceDir, string targetDir, string version)
    {
        string stamp = Path.Combine(targetDir, ".version");
        if (Directory.Exists(targetDir) && File.Exists(stamp) && File.ReadAllText(stamp) == version && File.Exists(Path.Combine(targetDir, "carddef.xml")))
            return;
        Directory.CreateDirectory(targetDir);
        CopyDir(assets, sourceDir, targetDir);
        File.WriteAllText(stamp, version);
    }

    private static void CopyDir(Android.Content.Res.AssetManager assets, string source, string target)
    {
        string[] entries = assets.List(source) ?? Array.Empty<string>();
        Directory.CreateDirectory(target);
        foreach (var name in entries)
        {
            string src = source.Length == 0 ? name : source + "/" + name;
            string dst = Path.Combine(target, name);
            string[] children = null;
            try { children = assets.List(src); } catch { }
            if (children != null && children.Length > 0)
            {
                CopyDir(assets, src, dst);
                continue;
            }
            try
            {
                using var input = assets.Open(src);
                using var output = File.Create(dst);
                input.CopyTo(output);
            }
            catch (Exception ex)
            {
                Globals.Log("Asset copy failed " + src + ": " + ex.Message);
            }
        }
    }
}
