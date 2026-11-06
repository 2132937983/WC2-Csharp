namespace WC2;

/// <summary>Package paths of the JSON game data (see docs/DATA.md).</summary>
public static class DataPaths
{
    public const string Units = "data/units.json";
    public const string Cards = "data/cards.json";
    public const string Commanders = "data/commanders.json";
    public const string Portraits = "data/portraits.json";
    public const string Campaigns = "data/campaigns.json";
    public const string Conquests = "data/conquests.json";
    public const string Terrain = "data/terrain.json";
    public const string Medals = "data/medals.json";
    public const string Rules = "data/rules.json";
    public const string OptionsLayout = "data/ui/options.json";

    /// <summary>Language settings of the current language folder (was Localizable.strings).</summary>
    public const string Language = "language.json";

    public static string MapAreas(int mapId) => $"maps/{mapId}/areas.json";

    /// <summary>Scenario of a legacy file name: "battle_axis1.xml" -> "scenarios/battle_axis1.json".</summary>
    public static string Scenario(string legacyName) => "scenarios/" + Path.GetFileNameWithoutExtension(legacyName) + ".json";

    /// <summary>Tutorial script of a legacy name: "tutorails_script-568h.xml" -> "scenarios/tutorial/tutorails_script-568h.json".</summary>
    public static string TutorialScript(string legacyName) => "scenarios/tutorial/" + Path.GetFileNameWithoutExtension(legacyName) + ".json";
}
