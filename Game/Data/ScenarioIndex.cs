using System.Text.Json.Nodes;

namespace WC2;

/// <summary>
/// <c>data/json/scenarios/index.json</c>: the scenarios of the game and the tutorial scripts. The scenario
/// files themselves stay the original XML (<c>data/xml/scenarios/</c>, <c>scripts/tutorial/</c>); the
/// index names each one, says where its file is and summarises it (mode, map, countries, areas), so
/// tools and mods can list scenarios without parsing XML, and a mod can add a scenario whose XML lives
/// anywhere. The summary is generated from the XML by <c>--convert-data</c> and checked by
/// <c>--verify-data</c>.
/// <code>
/// { "scenarios": { "battle_axis1": { "file": "data/xml/scenarios/battle_axis1.xml", "mode": "campaign",
///     "series": "axis", "level": 1, "map": 1, "areaMask": "data/binary/scenarios/battle_axis1.bin",
///     "countries": [ { "id": "de", "alliance": 1, "player": true }, ... ], "areas": 120, "dialogues": 3 } },
///   "tutorialScripts": { "tutorails_script": { "file": "scripts/tutorial/tutorails_script.xml" } } }
/// </code>
/// </summary>
public static class ScenarioIndex
{
    private static JsonObject s_index;

    /// <summary>Reads the index (merged with mods); a missing index means "every name is its own file".</summary>
    public static void Load() => s_index = JsonData.LoadMerged(DataPaths.ScenarioIndex) as JsonObject;

    private static JsonObject Entry(string section, string name)
    {
        if (s_index == null) Load();
        string key = Path.GetFileNameWithoutExtension(name ?? "");
        return s_index?.Obj(section)?.Obj(key);
    }

    /// <summary>The file to read for a scenario name ("battle_axis1.xml"): the index entry, else the name itself.</summary>
    public static string FileOf(string scenarioName) => Entry("scenarios", scenarioName)?.Str("file") is string f && f.Length > 0 ? f : scenarioName;

    /// <summary>The file to read for a tutorial script name.</summary>
    public static string ScriptFileOf(string scriptName) => Entry("tutorialScripts", scriptName)?.Str("file") is string f && f.Length > 0 ? f : scriptName;

    /// <summary>Builds the index from the original XML files of the package.</summary>
    public static JsonObject Build()
    {
        var scenarios = new JsonObject();
        foreach (var fileName in BattleScenario.ScenarioSources())
        {
            var data = new BattleScenarioData();
            if (!BattleScenario.LoadScenarioXml(fileName, data)) continue;
            string stem = Path.GetFileNameWithoutExtension(fileName);
            var entry = new JsonObject { ["file"] = AssetSystem.Resolve(fileName) ?? fileName };
            if (stem.StartsWith("battle_", StringComparison.OrdinalIgnoreCase))
            {
                string rest = stem.Substring(7);
                int digits = rest.Length;
                while (digits > 0 && char.IsDigit(rest[digits - 1])) digits--;
                entry["mode"] = "campaign";
                entry["series"] = rest.Substring(0, digits);
                if (int.TryParse(rest.Substring(digits), out int level)) entry["level"] = level;
            }
            else if (stem.StartsWith("conquest_", StringComparison.OrdinalIgnoreCase))
            {
                entry["mode"] = "conquest";
                if (int.TryParse(stem.Substring(9), out int number)) entry["level"] = number;
            }
            else entry["mode"] = "tutorial";
            entry["map"] = data.mapId;
            string mask = AssetSystem.Resolve(stem + ".bin");
            if (mask != null) entry["areaMask"] = mask;
            var countries = new JsonArray();
            foreach (var c in data.countries)
            {
                var co = new JsonObject { ["id"] = c.id, ["alliance"] = c.alliance };
                if (c.hasAi && c.ai == 0) co["player"] = true;
                countries.Add(co);
            }
            entry["countries"] = countries;
            entry["areas"] = data.areas.Count;
            entry["dialogues"] = data.dialogues.Count;
            scenarios[stem] = entry;
        }

        var scripts = new JsonObject();
        foreach (var path in AssetSystem.List().Where(p => Path.GetFileName(p).StartsWith("tutorails_script", StringComparison.OrdinalIgnoreCase)
                                                           && p.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                                               .OrderBy(p => p, StringComparer.Ordinal))
            scripts[Path.GetFileNameWithoutExtension(path)] = new JsonObject { ["file"] = path };

        return new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Scenarios and tutorial scripts. The files stay the original XML; this index says where each one is " +
                        "and summarises it (generated from the XML by --convert-data, checked by --verify-data). " +
                        "Field reference: DATA_SCHEMA.md#scenarios.",
            ["scenarios"] = scenarios,
            ["tutorialScripts"] = scripts,
        };
    }
}
