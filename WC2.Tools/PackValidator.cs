using System.Text.Json;

namespace WC2.Tools;

/// <summary>Consistency checks for a built package (see <c>validate</c> in Program.cs).</summary>
internal static class PackValidator
{
    public static int Validate(string packDir, string sourceDir)
    {
        string indexPath = Path.Combine(packDir, "index.json");
        if (!File.Exists(indexPath)) throw new ToolException("no index.json in " + packDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(indexPath));
        var files = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in doc.RootElement.GetProperty("files").EnumerateObject()) files[p.Name] = p.Value.GetInt64();
        int problems = 0;
        foreach (var (path, size) in files)
        {
            var fi = new FileInfo(Path.Combine(packDir, path));
            if (!fi.Exists) { Console.WriteLine("missing file: " + path); problems++; }
            else if (fi.Length != size) { Console.WriteLine($"size differs: {path} ({fi.Length} != {size})"); problems++; }
        }
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in doc.RootElement.GetProperty("aliases").EnumerateObject())
        {
            aliases[p.Name] = p.Value.GetString()!;
            if (!files.ContainsKey(p.Value.GetString()!)) { Console.WriteLine($"alias {p.Name} points to a missing file"); problems++; }
        }
        if (!string.IsNullOrEmpty(sourceDir))
        {
            foreach (var f in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string legacy = Path.GetRelativePath(sourceDir, f).Replace('\\', '/');
                if (!aliases.TryGetValue(legacy, out var target)) { Console.WriteLine("original not packaged: " + legacy); problems++; continue; }
                var a = new FileInfo(f); var b = new FileInfo(Path.Combine(packDir, target));
                if (!b.Exists || a.Length != b.Length) { Console.WriteLine($"original differs in package: {legacy}"); problems++; }
            }
        }
        Console.WriteLine($"validate: {files.Count} files, {aliases.Count} aliases, {problems} problem(s)");
        return problems == 0 ? 0 : 1;
    }
}
