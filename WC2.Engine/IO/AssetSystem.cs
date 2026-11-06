using System.Text.Json;

namespace WC2;

/// <summary>A read-only tree of asset files addressed by '/'-separated relative paths.</summary>
public interface IAssetSource
{
    /// <summary>Human readable origin (folder, APK, mod id) for logs.</summary>
    string Name { get; }
    /// <summary>Every file of the source as a relative path with '/' separators.</summary>
    IEnumerable<string> ListFiles();
    Stream OpenRead(string path);
}

/// <summary>Assets in a folder on disk (desktop package, a flat legacy asset folder, or a mod).</summary>
public sealed class DirectoryAssetSource : IAssetSource
{
    public readonly string Root;

    public DirectoryAssetSource(string root, string name = null)
    {
        Root = Path.GetFullPath(root);
        Name = name ?? Root;
    }

    public string Name { get; }

    public IEnumerable<string> ListFiles()
    {
        if (!Directory.Exists(Root)) yield break;
        foreach (var f in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            yield return Path.GetRelativePath(Root, f).Replace('\\', '/');
    }

    public Stream OpenRead(string path) => File.OpenRead(Path.Combine(Root, path));
}

/// <summary>One mounted layer: the base package or a mod.</summary>
public sealed class AssetLayer
{
    public string Id = "";
    public string Name = "";
    public bool IsMod;
    public IAssetSource Source;
    /// <summary>Legacy name to path inside this layer (from the package index); null = the path is the name.</summary>
    public Dictionary<string, string> Aliases;
    public override string ToString() => Id;
}

/// <summary>
/// The single entry point for reading game assets. Layers are mounted in order (base package first,
/// then mods by priority); a file in a later layer replaces the file with the same path below it.
/// Names are resolved in this order:
/// <list type="number">
/// <item>the language folder variant (<c>zh_CN.lproj/Localizable.strings</c>) when a language is set,</item>
/// <item>the path itself (<c>gfx/ui/ui1_hd.xml</c>),</item>
/// <item>the legacy flat name used by the ported code (<c>ui1_hd.xml</c>), through the package index
/// and the file names of mod files.</item>
/// </list>
/// Paths are case-insensitive. Saves and settings do not go through here; they are plain files in
/// the documents folder.
/// </summary>
public static class AssetSystem
{
    private sealed class Entry
    {
        public string path;          // path as stored in the layer
        public AssetLayer layer;
        public List<AssetLayer> lower; // earlier layers that also have this path (for data merging)
    }

    private static readonly List<AssetLayer> s_layers = new();
    private static readonly Dictionary<string, Entry> s_files = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> s_aliases = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Language folder of the legacy layout (e.g. "zh_CN.lproj"); tried first by <see cref="Resolve"/>.</summary>
    public static string LanguageFolder = "";
    /// <summary>
    /// Package folder of the current language (e.g. "lang/cn"), where its files and the authored
    /// JSON twins live; tried right after <see cref="LanguageFolder"/>.
    /// </summary>
    public static string LanguagePath = "";

    public static IReadOnlyList<AssetLayer> Layers => s_layers;
    public static int FileCount => s_files.Count;

    public static void Reset()
    {
        s_layers.Clear();
        s_files.Clear();
        s_aliases.Clear();
    }

    /// <summary>Mounts a layer on top of the ones already mounted.</summary>
    public static void Mount(AssetLayer layer)
    {
        if (layer?.Source == null) return;
        s_layers.Add(layer);
        int count = 0;
        foreach (var raw in layer.Source.ListFiles())
        {
            string path = Normalize(raw);
            if (path.Length == 0) continue;
            count++;
            if (s_files.TryGetValue(path, out var existing))
            {
                (existing.lower ??= new List<AssetLayer>()).Add(existing.layer);
                existing.layer = layer;
                existing.path = path;
            }
            else
            {
                s_files[path] = new Entry { path = path, layer = layer };
            }
            // every file is also reachable by its plain file name and, inside a language folder,
            // by "<folder>/<name>"; mod files registered later win over base files with the same name
            if (layer.IsMod || layer.Aliases == null)
            {
                s_aliases[Path.GetFileName(path)] = path;
                int slash = path.LastIndexOf('/');
                if (slash > 0)
                {
                    int prev = path.LastIndexOf('/', slash - 1);
                    string tail = path.Substring(prev + 1);
                    if (tail.Contains(".lproj/", StringComparison.OrdinalIgnoreCase)) s_aliases[tail] = path;
                }
            }
        }
        if (layer.Aliases != null)
        {
            foreach (var kv in layer.Aliases) s_aliases[Normalize(kv.Key)] = Normalize(kv.Value);
        }
        Log($"Assets: mounted {layer.Id} ({count} files) from {layer.Source.Name}");
    }

    /// <summary>Returns the path of the asset for a name or path, or null when it does not exist.</summary>
    public static string Resolve(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        string key = Normalize(name);
        if (!string.IsNullOrEmpty(LanguageFolder) && !key.Contains('/'))
        {
            string langKey = LanguageFolder + "/" + key;
            if (s_aliases.TryGetValue(langKey, out var langPath) && s_files.ContainsKey(langPath)) return s_files[langPath].path;
            if (s_files.TryGetValue(langKey, out var langEntry)) return langEntry.path;
        }
        if (!string.IsNullOrEmpty(LanguagePath) && !key.Contains('/') && s_files.TryGetValue(LanguagePath + "/" + key, out var langPathEntry))
            return langPathEntry.path;
        if (s_files.TryGetValue(key, out var entry)) return entry.path;
        if (s_aliases.TryGetValue(key, out var aliased) && s_files.TryGetValue(aliased, out var aliasedEntry)) return aliasedEntry.path;
        return null;
    }

    public static bool Exists(string name) => Resolve(name) != null;

    /// <summary>Opens the top-most version of an asset; null when it does not exist.</summary>
    public static Stream OpenRead(string name)
    {
        string path = Resolve(name);
        if (path == null) return null;
        var entry = s_files[path];
        return entry.layer.Source.OpenRead(entry.path);
    }

    public static byte[] ReadAllBytes(string name)
    {
        using var s = OpenRead(name);
        if (s == null) return null;
        if (s is MemoryStream ms0) return ms0.ToArray();
        using var ms = new MemoryStream(s.CanSeek ? (int)Math.Max(0, s.Length) : 0);
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public static string ReadAllText(string name)
    {
        var bytes = ReadAllBytes(name);
        if (bytes == null) return null;
        return new UTF8Encoding(false).GetString(bytes).TrimStart('﻿');
    }

    /// <summary>
    /// Every version of an asset, lowest layer first (base package, then mods). Data registries merge
    /// these so a mod can add or change single entries without copying the whole file.
    /// </summary>
    public static List<(AssetLayer layer, string text)> ReadAllVersions(string name)
    {
        var result = new List<(AssetLayer, string)>();
        string path = Resolve(name);
        if (path == null) return result;
        var entry = s_files[path];
        var layers = new List<AssetLayer>();
        if (entry.lower != null) layers.AddRange(entry.lower);
        layers.Add(entry.layer);
        foreach (var layer in layers)
        {
            using var s = layer.Source.OpenRead(entry.path);
            using var reader = new StreamReader(s, new UTF8Encoding(false), true);
            result.Add((layer, reader.ReadToEnd()));
        }
        return result;
    }

    /// <summary>The layer that supplies the top-most version of an asset (null when missing).</summary>
    public static AssetLayer GetLayer(string name)
    {
        string path = Resolve(name);
        return path == null ? null : s_files[path].layer;
    }

    /// <summary>All asset paths, optionally under a folder prefix ("data/scenarios/").</summary>
    public static IEnumerable<string> List(string prefix = null)
    {
        string p = string.IsNullOrEmpty(prefix) ? null : Normalize(prefix);
        foreach (var e in s_files.Values)
            if (p == null || e.path.StartsWith(p, StringComparison.OrdinalIgnoreCase)) yield return e.path;
    }

    public static string Normalize(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        string p = path.Replace('\\', '/');
        while (p.StartsWith("./")) p = p.Substring(2);
        return p.TrimStart('/');
    }

    // ------------------------------------------------------------------------------------
    // Package index
    // ------------------------------------------------------------------------------------

    public const string IndexFileName = "index.json";

    /// <summary>
    /// Reads the aliases of a generated package index ({"aliases": {"legacy name": "path"}}).
    /// Returns null when there is no index (a flat legacy asset folder).
    /// </summary>
    public static Dictionary<string, string> ReadIndexAliases(Stream indexStream)
    {
        if (indexStream == null) return null;
        using var doc = JsonDocument.Parse(indexStream);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (doc.RootElement.TryGetProperty("aliases", out var aliases))
            foreach (var p in aliases.EnumerateObject()) map[p.Name] = p.Value.GetString();
        return map;
    }
}

/// <summary>
/// Assets inside a zip archive (a packed package, or an APK with the package under assets/pack).
/// Used to run the desktop build from the very files an APK ships.
/// </summary>
public sealed class ZipAssetSource : IAssetSource, IDisposable
{
    private readonly System.IO.Compression.ZipArchive zip;
    private readonly string prefix;
    private readonly Dictionary<string, System.IO.Compression.ZipArchiveEntry> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly object gate = new();

    public ZipAssetSource(string archivePath, string root = "")
    {
        Name = archivePath + (string.IsNullOrEmpty(root) ? "" : "!" + root);
        zip = System.IO.Compression.ZipFile.OpenRead(archivePath);
        prefix = string.IsNullOrEmpty(root) ? "" : root.TrimEnd('/') + "/";
        foreach (var e in zip.Entries)
        {
            if (e.FullName.EndsWith("/")) continue;
            if (prefix.Length > 0 && !e.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            entries[e.FullName.Substring(prefix.Length)] = e;
        }
    }

    public string Name { get; }

    public IEnumerable<string> ListFiles() => entries.Keys;

    public Stream OpenRead(string path)
    {
        if (!entries.TryGetValue(path, out var e)) throw new FileNotFoundException(path);
        lock (gate)   // ZipArchive is not thread safe; music decodes on a worker thread
        {
            using var s = e.Open();
            var ms = new MemoryStream((int)e.Length);
            s.CopyTo(ms);
            ms.Position = 0;
            return ms;
        }
    }

    public Stream TryOpen(string path) => entries.ContainsKey(path) ? OpenRead(path) : null;

    public void Dispose() => zip.Dispose();
}
