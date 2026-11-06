using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;

namespace WC2;

[Activity(
    Label = "World Conqueror 2",
    MainLauncher = true,
    Icon = "@mipmap/appicon",
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

        string assetsDir = Path.Combine(FilesDir.AbsolutePath, "game");
        string docsDir = Path.Combine(FilesDir.AbsolutePath, "documents");
        try { AssetExtractor.EnsureExtracted(Assets, "game", assetsDir, PackageManager.GetPackageInfo(PackageName, 0).VersionCode.ToString()); }
        catch (Exception ex) { Globals.Log("Asset extraction failed: " + ex); }

        var metrics = Resources.DisplayMetrics;
        int w = Math.Max(metrics.WidthPixels, metrics.HeightPixels);
        int h = Math.Min(metrics.WidthPixels, metrics.HeightPixels);
        Globals.g_contenscalefactor = 2.0f;
        int logicalH = 320;
        int logicalW = Math.Clamp((int)MathF.Round(320f * w / Math.Max(1, h)), 480, 640);

        ecGame.ConfigurePaths(assetsDir, null);
        Globals.SetDocumentPath(docsDir);

        game = new WC2Game(logicalW, logicalH, w, h);
        view = game.Services.GetService(typeof(View)) as View;
        SetContentView(view);
        game.Run();
    }

    public override void OnBackPressed()
    {
        try
        {
            if (ecGame.ecBackPressed()) return;
        }
        catch (Exception ex) { Globals.Log("Back failed: " + ex.Message); }
        base.OnBackPressed();
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
