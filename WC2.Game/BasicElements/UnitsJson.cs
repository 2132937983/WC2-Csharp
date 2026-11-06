using System.Text.Json.Nodes;
using static WC2.armydef_internal;

namespace WC2;

/// <summary>
/// Unit definitions in JSON (<c>data/units.json</c>), the data-driven replacement of armydef.xml.
///
/// <code>
/// {
///   "legacyCombatDefaults": false,
///   "weaponArmorRules": { "1": { "2": "150%" } },          // weapon type -> armor type -> rate
///   "unitTypes": { "infantry": { "id": 0, "image": "soldier" }, ... },
///   "countries": {
///     "others": {                                           // used when a country has no own entry
///       "tank": { "unitType": 2, "strength": 110, "movement": 2, "minAttack": 2, "maxAttack": 9,
///                 "armorType": 2,
///                 "combat": { "traits": ["cross-entrenchment"],
///                             "attack": { "mode": 0, "engagement": 0, "range": { "shape": "graph", "min": 0, "max": 100 } },
///                             "result": { "movementRestoreOnKill": 1 } } }
///     },
///     "de": { ... }
///   }
/// }
/// </code>
/// Field meanings, accepted names and defaults are those of the XML format (documented in
/// docs/DATA.md); this reader goes through the same parsing helpers, and <c>--verify-data</c> proves
/// that both files produce identical definitions. Mods merge into this file entry by entry.
/// </summary>
public sealed partial class CObjectDef
{
    /// <summary>Loads data/units.json; returns false when the package has no units file.</summary>
    public bool LoadUnitsJson()
    {
        var root = JsonData.LoadMerged(DataPaths.Units);
        if (root == null) return false;
        ResetArmyTypeRegistry();
        bool useLegacyCombatDefaults = root.Bool("legacyCombatDefaults", true);

        weaponArmorDamageRates.Clear();
        foreach (var (weaponKey, armors) in root.Obj("weaponArmorRules").Entries())
        {
            if (!ParseUInt8Text(weaponKey, out byte weaponType) || weaponType == 0 || weaponType > 32) continue;
            foreach (var (armorKey, rate) in armors)
            {
                if (!ParseUInt8Text(armorKey, out byte armorType) || armorType == 0) continue;
                weaponArmorDamageRates[MakeWeaponArmorRuleKey(weaponType, armorType)] = ParseDamageRate(rate.Text());
            }
        }

        foreach (var (typeName, type) in root.Obj("unitTypes").Entries())
        {
            if (!type.TryInt("id", out int typeId)) continue;
            bool countryIndependent = ParseBoolString(type.Str("countryIndependent"), out bool ci) && ci;
            RegisterArmyType(typeName, typeId, type.Str("image"), countryIndependent);
        }

        foreach (var (countryName, units) in root.Obj("countries").Entries())
        {
            var list = new ArmyDefList();
            foreach (var (typeName, unit) in units.Entries())
            {
                var def = ParseUnitJson(typeName, unit, useLegacyCombatDefaults);
                list.def[def.id] = def;
            }
            mapArmyDefList[countryName] = list;
        }
        LevelTablesJson.ReadUnitLevels(root.Arr("levels"));
        return true;
    }

    private static ArmyDef ParseUnitJson(string typeName, JsonObject unit, bool useLegacyCombatDefaults)
    {
        var def = new ArmyDef { name = typeName, id = ResolveArmyTypeId(typeName) };
        string imageName = unit.Str("image");
        if (!string.IsNullOrEmpty(imageName)) def.imageName = imageName;
        def.imageCountryIndependent = false;
        def.hasImageCountryIndependent = ParseBoolString(unit.Str("countryIndependent"), out bool imageCountryIndependent);
        if (def.hasImageCountryIndependent) def.imageCountryIndependent = imageCountryIndependent;
        byte unitType = ParseCombatUnitType(unit.Str("unitType"), ARMY_UNIT_TYPE_UNSPECIFIED);
        SetLegacyCombatRules(def, useLegacyCombatDefaults, unitType);
        if (ParseBoolString(unit.Str("navy"), out bool navy))
        {
            def.isNavy = navy;
            def.hasExplicitNavy = true;
        }
        def.armorType = 0;
        def.weaponType = ParseWeaponTypeMask(unit.Str("weaponType"));
        if (ParseUInt8Text(unit.Str("armorType"), out byte armorType)) def.armorType = armorType;
        def.hp = unit.Int("strength");
        def.movement = unit.Int("movement");
        def.maxAttack = unit.Int("maxAttack");
        def.minAttack = unit.Int("minAttack");
        ParseCombatRulesJson(unit.Obj("combat"), def);
        return def;
    }

    // ------------------------------------------------------------------------------------
    // armydef.xml -> units.json (used by --convert-data)
    // ------------------------------------------------------------------------------------

    public static JsonObject ConvertArmyDefXml(string xmlName = "armydef.xml")
    {
        var doc = new TiXmlDocument(GetPath(xmlName));
        if (!doc.LoadFile()) return null;
        var armies = doc.FirstChildElement("armies");
        if (armies == null) return null;
        var root = new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Unit definitions (converted from armydef.xml). Field reference: docs/DATA.md#units.",
        };
        if (armies.Attribute("legacy-combat-defaults") is string legacy) root["legacyCombatDefaults"] = JsonData.Value(legacy);

        var rules = new JsonObject();
        var weaponArmor = armies.FirstChildElement("weapon-armor-rules");
        if (weaponArmor != null)
        {
            for (var w = weaponArmor.FirstChildElement("weapon"); w != null; w = w.NextSiblingElement("weapon"))
            {
                string wt = w.Attribute("type");
                if (wt == null) continue;
                var armors = rules[wt] as JsonObject ?? new JsonObject();
                for (var a = w.FirstChildElement("armor"); a != null; a = a.NextSiblingElement("armor"))
                    if (a.Attribute("type") is string at) armors[at] = a.Attribute("rate");
                rules[wt] = armors;
            }
        }
        root["weaponArmorRules"] = rules;

        var types = new JsonObject();
        var typesElement = armies.FirstChildElement("army-types");
        if (typesElement != null)
        {
            for (var t = typesElement.FirstChildElement("type"); t != null; t = t.NextSiblingElement("type"))
            {
                if (t.Attribute("name") is not string name) continue;
                var o = new JsonObject();
                CopyAttr(t, o, "id", "id");
                CopyText(t, o, "image", "image");
                CopyAttr(t, o, "country-independent", "countryIndependent");
                types[name] = o;
            }
        }
        root["unitTypes"] = types;

        var countries = new JsonObject();
        for (var list = armies.FirstChildElement(); list != null; list = list.NextSiblingElement())
        {
            if (list.Attribute("name") is not string countryName) continue;
            var units = new JsonObject();
            for (var army = list.FirstChildElement(); army != null; army = army.NextSiblingElement())
            {
                if (army.Attribute("type") is not string typeName) continue;
                var u = new JsonObject();
                CopyAttr(army, u, "unit-type", "unitType");
                CopyAttr(army, u, "navy", "navy");
                CopyAttr(army, u, "strength", "strength");
                CopyAttr(army, u, "movement", "movement");
                CopyAttr(army, u, "minatk", "minAttack");
                CopyAttr(army, u, "maxatk", "maxAttack");
                CopyAttr(army, u, "weapon-type", "weaponType");
                CopyAttr(army, u, "armor-type", "armorType");
                CopyText(army, u, "image", "image");
                CopyAttr(army, u, "country-independent", "countryIndependent");
                var combat = army.FirstChildElement("combat");
                if (combat != null) u["combat"] = ConvertCombat(combat);
                units[typeName] = u;   // a repeated type replaces the earlier one, as in the XML loader
            }
            countries[countryName] = units;
        }
        root["countries"] = countries;
        // unit levels were constants in the code; they start as the built-in table
        root["levels"] = LevelTablesJson.UnitLevelsJson();
        return root;
    }

    private static JsonObject ConvertCombat(TiXmlElement combat)
    {
        var c = new JsonObject();
        if (combat.Attribute("traits") is string traits)
        {
            var arr = new JsonArray();
            foreach (var token in traits.Split(new[] { ' ', '\t', ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)) arr.Add(token);
            c["traits"] = arr;
        }
        CopyAttr(combat, c, "can-occupy", "canOccupy");
        CopyAttr(combat, c, "ai-no-counter-bonus", "aiNoCounterBonus");
        var attack = combat.FirstChildElement("attack");
        if (attack != null) c["attack"] = ConvertProfile(attack, isCounter: false);
        var counter = combat.FirstChildElement("counter");
        if (counter != null) c["counter"] = ConvertProfile(counter, isCounter: true);
        var damage = ConvertDamage(combat);
        if (damage != null) c["damage"] = damage;
        var result = combat.FirstChildElement("result");
        if (result != null)
        {
            var r = new JsonObject();
            string degrade = result.Attribute("construction-level-degrade") ?? result.Attribute("construction-level-damage") ?? result.Attribute("construction-damage");
            if (degrade != null) r["constructionLevelDegrade"] = JsonData.Value(degrade);
            string chance = result.Attribute("construction-level-chance") ?? result.Attribute("construction-chance");
            if (chance != null) r["constructionLevelChance"] = JsonData.Value(chance);
            CopyAttr(result, r, "movement-restore-on-kill", "movementRestoreOnKill");
            c["result"] = r;
        }
        return c;
    }

    private static JsonObject ConvertProfile(TiXmlElement e, bool isCounter)
    {
        var p = new JsonObject();
        if (isCounter) CopyAttr(e, p, "inherit", "inherit");
        else
        {
            CopyAttr(e, p, "mode", "mode");
            CopyAttr(e, p, "payload", "payload");
            CopyAttr(e, p, "attack-cost", "attackCost");
            CopyAttr(e, p, "engagement", "engagement");
            // a child <engagement mode="..."/> is read after the attribute and wins
            var engagement = e.FirstChildElement("engagement");
            if (engagement?.Attribute("mode") is string mode) p["engagement"] = JsonData.Value(mode);
        }
        var range = e.FirstChildElement("range");
        if (range != null)
        {
            var r = new JsonObject();
            CopyAttr(range, r, "shape", "shape");
            CopyAttr(range, r, "min", "min");
            CopyAttr(range, r, "max", "max");
            CopyAttr(range, r, "radius", "radius");
            CopyAttr(range, r, "max-inclusive", "maxInclusive");
            CopyAttr(range, r, "radius-source", "radiusSource");
            p["range"] = r;
        }
        var damage = ConvertDamage(e);
        if (damage != null) p["damage"] = damage;
        return p;
    }

    private static JsonArray ConvertDamage(TiXmlElement parent)
    {
        JsonArray arr = null;
        for (var d = parent.FirstChildElement("damage"); d != null; d = d.NextSiblingElement("damage"))
        {
            var o = new JsonObject();
            CopyAttr(d, o, "target", "target");
            CopyAttr(d, o, "phase", "phase");
            CopyAttr(d, o, "rate", "rate");
            (arr ??= new JsonArray()).Add(o);
        }
        return arr;
    }

    /// <summary>Copies a numeric (or number-or-name) attribute: numbers become JSON numbers.</summary>
    internal static void CopyAttr(TiXmlElement e, JsonObject o, string attribute, string key)
    {
        string v = e.Attribute(attribute);
        if (v != null) o[key] = JsonData.Value(v);
    }

    /// <summary>Copies a text attribute as text, even when it looks like a number ("1940.10" is October 1940).</summary>
    internal static void CopyText(TiXmlElement e, JsonObject o, string attribute, string key)
    {
        string v = e.Attribute(attribute);
        if (v != null) o[key] = v;
    }
}

public static partial class armydef_internal
{
    /// <summary>The bool spelling of the XML format ("1"/"true"/"yes", "0"/"false"/"no").</summary>
    public static bool ParseBoolString(string text, out bool value) => ParseBoolText(text, out value);

    /// <summary>Action cost from text; -1 means "clear the movement" (see ParseActionCost).</summary>
    public static bool ParseActionCostText(string text, out int result)
    {
        result = 0;
        if (string.IsNullOrEmpty(text)) return false;
        if (text == "inf" || text == "infinity") { result = -1; return true; }
        if (!ParseIntegerText(text, out long parsed)) return false;
        result = parsed <= 0 ? -1 : (parsed > 0x7FFFFFFF ? 0x7FFFFFFF : (int)parsed);
        return true;
    }

    /// <summary>JSON twin of ParseCombatRange.</summary>
    public static void ParseCombatRangeJson(JsonObject range, ArmyCombatRange target)
    {
        if (range == null) return;
        target.shape = ParseRangeShape(range.Str("shape"), target.shape);
        if (ParseUInt16Text(range.Str("min"), out ushort minValue)) target.minRange = minValue;
        if (ParseUInt16Text(range.Str("max"), out ushort maxValue)) target.maxRange = maxValue;
        if (ParseUInt16Text(range.Str("radius"), out ushort radiusValue))
        {
            target.maxRange = radiusValue;
            target.shape = COMBAT_RANGE_CIRCLE;
            target.radiusFromCountry = false;
        }
        if (ParseBoolText(range.Str("maxInclusive"), out bool inclusive)) target.maxInclusive = inclusive;
        string radiusSource = range.Str("radiusSource");
        if (radiusSource != null) target.radiusFromCountry = radiusSource == "country";
    }

    /// <summary>JSON twin of ParseCombatProfile.</summary>
    public static void ParseCombatProfileJson(JsonObject p, ArmyCombatProfile profile)
    {
        if (p == null) return;
        profile.mode = ParseAttackMode(p.Str("mode"), profile.mode);
        profile.payloadType = ParseAirstrikePayload(p.Str("payload"), profile.payloadType);
        if (ParseActionCostText(p.Str("attackCost"), out int attackCost)) profile.attackCost = attackCost;
        var engagement = p.Get("engagement");
        string engagementText = engagement is JsonObject eo ? eo.Str("mode") : engagement?.Text();
        profile.engagementMode = ParseEngagementMode(engagementText, profile.engagementMode);
        ParseCombatRangeJson(p.Obj("range"), profile.range);
    }

    /// <summary>JSON twin of ParseDamageElements.</summary>
    public static void ParseDamageJson(JsonArray damage, byte inheritedPhase, ArmyDef armyDef)
    {
        foreach (var d in damage.Objects())
        {
            string targetText = d.Str("target");
            if (string.IsNullOrEmpty(targetText)) continue;
            if (!StrToUL(targetText, 0, out ulong targetValue, out int targetEnd) || targetEnd != targetText.Length || targetValue > 254u) continue;
            byte phaseMask = ParseDamagePhaseMask(d.Str("phase"), inheritedPhase);
            if (phaseMask == 0) continue;
            AddDamageRule(armyDef.combat, phaseMask, (byte)targetValue, ParseDamageRate(d.Str("rate")));
        }
    }

    /// <summary>JSON twin of ParseCombatRules (same order of steps, same defaults).</summary>
    public static void ParseCombatRulesJson(JsonObject combat, ArmyDef armyDef)
    {
        if (combat == null) return;
        var attack = combat.Obj("attack");
        ParseCombatProfileJson(attack, armyDef.combat.attack);
        NormalizeCombatProfile(armyDef.combat.attack);
        armyDef.combat.hasExplicitAttack = attack != null;

        var counter = combat.Obj("counter");
        if (counter != null)
        {
            string inherit = counter.Str("inherit");
            if (inherit != null && inherit == "attack") armyDef.combat.counter = armyDef.combat.attack.Clone();
            else if (armyDef.combat.attack.mode == COMBAT_ATTACK_AIRSTRIKE) SetDefaultCombatProfile(armyDef.combat.counter);
            else armyDef.combat.counter = armyDef.combat.attack.Clone();
            ParseCombatRangeJson(counter.Obj("range"), armyDef.combat.counter.range);
            armyDef.combat.hasExplicitCounter = true;
        }
        else if (armyDef.combat.attack.mode == COMBAT_ATTACK_AIRSTRIKE)
        {
            SetDefaultCombatProfile(armyDef.combat.counter);
            armyDef.combat.hasExplicitCounter = false;
        }
        else
        {
            armyDef.combat.counter = armyDef.combat.attack.Clone();
            armyDef.combat.hasExplicitCounter = false;
        }

        ParseDamageJson(combat.Arr("damage"), 0, armyDef);
        ParseDamageJson(attack?.Arr("damage"), COMBAT_DAMAGE_ATTACK, armyDef);
        ParseDamageJson(counter?.Arr("damage"), COMBAT_DAMAGE_COUNTER, armyDef);

        string traits = combat.Get("traits")?.Text();
        if (HasToken(traits, "cross-entrenchment")) armyDef.combat.traits |= ARMY_COMBAT_TRAIT_CROSS_ENTRENCHMENT;
        if (HasToken(traits, "secondary-attack")) armyDef.combat.traits |= ARMY_COMBAT_TRAIT_SECONDARY_ATTACK;
        if (HasToken(traits, "recv_fort_second_counter")) armyDef.combat.traits |= ARMY_COMBAT_TRAIT_RECV_FORT_SECOND_COUNTER;
        if (HasToken(traits, "cannot-occupy")) armyDef.combat.traits |= ARMY_COMBAT_TRAIT_CANNOT_OCCUPY;
        if (ParseBoolText(combat.Str("canOccupy"), out bool canOccupy))
        {
            if (canOccupy) armyDef.combat.traits &= ~ARMY_COMBAT_TRAIT_CANNOT_OCCUPY;
            else armyDef.combat.traits |= ARMY_COMBAT_TRAIT_CANNOT_OCCUPY;
        }
        if (combat.TryInt("aiNoCounterBonus", out int aiNoCounterBonus)) armyDef.combat.aiNoCounterBonus = aiNoCounterBonus;

        var result = combat.Obj("result");
        if (result != null)
        {
            if (result.TryInt("constructionLevelDegrade", out int degrade)) armyDef.combat.constructionLevelDegrade = degrade;
            if (result.TryInt("constructionLevelChance", out int chance)) armyDef.combat.constructionLevelDegradeChance = chance;
            if (result.TryInt("movementRestoreOnKill", out int restore)) armyDef.combat.movementRestoreAfterKill = restore;
        }
    }
}
