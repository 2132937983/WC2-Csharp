using System.Text.Json.Nodes;

namespace WC2;

/// <summary>
/// The countries the art knows (<c>data/json/countries/countries.json</c>): for each country id the
/// unit atlas may hold dedicated unit images (<c>&lt;unit&gt;_&lt;id&gt;.png</c>) and a flag
/// (<c>flag_&lt;id&gt;.png</c>); <c>commanderMedal</c> countries have an AI commander badge
/// (<c>medal_&lt;id&gt;.png</c>), the others use the <c>commonMedals</c>. Scenarios refer to these ids
/// (plus a digit for several countries of one nation: <c>de1</c>, <c>de2</c>). A mod adds a country by
/// adding its id here and its images to the atlas.
/// <code>
/// { "countries": { "gb": { "commanderMedal": true }, "ca": {}, ... }, "commonMedals": ["common_1", ...] }
/// </code>
/// The built-in values are the original lists (CGameRes country_list / medal_list).
/// </summary>
public static class CountryDefs
{
    private static readonly string[] BuiltInIds =
    {
        "gb", "ru", "am", "fr", "ja", "it", "de", "ca", "fl", "ro",
        "bg", "be", "gr", "cn", "tw", "in", "au", "pl", "hu", "yu",
        "ch", "se", "pt", "dk", "no", "nl", "es", "mx", "nk", "rk",
        "tr", "ne", "others", "tes",
    };
    private static readonly string[] BuiltInMedalCountries = { "gb", "de", "am", "fr", "it", "ru", "ja", "cn", "tw" };
    private static readonly string[] BuiltInCommonMedals = { "common_1", "common_2", "common_3" };

    /// <summary>Country ids with possible dedicated art, in order.</summary>
    public static List<string> ids = new();
    /// <summary>Badge names: the commander medal countries, then the common badges.</summary>
    public static List<string> medals = new();

    public static void LoadBuiltIn()
    {
        ids = BuiltInIds.ToList();
        medals = BuiltInMedalCountries.Concat(BuiltInCommonMedals).ToList();
    }

    public static bool LoadJson()
    {
        LoadBuiltIn();
        var root = JsonData.LoadMerged(DataPaths.Countries);
        if (root == null) return false;
        if (root.Obj("countries") is JsonObject countries)
        {
            ids = countries.Where(kv => kv.Value != null).Select(kv => kv.Key).ToList();
            medals = countries.Where(kv => (kv.Value as JsonObject).Bool("commanderMedal", false)).Select(kv => kv.Key).ToList();
        }
        if (root.Arr("commonMedals") is JsonArray common)
            medals.AddRange(common.Select(n => n?.Text()).Where(s => !string.IsNullOrEmpty(s)));
        else medals.AddRange(BuiltInCommonMedals);
        return true;
    }

    public static JsonObject BuiltInJson()
    {
        LoadBuiltIn();
        var countries = new JsonObject();
        foreach (var id in BuiltInIds)
        {
            var c = new JsonObject();
            if (BuiltInMedalCountries.Contains(id)) c["commanderMedal"] = true;
            countries[id] = c;
        }
        return new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Countries with their own unit art, flag and commander badge (the original lists). Field reference: DATA_SCHEMA.md#countries.",
            ["countries"] = countries,
            ["commonMedals"] = new JsonArray(BuiltInCommonMedals.Select(m => (JsonNode)m).ToArray()),
        };
    }

    /// <summary>Order-independent view (the art lookups fill dictionaries, so only membership matters).</summary>
    public static object Snapshot() => new
    {
        ids = ids.OrderBy(s => s, StringComparer.Ordinal).ToList(),
        medals = medals.OrderBy(s => s, StringComparer.Ordinal).ToList(),
    };
}
