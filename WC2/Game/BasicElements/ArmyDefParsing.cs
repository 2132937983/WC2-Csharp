using System.Globalization;

namespace WC2;

/// <summary>
/// Army type registry and the low level XML value parsers (ArmyDefParsing.cpp).
/// The file local statics of the C++ are private static fields here.
/// </summary>
public static partial class armydef_internal
{
    private static readonly Dictionary<string, int> g_armyTypeIds = new();
    private static readonly Dictionary<int, string> g_armyTypeNames = new();
    private static readonly Dictionary<int, string> g_armyTypeImageNames = new();
    private static readonly Dictionary<int, bool> g_armyTypeImageCountryIndependent = new();
    private static int g_nextDynamicArmyTypeId = 1000;

    private static void RegisterBuiltinArmyType(string name, int id, string imageName, bool imageCountryIndependent = false)
    {
        g_armyTypeIds[name] = id;
        g_armyTypeNames[id] = name;
        if (!string.IsNullOrEmpty(imageName))
        {
            g_armyTypeImageNames[id] = imageName;
        }
        g_armyTypeImageCountryIndependent[id] = imageCountryIndependent;
    }

    /// <summary>Clears the registry of this armydef.xml and registers the built in types again.</summary>
    public static void ResetArmyTypeRegistry()
    {
        g_armyTypeIds.Clear();
        g_armyTypeNames.Clear();
        g_armyTypeImageNames.Clear();
        g_armyTypeImageCountryIndependent.Clear();
        g_nextDynamicArmyTypeId = 1000;

        RegisterBuiltinArmyType("infantry", (int)INFANTRY, "soldier");
        RegisterBuiltinArmyType("panzer", (int)PANZER, "panzer");
        RegisterBuiltinArmyType("artillery", (int)ARTILLERY, "cannon");
        RegisterBuiltinArmyType("rocket", (int)ROCKET, "rocketlauncher");
        RegisterBuiltinArmyType("tank", (int)TANK, "tank");
        RegisterBuiltinArmyType("heavytank", (int)HEAVY_TANK, "heavytank");
        RegisterBuiltinArmyType("destroyer", (int)DESTROYER, "destroyer");
        RegisterBuiltinArmyType("cruiser", (int)CRUISER, "cruiser");
        RegisterBuiltinArmyType("battleship", (int)BATTLESHIP, "battleship");
        RegisterBuiltinArmyType("aircraftcarrier", (int)AIRCRAFT_CARRIER, "aircraftcarrier");
        RegisterBuiltinArmyType("carrier", (int)CARRIER_ARMY, "transportship");
        RegisterBuiltinArmyType("airstrike", (int)AIRSTRIKE_ARMY, null);
        RegisterBuiltinArmyType("bomber", (int)BOMBER_ARMY, "bomber");
        RegisterBuiltinArmyType("airborne", (int)AIRBORNE_ARMY, null);
        RegisterBuiltinArmyType("nuclearbomb", (int)NUCLEAR_BOMB_ARMY, "bomb");
        RegisterBuiltinArmyType("eliteinfantry", (int)ELITE_INFANTRY, "eliteinfantry");

        // the spaced spellings common in scenario XML map to the same built in ids
        g_armyTypeIds["heavy tank"] = (int)HEAVY_TANK;
        g_armyTypeIds["aircraft carrier"] = (int)AIRCRAFT_CARRIER;
        g_armyTypeIds["nuclear bomb"] = (int)NUCLEAR_BOMB_ARMY;
        g_armyTypeIds["elite infantry"] = (int)ELITE_INFANTRY;
    }

    /// <summary>Registers an explicit army type id.</summary>
    public static bool RegisterArmyType(string name, int id, string imageName = null, bool imageCountryIndependent = false)
    {
        if (string.IsNullOrEmpty(name) || id < 0) return false;
        string key = name;
        if (g_armyTypeIds.TryGetValue(key, out int existingId) && existingId != id) return false;
        if (g_armyTypeNames.TryGetValue(id, out string existingName) && existingName != key) return false;
        g_armyTypeIds[key] = id;
        g_armyTypeNames[id] = key;
        if (!string.IsNullOrEmpty(imageName))
        {
            g_armyTypeImageNames[id] = imageName;
        }
        g_armyTypeImageCountryIndependent[id] = imageCountryIndependent;
        if (id >= g_nextDynamicArmyTypeId)
        {
            g_nextDynamicArmyTypeId = id + 1;
        }
        return true;
    }

    /// <summary>Resolves an XML army type name to its id; unknown names get a dynamic id.</summary>
    public static int ResolveArmyTypeId(string name, int fallback = (int)ARMY_TYPE.INFANTRY)
    {
        if (string.IsNullOrEmpty(name)) return fallback;
        if (g_armyTypeIds.TryGetValue(name, out int existing)) return existing;

        // custom types without an explicit id use the reserved dynamic range so the legacy 0..15 stay intact
        int assignedId = g_nextDynamicArmyTypeId++;
        RegisterArmyType(name, assignedId);
        return assignedId;
    }

    /// <summary>Registered name of an id, or "" when unknown.</summary>
    public static string GetArmyTypeName(int id)
    {
        return g_armyTypeNames.TryGetValue(id, out string name) ? name : "";
    }

    /// <summary>Image base name of an id; the type name when none is configured.</summary>
    public static string GetArmyTypeImageName(int id)
    {
        if (g_armyTypeImageNames.TryGetValue(id, out string imageName)) return imageName;
        return GetArmyTypeName(id);
    }

    public static bool IsArmyTypeImageCountryIndependent(int id)
    {
        return g_armyTypeImageCountryIndependent.TryGetValue(id, out bool value) && value;
    }

    public static ushort EncodeDamageRate(double value, bool percentValue)
    {
        if (double.IsInfinity(value)) return DAMAGE_RATE_INFINITY;
        if (value <= 0.0) return 0;
        // with % one tenth of a percent is kept; without % the value is a multiplier, e.g. 1.5 = 150%
        double encoded = percentValue ? value * 10.0 : value * 1000.0;
        if (encoded >= (double)(DAMAGE_RATE_INFINITY - 1))
        {
            return (ushort)(DAMAGE_RATE_INFINITY - 1);
        }
        // round
        return (ushort)(encoded + 0.5);
    }

    public static ushort ParseDamageRate(string text)
    {
        if (text == null) return 1000; // missing means 100%
        if (text == "inf" || text == "infinity") return DAMAGE_RATE_INFINITY;

        if (!StrToD(text, 0, out double value, out int end) || value < 0.0) return 1000;
        while (end < text.Length && (text[end] == ' ' || text[end] == '\t')) ++end;
        bool percentValue = end < text.Length && text[end] == '%';
        return EncodeDamageRate(value, percentValue);
    }

    public static bool ParseIntegerText(string text, out long result)
    {
        result = 0;
        if (string.IsNullOrEmpty(text)) return false;
        if (!StrToL(text, 0, out long value, out int end) || end != text.Length) return false;
        result = value;
        return true;
    }

    public static bool ParseUInt8Text(string text, out byte result)
    {
        result = 0;
        if (string.IsNullOrEmpty(text)) return false;
        if (!StrToUL(text, 0, out ulong value, out int end) || end != text.Length || value > 255u) return false;
        result = (byte)value;
        return true;
    }

    public static bool ParseUInt8Attribute(TiXmlElement element, string name, out byte result)
    {
        result = 0;
        if (element == null || name == null) return false;
        return ParseUInt8Text(element.Attribute(name), out result);
    }

    public static bool ParseUInt16Text(string text, out ushort result)
    {
        result = 0;
        if (string.IsNullOrEmpty(text)) return false;
        if (!StrToUL(text, 0, out ulong value, out int end) || end != text.Length || value > 65535u) return false;
        result = (ushort)value;
        return true;
    }

    public static bool ParseUInt16Attribute(TiXmlElement element, string name, out ushort result)
    {
        result = 0;
        if (element == null || name == null) return false;
        return ParseUInt16Text(element.Attribute(name), out result);
    }

    public static byte ParseCombatUnitType(string text, byte fallback)
    {
        if (!ParseUInt8Text(text, out byte parsed) || parsed > 254u) return fallback;
        return parsed;
    }

    public static uint ParseWeaponTypeMask(string text)
    {
        // empty text: no weapon type (rate 1.0 against every armor; configuring one is recommended)
        if (string.IsNullOrEmpty(text)) return 0;

        uint mask = 0;
        bool parsedAny = false;
        int cursor = 0;
        // separators
        while (cursor < text.Length)
        {
            while (cursor < text.Length &&
                   (text[cursor] == ' ' || text[cursor] == '\t' || text[cursor] == ',' || text[cursor] == '|'))
            {
                ++cursor;
            }
            if (cursor >= text.Length) break;

            // the type number
            if (!StrToUL(text, cursor, out ulong type, out int end) || type < 1u || type > 32u) return 0;
            // the mask bit
            mask |= 1u << (int)(type - 1u);
            parsedAny = true;
            cursor = end;

            if (cursor < text.Length && text[cursor] != ' ' && text[cursor] != '\t' &&
                text[cursor] != ',' && text[cursor] != '|')
            {
                return 0;
            }
        }

        return parsedAny ? mask : 0;
    }

    private static bool ParseBoolText(string text, out bool value)
    {
        value = false;
        if (text == null) return false;
        if (text == "1" || text == "true" || text == "yes")
        {
            value = true;
            return true;
        }
        if (text == "0" || text == "false" || text == "no")
        {
            value = false;
            return true;
        }
        return false;
    }

    public static bool ParseBoolAttribute(TiXmlElement element, string name, out bool result)
    {
        result = false;
        if (element == null || name == null) return false;
        return ParseBoolText(element.Attribute(name), out result);
    }

    public static COMBAT_ATTACK_MODE ParseAttackMode(string value, COMBAT_ATTACK_MODE fallback)
    {
        if (value == null) return fallback;
        // numbers are allowed: 0 close, 1 projectile / harassment, 2 air strike
        if (ParseIntegerText(value, out long numericValue) &&
            numericValue >= (long)COMBAT_ATTACK_DIRECT &&
            numericValue <= (long)COMBAT_ATTACK_AIRSTRIKE)
        {
            return (COMBAT_ATTACK_MODE)numericValue;
        }
        if (value == "direct") return COMBAT_ATTACK_DIRECT;
        if (value == "projectile") return COMBAT_ATTACK_PROJECTILE;
        if (value == "airstrike") return COMBAT_ATTACK_AIRSTRIKE;
        if (value == "infiltration")
        {
            // harassment / infiltration shares type1 with the projectile mode
            return COMBAT_ATTACK_PROJECTILE;
        }
        return fallback;
    }

    public static COMBAT_AIRSTRIKE_PAYLOAD ParseAirstrikePayload(string value, COMBAT_AIRSTRIKE_PAYLOAD fallback)
    {
        if (value == null) return fallback;
        if (ParseIntegerText(value, out long numericValue) &&
            numericValue >= (long)COMBAT_AIRSTRIKE_PAYLOAD_AIRSTRIKE &&
            numericValue <= (long)COMBAT_AIRSTRIKE_PAYLOAD_NUCLEAR)
        {
            return (COMBAT_AIRSTRIKE_PAYLOAD)numericValue;
        }
        // air strike or fighter (alias)
        if (value == "airstrike" || value == "fighter") return COMBAT_AIRSTRIKE_PAYLOAD_AIRSTRIKE;
        if (value == "bomber") return COMBAT_AIRSTRIKE_PAYLOAD_BOMBER;
        if (value == "nuclear" || value == "nuclearbomb") return COMBAT_AIRSTRIKE_PAYLOAD_NUCLEAR;
        return fallback;
    }

    /// <summary>Parses an action cost attribute; -1 means "clear the movement".</summary>
    public static bool ParseActionCost(TiXmlElement element, string name, out int result)
    {
        result = 0;
        if (element == null) return false;
        string text = element.Attribute(name);
        if (string.IsNullOrEmpty(text)) return false;
        if (text == "inf" || text == "infinity")
        {
            result = -1;
            return true;
        }
        if (!ParseIntegerText(text, out long parsed)) return false;
        // there is no "free air strike"; 0 and negative values mean inf, i.e. clear the movement
        result = parsed <= 0
            ? -1
            : (parsed > 0x7FFFFFFF ? 0x7FFFFFFF : (int)parsed);
        return true;
    }

    public static COMBAT_RANGE_SHAPE ParseRangeShape(string value, COMBAT_RANGE_SHAPE fallback)
    {
        if (value == null) return fallback;
        if (value == "graph") return COMBAT_RANGE_GRAPH;
        if (value == "circle") return COMBAT_RANGE_CIRCLE;
        return fallback;
    }

    public static COMBAT_ENGAGEMENT_MODE ParseEngagementMode(string value, COMBAT_ENGAGEMENT_MODE fallback)
    {
        if (value == null) return fallback;
        if (ParseIntegerText(value, out long numericValue) &&
            numericValue >= (long)COMBAT_ENGAGEMENT_CLOSE &&
            numericValue <= (long)COMBAT_ENGAGEMENT_PROJECTILE)
        {
            return (COMBAT_ENGAGEMENT_MODE)numericValue;
        }
        if (value == "projectile") return COMBAT_ENGAGEMENT_PROJECTILE;
        if (value == "close") return COMBAT_ENGAGEMENT_CLOSE;
        return fallback;
    }

    /// <summary>Whether a comma separated attribute contains the token.</summary>
    public static bool HasToken(string list, string token)
    {
        if (list == null || token == null) return false;
        int tokenLength = token.Length;
        for (int current = 0; current < list.Length;)
        {
            while (current < list.Length && (list[current] == ',' || list[current] == ' ' || list[current] == '\t'))
            {
                ++current;
            }
            int end = current;
            while (end < list.Length && list[end] != ',' && list[end] != ' ' && list[end] != '\t')
            {
                ++end;
            }
            if (end - current == tokenLength && string.CompareOrdinal(list, current, token, 0, tokenLength) == 0)
            {
                return true;
            }
            current = end;
        }
        return false;
    }

    /// <summary>Parses the damage phase list (attack / counter).</summary>
    /// <param name="text">phase tokens</param>
    /// <param name="fallback">phase used without valid tokens</param>
    public static byte ParseDamagePhaseMask(string text, byte fallback)
    {
        if (string.IsNullOrEmpty(text)) return fallback;
        byte mask = 0;
        if (HasToken(text, "attack")) mask |= COMBAT_DAMAGE_ATTACK;
        if (HasToken(text, "counter")) mask |= COMBAT_DAMAGE_COUNTER;
        return mask == 0 ? fallback : mask;
    }

    // ---- C string number scanners (strtol / strtoul / strtod with base 10) -----------------

    /// <summary>
    /// strtol(text + start, &amp;end, 10): skips leading white space, accepts an optional sign and decimal
    /// digits. Returns false (end = start) when no digit was read.
    /// </summary>
    private static bool StrToL(string text, int start, out long value, out int end)
    {
        value = 0;
        end = start;
        if (text == null) return false;
        int i = start;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        bool negative = false;
        if (i < text.Length && (text[i] == '+' || text[i] == '-'))
        {
            negative = text[i] == '-';
            i++;
        }
        int digitsStart = i;
        long acc = 0;
        bool overflow = false;
        while (i < text.Length && text[i] >= '0' && text[i] <= '9')
        {
            int d = text[i] - '0';
            if (acc > (long.MaxValue - d) / 10) overflow = true;
            else acc = acc * 10 + d;
            i++;
        }
        if (i == digitsStart) return false;
        if (overflow) acc = long.MaxValue;
        value = negative ? -acc : acc;
        end = i;
        return true;
    }

    /// <summary>
    /// strtoul(text + start, &amp;end, 10). A negative number yields ulong.MaxValue (every caller
    /// rejects out of range values the same way strtoul's wrap around would).
    /// </summary>
    private static bool StrToUL(string text, int start, out ulong value, out int end)
    {
        value = 0;
        end = start;
        if (text == null) return false;
        int i = start;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        bool negative = false;
        if (i < text.Length && (text[i] == '+' || text[i] == '-'))
        {
            negative = text[i] == '-';
            i++;
        }
        int digitsStart = i;
        ulong acc = 0;
        bool overflow = false;
        while (i < text.Length && text[i] >= '0' && text[i] <= '9')
        {
            uint d = (uint)(text[i] - '0');
            if (acc > (ulong.MaxValue - d) / 10) overflow = true;
            else acc = acc * 10 + d;
            i++;
        }
        if (i == digitsStart) return false;
        if (overflow || (negative && acc != 0)) acc = ulong.MaxValue;
        value = acc;
        end = i;
        return true;
    }

    /// <summary>strtod(text + start, &amp;end) for plain decimal notation (sign, digits, fraction, exponent).</summary>
    private static bool StrToD(string text, int start, out double value, out int end)
    {
        value = 0;
        end = start;
        if (text == null) return false;
        int i = start;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        int numStart = i;
        if (i < text.Length && (text[i] == '+' || text[i] == '-')) i++;
        int digits = 0;
        while (i < text.Length && text[i] >= '0' && text[i] <= '9') { i++; digits++; }
        if (i < text.Length && text[i] == '.')
        {
            i++;
            while (i < text.Length && text[i] >= '0' && text[i] <= '9') { i++; digits++; }
        }
        if (digits == 0) return false;
        if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
        {
            int j = i + 1;
            if (j < text.Length && (text[j] == '+' || text[j] == '-')) j++;
            int expDigitsStart = j;
            while (j < text.Length && text[j] >= '0' && text[j] <= '9') j++;
            if (j > expDigitsStart) i = j;
        }
        if (!double.TryParse(text.AsSpan(numStart, i - numStart), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return false;
        end = i;
        return true;
    }
}
