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

        // the runtime package is read straight from the APK (assets/pack); mods come from the app's
        // external files folder (Android/data/<package>/files/mods, reachable over USB) and documents/mods
        var (package, aliases, description) = OpenPackage();
        string externalMods = GetExternalFilesDir(null)?.AbsolutePath is string ext ? Path.Combine(ext, "mods") : null;
        ecGame.ConfigureAssets(package, aliases, description, new[] { externalMods, Path.Combine(docsDir, "mods") }, null);
        ecGame.SelectLayout(w, h, out int logicalW, out int logicalH);   // after the assets: the layout profiles are data
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

    /// <summary>
    /// The package is read in place from the APK. Only when that fails (an asset that cannot be
    /// opened, a broken index) is it copied out once to files/pack-&lt;install time&gt; and read from there.
    /// </summary>
    private (IAssetSource source, Dictionary<string, string> aliases, string description) OpenPackage()
    {
        try
        {
            var apk = new AndroidAssetSource(Assets, "pack");
            apk.SelfTest();
            return (apk, apk.Aliases, apk.Name);
        }
        catch (Exception ex)
        {
            Globals.Log("Reading the package in the APK failed (" + ex.Message + "); using an extracted copy");
            string dir = ExtractPackage();
            var source = new DirectoryAssetSource(dir);
            var aliases = AssetSystem.ReadAliases(name =>
            {
                string file = Path.Combine(dir, name);
                return File.Exists(file) ? File.OpenRead(file) : null;
            });
            return (source, aliases, dir);
        }
    }

    private string InstallStamp()
    {
        try { return PackageManager!.GetPackageInfo(PackageName!, 0)!.LastUpdateTime.ToString(); }
        catch { return Globals.strVersionName; }
    }

    /// <summary>Copies assets/pack out of the APK (once per installed version); returns the folder.</summary>
    private string ExtractPackage()
    {
        string dir = Path.Combine(FilesDir!.AbsolutePath, "pack-" + InstallStamp());
        string done = Path.Combine(dir, ".complete");
        if (File.Exists(done)) return dir;
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        int count = 0;
        void Copy(string assetDir)
        {
            foreach (var name in Assets!.List(assetDir) ?? Array.Empty<string>())
            {
                string child = assetDir + "/" + name;
                string[] sub = Assets.List(child);
                if (sub != null && sub.Length > 0) { Copy(child); continue; }
                string target = Path.Combine(dir, child.Substring("pack/".Length));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var input = Assets.Open(child);
                using var output = File.Create(target);
                input.CopyTo(output);
                count++;
            }
        }
        Copy("pack");
        File.WriteAllText(done, count.ToString());
        Globals.Log($"Extracted {count} package files to {dir}");
        return dir;
    }

    /// <summary>Earlier builds copied all assets to files/game; extracted copies of older versions go too.</summary>
    private void RemoveOldExtraction()
    {
        try
        {
            string old = Path.Combine(FilesDir.AbsolutePath, "game");
            if (Directory.Exists(old)) Directory.Delete(old, true);
            string current = "pack-" + InstallStamp();
            foreach (var dir in Directory.GetDirectories(FilesDir.AbsolutePath, "pack-*"))
                if (!Path.GetFileName(dir).Equals(current, StringComparison.Ordinal)) Directory.Delete(dir, true);
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
#pragma warning disable CA1422 // the system UI flags are deprecated (API 30) but still honoured on every supported API level
            decor.SystemUiFlags =
                SystemUiFlags.ImmersiveSticky | SystemUiFlags.LayoutStable | SystemUiFlags.LayoutHideNavigation |
                SystemUiFlags.LayoutFullscreen | SystemUiFlags.HideNavigation | SystemUiFlags.Fullscreen;
#pragma warning restore CA1422
        }
        catch (Exception ex) { Globals.Log("Immersive mode failed: " + ex.Message); }
    }
}
