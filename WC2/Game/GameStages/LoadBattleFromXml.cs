namespace WC2;

/// <summary>
/// Battle XML pre-parsing and the file-local state of LoadBattleFromXml.cpp (BattleScenarioData.h).
/// GetBattleBelligerentList lives in another file of this partial class.
/// </summary>
public static partial class BattleScenario
{
    // ---- file-local statics of LoadBattleFromXml.cpp -------------------------------------
    internal static readonly BattleScenarioData g_preloadedBattleScenario = new();
    internal static bool g_hasPreloadedBattleScenario = false;
    internal static TiXmlDocument g_preloadedBattleDocument = null;
    internal static string g_preloadedBattleFile = "";

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
    /// <param name="fileName">file name</param>
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
        g_preloadedBattleDocument = new TiXmlDocument(GetPath(fileName, null));
        g_preloadedBattleFile = fileName;
        if (!LoadBattleScenarioData(fileName, g_preloadedBattleScenario))
        {
            g_preloadedBattleScenario.Clear();
            g_preloadedBattleDocument = null;
            g_preloadedBattleFile = "";
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
        g_preloadedBattleDocument = null;
        g_preloadedBattleFile = "";
    }

    /// <summary>
    /// The real pre-parse of a battle XML.
    /// </summary>
    /// <param name="fileName">file name</param>
    /// <param name="outData">receives the data</param>
    /// <returns>whether the load succeeded</returns>
    public static bool LoadBattleScenarioData(string fileName, BattleScenarioData outData)
    {
        if (fileName == null || outData == null) return false;

        outData.Clear();
        outData.fileName = fileName;

        var localDocument = new TiXmlDocument(GetPath(fileName, null));
        TiXmlDocument document = localDocument;
        if (g_preloadedBattleDocument != null &&
            g_preloadedBattleFile == fileName)
        {
            document = g_preloadedBattleDocument;
        }
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
            // country list: the key preload data
            if (listName == "country")
            {
                for (TiXmlElement node = list.FirstChildElement(); node != null;
                     node = node.NextSiblingElement())
                {
                    var country = new BattleScenarioCountryData();
                    string value = node.Attribute("id");
                    if (value == null) continue;
                    country.node = node;
                    country.id = value;
                    value = node.Attribute("name");
                    if (value != null) country.name = value;
                    value = node.Attribute("commander");
                    if (value != null) country.commander = value;
                    if (node.QueryIntAttribute("ai", out int aiValue) == TIXML_SUCCESS) country.ai = aiValue;
                    country.alliance = ParseAlliance(node.Attribute("alliance"));
                    country.defeated = ParseDefeat(node.Attribute("defeated"));
                    if (node.QueryIntAttribute("r", out int rValue) == TIXML_SUCCESS) country.r = rValue;
                    if (node.QueryIntAttribute("g", out int gValue) == TIXML_SUCCESS) country.g = gValue;
                    if (node.QueryIntAttribute("b", out int bValue) == TIXML_SUCCESS) country.b = bValue;
                    if (node.QueryIntAttribute("a", out int aValue) == TIXML_SUCCESS) country.a = aValue;
                    outData.countries.Add(country);
                }
            }
            else if (listName == "area")
            {
                // area list, not expanded further
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
                    area.node = node;
                    string value = node.Attribute("country");
                    if (value != null) area.country = value;
                    area.construction = ParseConstruction(node.Attribute("construction"));
                    if (node.QueryIntAttribute("level", out int levelValue) == TIXML_SUCCESS) area.constructionLevel = levelValue;
                    area.installation = ParseInstallation(node.Attribute("installation"));
                    for (TiXmlElement army = node.FirstChildElement("army"); army != null;
                         army = army.NextSiblingElement("army"))
                    {
                        value = army.Attribute("type");
                        if (value != null) area.armyTypes.Add(value);
                    }
                    outData.areas.Add(area);
                }
            }
        }
        return true;
    }
}

/// <summary>Battle XML loading into the live match (CGameManager part of LoadBattleFromXml.cpp).</summary>
public sealed partial class CGameManager
{
    /// <summary>
    /// Creates a CArmy from an XML element; only used by LoadBattle, extracted for readability.
    /// </summary>
    /// <param name="armyElement">xml element</param>
    /// <param name="curCountry">owner of the area, must not be null</param>
    public static CArmy InitArmyFromXml(TiXmlElement armyElement, CCountry curCountry)
    {
        if (curCountry == null)
        {
            return null;
        }
        int ArmyLevel, ArmyCards, Designation;
        string ArmyTypeStr;
        ArmyTypeStr = armyElement.Attribute("type");
        // scenarios and armydef.xml share one name/id table; unknown custom units no longer become infantry silently
        int ArmyType = armydef_internal.ResolveArmyTypeId(ArmyTypeStr);

        if (armyElement.QueryIntAttribute("level", out ArmyLevel) != TIXML_SUCCESS)
            ArmyLevel = 1;
        if (armyElement.QueryIntAttribute("cards", out ArmyCards) != TIXML_SUCCESS)
            ArmyCards = 0;

        // unused for now
        if (armyElement.QueryIntAttribute("designation", out Designation) != TIXML_SUCCESS)
        {
            Designation = 0;
        }
        ArmyDef ArmyDef = CObjectDef.Instance().GetArmyDef(ArmyType, curCountry.name);
        if (ArmyDef == null)
        {
            // the scenario references an unloaded custom unit: skip it instead of passing a null definition
            return null;
        }
        var army = new CArmy();
        // init the unit and set its attributes
        army.Init(ArmyDef, curCountry);
        army.cards = ArmyCards;
        army.level = ArmyLevel;
        army.movement = 0;
        if (army.HasCard(ARMY_CARD.COMMANDER))
            curCountry.commanderAlive = true;
        army.ResetMaxStrength(false);
        return army;
    }

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
    /// Loads a "level file": battle_axis.xml, conquest_1.xml, ...
    /// </summary>
    /// <param name="FileName">file name</param>
    public void LoadBattle(string FileName)
    {
        ClearBattle();
        CCSoundBox.GetInstance().LoadSE("boom.wav");
        SetRandSeed(unchecked((int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        LoadBattleXml(FileName);
    }

    /// <summary>
    /// Parses the level / conquest XML into this CGameManager and the global scene. Keeps the
    /// original global writes; an independent preload object can be built on top later.
    /// </summary>
    public void LoadBattleXmlLegacy(string FileName)
    {
        // the xml
        var localDocument = new TiXmlDocument(GetPath(FileName, null));
        TiXmlDocument Xml = localDocument;
        // is a preloaded result available?
        bool usingPreloadedDocument =
            g_preloadedBattleDocument != null &&
            g_preloadedBattleFile == (FileName != null ? FileName : "");
        if (usingPreloadedDocument)
        {
            Xml = g_preloadedBattleDocument;
        }
        // xml loaded
        if (usingPreloadedDocument || Xml.LoadFile(TIXML_ENCODING_UNKNOWN))
        {
            // battle node
            TiXmlElement BattleElement = Xml.FirstChild("battle") as TiXmlElement;
            // battle node exists
            if (BattleElement != null)
            {
                // map attribute missing: map 1
                if (BattleElement.QueryIntAttribute("map", out mapId) != TIXML_SUCCESS)
                {
                    mapId = 1;
                }
                areasEnable = "";
                string Enable = BattleElement.Attribute("areasenable");
                // areasenable decides the area switches
                if (Enable != null)
                {
                    areasEnable = Enable;
                }
                // init g_Scene
                g_Scene.Init(areasEnable, mapId);
                // all list nodes
                for (TiXmlElement ListElement = BattleElement.FirstChildElement();
                     ListElement != null; ListElement = ListElement.NextSiblingElement())
                {
                    string Name = ListElement.Attribute("name");
                    // country list
                    if (Name == "country")
                    {
                        for (TiXmlElement CountryElement = ListElement.FirstChildElement();
                             CountryElement != null; CountryElement = CountryElement.NextSiblingElement())
                        {
                            // one CCountry per node, read its attributes
                            var country = new CCountry();
                            string AllianceStr, DefeatedStr;
                            int AI, Money, Industry, TechLevel, Alliance, R, G, B, A;
                            DEFEAT_TYPE Defeated;
                            double TaxFactor;
                            // id is the country identifier, name the faction identifier
                            country.Init(CountryElement.Attribute("id"),
                                CountryElement.Attribute("name"));
                            if (CountryElement.QueryIntAttribute("ai", out AI) != TIXML_SUCCESS)
                                AI = 1;
                            DefeatedStr = CountryElement.Attribute("defeated");
                            Defeated = CGameManager.judgeDefeatTypeWhenLoadingBattle(DefeatedStr);
                            if (CountryElement.QueryDoubleAttribute("taxfactor", out TaxFactor) != TIXML_SUCCESS)
                                TaxFactor = 1.0;
                            if (CountryElement.QueryIntAttribute("money", out Money) != TIXML_SUCCESS)
                                Money = 0;
                            if (CountryElement.QueryIntAttribute("industry", out Industry) != TIXML_SUCCESS)
                                Industry = 0;
                            if (CountryElement.QueryIntAttribute("techlevel", out TechLevel) != TIXML_SUCCESS)
                                TechLevel = 1;
                            AllianceStr = CountryElement.Attribute("alliance");
                            // 26 alliances are supported here
                            Alliance = ParseAlliance(AllianceStr);
                            if (CountryElement.QueryIntAttribute("r", out R) != TIXML_SUCCESS)
                                R = 255;
                            if (CountryElement.QueryIntAttribute("g", out G) != TIXML_SUCCESS)
                            {
                                G = 255;
                            }
                            if (CountryElement.QueryIntAttribute("b", out B) != TIXML_SUCCESS)
                                B = 255;
                            if (CountryElement.QueryIntAttribute("a", out A) != TIXML_SUCCESS)
                                A = 255;

                            country.alliance = Alliance;
                            country.defeatType = Defeated;
                            country.taxRate = (float)TaxFactor;
                            country.ai = AI != 0;
                            country.money = Money;
                            country.industry = Industry;
                            country.tech = TechLevel;
                            country.a = (byte)A;
                            country.r = (byte)R;
                            country.g = (byte)G;
                            country.b = (byte)B;
                            // conquest: every faction is AI except the player's
                            if (gameMode == CONQUEST)
                            {
                                country.ai = country.id != conquestPlayerCountryId;
                            }
                            country.SetCommander(CountryElement.Attribute("commander"));
                            allCountry.Add(country);
                            // non neutral countries join the belligerent list
                            if (country.alliance != (int)NEUTRAL)
                                belligerentCountry.Add(country);
                            currentCountryIndex = 0;
                        }
                        // area list
                    }
                    else if (Name == "area")
                    {
                        for (TiXmlElement AreaElement = ListElement.FirstChildElement();
                             AreaElement != null; AreaElement = AreaElement.NextSiblingElement())
                        {
                            CONSTRUCTION_TYPE ConstructionType;
                            INSTALLATION_TYPE InstallationType;
                            int AreaID, ConstructionLevel;
                            string ConstructionStr, InstallationStr, CountryStr;
                            // attributes
                            if (AreaElement.QueryIntAttribute("id", out AreaID) != TIXML_SUCCESS)
                                AreaID = 0;
                            CArea area = g_Scene.GetArea(AreaID);
                            if (area != null && area.info.enable)
                            {
                                CountryStr = AreaElement.Attribute("country");
                                CCountry country = null;
                                if (CountryStr != null)
                                    country = FindCountry(CountryStr);
                                g_Scene.SetAreaCountry(AreaID, country);
                                if (country != null)
                                    country.AddArea(AreaID);
                                ConstructionStr = AreaElement.Attribute("construction");
                                // construction type parsing
                                ConstructionType = (CONSTRUCTION_TYPE)ParseConstruction(ConstructionStr);
                                if (AreaElement.QueryIntAttribute("level", out ConstructionLevel) != TIXML_SUCCESS)
                                    ConstructionLevel = 0;
                                if (ConstructionLevel <= 0)
                                {
                                    ConstructionType = CONSTRUCTION_TYPE.NO_CONSTRUCTION;
                                    ConstructionLevel = 0;
                                }
                                // only land areas get constructions
                                if (!area.info.sea)
                                    area.SetConstruction((int)ConstructionType, ConstructionLevel);
                                // installation type parsing
                                InstallationStr = AreaElement.Attribute("installation");
                                InstallationType = (INSTALLATION_TYPE)ParseInstallation(InstallationStr);
                                area.installationType = InstallationType;
                                // units of the area
                                for (TiXmlElement ArmyElement = AreaElement.FirstChildElement();
                                     ArmyElement != null; ArmyElement = ArmyElement.NextSiblingElement())
                                {
                                    CArmy army = InitArmyFromXml(ArmyElement, country);
                                    if (army != null)
                                    {
                                        area.AddArmy(army, true);
                                    }
                                }
                            }
                        }
                    }
                    else if (Name == "dialogue")
                    {
                        // dialogue list
                        for (TiXmlElement DialogElement = ListElement.FirstChildElement();
                             DialogElement != null; DialogElement = DialogElement.NextSiblingElement())
                        {
                            dialogueList.Add(ParseDialogue(DialogElement));
                        }
                    }
                }
            }
        }
        if (usingPreloadedDocument)
        {
            // the live match took over the document: release the shared preload cache
            ReleasePreloadedBattleScenario();
        }
    }

    /// <summary>
    /// Creates the live match from the preloaded scenario data; dialogues are still parsed from the
    /// XML when the match is created. (The legacy loader is kept behind WC2_USE_LEGACY_BATTLE_XML_LOADER.)
    /// </summary>
    /// <param name="FileName">scenario XML file name</param>
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

        // the countries were parsed during the preload; runtime only fields come from the node
        foreach (BattleScenarioCountryData countryData in data.countries)
        {
            TiXmlElement node = countryData.node;
            string name = node != null ? node.Attribute("name") : null;
            var country = new CCountry();
            country.Init(countryData.id,
                name != null
                    ? name
                    : (string.IsNullOrEmpty(countryData.name) ? countryData.id : countryData.name));

            int money = 0;
            int industry = 0;
            int techLevel = 1;
            double taxFactor = 1.0;
            if (node != null)
            {
                if (node.QueryIntAttribute("money", out int moneyValue) == TIXML_SUCCESS) money = moneyValue;
                if (node.QueryIntAttribute("industry", out int industryValue) == TIXML_SUCCESS) industry = industryValue;
                if (node.QueryIntAttribute("techlevel", out int techValue) == TIXML_SUCCESS) techLevel = techValue;
                if (node.QueryDoubleAttribute("taxfactor", out double taxValue) == TIXML_SUCCESS) taxFactor = taxValue;
            }

            country.alliance = countryData.alliance;
            country.defeatType = (DEFEAT_TYPE)countryData.defeated;
            country.taxRate = (float)taxFactor;
            country.ai = countryData.ai != 0;
            country.money = money;
            country.industry = industry;
            country.tech = techLevel;
            country.a = (byte)countryData.a;
            country.r = (byte)countryData.r;
            country.g = (byte)countryData.g;
            country.b = (byte)countryData.b;

            if (gameMode == CONQUEST)
            {
                country.ai = country.id != conquestPlayerCountryId;
            }
            country.SetCommander(string.IsNullOrEmpty(countryData.commander)
                ? (node != null ? node.Attribute("commander") : null)
                : countryData.commander);
            allCountry.Add(country);
            if (country.alliance != (int)NEUTRAL)
            {
                belligerentCountry.Add(country);
            }
        }
        currentCountryIndex = 0;

        // area ownership, constructions and units come from the preload data; unit details from the node
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

            TiXmlElement areaNode = areaData.node;
            if (areaNode == null)
            {
                continue;
            }
            for (TiXmlElement armyNode = areaNode.FirstChildElement(); armyNode != null;
                 armyNode = armyNode.NextSiblingElement())
            {
                CArmy army = InitArmyFromXml(armyNode, country);
                if (army != null)
                {
                    area.AddArmy(army, true);
                }
            }
        }

        // dialogues are not part of BattleScenarioData; they are read when the live match is created
        TiXmlDocument document = g_preloadedBattleDocument;
        if (document != null)
        {
            TiXmlElement battle = document.FirstChildElement("battle");
            for (TiXmlElement list = battle != null ? battle.FirstChildElement("list") : null;
                 list != null; list = list.NextSiblingElement("list"))
            {
                string listName = list.Attribute("name");
                if (listName == null || listName != "dialogue")
                {
                    continue;
                }
                for (TiXmlElement dialog = list.FirstChildElement(); dialog != null;
                     dialog = dialog.NextSiblingElement())
                {
                    dialogueList.Add(ParseDialogue(dialog));
                }
            }
        }

        ReleasePreloadedBattleScenario();
    }

    public static DEFEAT_TYPE judgeDefeatTypeWhenLoadingBattle(string defeat_type_str)
    {
        return (DEFEAT_TYPE)ParseDefeat(defeat_type_str);
    }
}
