using System.Reflection;
using System.Text.Json.Nodes;

namespace WC2;

/// <summary>
/// One-time conversion of the original XML data to the JSON game data, and the proof that nothing
/// changed. <c>--convert-data DIR</c> writes the JSON files (DIR is normally WC2/content); after a
/// rebuild <c>--verify-data</c> loads every type twice, from the original XML and from the JSON, and
/// compares the resulting definitions field by field.
/// </summary>
public static class DataMigration
{
    private sealed class Converter
    {
        public string name;
        public Func<IEnumerable<(string path, JsonNode json)>> convert;
        public Func<List<string>> verify;
        /// <summary>Nesting depth written on one line (0 = fully indented).</summary>
        public int inlineDepth;
    }

    private static List<Converter> Converters() => new()
    {
        new Converter
        {
            name = "units (armydef.xml)",
            convert = () => One(DataPaths.Units, CObjectDef.ConvertArmyDefXml()),
            verify = () =>
            {
                var xml = new CObjectDef(); xml.LoadArmyDef(); var regXml = ArmyRegistrySnapshot();
                var json = new CObjectDef();
                if (!json.LoadUnitsJson()) return new List<string> { DataPaths.Units + " missing" };
                var regJson = ArmyRegistrySnapshot();
                var d = DataCompare.Diff(xml.mapArmyDefList, json.mapArmyDefList, "units");
                d.AddRange(DataCompare.Diff(xml.weaponArmorDamageRates, json.weaponArmorDamageRates, "weaponArmor"));
                d.AddRange(DataCompare.Diff(regXml, regJson, "unitTypes"));
                return d;
            },
        },
        Table("cards (carddef.xml)", DataPaths.Cards, CObjectDef.ConvertCardDefXml, o => o.LoadCardDef(), o => o.LoadCardsJson(), o => o.mapNewCardDef),
        Table("commanders (commanderdef.xml)", DataPaths.Commanders, CObjectDef.ConvertCommanderDefXml, o => o.LoadCommanderDef(), o => o.LoadCommandersJson(), o => o.mapCommanderDef),
        Table("portraits (generalphotos.xml)", DataPaths.Portraits, CObjectDef.ConvertGeneralPhotosXml, o => o.LoadGeneralPhotos(), o => o.LoadPortraitsJson(), o => o.mapGeneralPhotos),
        Table("campaigns (battlelist.xml)", DataPaths.Campaigns, CObjectDef.ConvertBattleListXml, o => o.LoadBattleList(), o => o.LoadCampaignsJson(), o => o.mapBattleDef),
        Table("conquests (conquestlist.xml)", DataPaths.Conquests, CObjectDef.ConvertConquestListXml, o => o.LoadConquestList(), o => o.LoadConquestsJson(), o => o.mapConquestList),
        new Converter
        {
            name = "string tables (Localizable.strings, stringtable_*.xml)",
            convert = ConvertStringTables,
            verify = () =>
            {
                var diffs = new List<string>();
                int count = 0;
                foreach (var path in StringTableSources())
                {
                    var plist = new ecStringTable(); plist.LoadPlist(path);
                    var json = new ecStringTable();
                    if (!json.LoadJson(ecStringTable.JsonTwin(path))) { diffs.Add(ecStringTable.JsonTwin(path) + " missing"); continue; }
                    diffs.AddRange(DataCompare.Diff(plist.table, json.table, path));
                    count++;
                }
                if (count == 0) diffs.Add("no string tables found");
                return diffs;
            },
        },
        new Converter
        {
            name = "unit and commander levels (built-in tables)",
            convert = () => Enumerable.Empty<(string, JsonNode)>(),   // written with units.json / commanders.json
            verify = () =>
            {
                CArmy.SetLevelTable(null, null);
                CCommander.SetLevelTable(null, null);
                var builtIn = LevelTablesJson.Snapshot();
                var units = JsonData.LoadMerged(DataPaths.Units);
                var commanders = JsonData.LoadMerged(DataPaths.Commanders);
                if (units?.Arr("levels") == null || commanders?.Arr("levels") == null) return new List<string> { "levels missing" };
                LevelTablesJson.ReadUnitLevels(units.Arr("levels"));
                LevelTablesJson.ReadCommanderLevels(commanders.Arr("levels"));
                return DataCompare.Diff(builtIn, LevelTablesJson.Snapshot(), "levels");
            },
        },
        new Converter
        {
            name = "scenario index (battle_*.xml, conquest_*.xml, tutorails*.xml)",
            inlineDepth = 4,
            convert = () => One(DataPaths.ScenarioIndex, ScenarioIndex.Build()),
            verify = () =>
            {
                var diffs = new List<string>();
                var built = ScenarioIndex.Build();
                if (JsonData.LoadMerged(DataPaths.ScenarioIndex) is not JsonObject file) return new List<string> { DataPaths.ScenarioIndex + " missing" };
                foreach (var section in new[] { "scenarios", "tutorialScripts" })
                {
                    var a = built.Obj(section) ?? new JsonObject();
                    var b = file.Obj(section) ?? new JsonObject();
                    foreach (var kv in a)
                        if (!JsonNode.DeepEquals(kv.Value, b.Get(kv.Key))) diffs.Add($"{section}.{kv.Key}: the index does not match the XML");
                    foreach (var kv in b)
                    {
                        if (a.Get(kv.Key) == null) diffs.Add($"{section}.{kv.Key}: no such original file");
                        if ((kv.Value as JsonObject)?.Str("file") is string f && !AssetSystem.Exists(f)) diffs.Add($"{section}.{kv.Key}: file {f} missing");
                    }
                }
                return diffs;
            },
        },
        new Converter
        {
            name = "options page layout (guioptions.xml)",
            inlineDepth = 2,
            convert = () => One(DataPaths.OptionsLayout, OptionsLayoutJson.ConvertXml()),
            verify = () =>
            {
                var xml = new TiXmlDocument(GetPath("guioptions.xml", null));
                if (!xml.LoadFile()) return new List<string> { "guioptions.xml unreadable" };
                if (!AssetSystem.Exists(DataPaths.OptionsLayout)) return new List<string> { DataPaths.OptionsLayout + " missing" };
                return DataCompare.Diff(OptionsLayoutJson.Describe(xml), OptionsLayoutJson.Describe(OptionsLayoutJson.Load()), "options");
            },
        },
        new Converter
        {
            name = "layout profiles (built-in buckets)",
            inlineDepth = 2,
            convert = () => One(DataPaths.LayoutProfiles, LayoutProfiles.BuiltInJson()),
            verify = () => BuiltInVsJson(DataPaths.LayoutProfiles, "layout", LayoutProfiles.LoadBuiltIn, LayoutProfiles.LoadJson, LayoutProfiles.Snapshot),
        },
        new Converter
        {
            name = "audio (built-in file names)",
            convert = () => One(DataPaths.Audio, AudioDefs.BuiltInJson()),
            verify = () => BuiltInVsJson(DataPaths.Audio, "audio", AudioDefs.LoadBuiltIn, AudioDefs.LoadJson, AudioDefs.Snapshot),
        },
        new Converter
        {
            name = "countries (built-in lists)",
            inlineDepth = 2,
            convert = () => One(DataPaths.Countries, CountryDefs.BuiltInJson()),
            verify = () => BuiltInVsJson(DataPaths.Countries, "countries", CountryDefs.LoadBuiltIn, CountryDefs.LoadJson, CountryDefs.Snapshot),
        },
        new Converter
        {
            name = "global rules (built-in rules)",
            convert = () => One(DataPaths.Rules, GameRules.BuiltInJson()),
            verify = () =>
            {
                GameRules.LoadBuiltIn();
                var builtIn = GameRules.Snapshot();
                if (!GameRules.LoadJson()) return new List<string> { DataPaths.Rules + " missing" };
                return DataCompare.Diff(builtIn, GameRules.Snapshot(), "rules");
            },
        },
        new Converter
        {
            name = "war medals (built-in rules)",
            convert = () => One(DataPaths.Medals, Medals.BuiltInJson()),
            verify = () =>
            {
                Medals.LoadBuiltIn();
                var builtIn = Medals.Snapshot();
                if (!Medals.LoadJson()) return new List<string> { DataPaths.Medals + " missing" };
                return DataCompare.Diff(builtIn, Medals.Snapshot(), "medals");
            },
        },
        new Converter
        {
            name = "terrain (built-in rules)",
            convert = () => One(DataPaths.Terrain, Terrain.BuiltInJson()),
            verify = () =>
            {
                Terrain.LoadBuiltIn();
                var builtIn = Terrain.Snapshot();
                if (!Terrain.LoadJson()) return new List<string> { DataPaths.Terrain + " missing" };
                return DataCompare.Diff(builtIn, Terrain.Snapshot(), "terrain");
            },
        },
        new Converter
        {
            name = "map area tables (areatax*.xml)",
            inlineDepth = 2,
            convert = () => MapIds().Select(id => (DataPaths.MapAreas(id), (JsonNode)CScene.ConvertAreaTableXml(id))).Where(x => x.Item2 != null),
            verify = () =>
            {
                var diffs = new List<string>();
                foreach (int id in MapIds())
                {
                    var json = CScene.ReadAreaTableJson(id);
                    if (json == null) { diffs.Add(DataPaths.MapAreas(id) + " missing"); continue; }
                    diffs.AddRange(DataCompare.Diff(CScene.ReadAreaTableXml(id), json, "areas" + id));
                }
                return diffs;
            },
        },
    };

    private static List<int> MapIds() => AssetSystem.List()
        .Select(Path.GetFileName)
        .Where(n => n.StartsWith("areatax", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        .Select(n => int.TryParse(n.Substring(7, n.Length - 11), out int id) ? id : -1)
        .Where(id => id >= 0).OrderBy(id => id).ToList();

    /// <summary>Loads the built-in values, then the JSON file, and compares the two.</summary>
    private static List<string> BuiltInVsJson(string path, string label, Action loadBuiltIn, Func<bool> loadJson, Func<object> snapshot)
    {
        loadBuiltIn();
        var builtIn = snapshot();
        if (!loadJson()) return new List<string> { path + " missing" };
        return DataCompare.Diff(builtIn, snapshot(), label);
    }

    /// <summary>Every original string table of the package (all languages).</summary>
    private static List<string> StringTableSources() => AssetSystem.List()
        .Where(p => Path.GetFileName(p).Equals("Localizable.strings", StringComparison.OrdinalIgnoreCase)
                    || (Path.GetFileName(p).StartsWith("stringtable_", StringComparison.OrdinalIgnoreCase) && p.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
        .OrderBy(p => p, StringComparer.Ordinal).ToList();

    private static IEnumerable<(string, JsonNode)> ConvertStringTables()
    {
        foreach (var path in StringTableSources())
        {
            var plist = new ecStringTable();
            plist.LoadPlist(path);
            var strings = new JsonObject();
            foreach (var (k, v) in plist.table) strings[k] = v;
            var about = path.EndsWith(".strings", StringComparison.OrdinalIgnoreCase)
                ? "Language settings (converted from Localizable.strings): \"language\" is the code used in file names, \"stringtable\" / \"stringtable iPad\" name the string tables."
                : "Game texts of this language (converted from " + Path.GetFileName(path) + "). A mod can override single keys.";
            yield return (ecStringTable.JsonTwin(path), new JsonObject { ["format"] = 1, ["about"] = about, ["strings"] = strings });
        }
    }

    /// <summary>A CObjectDef table: convert the XML, then load both versions into fresh objects and compare.</summary>
    private static Converter Table(string name, string path, Func<JsonObject> convert, Action<CObjectDef> loadXml, Func<CObjectDef, bool> loadJson, Func<CObjectDef, object> select) => new()
    {
        name = name,
        convert = () => One(path, convert()),
        verify = () =>
        {
            var xml = new CObjectDef(); loadXml(xml);
            var json = new CObjectDef();
            if (!loadJson(json)) return new List<string> { path + " missing" };
            return DataCompare.Diff(select(xml), select(json), Path.GetFileNameWithoutExtension(path));
        },
    };

    private static IEnumerable<(string, JsonNode)> One(string path, JsonNode node)
    {
        if (node != null) yield return (path, node);
    }

    public static int Convert(string contentDir)
    {
        int written = 0;
        foreach (var c in Converters())
        {
            foreach (var (path, json) in c.convert())
            {
                string target = Path.Combine(contentDir, path.Replace('/', Path.DirectorySeparatorChar));
                JsonData.Write(target, json, c.inlineDepth);
                written++;
            }
            Console.WriteLine($"converted {c.name}");
        }
        Console.WriteLine($"convert-data: {written} file(s) written to {contentDir}");
        return written;
    }

    public static int Verify()
    {
        int failures = 0;
        // every JSON data file against its schema (loading validates; mods included)
        JsonData.ValidationProblems.Clear();
        int checkedFiles = 0;
        foreach (var path in AssetSystem.List("data/json/").Where(p => p.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            if (DataPaths.SchemaFor(path) == null) continue;
            JsonData.LoadMerged(path);
            checkedFiles++;
        }
        var schemaProblems = JsonData.ValidationProblems.ToList();
        if (schemaProblems.Count == 0) Console.WriteLine($"verify schemas ({checkedFiles} data files): identical");
        else
        {
            failures++;
            Console.WriteLine($"verify schemas ({checkedFiles} data files): {schemaProblems.Count} difference(s)");
            foreach (var problem in schemaProblems.Take(40)) Console.WriteLine("   " + problem);
        }

        foreach (var c in Converters())
        {
            List<string> diffs;
            try { diffs = c.verify(); }
            catch (Exception ex) { diffs = new List<string> { "exception: " + ex }; }
            if (diffs.Count == 0) Console.WriteLine($"verify {c.name}: identical");
            else
            {
                failures++;
                Console.WriteLine($"verify {c.name}: {diffs.Count} difference(s)");
                foreach (var d in diffs.Take(20)) Console.WriteLine("   " + d);
            }
        }
        Console.WriteLine(failures == 0 ? "verify-data: all identical" : $"verify-data: {failures} type(s) differ");
        return failures;
    }

    /// <summary>Copies the unit type registry (private statics of armydef_internal) for comparison.</summary>
    private static Dictionary<string, object> ArmyRegistrySnapshot()
    {
        var snapshot = new Dictionary<string, object>();
        foreach (var f in typeof(armydef_internal).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (f.IsLiteral) continue;
            object v = f.GetValue(null);
            snapshot[f.Name] = v switch
            {
                System.Collections.IDictionary dict => dict.Keys.Cast<object>().ToDictionary(k => k, k => dict[k]),
                _ => v,
            };
        }
        return snapshot;
    }
}
