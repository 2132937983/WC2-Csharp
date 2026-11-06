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

    /// <summary>Language settings of the current language folder (was Localizable.strings).</summary>
    public const string Language = "language.json";

    public static string MapAreas(int mapId) => $"data/json/maps/{mapId}/areas.json";

    /// <summary>Scenario of a legacy file name: "battle_axis1.xml" -> "data/json/scenarios/battle_axis1.json".</summary>
    public static string Scenario(string legacyName) => "data/json/scenarios/" + Path.GetFileNameWithoutExtension(legacyName) + ".json";

    /// <summary>Tutorial script of a legacy name: "tutorails_script-568h.xml" -> "data/json/scenarios/tutorial/tutorails_script-568h.json".</summary>
    public static string TutorialScript(string legacyName) => "data/json/scenarios/tutorial/" + Path.GetFileNameWithoutExtension(legacyName) + ".json";

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
