namespace WC2;

/// <summary>
/// Static definitions loaded from XML: commanders, general photos, battles, conquests (CObjectDef.cpp).
/// Cards live in CardDefs.cs, army definitions in ArmyDefLoader.cs / ArmyDefStore.cs.
/// </summary>
public sealed partial class CObjectDef
{
    private static CObjectDef m_Instance;

    public static CObjectDef Instance()
    {
        if (m_Instance == null)
        {
            m_Instance = new CObjectDef();
        }
        return m_Instance;
    }

    public Dictionary<string, ArmyDefList> mapArmyDefList = new();
    public DeprecatedCardDef[] arrayCardDef = NewCardArray();
    public Dictionary<string, UnitMotions> mapUnitMotions = new();
    public Dictionary<string, UnitPositions> mapUnitPos = new();
    public Dictionary<string, CommanderDef> mapCommanderDef = new();
    public Dictionary<string, GeneralPhoto> mapGeneralPhotos = new();
    public Dictionary<string, BattleDef> mapBattleDef = new();
    public Dictionary<string, ConquestDef> mapConquestList = new();
    public Dictionary<string, ToNewCardDef> mapNewCardDef = new();
    /// <summary>Weapon/armor damage multipliers keyed by MakeWeaponArmorRuleKey, value = rate * 1000.</summary>
    public Dictionary<ushort, ushort> weaponArmorDamageRates = new();

    private static DeprecatedCardDef[] NewCardArray()
    {
        var a = new DeprecatedCardDef[28];
        for (int i = 0; i < a.Length; i++) a[i] = new DeprecatedCardDef();
        return a;
    }

    /// <summary>
    /// Constructs the seven original maps and the 28 legacy card definitions
    /// (the integer fields of DeprecatedCardDef stay at their default, like the original).
    /// </summary>
    public CObjectDef() { }

    /// <summary>
    /// Loads the battle definitions of the campaign selection.
    /// flag and arrow of battlelist.xml share the same element layout and go to two lists.
    /// </summary>
    public void LoadBattleList()
    {
        var document = new TiXmlDocument(GetPath("battlelist.xml", null));
        if (!document.LoadFile(TIXML_ENCODING_UNKNOWN)) return;

        TiXmlNode battleListNode = document.FirstChild("battlelist");
        if (battleListNode == null) return;

        for (TiXmlElement battleElement = battleListNode.FirstChildElement();
             battleElement != null;
             battleElement = battleElement.NextSiblingElement())
        {
            string name = battleElement.Attribute("name");
            if (name == null) continue;

            var battle = new BattleDef();
            battle.name = name;

            string age = battleElement.Attribute("age");
            battle.age = age ?? "";
            battle.centerX = 0.0f;
            battle.centerY = 0.0f;
            battle.ageX = 0.0f;
            battle.ageY = 0.0f;
            battle.victory = 15;
            battle.greatVictory = 10;

            // the original TinyXML build had no QueryFloatAttribute, so doubles are read
            double value;
            if (battleElement.QueryDoubleAttribute("centerx", out value) == TIXML_SUCCESS) battle.centerX = (float)value;
            if (battleElement.QueryDoubleAttribute("centery", out value) == TIXML_SUCCESS) battle.centerY = (float)value;
            if (battleElement.QueryDoubleAttribute("agex", out value) == TIXML_SUCCESS) battle.ageX = (float)value;
            if (battleElement.QueryDoubleAttribute("agey", out value) == TIXML_SUCCESS) battle.ageY = (float)value;

            if (battleElement.QueryIntAttribute("victory", out int victory) == TIXML_SUCCESS) battle.victory = victory;
            if (battleElement.QueryIntAttribute("greatvictory", out int greatVictory) == TIXML_SUCCESS) battle.greatVictory = greatVictory;

            for (TiXmlElement groupElement = battleElement.FirstChildElement();
                 groupElement != null;
                 groupElement = groupElement.NextSiblingElement())
            {
                string type = groupElement.Attribute("type");
                if (type == null) continue;

                List<FlagInfo> destination;
                if (type == "flag") destination = battle.flag;
                else if (type == "arrow") destination = battle.arrow;
                else continue;

                for (TiXmlElement infoElement = groupElement.FirstChildElement();
                     infoElement != null;
                     infoElement = infoElement.NextSiblingElement())
                {
                    var info = new FlagInfo();

                    string infoName = infoElement.Attribute("name");
                    info.name = infoName ?? "";
                    info.x = 0.0f;
                    info.y = 0.0f;
                    info.rot = 0.0f;
                    info.scale = 1.0f;

                    if (infoElement.QueryDoubleAttribute("x", out value) == TIXML_SUCCESS) info.x = (float)value;
                    if (infoElement.QueryDoubleAttribute("y", out value) == TIXML_SUCCESS) info.y = (float)value;
                    if (infoElement.QueryDoubleAttribute("rot", out value) == TIXML_SUCCESS) info.rot = (float)(value * 3.14159265 / 180.0);
                    if (infoElement.QueryDoubleAttribute("scale", out value) == TIXML_SUCCESS) info.scale = (float)value;

                    destination.Add(info);
                }
            }

            mapBattleDef[name] = battle;
        }
    }

    /// <summary>Releases the battle list with its flags and arrows.</summary>
    public void ReleaseBattleList()
    {
        foreach (var entry in mapBattleDef)
        {
            BattleDef battle = entry.Value;
            if (battle == null) continue;
            battle.arrow.Clear();
            battle.flag.Clear();
        }
        mapBattleDef.Clear();
    }

    /// <summary>Battle definition by key.</summary>
    public BattleDef GetBattleDef(string key)
    {
        if (key == null) return null;
        return mapBattleDef.TryGetValue(key, out BattleDef battle) ? battle : null;
    }

    /// <summary>Loads the conquest definitions and their map flag positions.</summary>
    public void LoadConquestList()
    {
        var document = new TiXmlDocument(GetPath("conquestlist.xml", null));
        if (!document.LoadFile(TIXML_ENCODING_UNKNOWN)) return;

        // the original conquestlist.xml reuses battlelist as root name
        TiXmlNode battleListNode = document.FirstChild("battlelist");
        if (battleListNode == null) return;

        for (TiXmlElement conquestElement = battleListNode.FirstChildElement();
             conquestElement != null;
             conquestElement = conquestElement.NextSiblingElement())
        {
            string name = conquestElement.Attribute("name");
            if (name == null) continue;

            var conquest = new ConquestDef();
            conquest.name = name;
            conquest.centerX = 0.0f;
            conquest.centerY = 0.0f;

            // the original TinyXML build had no QueryFloatAttribute, so doubles are read
            double value;
            if (conquestElement.QueryDoubleAttribute("centerx", out value) == TIXML_SUCCESS) conquest.centerX = (float)value;
            if (conquestElement.QueryDoubleAttribute("centery", out value) == TIXML_SUCCESS) conquest.centerY = (float)value;

            for (TiXmlElement groupElement = conquestElement.FirstChildElement();
                 groupElement != null;
                 groupElement = groupElement.NextSiblingElement())
            {
                string type = groupElement.Attribute("type");
                if (type == null || type != "flag") continue;

                for (TiXmlElement flagElement = groupElement.FirstChildElement();
                     flagElement != null;
                     flagElement = flagElement.NextSiblingElement())
                {
                    var flag = new FlagInfo();

                    string flagName = flagElement.Attribute("name");
                    flag.name = flagName ?? "";
                    flag.x = 0.0f;
                    flag.y = 0.0f;
                    flag.rot = 0.0f;
                    flag.scale = 1.0f;

                    if (flagElement.QueryDoubleAttribute("x", out value) == TIXML_SUCCESS) flag.x = (float)value;
                    if (flagElement.QueryDoubleAttribute("y", out value) == TIXML_SUCCESS) flag.y = (float)value;
                    if (flagElement.QueryDoubleAttribute("rot", out value) == TIXML_SUCCESS) flag.rot = (float)(value * 3.14159265 / 180.0);
                    if (flagElement.QueryDoubleAttribute("scale", out value) == TIXML_SUCCESS) flag.scale = (float)value;

                    conquest.flag.Add(flag);
                }
            }

            mapConquestList[name] = conquest;
        }
    }

    /// <summary>Releases the conquest list with its flags.</summary>
    public void ReleaseConquestList()
    {
        foreach (var entry in mapConquestList)
        {
            ConquestDef conquest = entry.Value;
            if (conquest == null) continue;
            conquest.flag.Clear();
        }
        mapConquestList.Clear();
    }

    /// <summary>Conquest definition by name.</summary>
    public ConquestDef GetConquestDef(string name)
    {
        if (name == null) return null;
        return mapConquestList.TryGetValue(name, out ConquestDef conquest) ? conquest : null;
    }

    private static readonly string[] medalAttributes =
    {
        "infantry",
        "airforce",
        "artillery",
        "armour",
        "navy",
        "honour"
    };

    /// <summary>Loads the country, rank and six medals of every commander.</summary>
    public void LoadCommanderDef()
    {
        var document = new TiXmlDocument(GetPath("commanderdef.xml", null));
        if (!document.LoadFile(TIXML_ENCODING_UNKNOWN)) return;

        TiXmlNode commandersNode = document.FirstChild("commanders");
        if (commandersNode == null) return;

        for (TiXmlElement element = commandersNode.FirstChildElement();
             element != null;
             element = element.NextSiblingElement())
        {
            string name = element.Attribute("name");
            if (name == null) continue;

            var commander = new CommanderDef();
            commander.name = name;

            string country = element.Attribute("country");
            commander.country = country ?? "";
            commander.rank = 0;
            if (element.QueryIntAttribute("rank", out int rank) == TIXML_SUCCESS) commander.rank = rank;

            for (int index = 0; index < 6; ++index)
            {
                commander.medal[index] = 0;
                if (element.QueryIntAttribute(medalAttributes[index], out int medal) == TIXML_SUCCESS) commander.medal[index] = medal;
            }

            mapCommanderDef[name] = commander;
        }
    }

    /// <summary>Releases every commander definition.</summary>
    public void ReleaseCommanderDef()
    {
        mapCommanderDef.Clear();
    }

    /// <summary>Commander definition by name.</summary>
    public CommanderDef GetCommanderDef(string name)
    {
        if (name == null) return null;
        return mapCommanderDef.TryGetValue(name, out CommanderDef commander) ? commander : null;
    }

    /// <summary>Loads the general photos used by the dialogue and commander screens.</summary>
    public void LoadGeneralPhotos()
    {
        var document = new TiXmlDocument(GetPath("generalphotos.xml", null));
        if (!document.LoadFile(TIXML_ENCODING_UNKNOWN)) return;

        TiXmlNode generalsNode = document.FirstChild("generals");
        if (generalsNode == null) return;

        for (TiXmlElement element = generalsNode.FirstChildElement();
             element != null;
             element = element.NextSiblingElement())
        {
            string generalName = element.Attribute("name");
            // the original would construct a std::string from a null name; skip such entries
            if (generalName == null) continue;
            var photo = new GeneralPhoto();
            photo.fileName = element.Attribute("filename") ?? "";

            // the original TinyXML build had no QueryFloatAttribute, so doubles are read
            double value;
            if (element.QueryDoubleAttribute("w", out value) == TIXML_SUCCESS) photo.w = (float)value;
            if (element.QueryDoubleAttribute("h", out value) == TIXML_SUCCESS) photo.h = (float)value;
            if (element.QueryDoubleAttribute("refx", out value) == TIXML_SUCCESS) photo.refx = (float)value;

            mapGeneralPhotos[generalName] = photo;
        }
    }

    /// <summary>Photo of a general, null when unknown.</summary>
    public GeneralPhoto GetGeneralPhoto(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return mapGeneralPhotos.TryGetValue(name, out GeneralPhoto photo) ? photo : null;
    }

    /// <summary>Releases every general photo.</summary>
    public void ReleaseGeneralPhotos()
    {
        mapGeneralPhotos.Clear();
    }

    /// <summary>Loads every object definition in the original order.</summary>
    public void Init()
    {
        // JSON game data (data/*.json); the original XML is the fallback for a flat legacy asset folder
        Terrain.LoadJson();   // built-in values when data/terrain.json is missing
        Medals.LoadJson();    // built-in values when data/medals.json is missing
        GameRules.LoadJson(); // built-in values when data/rules.json is missing
        if (!LoadUnitsJson()) LoadArmyDef();
        if (!LoadCardsJson()) LoadCardDef();

        // unit motions / positions belong to the battle animation (out of scope)
        LoadUnitMotions();
        LoadUnitPositons();

        if (!LoadCommandersJson()) LoadCommanderDef();
        if (!LoadPortraitsJson()) LoadGeneralPhotos();
        if (!LoadCampaignsJson()) LoadBattleList();
        if (!LoadConquestsJson()) LoadConquestList();
    }

    public void Destroy()
    {
        if (m_Instance != null)
        {
            m_Instance.Release();
            m_Instance = null;
        }
    }

    public void Release()
    {
        ReleaseArmyDef();
        weaponArmorDamageRates.Clear();
        // battle animation data (out of scope)
        ReleaseUnitMotions();
        ReleaseUnitPositions();
        ReleaseBattleList();
        ReleaseCommanderDef();
        ReleaseGeneralPhotos();
        ReleaseConquestList();
        ReleaseCardDef();
    }

    // ---- battle animation data (GUIBattle is out of scope) -------------------------------

    public void ReleaseUnitMotions() { mapUnitMotions.Clear(); }
    public void ReleaseUnitPositions() { mapUnitPos.Clear(); }
    public UnitMotions GetUnitMotions(string unitName, string motionName) => null;
    public UnitPositions GetUnitPositions(string unitName) => null;
    public void LoadUnitMotions() { }
    public void LoadUnitPositons() { }
}
