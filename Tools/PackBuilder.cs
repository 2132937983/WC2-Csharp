using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace WC2.Tools;

/// <summary>
/// Builds the runtime asset package from the asset tree (<c>assets/</c>, see ASSET_LAYOUT.md).
/// <list type="number">
/// <item>Every file of the tree is copied to the same path in the package, except the build files
/// (<c>build.json</c>, <c>legacy-names.json</c>, documentation). The originals are never modified.</item>
/// <item>Runtime formats are generated under <c>generated/</c> (see <see cref="RuntimeFormats"/>).</item>
/// <item><c>generated/asset_index.json</c> lists every file (size, variant, language) and maps every
/// original asset name to its path (from <c>legacy-names.json</c>). With <c>--index-copy</c> the
/// index is also written to that file (the repository keeps a copy for review).</item>
/// </list>
/// The build is incremental: unchanged files are not copied again, and files an earlier build produced
/// but this one does not are removed (also everything an older package layout left behind).
/// </summary>
internal static class PackBuilder
{
    public const string IndexPath = "generated/asset_index.json";
    public const string LegacyNamesFile = "legacy-names.json";
    public const string BuildFile = "build.json";

    /// <summary>Variant folders (see ASSET_LAYOUT.md); a path segment with one of these names is the variant.</summary>
    public static readonly HashSet<string> Variants = new(StringComparer.OrdinalIgnoreCase)
    {
        "common", "hd", "tablet", "tablet-hd", "phone-568", "phone-568-hd", "phone-640", "phone-640-hd",
    };
    public static readonly HashSet<string> Languages = new(StringComparer.OrdinalIgnoreCase) { "cn", "en", "ja", "kr", "ru", "tw" };

    public static int Build(string assetsDir, string outDir, string indexCopy, bool force)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        assetsDir = Path.GetFullPath(assetsDir);
        outDir = Path.GetFullPath(outDir);
        if (!Directory.Exists(assetsDir)) throw new ToolException("asset folder not found: " + assetsDir);
        var settings = BuildSettings.Read(assetsDir);
        var legacy = ReadLegacyNames(assetsDir);

        // plan: package path -> source file (the same relative path)
        var plan = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(assetsDir, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(assetsDir, file).Replace('\\', '/');
            if (settings.IsExcluded(rel)) continue;
            if (rel.StartsWith("generated/", StringComparison.OrdinalIgnoreCase))
                throw new ToolException("generated/ is written by the build; remove " + rel + " from the asset tree");
            plan[rel] = file;
        }
        // built-in mods (assets/build.json "builtInMods": folders relative to assets/): packaged under
        // mods/<folder name>/ and mounted by the game as mods (--no-mods leaves them out)
        foreach (var modDir in settings.BuiltInMods)
        {
            string dir = Path.GetFullPath(Path.Combine(assetsDir, modDir));
            if (!File.Exists(Path.Combine(dir, "mod.json"))) throw new ToolException("built-in mod without mod.json: " + dir);
            string name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, '/'));
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
                if (rel.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) continue;
                plan["mods/" + name + "/" + rel] = file;
            }
        }
        var planKeys = new HashSet<string>(plan.Keys, StringComparer.OrdinalIgnoreCase);
        var missing = legacy.Where(kv => !planKeys.Contains(kv.Value)).Select(kv => $"{kv.Key} -> {kv.Value}").ToList();
        if (missing.Count > 0)
            throw new ToolException($"{missing.Count} legacy name(s) point to missing files, e.g. {string.Join(", ", missing.Take(5))}");

        Directory.CreateDirectory(outDir);
        string indexFile = Path.Combine(outDir, IndexPath);
        var previous = ReadPreviousFiles(outDir);

        int copied = 0;
        var sizes = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var (target, source) in plan)
        {
            string dest = Path.Combine(outDir, target);
            if (force || NeedsCopy(source, dest))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(source, dest, overwrite: true);
                File.SetLastWriteTimeUtc(dest, File.GetLastWriteTimeUtc(source));
                copied++;
            }
            sizes[target] = new FileInfo(dest).Length;
        }

        var generatedFrom = new SortedDictionary<string, string>(StringComparer.Ordinal);
        int generated = RuntimeFormats.Generate(outDir, settings.RuntimeFormats, plan, sizes, generatedFrom, force);

        // remove what an earlier build (or an older package layout) produced and this one does not
        int removed = 0;
        foreach (var old in previous)
        {
            if (sizes.ContainsKey(old) || old.Equals(IndexPath, StringComparison.OrdinalIgnoreCase)) continue;
            string stale = Path.Combine(outDir, old);
            if (File.Exists(stale)) { File.Delete(stale); removed++; }
        }
        RemoveEmptyDirectories(outDir);

        var legacyByPath = legacy.GroupBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
                                 .ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToList(), StringComparer.OrdinalIgnoreCase);
        var files = new JsonObject();
        foreach (var (path, size) in sizes)
        {
            var entry = new JsonObject { ["size"] = size };
            var (variant, lang) = Describe(path);
            if (variant != null) entry["variant"] = variant;
            if (lang != null) entry["lang"] = lang;
            if (legacyByPath.TryGetValue(path, out var names))
                entry["legacy"] = names.Count == 1 ? JsonValue.Create(names[0]) : new JsonArray(names.Select(n => (JsonNode)n).ToArray());
            if (generatedFrom.TryGetValue(path, out var source)) entry["from"] = source;
            files[path] = entry;
        }
        var index = new JsonObject
        {
            ["format"] = 2,
            ["about"] = "Generated by WC2.Tools build-pack from assets/ (do not edit; see ASSET_LAYOUT.md). " +
                        "files: every package path with its size, variant folder, language and original name(s); " +
                        "legacy: every original asset name -> package path.",
            ["files"] = files,
            ["legacy"] = new JsonObject(legacy.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                                              .Select(kv => KeyValuePair.Create(kv.Key, (JsonNode)JsonValue.Create(kv.Value)))),
        };
        string json = index.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })
                           .Replace("\r\n", "\n") + "\n";
        WriteIfChanged(indexFile, json);
        if (!string.IsNullOrEmpty(indexCopy)) WriteIfChanged(Path.GetFullPath(indexCopy), json);

        Console.WriteLine($"build-pack: {plan.Count} files ({legacy.Count} with an original name), {generated} generated, " +
                          $"{copied} copied, {removed} removed, {sw.ElapsedMilliseconds} ms -> {outDir}");
        return 0;
    }

    /// <summary>Variant folder and language of a package path (null when the path has none).</summary>
    public static (string variant, string lang) Describe(string path)
    {
        var parts = path.Split('/');
        string variant = null, lang = null;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (variant == null && parts[0] == "image" && Variants.Contains(parts[i])) variant = parts[i];
            else if (lang == null && Languages.Contains(parts[i]) && i > 0) lang = parts[i];
        }
        return (variant, lang);
    }

    public static SortedDictionary<string, string> ReadLegacyNames(string assetsDir)
    {
        var map = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string file = Path.Combine(assetsDir, LegacyNamesFile);
        if (!File.Exists(file)) return map;
        using var doc = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        foreach (var p in doc.RootElement.GetProperty("names").EnumerateObject())
            map[p.Name] = p.Value.GetString()!;
        return map;
    }

    private static void WriteIfChanged(string path, string text)
    {
        if (File.Exists(path) && File.ReadAllText(path) == text) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static bool NeedsCopy(string source, string dest)
    {
        if (!File.Exists(dest)) return true;
        var s = new FileInfo(source);
        var d = new FileInfo(dest);
        return s.Length != d.Length || s.LastWriteTimeUtc != d.LastWriteTimeUtc;
    }

    /// <summary>Files listed by the previous index (this layout or the older index.json one).</summary>
    private static HashSet<string> ReadPreviousFiles(string outDir)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { IndexPath, "index.json" })
        {
            string indexPath = Path.Combine(outDir, name);
            if (!File.Exists(indexPath)) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(indexPath));
                if (doc.RootElement.TryGetProperty("files", out var files))
                    foreach (var p in files.EnumerateObject()) set.Add(p.Name);
            }
            catch (JsonException) { }
            if (name == "index.json") set.Add("index.json");
        }
        return set;
    }

    private static void RemoveEmptyDirectories(string dir)
    {
        foreach (var sub in Directory.EnumerateDirectories(dir))
        {
            RemoveEmptyDirectories(sub);
            if (!Directory.EnumerateFileSystemEntries(sub).Any()) Directory.Delete(sub);
        }
    }
}

/// <summary><c>assets/build.json</c>: files kept out of the package, runtime formats, known gaps.</summary>
internal sealed class BuildSettings
{
    public readonly List<Regex> Exclude = new();
    public readonly List<(Regex match, string format)> RuntimeFormats = new();
    public readonly HashSet<string> KnownMissingReferences = new(StringComparer.OrdinalIgnoreCase);
    public readonly List<string> BuiltInMods = new();

    public bool IsExcluded(string rel) => Exclude.Any(r => r.IsMatch(rel));

    public static BuildSettings Read(string assetsDir)
    {
        var s = new BuildSettings();
        string file = Path.Combine(assetsDir, PackBuilder.BuildFile);
        if (!File.Exists(file))
        {
            s.Exclude.Add(new Regex("^(build|legacy-names)\\.json$|\\.md$", RegexOptions.IgnoreCase));
            return s;
        }
        using var doc = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = doc.RootElement;
        if (root.TryGetProperty("exclude", out var ex))
            foreach (var e in ex.EnumerateArray()) s.Exclude.Add(new Regex(e.GetString()!, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
        if (root.TryGetProperty("runtimeFormats", out var rf))
            foreach (var r in rf.EnumerateArray())
                s.RuntimeFormats.Add((new Regex(r.GetProperty("match").GetString()!, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), r.GetProperty("format").GetString()!));
        if (root.TryGetProperty("builtInMods", out var bm))
            foreach (var m in bm.EnumerateArray()) s.BuiltInMods.Add(m.GetString()!);
        if (root.TryGetProperty("knownMissingReferences", out var km))
            foreach (var k in km.EnumerateArray()) s.KnownMissingReferences.Add(k.GetString()!);
        return s;
    }
}
