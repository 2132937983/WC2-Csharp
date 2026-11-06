namespace WC2;

/// <summary>Combat rule defaults and XML parsing of the combat node (ArmyDefCombatRules.cpp).</summary>
public static partial class armydef_internal
{
    /// <summary>Default combat profile.</summary>
    public static void SetDefaultCombatProfile(ArmyCombatProfile profile)
    {
        profile.mode = COMBAT_ATTACK_DIRECT;
        profile.range.shape = COMBAT_RANGE_GRAPH;
        profile.range.minRange = 0;
        // graph ranges use 1000 for one step
        profile.range.maxRange = 1000;
        profile.range.maxInclusive = true;
        profile.range.radiusFromCountry = false;
        profile.engagementMode = COMBAT_ENGAGEMENT_CLOSE;
        profile.payloadType = COMBAT_AIRSTRIKE_PAYLOAD_AIRSTRIKE;
        profile.attackCost = 1;
    }

    public static void AddDamageRule(ArmyCombatRules rules, byte phaseMask, byte targetType, ushort rate)
    {
        if (rules == null || targetType == ARMY_UNIT_TYPE_UNSPECIFIED) return;
        if ((phaseMask & COMBAT_DAMAGE_ATTACK) != 0)
        {
            rules.damageRates[MakeDamageRuleKey(COMBAT_DAMAGE_ATTACK, targetType)] = rate;
        }
        if ((phaseMask & COMBAT_DAMAGE_COUNTER) != 0)
        {
            rules.damageRates[MakeDamageRuleKey(COMBAT_DAMAGE_COUNTER, targetType)] = rate;
        }
    }

    /// <summary>Adds the original damage rules.</summary>
    private static void SetLegacyDamageRules(ArmyDef armyDef)
    {
        ushort legacyRate = EncodeDamageRate(1.3, false);
        // XML type convention: 1 infantry, 2 armour, 3 artillery, 4 navy.
        // Old rule: armour deals bonus damage to everything, everything deals bonus damage to infantry.
        // Both conditions merge into one rate so a double hit is not 1.69.
        for (byte targetType = 1; targetType <= 4; ++targetType)
        {
            if (armyDef.unitType == 2 || targetType == 1)
            {
                AddDamageRule(armyDef.combat, COMBAT_DAMAGE_ATTACK | COMBAT_DAMAGE_COUNTER, targetType, legacyRate);
            }
        }
    }

    /// <summary>Writes the original compatible default combat rules.</summary>
    /// <param name="armyDef">unit definition</param>
    /// <param name="useLegacyDefaults">enable the original compatible rules</param>
    /// <param name="unitType">combat class (armour, infantry, ...; separate from the id)</param>
    public static void SetLegacyCombatRules(ArmyDef armyDef, bool useLegacyDefaults, byte unitType)
    {
        SetDefaultCombatProfile(armyDef.combat.attack);
        armyDef.combat.counter = armyDef.combat.attack.Clone();
        armyDef.combat.hasExplicitAttack = false;
        armyDef.combat.hasExplicitCounter = false;
        armyDef.combat.traits = ARMY_COMBAT_TRAIT_NONE;
        armyDef.combat.constructionLevelDegrade = 0;
        armyDef.combat.constructionLevelDegradeChance = 0;
        armyDef.combat.movementRestoreAfterKill = 0;
        armyDef.combat.aiNoCounterBonus = 300;
        armyDef.unitType = unitType;
        // the navy flag is independent; without an explicit value the old XML rule applies
        armyDef.hasExplicitNavy = false;
        armyDef.isNavy =
            useLegacyDefaults &&
            armyDef.id >= (int)DESTROYER && armyDef.id <= (int)AIRCRAFT_CARRIER;
        armyDef.combat.damageRates.Clear();

        if (!useLegacyDefaults) return;

        // these defaults only serve old XML files without a combat node
        if (armyDef.id == (int)ROCKET)
        {
            armyDef.combat.traits |= ARMY_COMBAT_TRAIT_CANNOT_OCCUPY;
            armyDef.combat.attack.mode = COMBAT_ATTACK_PROJECTILE;
            armyDef.combat.attack.engagementMode = COMBAT_ENGAGEMENT_PROJECTILE;
            // as in armydef.xml: the rocket covers 1.5 to 2 steps of graph distance
            armyDef.combat.attack.range.minRange = 1500;
            armyDef.combat.attack.range.maxRange = 2000;
            armyDef.combat.counter = armyDef.combat.attack.Clone();
        }
        else if (armyDef.id == (int)AIRCRAFT_CARRIER)
        {
            armyDef.combat.attack.mode = COMBAT_ATTACK_AIRSTRIKE;
            armyDef.combat.attack.payloadType = COMBAT_AIRSTRIKE_PAYLOAD_AIRSTRIKE;
            armyDef.combat.attack.attackCost = 1;
            armyDef.combat.attack.engagementMode = COMBAT_ENGAGEMENT_PROJECTILE;
            armyDef.combat.attack.range.shape = COMBAT_RANGE_CIRCLE;
            armyDef.combat.attack.range.minRange = 0;
            armyDef.combat.attack.range.maxRange = 0;
            armyDef.combat.attack.range.maxInclusive = false;
            armyDef.combat.attack.range.radiusFromCountry = true;
            // an air strike unit never counters with an air strike; the counter starts from the close defaults
            SetDefaultCombatProfile(armyDef.combat.counter);
        }
        else if (armyDef.id == (int)AIRSTRIKE_ARMY ||
                 armyDef.id == (int)BOMBER_ARMY ||
                 armyDef.id == (int)AIRBORNE_ARMY ||
                 armyDef.id == (int)NUCLEAR_BOMB_ARMY)
        {
            armyDef.combat.attack.mode = COMBAT_ATTACK_AIRSTRIKE;
            if (armyDef.id == (int)BOMBER_ARMY)
                armyDef.combat.attack.payloadType = COMBAT_AIRSTRIKE_PAYLOAD_BOMBER;
            else if (armyDef.id == (int)NUCLEAR_BOMB_ARMY)
                armyDef.combat.attack.payloadType = COMBAT_AIRSTRIKE_PAYLOAD_NUCLEAR;
            else
                armyDef.combat.attack.payloadType = COMBAT_AIRSTRIKE_PAYLOAD_AIRSTRIKE;
            armyDef.combat.attack.engagementMode = COMBAT_ENGAGEMENT_PROJECTILE;
            armyDef.combat.attack.range.shape = COMBAT_RANGE_CIRCLE;
            armyDef.combat.attack.range.minRange = 0;
            armyDef.combat.attack.range.maxRange = 0;
            armyDef.combat.attack.range.maxInclusive = false;
            armyDef.combat.attack.range.radiusFromCountry = true;
            SetDefaultCombatProfile(armyDef.combat.counter);
        }

        // Keep the legacy fallback in sync with the explicit others entries:
        // infantry, panzer, and tank use a short graph range (0..100).
        if (armyDef.id == (int)INFANTRY || armyDef.id == (int)PANZER || armyDef.id == (int)TANK)
        {
            armyDef.combat.attack.range.maxRange = 100;
            armyDef.combat.counter.range.maxRange = 100;
        }

        // the original numeric traits; the actual combat still runs on the old logic
        if (armyDef.id == (int)ARTILLERY)
        {
            // the original artillery never enters the target area after a kill
            armyDef.combat.traits |= ARMY_COMBAT_TRAIT_CANNOT_OCCUPY;
        }
        if (armyDef.id == (int)BATTLESHIP)
        {
            armyDef.combat.constructionLevelDegrade = 1;
            armyDef.combat.constructionLevelDegradeChance = 30;
        }
        // original rule: only tanks and heavy tanks keep their movement when attacking an
        // entrenchment; every other unit (artillery, rockets and ships included) loses it all
        if (armyDef.id == (int)TANK || armyDef.id == (int)HEAVY_TANK)
        {
            armyDef.combat.traits |= ARMY_COMBAT_TRAIT_CROSS_ENTRENCHMENT;
        }
        if (armyDef.id == (int)TANK || armyDef.id == (int)HEAVY_TANK)
        {
            // the original refunds the one movement point of the attack after a kill
            armyDef.combat.movementRestoreAfterKill = 1;
        }
        SetLegacyDamageRules(armyDef);
    }

    /// <summary>Parses a whole range node.</summary>
    public static void ParseCombatRange(TiXmlElement rangeElement, ArmyCombatRange range)
    {
        if (rangeElement == null) return;
        range.shape = ParseRangeShape(rangeElement.Attribute("shape"), range.shape);

        if (ParseUInt16Attribute(rangeElement, "min", out ushort minValue)) range.minRange = minValue;
        if (ParseUInt16Attribute(rangeElement, "max", out ushort maxValue)) range.maxRange = maxValue;
        if (ParseUInt16Attribute(rangeElement, "radius", out ushort radiusValue))
        {
            range.maxRange = radiusValue;
            range.shape = COMBAT_RANGE_CIRCLE;
            range.radiusFromCountry = false;
        }
        if (ParseBoolAttribute(rangeElement, "max-inclusive", out bool boolValue)) range.maxInclusive = boolValue;
        string radiusSource = rangeElement.Attribute("radius-source");
        if (radiusSource != null)
        {
            range.radiusFromCountry = radiusSource == "country";
        }
    }

    /// <summary>Parses a complete attack / counter profile.</summary>
    public static void ParseCombatProfile(TiXmlElement profileElement, ArmyCombatProfile profile)
    {
        if (profileElement == null) return;
        // mode accepts strings and numbers: 0 close, 1 projectile / harassment, 2 air strike
        profile.mode = ParseAttackMode(profileElement.Attribute("mode"), profile.mode);
        profile.payloadType = ParseAirstrikePayload(profileElement.Attribute("payload"), profile.payloadType);
        if (ParseActionCost(profileElement, "attack-cost", out int attackCost)) profile.attackCost = attackCost;
        profile.engagementMode = ParseEngagementMode(profileElement.Attribute("engagement"), profile.engagementMode);

        TiXmlElement rangeElement = profileElement.FirstChildElement("range");
        ParseCombatRange(rangeElement, profile.range);

        TiXmlElement engagementElement = profileElement.FirstChildElement("engagement");
        if (engagementElement != null)
        {
            string mode = engagementElement.Attribute("mode");
            profile.engagementMode = ParseEngagementMode(mode, profile.engagementMode);
        }
    }

    /// <summary>Parses the counter range (mainly for units that attack with air strikes).</summary>
    public static void ParseCombatCounterRange(TiXmlElement counterElement, ArmyCombatProfile profile)
    {
        if (counterElement == null || profile == null) return;
        // the counter has no type / mode, only its range is read
        ParseCombatRange(counterElement.FirstChildElement("range"), profile.range);
    }

    /// <summary>Forces the yellow arrow for circle ranges and graph ranges beyond one step.</summary>
    public static void NormalizeCombatProfile(ArmyCombatProfile profile)
    {
        if (profile == null || profile.mode != COMBAT_ATTACK_DIRECT) return;
        // a circle range is ranged by nature; a graph range above 1000 (one step) cannot use the close arrow.
        // type1 covers both rockets and harassment / infiltration.
        if (profile.range.shape == COMBAT_RANGE_CIRCLE || profile.range.maxRange > 1000)
        {
            profile.mode = COMBAT_ATTACK_PROJECTILE;
        }
    }

    /// <summary>Parses the damage children of a node.</summary>
    /// <param name="parent">parent node</param>
    /// <param name="inheritedPhase">phase used when the damage node has none</param>
    /// <param name="armyDef">unit definition</param>
    public static void ParseDamageElements(TiXmlElement parent, byte inheritedPhase, ArmyDef armyDef)
    {
        if (parent == null) return;
        for (TiXmlElement damageElement = parent.FirstChildElement("damage");
             damageElement != null;
             damageElement = damageElement.NextSiblingElement("damage"))
        {
            string targetText = damageElement.Attribute("target");
            if (string.IsNullOrEmpty(targetText)) continue;
            if (!StrToUL(targetText, 0, out ulong targetValue, out int targetEnd) ||
                targetEnd != targetText.Length || targetValue > 254u)
                continue;
            byte targetType = (byte)targetValue;
            byte phaseMask = ParseDamagePhaseMask(damageElement.Attribute("phase"), inheritedPhase);
            if (phaseMask == 0) continue;
            AddDamageRule(armyDef.combat, phaseMask, targetType, ParseDamageRate(damageElement.Attribute("rate")));
        }
    }

    /// <summary>Parses the global weapon / armor table.</summary>
    public static void ParseWeaponArmorRules(TiXmlElement parent, Dictionary<ushort, ushort> rules)
    {
        if (parent == null || rules == null) return;
        // the new format groups by weapon so the weapon type is not repeated per rule:
        // <weapon type="1"><armor type="2" rate="150%"/></weapon>
        for (TiXmlElement weaponElement = parent.FirstChildElement("weapon");
             weaponElement != null;
             weaponElement = weaponElement.NextSiblingElement("weapon"))
        {
            if (!ParseUInt8Attribute(weaponElement, "type", out byte weaponType) || weaponType == 0 || weaponType > 32)
                continue;
            for (TiXmlElement armorElement = weaponElement.FirstChildElement("armor");
                 armorElement != null;
                 armorElement = armorElement.NextSiblingElement("armor"))
            {
                if (!ParseUInt8Attribute(armorElement, "type", out byte armorType) || armorType == 0) continue;
                rules[MakeWeaponArmorRuleKey(weaponType, armorType)] = ParseDamageRate(armorElement.Attribute("rate"));
            }
        }
    }

    /// <summary>Parses and stores the combat rules of a unit.</summary>
    public static void ParseCombatRules(TiXmlElement combatElement, ArmyDef armyDef)
    {
        if (combatElement == null) return;
        TiXmlElement attackElement = combatElement.FirstChildElement("attack");
        ParseCombatProfile(attackElement, armyDef.combat.attack);
        NormalizeCombatProfile(armyDef.combat.attack);
        armyDef.combat.hasExplicitAttack = attackElement != null;

        TiXmlElement counterElement = combatElement.FirstChildElement("counter");
        if (counterElement != null)
        {
            string inherit = counterElement.Attribute("inherit");
            if (inherit != null && inherit == "attack")
            {
                armyDef.combat.counter = armyDef.combat.attack.Clone();
            }
            else if (armyDef.combat.attack.mode == COMBAT_ATTACK_AIRSTRIKE)
            {
                // an air strike attack without a declared counter mode counters in close combat
                SetDefaultCombatProfile(armyDef.combat.counter);
            }
            else
            {
                armyDef.combat.counter = armyDef.combat.attack.Clone();
            }
            ParseCombatCounterRange(counterElement, armyDef.combat.counter);
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

        // damage nodes directly below combat as well as below attack / counter
        ParseDamageElements(combatElement, 0, armyDef);
        ParseDamageElements(attackElement, COMBAT_DAMAGE_ATTACK, armyDef);
        ParseDamageElements(counterElement, COMBAT_DAMAGE_COUNTER, armyDef);

        string traits = combatElement.Attribute("traits");
        if (HasToken(traits, "cross-entrenchment"))
        {
            armyDef.combat.traits |= ARMY_COMBAT_TRAIT_CROSS_ENTRENCHMENT;
        }
        if (HasToken(traits, "secondary-attack"))
        {
            armyDef.combat.traits |= ARMY_COMBAT_TRAIT_SECONDARY_ATTACK;
        }
        // receives the second counter of the fort guns
        if (HasToken(traits, "recv_fort_second_counter"))
        {
            armyDef.combat.traits |= ARMY_COMBAT_TRAIT_RECV_FORT_SECOND_COUNTER;
        }
        if (HasToken(traits, "cannot-occupy"))
        {
            armyDef.combat.traits |= ARMY_COMBAT_TRAIT_CANNOT_OCCUPY;
        }

        // can-occupy is the more readable form; unset keeps the legacy default.
        // The explicit attribute wins over a cannot-occupy trait.
        if (ParseBoolAttribute(combatElement, "can-occupy", out bool canOccupy))
        {
            if (canOccupy) armyDef.combat.traits &= ~ARMY_COMBAT_TRAIT_CANNOT_OCCUPY;
            else armyDef.combat.traits |= ARMY_COMBAT_TRAIT_CANNOT_OCCUPY;
        }

        if (combatElement.QueryIntAttribute("ai-no-counter-bonus", out int aiNoCounterBonus) == TIXML_SUCCESS)
        {
            armyDef.combat.aiNoCounterBonus = aiNoCounterBonus;
        }

        TiXmlElement resultElement = combatElement.FirstChildElement("result");
        if (resultElement != null)
        {
            int value;
            if (resultElement.QueryIntAttribute("construction-level-degrade", out value) == TIXML_SUCCESS ||
                // older provisional attribute names
                resultElement.QueryIntAttribute("construction-level-damage", out value) == TIXML_SUCCESS ||
                resultElement.QueryIntAttribute("construction-damage", out value) == TIXML_SUCCESS)
            {
                armyDef.combat.constructionLevelDegrade = value;
            }
            if (resultElement.QueryIntAttribute("construction-level-chance", out value) == TIXML_SUCCESS ||
                resultElement.QueryIntAttribute("construction-chance", out value) == TIXML_SUCCESS)
            {
                armyDef.combat.constructionLevelDegradeChance = value;
            }
            // movement restored after a kill
            if (resultElement.QueryIntAttribute("movement-restore-on-kill", out value) == TIXML_SUCCESS)
            {
                armyDef.combat.movementRestoreAfterKill = value;
            }
        }
    }
}

/// <summary>Combat rule queries from ArmyDef.h (ArmyDefCombatRules.cpp).</summary>
public static partial class ArmyDefFunctions
{
    /// <summary>Conditional damage rate of a unit against a target type in the given phase.</summary>
    public static float GetCombatDamageRate(ArmyCombatRules rules, byte targetType, byte phase)
    {
        if (targetType == ARMY_UNIT_TYPE_UNSPECIFIED) return 1.0f;
        if (!rules.damageRates.TryGetValue(MakeDamageRuleKey(phase, targetType), out ushort code)) return 1.0f;
        if (code == DAMAGE_RATE_INFINITY) return float.PositiveInfinity;
        return code / 1000.0f;
    }

    /// <summary>Weapon / armor rate: the maximum over every weapon type of the mask.</summary>
    /// <param name="rules">rule table</param>
    /// <param name="weaponTypeMask">weapon types of the unit</param>
    /// <param name="armorType">armor type of the target</param>
    public static float GetWeaponArmorDamageRate(Dictionary<ushort, ushort> rules, uint weaponTypeMask, byte armorType)
    {
        if (weaponTypeMask == 0 || armorType == 0) return 1.0f;
        float bestRate = 0.0f;
        bool hasWeaponType = false;
        for (byte weaponType = 1; weaponType <= 32; ++weaponType)
        {
            uint bit = 1u << (weaponType - 1);
            if ((weaponTypeMask & bit) == 0) continue;
            hasWeaponType = true;
            if (!rules.TryGetValue(MakeWeaponArmorRuleKey(weaponType, armorType), out ushort code))
            {
                // an unconfigured weapon type keeps the default 1.0
                bestRate = Math.Max(bestRate, 1.0f);
                continue;
            }
            if (code == DAMAGE_RATE_INFINITY) return float.PositiveInfinity;
            bestRate = Math.Max(bestRate, code / 1000.0f);
        }
        return hasWeaponType ? bestRate : 1.0f;
    }

    /// <summary>Sets a damage rate (reserved for tech bonuses etc.).</summary>
    public static void SetCombatDamageRate(ArmyCombatRules rules, byte phase, byte targetType, double value, bool percentValue = false)
    {
        if (rules == null) return;
        if (targetType == ARMY_UNIT_TYPE_UNSPECIFIED) return;
        rules.damageRates[MakeDamageRuleKey(phase, targetType)] = armydef_internal.EncodeDamageRate(value, percentValue);
    }

    /// <summary>Clears a damage rate (reserved for tech bonuses etc.).</summary>
    public static void ClearCombatDamageRate(ArmyCombatRules rules, byte phase, byte targetType)
    {
        if (rules == null) return;
        if (targetType == ARMY_UNIT_TYPE_UNSPECIFIED) return;
        rules.damageRates.Remove(MakeDamageRuleKey(phase, targetType));
    }
}
