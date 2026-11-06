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
    /// <summary>Shipped inside the game package (mods/&lt;id&gt;/), not from a mod folder.</summary>
    public bool builtIn;
    /// <summary>Where the mod's files come from (null: <see cref="folder"/>).</summary>
    public IAssetSource source;
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

    /// <summary>Folder in the base package that holds built-in mods (<c>mods/&lt;id&gt;/mod.json</c>).</summary>
    public const string BuiltInFolder = "mods/";

    /// <summary>
    /// Mounts every enabled mod found under the given folders, then the built-in mods of the base
    /// package (<c>mods/&lt;id&gt;/</c> inside it: shipped with the game, e.g. the generated world map).
    /// A mod in a folder replaces a built-in mod with the same id.
    /// </summary>
    public static void MountMods(string[] folders, IAssetSource builtInSource = null)
    {
        s_loaded.Clear();
        var found = new List<ModInfo>();
        void Add(ModInfo info, string where)
        {
            if (info == null) return;
            if (!info.enabled && !IncludeDisabled) { Log($"Mods: {info.id} is disabled"); return; }
            if (found.Any(m => string.Equals(m.id, info.id, StringComparison.OrdinalIgnoreCase)))
            {
                Log($"Mods: duplicate id {info.id} in {where} ignored");
                return;
            }
            found.Add(info);
        }
        foreach (var folder in folders ?? Array.Empty<string>())
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) continue;
            foreach (var dir in Directory.GetDirectories(folder).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                Add(ReadManifest(dir), dir);
        }
        if (builtInSource != null)
        {
            var manifests = builtInSource.ListFiles()
                .Select(f => f.Replace('\\', '/'))
                .Where(f => f.StartsWith(BuiltInFolder, StringComparison.OrdinalIgnoreCase) && f.EndsWith("/" + ManifestName, StringComparison.OrdinalIgnoreCase)
                            && f.Count(c => c == '/') == 2)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var manifest in manifests)
            {
                string prefix = manifest.Substring(0, manifest.Length - ManifestName.Length);
                string text;
                try { using var s = builtInSource.OpenRead(manifest); using var r = new StreamReader(s); text = r.ReadToEnd(); }
                catch (Exception ex) { Log($"Mods: cannot read {manifest}: {ex.Message}"); continue; }
                var info = ParseManifest(text, prefix.TrimEnd('/').Substring(BuiltInFolder.Length), "built-in " + prefix);
                if (info == null) continue;
                info.builtIn = true;
                info.source = new PrefixedSource(builtInSource, prefix, "built-in mod " + info.id);
                Add(info, "the game package");
            }
        }
        foreach (var mod in found.OrderBy(m => m.priority).ThenBy(m => m.id, StringComparer.OrdinalIgnoreCase))
        {
            AssetSystem.Mount(new AssetLayer
            {
                Id = "mod:" + mod.id,
                Name = mod.name,
                IsMod = true,
                Source = new FilteredSource(mod.source ?? new DirectoryAssetSource(mod.folder, "mod " + mod.id), ManifestName),
            });
            s_loaded.Add(mod);
            Log($"Mods: loaded {mod.id} {mod.version} (priority {mod.priority}{(mod.builtIn ? ", built in" : "")}) - {mod.name}");
        }
    }

    /// <summary>Mounts every enabled mod found under the given folders.</summary>
    public static void MountMods(params string[] folders) => MountMods(folders, null);

    public static ModInfo ReadManifest(string dir)
    {
        string path = Path.Combine(dir, ManifestName);
        if (!File.Exists(path)) return null;
        try
        {
            var info = ParseManifest(File.ReadAllText(path), Path.GetFileName(dir), path);
            if (info != null) info.folder = dir;
            return info;
        }
        catch (Exception ex)
        {
            Log($"Mods: cannot read {path}: {ex.Message}");
            return null;
        }
    }

    private static ModInfo ParseManifest(string text, string defaultId, string where)
    {
        try
        {
            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var r = doc.RootElement;
            var info = new ModInfo
            {
                id = Str(r, "id") ?? defaultId,
                name = Str(r, "name") ?? defaultId,
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
            Log($"Mods: cannot read {where}: {ex.Message}");
            return null;
        }
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>The files of a source below a folder, as a source of their own (built-in mods).</summary>
    private sealed class PrefixedSource : IAssetSource
    {
        private readonly IAssetSource inner;
        private readonly string prefix;
        public PrefixedSource(IAssetSource inner, string prefix, string name) { this.inner = inner; this.prefix = prefix; Name = name; }
        public string Name { get; }
        public IEnumerable<string> ListFiles() => inner.ListFiles()
            .Select(f => f.Replace('\\', '/'))
            .Where(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Substring(prefix.Length));
        public Stream OpenRead(string path) => inner.OpenRead(prefix + path);
    }

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
