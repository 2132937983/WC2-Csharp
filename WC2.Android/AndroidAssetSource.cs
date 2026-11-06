using Android.Content.Res;

namespace WC2;

/// <summary>
/// The runtime package inside the APK (assets/pack/...). Files are read straight from the APK, so
/// nothing is extracted on first start; the file list comes from the generated index
/// (generated/asset_index.json), which also makes existence checks cheap (AssetManager.List is slow).
/// MainActivity falls back to an extracted copy only when reading in place fails.
/// </summary>
public sealed class AndroidAssetSource : IAssetSource
{
    private readonly AssetManager assets;
    private readonly string root;
    private readonly List<string> files = new();

    public Dictionary<string, string> Aliases { get; }

    public AndroidAssetSource(AssetManager assets, string root)
    {
        this.assets = assets;
        this.root = root.TrimEnd('/');
        using var index = assets.Open(this.root + "/" + AssetSystem.IndexFileName);
        using var ms = new MemoryStream();
        index.CopyTo(ms);
        ms.Position = 0;
        using (var doc = System.Text.Json.JsonDocument.Parse(ms))
        {
            if (doc.RootElement.TryGetProperty("files", out var list))
                foreach (var p in list.EnumerateObject()) files.Add(p.Name);
        }
        ms.Position = 0;
        Aliases = AssetSystem.ReadIndexAliases(ms);
    }

    public string Name => "apk:" + root;

    /// <summary>Opens one listed file, so a package that cannot be read in place fails at start-up.</summary>
    public void SelfTest()
    {
        string probe = files.FirstOrDefault(f => !f.StartsWith("generated/", StringComparison.OrdinalIgnoreCase))
                       ?? throw new InvalidDataException("the package index lists no files");
        using var s = OpenRead(probe);
        if (s.Length == 0 && probe.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("empty " + probe);
    }

    public IEnumerable<string> ListFiles() => files;

    public Stream OpenRead(string path)
    {
        // asset streams cannot seek; hand out a memory copy so every reader can
        using var s = assets.Open(root + "/" + path);
        var ms = new MemoryStream();
        s.CopyTo(ms);
        ms.Position = 0;
        return ms;
    }
}
