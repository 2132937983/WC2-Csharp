using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WC2;

/// <summary>
/// JSON game data: reading with comments and trailing commas, layering of the base package and the
/// mods, and small typed accessors.
///
/// Layering (<see cref="LoadMerged"/>): every version of a data file is read, base package first,
/// then the mods in load order, and merged into one document:
/// <list type="bullet">
/// <item>objects merge key by key, recursively, so a mod only lists what it changes;</item>
/// <item>arrays and plain values replace the lower version;</item>
/// <item><c>null</c> removes the key (for example a unit a mod wants gone);</item>
/// <item>an object with <c>"$replace": true</c> replaces the lower object entirely.</item>
/// </list>
/// Registries are therefore written as objects keyed by id ("units": {"tank": {...}}), never as arrays.
/// </summary>
public static class JsonData
{
    public static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static readonly JsonNodeOptions NodeOptions = new() { PropertyNameCaseInsensitive = false };

    public static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static JsonNode Parse(string text) => JsonNode.Parse(text, NodeOptions, DocumentOptions);

    /// <summary>
    /// The schema (asset path) a data file is checked against, or null; set by the game
    /// (DataPaths.SchemaFor). Without a resolver nothing is validated.
    /// </summary>
    public static Func<string, string> SchemaResolver;

    /// <summary>Validation problems found while loading ("path: problem"); --verify-data reports them.</summary>
    public static readonly List<string> ValidationProblems = new();

    private static readonly Dictionary<string, JsonSchema> s_schemas = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Forgets the loaded schemas (after the asset layers change).</summary>
    public static void ResetSchemas() => s_schemas.Clear();

    /// <summary>
    /// The merged document of a data file over all layers, or null when no layer has it. The result is
    /// validated against the file's schema (see <see cref="SchemaResolver"/>): problems are logged and
    /// collected in <see cref="ValidationProblems"/>; when only the mods' version is invalid, the mods'
    /// changes to this file are dropped and the game's own data is used.
    /// </summary>
    public static JsonObject LoadMerged(string path)
    {
        JsonObject merged = null, baseOnly = null;
        var mods = new List<string>();
        foreach (var (layer, text) in AssetSystem.ReadAllVersions(path))
        {
            JsonNode node;
            try { node = Parse(text); }
            catch (JsonException ex)
            {
                Report($"{path} ({layer.Id}): JSON error: {ex.Message}");
                continue;
            }
            if (node is not JsonObject obj)
            {
                Report($"{path} ({layer.Id}): the root must be an object");
                continue;
            }
            if (layer.IsMod && mods.Count == 0) baseOnly = (JsonObject)merged?.DeepClone();
            if (layer.IsMod) mods.Add(layer.Id);
            merged = merged == null ? obj : (JsonObject)Merge(merged, obj);
        }
        if (merged == null) return null;

        var schema = SchemaOf(path);
        if (schema == null) return merged;
        var errors = schema.Validate(merged);
        if (errors.Count == 0) return merged;
        if (mods.Count > 0 && baseOnly != null && schema.Validate(baseOnly).Count == 0)
        {
            foreach (var e in errors) Report($"{path}: {e} (from {string.Join(", ", mods)}; their changes to this file are ignored)");
            return baseOnly;
        }
        foreach (var e in errors) Report($"{path}: {e}");
        return merged;
    }

    private static JsonSchema SchemaOf(string path)
    {
        string schemaPath = SchemaResolver?.Invoke(path);
        if (string.IsNullOrEmpty(schemaPath)) return null;
        if (!s_schemas.TryGetValue(schemaPath, out var schema))
        {
            try { schema = JsonSchema.Load(schemaPath); }
            catch (Exception ex) { Report($"{schemaPath}: unreadable schema: {ex.Message}"); }
            s_schemas[schemaPath] = schema;
        }
        return schema;
    }

    private static void Report(string problem)
    {
        Log("Data: " + problem);
        if (ValidationProblems.Count < 200) ValidationProblems.Add(problem);
    }

    /// <summary>Merges <paramref name="overlay"/> into <paramref name="baseNode"/> (see the class remarks).</summary>
    public static JsonNode Merge(JsonNode baseNode, JsonNode overlay)
    {
        if (overlay is not JsonObject over || baseNode is not JsonObject target) return overlay?.DeepClone();
        if (over.TryGetPropertyValue("$replace", out var replace) && replace is JsonValue rv && rv.TryGetValue(out bool r) && r)
        {
            var copy = (JsonObject)over.DeepClone();
            copy.Remove("$replace");
            return copy;
        }
        foreach (var (key, value) in over.ToList())
        {
            if (value == null) { target.Remove(key); continue; }
            if (target.TryGetPropertyValue(key, out var existing) && existing is JsonObject && value is JsonObject)
                target[key] = Merge(existing, value);
            else
                target[key] = value.DeepClone();
        }
        return target;
    }

    /// <summary>
    /// Writes indented JSON. With <paramref name="inlineDepth"/> &gt; 0, objects and arrays nested that deep
    /// are written on one line (one area or unit per line keeps big tables readable).
    /// </summary>
    public static void Write(string path, JsonNode node, int inlineDepth = 0)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        string text = (inlineDepth > 0 ? Format(node, 0, inlineDepth) : node.ToJsonString(WriteOptions)) + "\n";
        // an unchanged file is left alone (also when only its line endings differ, e.g. after a git checkout)
        if (File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == text.Replace("\r\n", "\n")) return;
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    private static readonly JsonSerializerOptions CompactOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Indented JSON whose nodes at <paramref name="inlineDepth"/> and deeper stay on one line.</summary>
    public static string Format(JsonNode node, int depth, int inlineDepth)
    {
        if (node is not JsonObject && node is not JsonArray) return node?.ToJsonString(CompactOptions) ?? "null";
        if (depth >= inlineDepth) return InlineFormat(node);
        string pad = new string(' ', (depth + 1) * 2);
        string end = new string(' ', depth * 2);
        var sb = new StringBuilder();
        if (node is JsonObject o)
        {
            if (o.Count == 0) return "{}";
            sb.Append("{\n");
            int i = 0;
            foreach (var (k, v) in o)
            {
                sb.Append(pad).Append(JsonValue.Create(k).ToJsonString(CompactOptions)).Append(": ").Append(Format(v, depth + 1, inlineDepth));
                sb.Append(++i < o.Count ? ",\n" : "\n");
            }
            return sb.Append(end).Append('}').ToString();
        }
        var a = (JsonArray)node;
        if (a.Count == 0) return "[]";
        sb.Append("[\n");
        for (int i = 0; i < a.Count; i++)
        {
            sb.Append(pad).Append(Format(a[i], depth + 1, inlineDepth));
            sb.Append(i + 1 < a.Count ? ",\n" : "\n");
        }
        return sb.Append(end).Append(']').ToString();
    }

    /// <summary>One-line JSON with a space after colons and commas.</summary>
    private static string InlineFormat(JsonNode node)
    {
        switch (node)
        {
            case JsonObject o:
                return "{" + string.Join(", ", o.Select(kv => JsonValue.Create(kv.Key).ToJsonString(CompactOptions) + ": " + InlineFormat(kv.Value))) + "}";
            case JsonArray a:
                return "[" + string.Join(", ", a.Select(InlineFormat)) + "]";
            default:
                return node?.ToJsonString(CompactOptions) ?? "null";
        }
    }

    // ------------------------------------------------------------------------------------
    // Accessors. They accept numbers written as strings and strings written as numbers, so data
    // converted from XML attributes reads the same either way.
    // ------------------------------------------------------------------------------------

    public static JsonNode Get(this JsonObject o, string key) => o != null && o.TryGetPropertyValue(key, out var v) ? v : null;

    public static bool Has(this JsonObject o, string key) => o != null && o.TryGetPropertyValue(key, out var v) && v != null;

    public static JsonObject Obj(this JsonObject o, string key) => o.Get(key) as JsonObject;

    public static JsonArray Arr(this JsonObject o, string key) => o.Get(key) as JsonArray;

    /// <summary>Scalar value as text (numbers in invariant form, booleans as true/false); arrays of scalars joined with spaces.</summary>
    public static string Text(this JsonNode n)
    {
        switch (n)
        {
            case null: return null;
            case JsonArray a: return string.Join(' ', a.Select(x => x.Text()).Where(x => x != null));
            case JsonValue v:
                if (v.TryGetValue(out JsonElement e))
                {
                    return e.ValueKind switch
                    {
                        JsonValueKind.String => e.GetString(),
                        JsonValueKind.Number => e.GetRawText(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        _ => null,
                    };
                }
                if (v.TryGetValue(out string s)) return s;
                if (v.TryGetValue(out bool b)) return b ? "true" : "false";
                if (v.TryGetValue(out long l)) return l.ToString(CultureInfo.InvariantCulture);
                if (v.TryGetValue(out int i)) return i.ToString(CultureInfo.InvariantCulture);
                if (v.TryGetValue(out double d)) return d.ToString("R", CultureInfo.InvariantCulture);
                if (v.TryGetValue(out float f)) return f.ToString("R", CultureInfo.InvariantCulture);
                return v.ToJsonString().Trim('"');
            default: return null;
        }
    }

    public static string Str(this JsonObject o, string key, string fallback = null) => o.Get(key)?.Text() ?? fallback;

    // Numbers follow the XML facade exactly (TiXmlElement.QueryIntAttribute / QueryDoubleAttribute),
    // so a value reads the same from the original XML and from the converted JSON: integers fall back
    // to a float prefix, and doubles are parsed as float first.
    public static bool TryInt(this JsonObject o, string key, out int value)
    {
        value = 0;
        string t = o.Get(key)?.Text();
        if (t == null) return false;
        if (TryParseInt(t, out value)) return true;
        if (TryParseFloat(t, out float f)) { value = (int)f; return true; }
        return false;
    }

    public static bool TryFloat(this JsonObject o, string key, out float value)
    {
        value = 0;
        string t = o.Get(key)?.Text();
        return t != null && TryParseFloat(t, out value);
    }

    public static bool TryDouble(this JsonObject o, string key, out double value)
    {
        bool ok = o.TryFloat(key, out float f);
        value = f;
        return ok;
    }

    public static bool TryBool(this JsonObject o, string key, out bool value)
    {
        value = false;
        string t = o.Get(key)?.Text();
        if (t == null) return false;
        switch (t.Trim().ToLowerInvariant())
        {
            case "true": case "1": case "yes": value = true; return true;
            case "false": case "0": case "no": value = false; return true;
            default: return false;
        }
    }

    public static int Int(this JsonObject o, string key, int fallback = 0) => o.TryInt(key, out int v) ? v : fallback;
    public static float Float(this JsonObject o, string key, float fallback = 0f) => o.TryFloat(key, out float v) ? v : fallback;
    public static double Double(this JsonObject o, string key, double fallback = 0) => o.TryDouble(key, out double v) ? v : fallback;
    public static bool Bool(this JsonObject o, string key, bool fallback = false) => o.TryBool(key, out bool v) ? v : fallback;

    /// <summary>The objects of a registry ({"id": {...}}) in file order, with their keys.</summary>
    public static IEnumerable<(string key, JsonObject value)> Entries(this JsonObject o)
    {
        if (o == null) yield break;
        foreach (var (k, v) in o)
            if (v is JsonObject obj) yield return (k, obj);
    }

    /// <summary>The objects of an array, skipping anything else.</summary>
    public static IEnumerable<JsonObject> Objects(this JsonArray a)
    {
        if (a == null) yield break;
        foreach (var n in a) if (n is JsonObject o) yield return o;
    }

    // ------------------------------------------------------------------------------------
    // Builders used by the converters
    // ------------------------------------------------------------------------------------

    /// <summary>A JSON value for attribute text: integers and plain decimals become numbers, the rest stays text.</summary>
    public static JsonNode Value(string text)
    {
        if (text == null) return null;
        string t = text.Trim();
        if (t.Length > 0 && t.Length < 16 && long.TryParse(t, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l)
            && l.ToString(CultureInfo.InvariantCulture) == t)
            return JsonValue.Create(l);
        if (t.Length > 0 && t.Length < 24 && double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
            && !double.IsInfinity(d) && !double.IsNaN(d) && d.ToString("R", CultureInfo.InvariantCulture) == t)
            return JsonValue.Create(d);
        // a plain decimal with trailing zeros ("1.350000") is the same number written shorter (1.35)
        if (t.Length > 0 && t.Length < 20 && System.Text.RegularExpressions.Regex.IsMatch(t, "^-?[0-9]+[.][0-9]+$")
            && double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double dec) && !double.IsInfinity(dec))
            return JsonValue.Create(dec);
        if (t == "true" || t == "false") return JsonValue.Create(t == "true");
        return JsonValue.Create(text);
    }
}
