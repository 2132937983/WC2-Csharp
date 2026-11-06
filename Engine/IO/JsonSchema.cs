using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace WC2;

/// <summary>
/// A validator for the subset of JSON Schema (draft 2020-12) the game's data schemas use:
/// <c>type</c> (one or a list; <c>integer</c> = a number without fraction), <c>properties</c>,
/// <c>required</c>, <c>additionalProperties</c> (false or a schema), <c>patternProperties</c>,
/// <c>propertyNames</c>, <c>items</c>, <c>minItems</c>/<c>maxItems</c>, <c>enum</c>, <c>const</c>,
/// <c>minimum</c>/<c>maximum</c>/<c>exclusiveMinimum</c>/<c>exclusiveMaximum</c>,
/// <c>minLength</c>/<c>maxLength</c>/<c>pattern</c>, <c>anyOf</c>/<c>oneOf</c>/<c>allOf</c>, and
/// <c>$ref</c> to <c>#/$defs/name</c>. Annotations (<c>title</c>, <c>description</c>, <c>$comment</c>,
/// <c>default</c>, <c>examples</c>) are ignored. Unknown keywords are ignored too, like the standard says.
/// </summary>
public sealed class JsonSchema
{
    private readonly JsonObject root;
    private readonly Dictionary<string, Regex> regexCache = new();

    public JsonSchema(JsonObject schema) { root = schema ?? new JsonObject(); }

    public static JsonSchema Load(string assetPath)
    {
        string text = AssetSystem.ReadAllText(assetPath);
        if (text == null) return null;
        return JsonData.Parse(text) is JsonObject o ? new JsonSchema(o) : null;
    }

    /// <summary>Validates <paramref name="instance"/>; returns the problems ("path: message"), empty when valid.</summary>
    public List<string> Validate(JsonNode instance, int maxErrors = 20)
    {
        var errors = new List<string>();
        Check(root, instance, "$", errors, maxErrors);
        return errors;
    }

    private void Check(JsonNode schemaNode, JsonNode value, string path, List<string> errors, int max)
    {
        if (errors.Count >= max) return;
        if (schemaNode is JsonValue b && b.TryGetValue(out bool allow)) { if (!allow) errors.Add($"{path}: not allowed"); return; }
        if (schemaNode is not JsonObject s) return;

        if (s["$ref"]?.GetValue<string>() is string reference)
        {
            var target = Resolve(reference);
            if (target == null) errors.Add($"{path}: schema reference {reference} not found");
            else Check(target, value, path, errors, max);
        }

        if (s["type"] is JsonNode typeNode && !MatchesType(typeNode, value))
        {
            errors.Add($"{path}: expected {Describe(typeNode)}, found {Kind(value)}");
            return;
        }
        if (s["const"] is JsonNode constant && !JsonNode.DeepEquals(constant, value)) errors.Add($"{path}: must be {constant.ToJsonString()}");
        if (s["enum"] is JsonArray options && !options.Any(o => JsonNode.DeepEquals(o, value)))
            errors.Add($"{path}: {Short(value)} is not one of {string.Join(", ", options.Select(o => o?.ToJsonString()))}");

        switch (value)
        {
            case JsonObject obj: CheckObject(s, obj, path, errors, max); break;
            case JsonArray arr: CheckArray(s, arr, path, errors, max); break;
            case JsonValue v:
                if (TryNumber(v, out double number)) CheckNumber(s, number, path, errors);
                else if (v.TryGetValue(out string text)) CheckString(s, text, path, errors);
                break;
        }

        if (s["allOf"] is JsonArray all) foreach (var sub in all) Check(sub, value, path, errors, max);
        if (s["anyOf"] is JsonArray any && !any.Any(sub => Passes(sub, value))) errors.Add($"{path}: matches none of the allowed forms");
        if (s["oneOf"] is JsonArray one)
        {
            int matches = one.Count(sub => Passes(sub, value));
            if (matches != 1) errors.Add($"{path}: must match exactly one form (matches {matches})");
        }
    }

    private bool Passes(JsonNode schema, JsonNode value)
    {
        var e = new List<string>();
        Check(schema, value, "", e, 1);
        return e.Count == 0;
    }

    private void CheckObject(JsonObject s, JsonObject obj, string path, List<string> errors, int max)
    {
        var properties = s["properties"] as JsonObject;
        if (s["required"] is JsonArray required)
            foreach (var r in required)
                if (r?.GetValue<string>() is string name && !obj.ContainsKey(name)) errors.Add($"{path}: missing \"{name}\"");
        var patterns = s["patternProperties"] as JsonObject;
        var additional = s["additionalProperties"];
        var names = s["propertyNames"];
        foreach (var (key, child) in obj)
        {
            if (errors.Count >= max) return;
            string childPath = path + "." + key;
            if (names != null) Check(names, JsonValue.Create(key), childPath + " (name)", errors, max);
            bool matched = false;
            if (properties != null && properties.TryGetPropertyValue(key, out var ps)) { Check(ps, child, childPath, errors, max); matched = true; }
            if (patterns != null)
                foreach (var (pattern, pschema) in patterns)
                    if (Regex(pattern).IsMatch(key)) { Check(pschema, child, childPath, errors, max); matched = true; }
            if (!matched && additional != null)
            {
                if (additional is JsonValue av && av.TryGetValue(out bool allowed))
                {
                    if (!allowed) errors.Add($"{childPath}: unknown field");
                }
                else Check(additional, child, childPath, errors, max);
            }
        }
    }

    private void CheckArray(JsonObject s, JsonArray arr, string path, List<string> errors, int max)
    {
        if (s["minItems"] is JsonNode mi && arr.Count < mi.GetValue<int>()) errors.Add($"{path}: needs at least {mi} item(s)");
        if (s["maxItems"] is JsonNode ma && arr.Count > ma.GetValue<int>()) errors.Add($"{path}: allows at most {ma} item(s)");
        if (s["items"] is JsonNode items)
            for (int i = 0; i < arr.Count && errors.Count < max; i++) Check(items, arr[i], $"{path}[{i}]", errors, max);
    }

    private static void CheckNumber(JsonObject s, double n, string path, List<string> errors)
    {
        if (s["minimum"] is JsonNode min && n < min.GetValue<double>()) errors.Add($"{path}: {Fmt(n)} is below the minimum {min}");
        if (s["maximum"] is JsonNode max && n > max.GetValue<double>()) errors.Add($"{path}: {Fmt(n)} is above the maximum {max}");
        if (s["exclusiveMinimum"] is JsonNode emin && n <= emin.GetValue<double>()) errors.Add($"{path}: {Fmt(n)} must be above {emin}");
        if (s["exclusiveMaximum"] is JsonNode emax && n >= emax.GetValue<double>()) errors.Add($"{path}: {Fmt(n)} must be below {emax}");
    }

    private void CheckString(JsonObject s, string text, string path, List<string> errors)
    {
        if (s["minLength"] is JsonNode min && text.Length < min.GetValue<int>()) errors.Add($"{path}: shorter than {min}");
        if (s["maxLength"] is JsonNode max && text.Length > max.GetValue<int>()) errors.Add($"{path}: longer than {max}");
        if (s["pattern"]?.GetValue<string>() is string pattern && !Regex(pattern).IsMatch(text)) errors.Add($"{path}: \"{text}\" does not match {pattern}");
    }

    private Regex Regex(string pattern)
    {
        if (!regexCache.TryGetValue(pattern, out var r)) regexCache[pattern] = r = new Regex(pattern, RegexOptions.CultureInvariant);
        return r;
    }

    private JsonNode Resolve(string reference)
    {
        if (!reference.StartsWith("#/")) return null;
        JsonNode node = root;
        foreach (var part in reference.Substring(2).Split('/'))
            node = (node as JsonObject)?[part.Replace("~1", "/").Replace("~0", "~")];
        return node;
    }

    private static bool MatchesType(JsonNode typeNode, JsonNode value)
    {
        if (typeNode is JsonArray list) return list.Any(t => MatchesType(t, value));
        string type = typeNode.GetValue<string>();
        switch (type)
        {
            case "null": return value == null;
            case "object": return value is JsonObject;
            case "array": return value is JsonArray;
            case "boolean": return value is JsonValue b && b.TryGetValue(out bool _);
            case "string": return value is JsonValue s && s.TryGetValue(out string _);
            case "number": return value is JsonValue n && TryNumber(n, out _);
            case "integer": return value is JsonValue i && TryNumber(i, out double d) && Math.Abs(d % 1) < 1e-12;
            default: return true;
        }
    }

    private static bool TryNumber(JsonValue v, out double d)
    {
        if (v.TryGetValue(out d)) return true;
        if (v.TryGetValue(out long l)) { d = l; return true; }
        if (v.TryGetValue(out int i)) { d = i; return true; }
        if (v.TryGetValue(out float f)) { d = f; return true; }
        if (v.TryGetValue(out decimal m)) { d = (double)m; return true; }
        return false;
    }

    private static string Kind(JsonNode value) => value switch
    {
        null => "null",
        JsonObject => "an object",
        JsonArray => "an array",
        JsonValue v when v.TryGetValue(out bool _) => "a boolean",
        JsonValue v when v.TryGetValue(out string s) => $"the text \"{(s.Length > 30 ? s[..30] + "..." : s)}\"",
        JsonValue v when TryNumber(v, out double d) => "the number " + Fmt(d),
        _ => "a value",
    };

    private static string Describe(JsonNode typeNode) => typeNode is JsonArray l ? string.Join(" or ", l.Select(t => t?.GetValue<string>())) : typeNode.GetValue<string>();
    private static string Short(JsonNode v) => v == null ? "null" : (v.ToJsonString() is string t && t.Length > 40 ? t[..40] + "..." : v.ToJsonString());
    private static string Fmt(double d) => d.ToString("G", CultureInfo.InvariantCulture);
}
