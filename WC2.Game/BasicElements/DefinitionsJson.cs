using System.Text.Json.Nodes;

namespace WC2;

/// <summary>
/// JSON versions of the smaller definition tables (see docs/DATA.md):
/// <list type="bullet">
/// <item><c>data/cards.json</c> (carddef.xml): <c>{"countries": {"others": {"Tank": {"id": 4, "type": "army", "price": 220, ...}}}}</c></item>
/// <item><c>data/commanders.json</c> (commanderdef.xml): <c>{"commanders": {"Rommel": {"country": "de", "rank": 9, "armour": 3, ...}}}</c></item>
/// <item><c>data/portraits.json</c> (generalphotos.xml): <c>{"portraits": {"Rommel": {"file": "general_rommel.webp", "w": 113, "h": 137, "refx": 20}}}</c></item>
/// <item><c>data/campaigns.json</c> (battlelist.xml): <c>{"battles": {"poland": {"age": "...", "centerX": ..., "flags": [...], "arrows": [...]}}}</c></item>
/// <item><c>data/conquests.json</c> (conquestlist.xml): <c>{"conquests": {"conquest1": {"centerX": ..., "flags": [...]}}}</c></item>
/// </list>
/// Every table is keyed by name so a mod can add or change single entries. Defaults equal the XML
/// loaders' defaults; <c>--verify-data</c> checks both produce the same definitions.
/// </summary>
public sealed partial class CObjectDef
{
    // ---- cards -----------------------------------------------------------------------------

    public bool LoadCardsJson()
    {
        var root = JsonData.LoadMerged(DataPaths.Cards);
        if (root == null) return false;
        ReleaseCardDef();
        foreach (var (country, cards) in root.Obj("countries").Entries())
        {
            InitCountryCardDefMap(country);
            foreach (var (name, c) in cards.Entries())
            {
                var def = new CardDef { name = name };
                def.id = (CARD_ID)c.Int("id", 0);
                def.type = ParseCardType(c.Str("type"));
                def.price = c.Int("price");
                def.industry = c.Int("industry");
                def.round = c.Int("round");
                def.tech = c.Int("tech");
                def.relatedArmyId = c.TryInt("relatedArmyId", out int related) ? related : (int)def.id;
                def.maxTimes = c.Int("maxTimes");
                def.image = c.Str("image") ?? "card_infantry.png";
                def.intro = c.Str("intro") ?? "Infantry Intro";
                AddNewCardDefToMap(def, country);
            }
        }
        RebuildTroopCardLists();
        return true;
    }

    private static CARD_TYPE ParseCardType(string type) => type switch
    {
        "navy" => NAVY_TYPE_CARD,
        "airforce" => AIR_FORCE_TYPE_CARD,
        "development" => DEVELOPMENT_TYPE_CARD,
        "strategy" => STRATEGY_TYPE_CARD,
        _ => ARMY_TYPE_CARD,
    };

    public static JsonObject ConvertCardDefXml()
    {
        var doc = new TiXmlDocument(GetPath("carddef.xml"));
        if (!doc.LoadFile()) return null;
        var cardsElement = doc.FirstChildElement("cards");
        if (cardsElement == null) return null;
        var countries = new JsonObject();
        for (var list = cardsElement.FirstChildElement(); list != null; list = list.NextSiblingElement())
        {
            if (list.Attribute("call") is not string country) continue;
            var cards = countries[country] as JsonObject ?? new JsonObject();
            for (var card = list.FirstChildElement(); card != null; card = card.NextSiblingElement())
            {
                if (card.Attribute("name") is not string name) continue;
                var o = new JsonObject();
                CopyAttr(card, o, "id", "id");
                CopyText(card, o, "type", "type");
                CopyAttr(card, o, "price", "price");
                CopyAttr(card, o, "industry", "industry");
                CopyAttr(card, o, "round", "round");
                CopyAttr(card, o, "tech", "tech");
                CopyAttr(card, o, "relatedArmyId", "relatedArmyId");
                CopyAttr(card, o, "maxTimes", "maxTimes");
                // the XML loader takes the intro only when an image is given (a quirk of the original);
                // the JSON holds the values that loader ends up with
                string image = card.Attribute("image");
                o["image"] = image ?? "card_infantry.png";
                o["intro"] = image != null ? card.Attribute("intro") ?? "Infantry Intro" : "Infantry Intro";
                cards[name] = o;
            }
            countries[country] = cards;
        }
        return new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Cards per country (converted from carddef.xml); \"others\" applies to every country without an own entry. Field reference: docs/DATA.md#cards.",
            ["countries"] = countries,
        };
    }

    // ---- commanders ------------------------------------------------------------------------

    public bool LoadCommandersJson()
    {
        var root = JsonData.LoadMerged(DataPaths.Commanders);
        if (root == null) return false;
        ReleaseCommanderDef();
        foreach (var (name, c) in root.Obj("commanders").Entries())
        {
            var commander = new CommanderDef { name = name, country = c.Str("country") ?? "", rank = c.Int("rank") };
            for (int index = 0; index < 6; ++index) commander.medal[index] = c.Int(medalAttributes[index]);
            mapCommanderDef[name] = commander;
        }
        LevelTablesJson.ReadCommanderLevels(root.Arr("levels"));
        return true;
    }

    public static JsonObject ConvertCommanderDefXml()
    {
        var doc = new TiXmlDocument(GetPath("commanderdef.xml"));
        if (!doc.LoadFile()) return null;
        var node = doc.FirstChild("commanders");
        if (node == null) return null;
        var commanders = new JsonObject();
        for (var e = node.FirstChildElement(); e != null; e = e.NextSiblingElement())
        {
            if (e.Attribute("name") is not string name) continue;
            var o = new JsonObject();
            CopyText(e, o, "country", "country");
            CopyAttr(e, o, "rank", "rank");
            foreach (var medal in medalAttributes) CopyAttr(e, o, medal, medal);
            commanders[name] = o;
        }
        return new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Commanders that scenarios can assign (converted from commanderdef.xml): country, rank and the six medal levels. Field reference: docs/DATA.md#commanders.",
            ["commanders"] = commanders,
            // commander levels were constants in the code; they start as the built-in table
            ["levels"] = LevelTablesJson.CommanderLevelsJson(),
        };
    }

    // ---- general portraits -----------------------------------------------------------------

    public bool LoadPortraitsJson()
    {
        var root = JsonData.LoadMerged(DataPaths.Portraits);
        if (root == null) return false;
        ReleaseGeneralPhotos();
        foreach (var (name, p) in root.Obj("portraits").Entries())
        {
            var photo = new GeneralPhoto { fileName = p.Str("file") ?? "" };
            if (p.TryDouble("w", out double w)) photo.w = (float)w;
            if (p.TryDouble("h", out double h)) photo.h = (float)h;
            if (p.TryDouble("refx", out double refx)) photo.refx = (float)refx;
            mapGeneralPhotos[name] = photo;
        }
        return true;
    }

    public static JsonObject ConvertGeneralPhotosXml()
    {
        var doc = new TiXmlDocument(GetPath("generalphotos.xml"));
        if (!doc.LoadFile()) return null;
        var node = doc.FirstChild("generals");
        if (node == null) return null;
        var portraits = new JsonObject();
        for (var e = node.FirstChildElement(); e != null; e = e.NextSiblingElement())
        {
            if (e.Attribute("name") is not string name) continue;
            var o = new JsonObject();
            CopyText(e, o, "filename", "file");
            CopyAttr(e, o, "w", "w");
            CopyAttr(e, o, "h", "h");
            CopyAttr(e, o, "refx", "refx");
            portraits[name] = o;
        }
        return new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Portraits shown in dialogues and on the commander screen (converted from generalphotos.xml). Field reference: docs/DATA.md#portraits.",
            ["portraits"] = portraits,
        };
    }

    // ---- campaign battles and conquests ----------------------------------------------------

    public bool LoadCampaignsJson()
    {
        var root = JsonData.LoadMerged(DataPaths.Campaigns);
        if (root == null) return false;
        ReleaseBattleList();
        foreach (var (name, b) in root.Obj("battles").Entries())
        {
            var battle = new BattleDef { name = name, age = b.Str("age") ?? "", victory = 15, greatVictory = 10 };
            if (b.TryDouble("centerX", out double v)) battle.centerX = (float)v;
            if (b.TryDouble("centerY", out v)) battle.centerY = (float)v;
            if (b.TryDouble("ageX", out v)) battle.ageX = (float)v;
            if (b.TryDouble("ageY", out v)) battle.ageY = (float)v;
            if (b.TryInt("victory", out int victory)) battle.victory = victory;
            if (b.TryInt("greatVictory", out int greatVictory)) battle.greatVictory = greatVictory;
            foreach (var f in b.Arr("flags").Objects()) battle.flag.Add(ReadFlag(f));
            foreach (var f in b.Arr("arrows").Objects()) battle.arrow.Add(ReadFlag(f));
            mapBattleDef[name] = battle;
        }
        return true;
    }

    public bool LoadConquestsJson()
    {
        var root = JsonData.LoadMerged(DataPaths.Conquests);
        if (root == null) return false;
        ReleaseConquestList();
        foreach (var (name, c) in root.Obj("conquests").Entries())
        {
            var conquest = new ConquestDef { name = name };
            if (c.TryDouble("centerX", out double v)) conquest.centerX = (float)v;
            if (c.TryDouble("centerY", out v)) conquest.centerY = (float)v;
            foreach (var f in c.Arr("flags").Objects()) conquest.flag.Add(ReadFlag(f));
            mapConquestList[name] = conquest;
        }
        return true;
    }

    private static FlagInfo ReadFlag(JsonObject f)
    {
        var info = new FlagInfo { name = f.Str("name") ?? "", scale = 1.0f };
        if (f.TryDouble("x", out double v)) info.x = (float)v;
        if (f.TryDouble("y", out v)) info.y = (float)v;
        // rot is written in degrees, like the XML
        if (f.TryDouble("rot", out v)) info.rot = (float)(v * 3.14159265 / 180.0);
        if (f.TryDouble("scale", out v)) info.scale = (float)v;
        return info;
    }

    public static JsonObject ConvertBattleListXml()
    {
        var doc = new TiXmlDocument(GetPath("battlelist.xml"));
        if (!doc.LoadFile()) return null;
        var node = doc.FirstChild("battlelist");
        if (node == null) return null;
        var battles = new JsonObject();
        for (var e = node.FirstChildElement(); e != null; e = e.NextSiblingElement())
        {
            if (e.Attribute("name") is not string name) continue;
            var o = new JsonObject();
            CopyText(e, o, "age", "age");
            CopyAttr(e, o, "centerx", "centerX");
            CopyAttr(e, o, "centery", "centerY");
            CopyAttr(e, o, "agex", "ageX");
            CopyAttr(e, o, "agey", "ageY");
            CopyAttr(e, o, "victory", "victory");
            CopyAttr(e, o, "greatvictory", "greatVictory");
            var flags = new JsonArray();
            var arrows = new JsonArray();
            for (var g = e.FirstChildElement(); g != null; g = g.NextSiblingElement())
            {
                string type = g.Attribute("type");
                var target = type == "flag" ? flags : type == "arrow" ? arrows : null;
                if (target == null) continue;
                for (var f = g.FirstChildElement(); f != null; f = f.NextSiblingElement()) target.Add(ConvertFlag(f));
            }
            o["flags"] = flags;
            o["arrows"] = arrows;
            battles[name] = o;
        }
        return new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Campaign battles on the selection map (converted from battlelist.xml): position, era label, victory turns, flags and arrows (rot in degrees). Field reference: docs/DATA.md#campaigns.",
            ["battles"] = battles,
        };
    }

    public static JsonObject ConvertConquestListXml()
    {
        var doc = new TiXmlDocument(GetPath("conquestlist.xml"));
        if (!doc.LoadFile()) return null;
        var node = doc.FirstChild("battlelist");
        if (node == null) return null;
        var conquests = new JsonObject();
        for (var e = node.FirstChildElement(); e != null; e = e.NextSiblingElement())
        {
            if (e.Attribute("name") is not string name) continue;
            var o = new JsonObject();
            CopyAttr(e, o, "centerx", "centerX");
            CopyAttr(e, o, "centery", "centerY");
            var flags = new JsonArray();
            for (var g = e.FirstChildElement(); g != null; g = g.NextSiblingElement())
            {
                if (g.Attribute("type") != "flag") continue;
                for (var f = g.FirstChildElement(); f != null; f = f.NextSiblingElement()) flags.Add(ConvertFlag(f));
            }
            o["flags"] = flags;
            conquests[name] = o;
        }
        return new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Conquests on the selection map (converted from conquestlist.xml): position and flags (rot in degrees). Field reference: docs/DATA.md#conquests.",
            ["conquests"] = conquests,
        };
    }

    private static JsonObject ConvertFlag(TiXmlElement f)
    {
        var o = new JsonObject();
        CopyText(f, o, "name", "name");
        CopyAttr(f, o, "x", "x");
        CopyAttr(f, o, "y", "y");
        CopyAttr(f, o, "rot", "rot");
        CopyAttr(f, o, "scale", "scale");
        return o;
    }
}
