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

    protected override void OnCreate(Bundle bundle)
    {
        base.OnCreate(bundle);

        string docsDir = Path.Combine(FilesDir.AbsolutePath, "documents");
        Globals.SetDocumentPath(docsDir);
        RemoveOldExtraction();

        // original nativeInit: four phone buckets and the 1024x768 tablet layout, HD content on every device
        var metrics = Resources.DisplayMetrics;
        int w = Math.Max(metrics.WidthPixels, metrics.HeightPixels);
        int h = Math.Min(metrics.WidthPixels, metrics.HeightPixels);
        Globals.g_contenscalefactor = 2.0f;
        ecGame.SelectLayout(w, h, out int logicalW, out int logicalH);

        // the runtime package is read straight from the APK (assets/pack); mods come from the app's
        // external files folder (Android/data/<package>/files/mods, reachable over USB) and documents/mods
        var package = new AndroidAssetSource(Assets, "pack");
        string externalMods = GetExternalFilesDir(null)?.AbsolutePath is string ext ? Path.Combine(ext, "mods") : null;
        ecGame.ConfigureAssets(package, package.Aliases, package.Name, new[] { externalMods, Path.Combine(docsDir, "mods") }, null);
        ecGame.HostHandlesLifecycle = true; // OnPause/OnResume below replace the window-focus polling

        ApplyWindowFlags();
        game = new WC2Game(logicalW, logicalH, w, h);
        view = game.Services.GetService(typeof(View)) as View;
        SetContentView(view);
        HideSystemUi();
        game.Run();
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

    public override void OnBackPressed()
    {
        // original CallNativeExit: the game handles the key, or the exit box appears; the activity never finishes here
        try { ecGame.CallNativeExit(); }
        catch (Exception ex) { Globals.Log("Back failed: " + ex.Message); }
    }

    /// <summary>Earlier builds copied all assets to files/game on first start; that copy is no longer used.</summary>
    private void RemoveOldExtraction()
    {
        try
        {
            string old = Path.Combine(FilesDir.AbsolutePath, "game");
            if (Directory.Exists(old)) Directory.Delete(old, true);
        }
        catch (Exception ex) { Globals.Log("Removing the old asset copy failed: " + ex.Message); }
    }

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
