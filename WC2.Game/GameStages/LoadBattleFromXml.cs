using System.Text.Json.Nodes;

namespace WC2;

/// <summary>
/// Scenario loading (LoadBattleFromXml.cpp, BattleScenarioData.h). A scenario is read into
/// <see cref="BattleScenarioData"/>, from <c>scenarios/&lt;name&gt;.json</c> or, when the package has no
/// JSON for it, from the original battle XML; the live match is then built from that data only.
/// GetBattleBelligerentList lives in SelectCountry.cs.
///
/// <code>
/// {
///   "map": 1, "areasEnable": "battle_axis1.bin",
///   "countries": [ {"id": "de", "name": "Germany", "commander": "Guderian", "ai": 0, "alliance": "a",
///                   "defeated": "land", "money": 300, "industry": 100, "techLevel": 2, "taxFactor": 1.0,
///                   "r": 120, "g": 120, "b": 120, "a": 255}, ... ],          // turn order
///   "areas": [ {"id": 512, "country": "de", "construction": "city", "level": 2, "installation": "fort",
///               "units": [ {"type": "infantry", "level": 1, "cards": 0} ]}, ... ],
///   "dialogues": [ {"commander": "Guderian", "index": 1, "atRound": 1, "left": true}, ... ]
/// }
/// </code>
/// Alliances: "a", "b", "c" are the three sides, anything else is neutral (original rule).
/// </summary>
public static partial class BattleScenario
{
    // ---- file-local statics of LoadBattleFromXml.cpp -------------------------------------
    internal static readonly BattleScenarioData g_preloadedBattleScenario = new();
    internal static bool g_hasPreloadedBattleScenario = false;

    internal const int WC2_USE_LEGACY_BATTLE_XML_LOADER = 0;

    /// <summary>
    /// The original alliance parsing.
    /// </summary>
    /// <param name="value">attribute string</param>
    /// <returns>alliance value</returns>
    internal static int ParseAlliance(string value)
    {
        // original: a/b/c are the three alliances, anything else (the shipped files use "n" and
        // "d") is neutral. The remake mapped every letter to its index, which turned the "n"
        // countries of 15 scenarios into a belligerent alliance 14.
        if (value != null && value.Length > 0)
        {
            switch (value[0])
            {
                case 'a': return 1;
                case 'b': return 2;
                case 'c': return 3;
            }
        }
        return (int)NEUTRAL;
    }

    /// <summary>The original construction parsing.</summary>
    internal static int ParseConstruction(string value)
    {
        if (value == null) return (int)NO_CONSTRUCTION;
        if (value == "city") return (int)CITY;
        if (value == "industry") return (int)INDUSTRY;
        if (value == "airport") return (int)AIRPORT;
        return (int)NO_CONSTRUCTION;
    }

    /// <summary>The original installation parsing.</summary>
    internal static int ParseInstallation(string value)
    {
        if (value == null) return (int)NO_INSTALLATION;
        if (value == "fort") return (int)FORT;
        if (value == "entrenchment") return (int)ENTRENCHMENT;
        if (value == "antiaircraft") return (int)ANTIAIRCRAFT;
        if (value == "radar") return (int)RADAR;
        return (int)NO_INSTALLATION;
    }

    /// <summary>Defeat condition parsing; adds the core defeat to the original.</summary>
    internal static int ParseDefeat(string value)
    {
        if (value == null) return (int)DEFEAT_TYPE.LAND;
        if (value == "army") return (int)DEFEAT_TYPE.ARMY;
        if (value == "core") return (int)DEFEAT_TYPE.CORE;
        return (int)DEFEAT_TYPE.LAND;
    }

    /// <summary>
    /// Preloads a scenario for the conquest preview; campaigns do not need it.
    /// </summary>
    /// <param name="fileName">scenario name (the original XML file name)</param>
    /// <returns>whether the load succeeded</returns>
    public static bool PreloadBattleScenario(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return false;
        if (g_hasPreloadedBattleScenario &&
            g_preloadedBattleScenario.fileName == fileName)
        {
            return true;
        }
        g_hasPreloadedBattleScenario = false;
        g_preloadedBattleScenario.Clear();
        if (!LoadBattleScenarioData(fileName, g_preloadedBattleScenario))
        {
            g_preloadedBattleScenario.Clear();
            return false;
        }
        g_hasPreloadedBattleScenario = true;
        return true;
    }

    /// <summary>The preloaded data for the file, or null.</summary>
    public static BattleScenarioData GetPreloadedBattleScenario(string fileName)
    {
        if (!g_hasPreloadedBattleScenario || fileName == null ||
            g_preloadedBattleScenario.fileName != fileName)
        {
            return null;
        }
        return g_preloadedBattleScenario;
    }

    /// <summary>Releases the preloaded data.</summary>
    public static void ReleasePreloadedBattleScenario()
    {
        g_hasPreloadedBattleScenario = false;
        g_preloadedBattleScenario.Clear();
    }

    /// <summary>
    /// Reads a scenario: the original XML (located through data/json/scenarios/index.json). A scenario in
    /// the JSON format (data/json/scenarios/&lt;name&gt;.json: a battle exported by the pause box Save button
    /// and put into a mod) is read instead when one exists; the game itself ships none.
    /// </summary>
    /// <param name="fileName">scenario name (the original XML file name, e.g. "conquest_1.xml")</param>
    /// <param name="outData">receives the data</param>
    /// <returns>whether the load succeeded</returns>
    public static bool LoadBattleScenarioData(string fileName, BattleScenarioData outData)
    {
        if (fileName == null || outData == null) return false;
        string jsonPath = DataPaths.Scenario(fileName);
        if (AssetSystem.Exists(jsonPath)) return LoadScenarioJson(jsonPath, fileName, outData);
        return LoadScenarioXml(fileName, outData, ScenarioIndex.FileOf(fileName));
    }

    /// <summary>The original battle XML (<paramref name="path"/>: where to read it, default the name itself).</summary>
    public static bool LoadScenarioXml(string fileName, BattleScenarioData outData, string path = null)
    {
        outData.Clear();
        outData.fileName = fileName;

        var document = new TiXmlDocument(GetPath(path ?? fileName, null));
        if (!document.LoadFile(TIXML_ENCODING_UNKNOWN)) return false;

        TiXmlElement battle = document.FirstChildElement("battle");
        if (battle == null) return false;

        if (battle.QueryIntAttribute("map", out int mapIdValue) == TIXML_SUCCESS) outData.mapId = mapIdValue;
        string enabled = battle.Attribute("areasenable");
        if (enabled != null) outData.areasEnable = enabled;

        for (TiXmlElement list = battle.FirstChildElement("list"); list != null;
             list = list.NextSiblingElement("list"))
        {
            string listName = list.Attribute("name");
            if (listName == null) continue;
            if (listName == "country")
            {
                for (TiXmlElement node = list.FirstChildElement(); node != null;
                     node = node.NextSiblingElement())
                {
                    var country = new BattleScenarioCountryData();
                    string value = node.Attribute("id");
                    if (value == null) continue;
                    country.id = value;
                    value = node.Attribute("name");
                    if (value != null) { country.name = value; country.hasName = true; }
                    value = node.Attribute("commander");
                    if (value != null) { country.commander = value; country.hasCommander = true; }
                    if (node.QueryIntAttribute("ai", out int aiValue) == TIXML_SUCCESS) { country.ai = aiValue; country.hasAi = true; }
                    country.alliance = ParseAlliance(node.Attribute("alliance"));
                    country.defeated = ParseDefeat(node.Attribute("defeated"));
                    if (node.QueryIntAttribute("r", out int rValue) == TIXML_SUCCESS) country.r = rValue;
                    if (node.QueryIntAttribute("g", out int gValue) == TIXML_SUCCESS) country.g = gValue;
                    if (node.QueryIntAttribute("b", out int bValue) == TIXML_SUCCESS) country.b = bValue;
                    if (node.QueryIntAttribute("a", out int aValue) == TIXML_SUCCESS) country.a = aValue;
                    if (node.QueryIntAttribute("money", out int moneyValue) == TIXML_SUCCESS) country.money = moneyValue;
                    if (node.QueryIntAttribute("industry", out int industryValue) == TIXML_SUCCESS) country.industry = industryValue;
                    if (node.QueryIntAttribute("techlevel", out int techValue) == TIXML_SUCCESS) country.techLevel = techValue;
                    if (node.QueryDoubleAttribute("taxfactor", out double taxValue) == TIXML_SUCCESS) country.taxFactor = taxValue;
                    outData.countries.Add(country);
                }
            }
            else if (listName == "area")
            {
                for (TiXmlElement node = list.FirstChildElement(); node != null;
                     node = node.NextSiblingElement())
                {
                    var area = new BattleScenarioAreaData();
                    if (node.QueryIntAttribute("id", out int areaIdValue) != TIXML_SUCCESS ||
                        areaIdValue < 0)
                    {
                        continue;
                    }
                    area.id = areaIdValue;
                    string value = node.Attribute("country");
                    if (value != null) area.country = value;
                    area.construction = ParseConstruction(node.Attribute("construction"));
                    if (node.QueryIntAttribute("level", out int levelValue) == TIXML_SUCCESS) area.constructionLevel = levelValue;
                    area.installation = ParseInstallation(node.Attribute("installation"));
                    // the live match turns every child element into a unit; the preview lists the <army> types
                    for (TiXmlElement army = node.FirstChildElement(); army != null; army = army.NextSiblingElement())
                    {
                        var armyData = ReadArmyXml(army);
                        area.armies.Add(armyData);
                        if (army.Value() == "army" && armyData.type != null) area.armyTypes.Add(armyData.type);
                    }
                    outData.areas.Add(area);
                }
            }
            else if (listName == "dialogue")
            {
                for (TiXmlElement dialog = list.FirstChildElement(); dialog != null; dialog = dialog.NextSiblingElement())
                {
                    outData.dialogues.Add(CGameManager.ParseDialogue(dialog));
                }
            }
        }
        return true;
    }

    /// <summary>One unit element of the original XML.</summary>
    public static BattleScenarioArmyData ReadArmyXml(TiXmlElement armyElement)
    {
        var data = new BattleScenarioArmyData { type = armyElement.Attribute("type") };
        if (armyElement.QueryIntAttribute("level", out int level) == TIXML_SUCCESS) data.level = level;
        if (armyElement.QueryIntAttribute("cards", out int cards) == TIXML_SUCCESS) data.cards = cards;
        if (armyElement.QueryIntAttribute("designation", out int designation) == TIXML_SUCCESS) data.designation = designation;
        return data;
    }

    /// <summary>scenarios/&lt;name&gt;.json (format in the class remarks).</summary>
    public static bool LoadScenarioJson(string jsonPath, string fileName, BattleScenarioData outData)
    {
        outData.Clear();
        outData.fileName = fileName;
        var root = JsonData.LoadMerged(jsonPath);
        if (root == null) return false;

        if (root.TryInt("map", out int map)) outData.mapId = map;
        string enabled = root.Str("areasEnable");
        if (enabled != null) outData.areasEnable = enabled;

        foreach (var c in root.Arr("countries").Objects())
        {
            string id = c.Str("id");
            if (id == null) continue;
            var country = new BattleScenarioCountryData { id = id };
            if (c.Str("name") is string name) { country.name = name; country.hasName = true; }
            if (c.Str("commander") is string commander) { country.commander = commander; country.hasCommander = true; }
            if (c.TryInt("ai", out int ai)) { country.ai = ai; country.hasAi = true; }
            country.alliance = ParseAlliance(c.Str("alliance"));
            country.defeated = ParseDefeat(c.Str("defeated"));
            if (c.TryInt("r", out int r)) country.r = r;
            if (c.TryInt("g", out int g)) country.g = g;
            if (c.TryInt("b", out int b)) country.b = b;
            if (c.TryInt("a", out int a)) country.a = a;
            if (c.TryInt("money", out int money)) country.money = money;
            if (c.TryInt("industry", out int industry)) country.industry = industry;
            if (c.TryInt("techLevel", out int tech)) country.techLevel = tech;
            if (c.TryDouble("taxFactor", out double tax)) country.taxFactor = tax;
            outData.countries.Add(country);
        }

        foreach (var a in root.Arr("areas").Objects())
        {
            if (!a.TryInt("id", out int areaId) || areaId < 0) continue;
            var area = new BattleScenarioAreaData { id = areaId };
            if (a.Str("country") is string owner) area.country = owner;
            area.construction = ParseConstruction(a.Str("construction"));
            if (a.TryInt("level", out int level)) area.constructionLevel = level;
            area.installation = ParseInstallation(a.Str("installation"));
            foreach (var u in a.Arr("units").Objects())
            {
                var army = new BattleScenarioArmyData { type = u.Str("type") };
                if (u.TryInt("level", out int armyLevel)) army.level = armyLevel;
                if (u.TryInt("cards", out int cards)) army.cards = cards;
                if (u.TryInt("designation", out int designation)) army.designation = designation;
                area.armies.Add(army);
                if (army.type != null) area.armyTypes.Add(army.type);
            }
            outData.areas.Add(area);
        }

        foreach (var d in root.Arr("dialogues").Objects())
        {
            outData.dialogues.Add(new DialogueDef
            {
                commander = d.Str("commander") ?? "",
                index = d.Int("index", 1),
                atRound = d.Int("atRound", 1),
                left = d.TryBool("left", out bool left) ? left : d.Int("left", 0) != 0,
            });
        }
        return true;
    }

    /// <summary>Converts an original battle XML to the scenario JSON (--convert-data).</summary>
    public static JsonObject ConvertScenarioXml(string fileName)
    {
        var document = new TiXmlDocument(GetPath(fileName, null));
        if (!document.LoadFile(TIXML_ENCODING_UNKNOWN)) return null;
        TiXmlElement battle = document.FirstChildElement("battle");
        if (battle == null) return null;
        var root = new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Scenario (converted from " + fileName + "). Countries are in turn order; alliance a/b/c are the sides, anything else is neutral. Field reference: DATA_SCHEMA.md#scenarios.",
        };
        Copy(battle, root, "map", "map");
        Copy(battle, root, "areasenable", "areasEnable");
        var countries = new JsonArray();
        var areas = new JsonArray();
        var dialogues = new JsonArray();
        for (TiXmlElement list = battle.FirstChildElement("list"); list != null; list = list.NextSiblingElement("list"))
        {
            string listName = list.Attribute("name");
            if (listName == "country")
            {
                for (var node = list.FirstChildElement(); node != null; node = node.NextSiblingElement())
                {
                    var c = new JsonObject();
                    // text attributes are copied as text, numbers as numbers
                    foreach (var key in new[] { "id", "name", "commander" })
                        if (node.Attribute(key) is string text) c[key] = text;
                    Copy(node, c, "ai", "ai");
                    foreach (var key in new[] { "alliance", "defeated" })
                        if (node.Attribute(key) is string text) c[key] = text;
                    foreach (var (attr, key) in new[] { ("money", "money"), ("industry", "industry"), ("techlevel", "techLevel"),
                                                         ("taxfactor", "taxFactor"), ("r", "r"), ("g", "g"), ("b", "b"), ("a", "a") })
                        Copy(node, c, attr, key);
                    countries.Add(c);
                }
            }
            else if (listName == "area")
            {
                for (var node = list.FirstChildElement(); node != null; node = node.NextSiblingElement())
                {
                    var a = new JsonObject();
                    Copy(node, a, "id", "id");
                    if (node.Attribute("country") is string owner) a["country"] = owner;
                    if (node.Attribute("construction") is string construction) a["construction"] = construction;
                    Copy(node, a, "level", "level");
                    if (node.Attribute("installation") is string installation) a["installation"] = installation;
                    var units = new JsonArray();
                    for (var army = node.FirstChildElement(); army != null; army = army.NextSiblingElement())
                    {
                        var u = new JsonObject();
                        if (army.Attribute("type") is string type) u["type"] = type;
                        Copy(army, u, "level", "level");
                        Copy(army, u, "cards", "cards");
                        Copy(army, u, "designation", "designation");
                        units.Add(u);
                    }
                    if (units.Count > 0) a["units"] = units;
                    areas.Add(a);
                }
            }
            else if (listName == "dialogue")
            {
                for (var node = list.FirstChildElement(); node != null; node = node.NextSiblingElement())
                {
                    var d = CGameManager.ParseDialogue(node);
                    var o = new JsonObject();
                    if (node.Attribute("commander") is string commander) o["commander"] = commander;
                    o["index"] = d.index;
                    o["atRound"] = d.atRound;
                    o["left"] = d.left;
                    dialogues.Add(o);
                }
            }
        }
        root["countries"] = countries;
        root["areas"] = areas;
        if (dialogues.Count > 0) root["dialogues"] = dialogues;
        return root;
    }

    private static void Copy(TiXmlElement e, JsonObject o, string attribute, string key)
    {
        string v = e.Attribute(attribute);
        if (v != null) o[key] = JsonData.Value(v);
    }

    /// <summary>Every original scenario file of the package (campaign battles, conquests, the tutorial).</summary>
    public static List<string> ScenarioSources() => AssetSystem.List()
        .Select(Path.GetFileName)
        .Where(n => n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                    && (n.StartsWith("battle_", StringComparison.OrdinalIgnoreCase)
                        || n.StartsWith("conquest_", StringComparison.OrdinalIgnoreCase)
                        || n.Equals("tutorails.xml", StringComparison.OrdinalIgnoreCase)))
        .OrderBy(n => n, StringComparer.Ordinal).ToList();
}

/// <summary>Scenario loading into the live match (CGameManager part of LoadBattleFromXml.cpp).</summary>
public sealed partial class CGameManager
{
    /// <summary>Creates a CArmy from scenario data; null when the unit type has no definition.</summary>
    /// <param name="armyData">unit of the scenario</param>
    /// <param name="curCountry">owner of the area, must not be null</param>
    public static CArmy InitArmy(BattleScenarioArmyData armyData, CCountry curCountry)
    {
        if (curCountry == null || armyData == null)
        {
            return null;
        }
        // scenarios and units.json share one name/id table; unknown custom units no longer become infantry silently
        int ArmyType = armydef_internal.ResolveArmyTypeId(armyData.type);
        ArmyDef ArmyDef = CObjectDef.Instance().GetArmyDef(ArmyType, curCountry.name);
        if (ArmyDef == null)
        {
            // the scenario references an unloaded custom unit: skip it instead of passing a null definition
            return null;
        }
        var army = new CArmy();
        // init the unit and set its attributes
        army.Init(ArmyDef, curCountry);
        army.cards = armyData.cards;
        army.level = armyData.level;
        army.movement = 0;
        if (army.HasCard(ARMY_CARD.COMMANDER))
            curCountry.commanderAlive = true;
        army.ResetMaxStrength(false);
        return army;
    }

    /// <summary>Creates a CArmy from an original XML element (legacy loader).</summary>
    public static CArmy InitArmyFromXml(TiXmlElement armyElement, CCountry curCountry) =>
        InitArmy(BattleScenario.ReadArmyXml(armyElement), curCountry);

    /// <summary>
    /// Parses a dialogue element.
    /// </summary>
    public static DialogueDef ParseDialogue(TiXmlElement dialogElement)
    {
        var dialogueDef = new DialogueDef();
        int Index, AtRound, Left;
        string commander = dialogElement.Attribute("commander");
        dialogueDef.commander = commander != null ? commander : "";
        // dialogue index
        if (dialogElement.QueryIntAttribute("index", out Index) != TIXML_SUCCESS)
            Index = 1;
        // trigger round
        if (dialogElement.QueryIntAttribute("atround", out AtRound) != TIXML_SUCCESS)
            AtRound = 1;
        if (dialogElement.QueryIntAttribute("left", out Left) != TIXML_SUCCESS)
            Left = 0;
        if (Left != 0)
            Left = 1;
        dialogueDef.index = Index;
        dialogueDef.atRound = AtRound;
        dialogueDef.left = Left != 0;
        return dialogueDef;
    }

    /// <summary>
    /// Loads a "level file": battle_axis.xml, conquest_1.xml, ... (scenarios/*.json when available)
    /// </summary>
    /// <param name="FileName">scenario name</param>
    public void LoadBattle(string FileName)
    {
        ClearBattle();
        CCSoundBox.GetInstance().LoadSE(AudioDefs.Ui("explosion"));
        SetRandSeed(TimeSeed());
        LoadBattleXml(FileName);
    }

    /// <summary>
    /// The original loader working directly on the XML; kept for reference behind
    /// WC2_USE_LEGACY_BATTLE_XML_LOADER (the scenario data path below is the live one).
    /// </summary>
    public void LoadBattleXmlLegacy(string FileName)
    {
        var Xml = new TiXmlDocument(GetPath(FileName, null));
        if (!Xml.LoadFile(TIXML_ENCODING_UNKNOWN)) return;
        TiXmlElement BattleElement = Xml.FirstChild("battle") as TiXmlElement;
        if (BattleElement == null) return;
        if (BattleElement.QueryIntAttribute("map", out mapId) != TIXML_SUCCESS) mapId = 1;
        areasEnable = BattleElement.Attribute("areasenable") ?? "";
        g_Scene.Init(areasEnable, mapId);
        for (TiXmlElement ListElement = BattleElement.FirstChildElement(); ListElement != null; ListElement = ListElement.NextSiblingElement())
        {
            string Name = ListElement.Attribute("name");
            if (Name == "country")
            {
                for (TiXmlElement CountryElement = ListElement.FirstChildElement(); CountryElement != null; CountryElement = CountryElement.NextSiblingElement())
                {
                    var country = new CCountry();
                    country.Init(CountryElement.Attribute("id"), CountryElement.Attribute("name"));
                    if (CountryElement.QueryIntAttribute("ai", out int AI) != TIXML_SUCCESS) AI = 1;
                    if (CountryElement.QueryDoubleAttribute("taxfactor", out double TaxFactor) != TIXML_SUCCESS) TaxFactor = 1.0;
                    if (CountryElement.QueryIntAttribute("money", out int Money) != TIXML_SUCCESS) Money = 0;
                    if (CountryElement.QueryIntAttribute("industry", out int Industry) != TIXML_SUCCESS) Industry = 0;
                    if (CountryElement.QueryIntAttribute("techlevel", out int TechLevel) != TIXML_SUCCESS) TechLevel = 1;
                    if (CountryElement.QueryIntAttribute("r", out int R) != TIXML_SUCCESS) R = 255;
                    if (CountryElement.QueryIntAttribute("g", out int G) != TIXML_SUCCESS) G = 255;
                    if (CountryElement.QueryIntAttribute("b", out int B) != TIXML_SUCCESS) B = 255;
                    if (CountryElement.QueryIntAttribute("a", out int A) != TIXML_SUCCESS) A = 255;
                    country.alliance = ParseAlliance(CountryElement.Attribute("alliance"));
                    country.defeatType = judgeDefeatTypeWhenLoadingBattle(CountryElement.Attribute("defeated"));
                    country.taxRate = (float)TaxFactor;
                    country.ai = AI != 0;
                    country.money = Money;
                    country.industry = Industry;
                    country.tech = TechLevel;
                    country.a = (byte)A; country.r = (byte)R; country.g = (byte)G; country.b = (byte)B;
                    if (gameMode == CONQUEST) country.ai = country.id != conquestPlayerCountryId;
                    country.SetCommander(CountryElement.Attribute("commander"));
                    allCountry.Add(country);
                    if (country.alliance != (int)NEUTRAL) belligerentCountry.Add(country);
                    currentCountryIndex = 0;
                }
            }
            else if (Name == "area")
            {
                for (TiXmlElement AreaElement = ListElement.FirstChildElement(); AreaElement != null; AreaElement = AreaElement.NextSiblingElement())
                {
                    if (AreaElement.QueryIntAttribute("id", out int AreaID) != TIXML_SUCCESS) AreaID = 0;
                    CArea area = g_Scene.GetArea(AreaID);
                    if (area == null || !area.info.enable) continue;
                    string CountryStr = AreaElement.Attribute("country");
                    CCountry country = CountryStr != null ? FindCountry(CountryStr) : null;
                    g_Scene.SetAreaCountry(AreaID, country);
                    country?.AddArea(AreaID);
                    var ConstructionType = (CONSTRUCTION_TYPE)ParseConstruction(AreaElement.Attribute("construction"));
                    if (AreaElement.QueryIntAttribute("level", out int ConstructionLevel) != TIXML_SUCCESS) ConstructionLevel = 0;
                    if (ConstructionLevel <= 0) { ConstructionType = CONSTRUCTION_TYPE.NO_CONSTRUCTION; ConstructionLevel = 0; }
                    if (!area.info.sea) area.SetConstruction((int)ConstructionType, ConstructionLevel);
                    area.installationType = (INSTALLATION_TYPE)ParseInstallation(AreaElement.Attribute("installation"));
                    for (TiXmlElement ArmyElement = AreaElement.FirstChildElement(); ArmyElement != null; ArmyElement = ArmyElement.NextSiblingElement())
                    {
                        CArmy army = InitArmyFromXml(ArmyElement, country);
                        if (army != null) area.AddArmy(army, true);
                    }
                }
            }
            else if (Name == "dialogue")
            {
                for (TiXmlElement DialogElement = ListElement.FirstChildElement(); DialogElement != null; DialogElement = DialogElement.NextSiblingElement())
                    dialogueList.Add(ParseDialogue(DialogElement));
            }
        }
    }

    /// <summary>
    /// Creates the live match from the scenario data (preloaded by the country selection preview,
    /// or read here).
    /// </summary>
    /// <param name="FileName">scenario name</param>
    public void LoadBattleXml(string FileName)
    {
        if (WC2_USE_LEGACY_BATTLE_XML_LOADER != 0)
        {
            LoadBattleXmlLegacy(FileName);
            return;
        }

        // the country selection preview may already have preloaded this scenario; other entries preload here
        if (GetPreloadedBattleScenario(FileName) == null &&
            !PreloadBattleScenario(FileName))
        {
            return;
        }

        BattleScenarioData data = GetPreloadedBattleScenario(FileName);
        if (data == null)
        {
            ReleasePreloadedBattleScenario();
            return;
        }

        mapId = data.mapId;
        areasEnable = "";
        if (!string.IsNullOrEmpty(data.areasEnable))
        {
            areasEnable = FitBuffer(data.areasEnable, 32);
        }
        g_Scene.Init(areasEnable, mapId);

        foreach (BattleScenarioCountryData countryData in data.countries)
        {
            var country = new CCountry();
            // a missing name falls back to the id
            country.Init(countryData.id, countryData.hasName ? countryData.name : countryData.id);
            country.alliance = countryData.alliance;
            country.defeatType = (DEFEAT_TYPE)countryData.defeated;
            country.taxRate = (float)countryData.taxFactor;
            country.ai = countryData.ai != 0;
            country.money = countryData.money;
            country.industry = countryData.industry;
            country.tech = countryData.techLevel;
            country.a = (byte)countryData.a;
            country.r = (byte)countryData.r;
            country.g = (byte)countryData.g;
            country.b = (byte)countryData.b;

            if (gameMode == CONQUEST)
            {
                country.ai = country.id != conquestPlayerCountryId;
            }
            country.SetCommander(countryData.hasCommander ? countryData.commander : null);
            allCountry.Add(country);
            if (country.alliance != (int)NEUTRAL)
            {
                belligerentCountry.Add(country);
            }
        }
        currentCountryIndex = 0;

        foreach (BattleScenarioAreaData areaData in data.areas)
        {
            CArea area = g_Scene.GetArea(areaData.id);
            if (area == null || !area.info.enable)
            {
                continue;
            }

            CCountry country = string.IsNullOrEmpty(areaData.country)
                ? null
                : FindCountry(areaData.country);
            g_Scene.SetAreaCountry(areaData.id, country);
            if (country != null)
            {
                country.AddArea(areaData.id);
            }

            CONSTRUCTION_TYPE constructionType = (CONSTRUCTION_TYPE)areaData.construction;
            int constructionLevel = areaData.constructionLevel;
            if (constructionLevel <= 0)
            {
                constructionType = CONSTRUCTION_TYPE.NO_CONSTRUCTION;
                constructionLevel = 0;
            }
            if (!area.info.sea)
            {
                area.SetConstruction((int)constructionType, constructionLevel);
            }
            area.installationType = (INSTALLATION_TYPE)areaData.installation;

            foreach (BattleScenarioArmyData armyData in areaData.armies)
            {
                CArmy army = InitArmy(armyData, country);
                if (army != null)
                {
                    area.AddArmy(army, true);
                }
            }
        }

        foreach (DialogueDef d in data.dialogues)
        {
            dialogueList.Add(new DialogueDef { commander = d.commander, index = d.index, atRound = d.atRound, left = d.left });
        }

        ReleasePreloadedBattleScenario();
    }

    public static DEFEAT_TYPE judgeDefeatTypeWhenLoadingBattle(string defeat_type_str)
    {
        return (DEFEAT_TYPE)ParseDefeat(defeat_type_str);
    }
}
