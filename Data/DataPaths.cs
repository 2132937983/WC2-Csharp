namespace WC2;

/// <summary>Package paths of the JSON game data (see DATA_SCHEMA.md and ASSET_LAYOUT.md).</summary>
public static class DataPaths
{
    public const string Units = "data/json/units/units.json";
    public const string Cards = "data/json/cards/cards.json";
    public const string Commanders = "data/json/commanders/commanders.json";
    public const string Portraits = "data/json/commanders/portraits.json";
    public const string Campaigns = "data/json/campaigns/campaigns.json";
    public const string Conquests = "data/json/conquests/conquests.json";
    public const string Terrain = "data/json/terrain/terrain.json";
    public const string Medals = "data/json/rules/medals.json";
    public const string Rules = "data/json/rules/rules.json";
    public const string OptionsLayout = "data/json/layout/options.json";
    public const string LayoutProfiles = "data/json/layout/profiles.json";
    public const string Audio = "data/json/audio/audio.json";
    public const string Countries = "data/json/countries/countries.json";
    public const string AiProfiles = "data/json/ai/profiles.json";

    /// <summary>Language settings of the current language folder (was Localizable.strings).</summary>
    public const string Language = "language.json";

    public static string MapAreas(int mapId) => $"data/json/maps/{mapId}/areas.json";

    /// <summary>Index of the scenarios and tutorial scripts (the files stay the original XML).</summary>
    public const string ScenarioIndex = "data/json/scenarios/index.json";

    /// <summary>
    /// A scenario in the JSON format (exported battles, mods; the game ships none):
    /// "battle_axis1.xml" -> "data/json/scenarios/battle_axis1.json".
    /// </summary>
    public static string Scenario(string legacyName) => "data/json/scenarios/" + Path.GetFileNameWithoutExtension(legacyName) + ".json";

    /// <summary>A tutorial script in the JSON format (mods only): "tutorails_script-568h.xml" -> "data/json/scenarios/tutorial/tutorails_script-568h.json".</summary>
    public static string TutorialScript(string legacyName) => "data/json/scenarios/tutorial/" + Path.GetFileNameWithoutExtension(legacyName) + ".json";

    /// <summary>Folder of the JSON schemas (one per data file kind, DATA_SCHEMA.md).</summary>
    public const string SchemaFolder = "data/json/schemas/";

    private static readonly (string prefix, string suffix, string schema)[] SchemaRules =
    {
        (Units, null, "units"), (Cards, null, "cards"), (Commanders, null, "commanders"), (Portraits, null, "portraits"),
        (Campaigns, null, "campaigns"), (Conquests, null, "conquests"), (Terrain, null, "terrain"), (Medals, null, "medals"),
        (Rules, null, "rules"), (OptionsLayout, null, "options-layout"), (LayoutProfiles, null, "layout-profiles"),
        (Audio, null, "audio"), (Countries, null, "countries"), (ScenarioIndex, null, "scenario-index"), (AiProfiles, null, "ai"),
        ("data/json/maps/", "/areas.json", "map-areas"), ("data/json/text/", ".json", "text"),
        ("data/maps/", "/map.json", "map-pack"), ("data/maps/", "/regions.json", "map-regions"),
    };

    /// <summary>The schema a data file is checked against when it loads (null: none).</summary>
    public static string SchemaFor(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        string p = path.Replace('\\', '/');
        foreach (var (prefix, suffix, schema) in SchemaRules)
        {
            bool match = suffix == null ? p.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                                        : p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && p.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
            if (match) return SchemaFolder + schema + ".schema.json";
        }
        return null;
    }

    /// <summary>
    /// JSON version of an original string table, by original name or package path:
    /// <c>zh_CN.lproj/Localizable.strings</c> or <c>data/strings/cn/Localizable.strings</c> ->
    /// <c>data/json/text/cn/Localizable.json</c>; <c>stringtable_cn_iPad.xml</c> ->
    /// <c>data/json/text/cn/stringtable_cn_iPad.json</c>.
    /// </summary>
    public static string TextJson(string fileName)
    {
        string name = Path.GetFileName(fileName ?? "");
        string stem = Path.GetFileNameWithoutExtension(name);
        string lang = null;
        if (stem.StartsWith("stringtable_", StringComparison.OrdinalIgnoreCase) && stem.Length >= 14)
            lang = stem.Substring(12, 2).ToLowerInvariant();
        else
        {
            var parts = (fileName ?? "").Replace('\\', '/').Split('/');
            for (int i = parts.Length - 2; i >= 0 && lang == null; i--)
            {
                if (parts[i].EndsWith(".lproj", StringComparison.OrdinalIgnoreCase)) lang = AssetSystem.LanguageCodeOf(parts[i]);
                else if (i > 0 && parts[i - 1].Equals("strings", StringComparison.OrdinalIgnoreCase)) lang = parts[i];
            }
            lang ??= string.IsNullOrEmpty(AssetSystem.LanguageCode) ? "en" : AssetSystem.LanguageCode;
        }
        return $"data/json/text/{lang}/{stem}.json";
    }
}
