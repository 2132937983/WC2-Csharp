using System.Text.Json;
using System.Text.RegularExpressions;

namespace WC2.Tools;

/// <summary>
/// Asset checker for a built package (<c>validate</c>):
/// <list type="number">
/// <item>every file the index lists exists with its size, and every file in the package is listed;</item>
/// <item>every original name (legacy) points to a listed file;</item>
/// <item>every asset name mentioned by the data (JSON and XML data, scripts, atlas and font
/// descriptors) resolves: by package path, by original name, by original name with the variant
/// suffixes the game adds itself (<c>_hd</c>, <c>@2x</c>, <c>_iPad</c>, language codes, ...), or as a
/// sprite of an atlas (<c>&lt;Image name="..."&gt;</c>);
/// names in <c>assets/build.json knownMissingReferences</c> are reported but do not fail;</item>
/// <item>with <c>--source</c> (the asset tree): every file of the tree is in the package, unchanged.</item>
/// </list>
/// </summary>
internal static class PackValidator
{
    private static readonly Regex Reference = new(@"[A-Za-z0-9_@\-\.]+\.(png|webp|pkm|jpg|wav|mp3|fnt|xml|bin|raw)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex VariantSuffix = new(@"(_hd|@2x|_ipad|-568h|-640h|_568hd|_640hd|_(cn|en|ja|kr|ru|tw|ko))$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex AtlasSprite = new(@"<Image\s+name=""[^""]*""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex AtlasSpriteName = new(@"<Image\s+name=""([^""]*)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".webp", ".pkm", ".jpg", ".wctex" };

    public static int Validate(string packDir, string sourceDir)
    {
        packDir = Path.GetFullPath(packDir);
        string indexPath = Path.Combine(packDir, PackBuilder.IndexPath);
        if (!File.Exists(indexPath)) throw new ToolException("no " + PackBuilder.IndexPath + " in " + packDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(indexPath));
        var files = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in doc.RootElement.GetProperty("files").EnumerateObject()) files[p.Name] = p.Value.GetProperty("size").GetInt64();
        int problems = 0;
        void Problem(string text) { if (problems++ < 50) Console.WriteLine(text); }

        // 1. index <-> files on disk
        foreach (var (path, size) in files)
        {
            var fi = new FileInfo(Path.Combine(packDir, path));
            if (!fi.Exists) Problem("missing file: " + path);
            else if (fi.Length != size) Problem($"size differs: {path} ({fi.Length} != {size})");
        }
        foreach (var f in Directory.EnumerateFiles(packDir, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(packDir, f).Replace('\\', '/');
            if (!rel.Equals(PackBuilder.IndexPath, StringComparison.OrdinalIgnoreCase) && !files.ContainsKey(rel)) Problem("not in the index: " + rel);
        }

        // 2. original names
        var legacy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in doc.RootElement.GetProperty("legacy").EnumerateObject())
        {
            legacy[p.Name] = p.Value.GetString()!;
            if (!files.ContainsKey(p.Value.GetString()!)) Problem($"original name {p.Name} points to a missing file");
        }

        // 3. references from the data
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in legacy.Keys.Concat(files.Keys))
        {
            string file = Path.GetFileName(name);
            names.Add(file);
            stems.Add(StemKey(file));
        }
        // sprites defined by the atlas descriptors (<Image name="sprite.png">) count as resolvable names
        var sprites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in files.Keys.Where(p => p.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && p.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
            foreach (Match m in AtlasSpriteName.Matches(File.ReadAllText(Path.Combine(packDir, path))))
                sprites.Add(m.Groups[1].Value);
        var settings = BuildSettings.Read(string.IsNullOrEmpty(sourceDir) ? packDir : sourceDir);
        var unresolved = new SortedDictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        int scanned = 0, references = 0;
        foreach (var path in files.Keys.Where(IsReferencingFile))
        {
            scanned++;
            byte[] bytes = File.ReadAllBytes(Path.Combine(packDir, path));
            if (Array.IndexOf(bytes, (byte)0, 0, Math.Min(bytes.Length, 512)) >= 0) continue;   // binary (BMFont .fnt)
            // atlas descriptors: <Texture name="file"> is a file, <Image name="sprite.png"> only names a sprite
            string text = AtlasSprite.Replace(System.Text.Encoding.UTF8.GetString(bytes), "");
            foreach (Match m in Reference.Matches(text))
            {
                string reference = m.Value.TrimStart('.');
                references++;
                if (names.Contains(reference) || stems.Contains(StemKey(reference)) || sprites.Contains(reference)) continue;
                if (!unresolved.TryGetValue(reference, out var from)) unresolved[reference] = from = new SortedSet<string>(StringComparer.Ordinal);
                from.Add(path);
            }
        }
        int known = 0;
        foreach (var (reference, from) in unresolved)
        {
            if (settings.KnownMissingReferences.Contains(reference)) { known++; continue; }
            Problem($"unresolved reference {reference} (in {string.Join(", ", from.Take(3))}{(from.Count > 3 ? ", ..." : "")})");
        }

        // 4. the asset tree is completely in the package
        if (!string.IsNullOrEmpty(sourceDir))
        {
            sourceDir = Path.GetFullPath(sourceDir);
            foreach (var f in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(sourceDir, f).Replace('\\', '/');
                if (settings.IsExcluded(rel)) continue;
                var b = new FileInfo(Path.Combine(packDir, rel));
                if (!b.Exists || b.Length != new FileInfo(f).Length) Problem("not packaged (or changed): " + rel);
            }
        }

        Console.WriteLine($"validate: {files.Count} files, {legacy.Count} original names, {references} references in {scanned} data files " +
                          $"({known} known gaps of the original data), {problems} problem(s)");
        return problems == 0 ? 0 : 1;
    }

    /// <summary>Data that names other assets: JSON and XML data, scripts, atlas and font descriptors.</summary>
    private static bool IsReferencingFile(string path)
    {
        string ext = Path.GetExtension(path);
        if (path.StartsWith("generated/", StringComparison.OrdinalIgnoreCase)) return false;
        return ext.Equals(".json", StringComparison.OrdinalIgnoreCase) || ext.Equals(".fnt", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".xml", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>File name without variant suffixes; image extensions are treated as one.</summary>
    private static string StemKey(string fileName)
    {
        string ext = Path.GetExtension(fileName);
        string stem = Path.GetFileNameWithoutExtension(fileName);
        string previous;
        do { previous = stem; stem = VariantSuffix.Replace(stem, ""); } while (stem != previous);
        return stem + (ImageExtensions.Contains(ext) ? ".image" : ext.ToLowerInvariant());
    }
}
