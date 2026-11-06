using System.Text.Json.Nodes;

namespace WC2.Tests;

/// <summary>Paths and helpers shared by the unit tests.</summary>
internal static class Support
{
    /// <summary>The repository root (the folder with WC2.slnx).</summary>
    public static string Repo
    {
        get
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "WC2.slnx"))) return d.FullName;
            throw new InvalidOperationException("repository root not found");
        }
    }

    public static string Assets => Path.Combine(Repo, "assets");

    /// <summary>Mounts the repository's asset tree (no mods) as the only layer.</summary>
    public static void MountRepositoryAssets()
    {
        AssetSystem.Reset();
        var aliases = AssetSystem.ReadAliases(name =>
        {
            string f = Path.Combine(Assets, name);
            return File.Exists(f) ? File.OpenRead(f) : null;
        });
        AssetSystem.Mount(new AssetLayer { Id = "base", Source = new DirectoryAssetSource(Assets), Aliases = aliases });
        JsonData.SchemaResolver = DataPaths.SchemaFor;
        JsonData.ResetSchemas();
    }

    /// <summary>A fresh temporary folder with the given files (relative path -> text).</summary>
    public static string TempTree(params (string path, string text)[] files)
    {
        string dir = Path.Combine(Path.GetTempPath(), "wc2-tests", Guid.NewGuid().ToString("N"));
        foreach (var (path, text) in files)
        {
            string full = Path.Combine(dir, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static JsonNode Json(string text) => JsonData.Parse(text);
}

/// <summary>Tests that change the static asset layer must not run in parallel with each other.</summary>
[CollectionDefinition("assets", DisableParallelization = true)]
public sealed class AssetCollection { }
