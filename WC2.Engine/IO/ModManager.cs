using System.Text.Json;

namespace WC2;

/// <summary>Contents of a mod's <c>mod.json</c>.</summary>
public sealed class ModInfo
{
    public string id = "";
    public string name = "";
    public string version = "";
    public string author = "";
    public string description = "";
    /// <summary>Mods are mounted in ascending priority; a higher priority wins over a lower one.</summary>
    public int priority;
    public bool enabled = true;
    /// <summary>Folder the mod was loaded from.</summary>
    public string folder = "";
}

/// <summary>
/// Finds mods and mounts them on top of the base package. A mod is a folder with a <c>mod.json</c>
/// and files laid out like the package (<c>gfx/...</c>, <c>data/...</c>, <c>lang/...</c>):
/// <list type="bullet">
/// <item>a file with the same path as a package file replaces it; a file with the same name as a
/// legacy asset name (for example <c>mainbg-568h@2x.webp</c>) replaces that asset wherever it sits;</item>
/// <item>new files are simply added and can be referenced from data;</item>
/// <item>data registries (units, terrain, cards, ...) are merged entry by entry instead of replaced,
/// see <see cref="JsonData"/>.</item>
/// </list>
/// </summary>
public static class ModManager
{
    public const string ManifestName = "mod.json";

    private static readonly List<ModInfo> s_loaded = new();
    public static IReadOnlyList<ModInfo> Loaded => s_loaded;

    /// <summary>Also mount mods whose mod.json says "enabled": false (<c>--all-mods</c>, for checking mods).</summary>
    public static bool IncludeDisabled;

    /// <summary>Mounts every enabled mod found under the given folders.</summary>
    public static void MountMods(params string[] folders)
    {
        s_loaded.Clear();
        var found = new List<ModInfo>();
        foreach (var folder in folders)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) continue;
            foreach (var dir in Directory.GetDirectories(folder).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var info = ReadManifest(dir);
                if (info == null) continue;
                if (!info.enabled && !IncludeDisabled) { Log($"Mods: {info.id} is disabled"); continue; }
                if (found.Any(m => string.Equals(m.id, info.id, StringComparison.OrdinalIgnoreCase)))
                {
                    Log($"Mods: duplicate id {info.id} in {dir} ignored");
                    continue;
                }
                found.Add(info);
            }
        }
        foreach (var mod in found.OrderBy(m => m.priority).ThenBy(m => m.id, StringComparer.OrdinalIgnoreCase))
        {
            AssetSystem.Mount(new AssetLayer
            {
                Id = "mod:" + mod.id,
                Name = mod.name,
                IsMod = true,
                Source = new FilteredSource(new DirectoryAssetSource(mod.folder, "mod " + mod.id), ManifestName),
            });
            s_loaded.Add(mod);
            Log($"Mods: loaded {mod.id} {mod.version} (priority {mod.priority}) - {mod.name}");
        }
    }

    public static ModInfo ReadManifest(string dir)
    {
        string path = Path.Combine(dir, ManifestName);
        if (!File.Exists(path)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var r = doc.RootElement;
            var info = new ModInfo
            {
                folder = dir,
                id = Str(r, "id") ?? Path.GetFileName(dir),
                name = Str(r, "name") ?? Path.GetFileName(dir),
                version = Str(r, "version") ?? "",
                author = Str(r, "author") ?? "",
                description = Str(r, "description") ?? "",
                priority = r.TryGetProperty("priority", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0,
                enabled = !r.TryGetProperty("enabled", out var e) || e.ValueKind != JsonValueKind.False,
            };
            return info;
        }
        catch (Exception ex)
        {
            Log($"Mods: cannot read {path}: {ex.Message}");
            return null;
        }
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>Hides the manifest itself from the mounted file list.</summary>
    private sealed class FilteredSource : IAssetSource
    {
        private readonly IAssetSource inner;
        private readonly string hidden;
        public FilteredSource(IAssetSource inner, string hidden) { this.inner = inner; this.hidden = hidden; }
        public string Name => inner.Name;
        public IEnumerable<string> ListFiles() => inner.ListFiles().Where(f => !string.Equals(f, hidden, StringComparison.OrdinalIgnoreCase));
        public Stream OpenRead(string path) => inner.OpenRead(path);
    }
}
