using System.Text.Json.Nodes;

namespace WC2;

/// <summary>
/// One AI profile (an entry of <c>data/json/ai/profiles.json</c>). The rules code (CActionAI,
/// CActionAssist) keeps its original structure and names; every number it decides with comes from
/// here. The "classic" profile holds the original game's numbers, so classic reproduces the original
/// decisions exactly; the improved profiles change numbers and switch on the extra terms below (all 0
/// in classic). Field reference: DATA_SCHEMA.md#ai.
/// </summary>
public sealed class AiProfile
{
    public string id = "classic";
    public string description = "";

    // ---- area value (calcAreaValue): tax * taxWeight + industry * industryWeight + type + installation
    public int taxWeight = 2;
    public int industryWeight = 3;
    /// <summary>By area type id (normal, capital, port, large city, normal city).</summary>
    public int[] areaTypeValue = { 1, 250, 80, 150, 80 };
    /// <summary>By installation id (none, fort, entrenchment, anti-aircraft, radar).</summary>
    public int[] installationValue = { 0, 20, 15, 10, 0 };

    // ---- staying (action 0x1000)
    /// <summary>No enemy around: the stay value is divided by (this * units here + 1).</summary>
    public int stayNoEnemyDivisorPerUnit = 2;
    /// <summary>Enemies around stronger than the units here: the stay value is multiplied by this.</summary>
    public int stayThreatenedMultiplier = 2;

    // ---- moving (action 0x2000)
    public int moveEnemyBonus = 80;
    public int moveNeutralBonus = 60;
    public int landingBonus = 80;
    public int movePlayerBonus = 40;
    /// <summary>Moving into an empty own area (when others stay behind): percent of its value.</summary>
    public int moveEmptyOwnAreaPercent = 50;

    // ---- attacking (action 0x3000)
    public int attackEnemyExtra = 120;
    public int attackNeutralExtra = 40;
    public int navyFortPenalty = 90;
    public int navyShoreBonus = 50;
    public int assaultBonus = 20;
    public int targetDefencePenalty = 20;
    public int commanderBonus = 30;
    public int carrierTargetBonus = 5;
    public int attackPlayerBonus = 40;
    /// <summary>Percent of the hp of the units behind the defender's front unit subtracted from the value (classic 0: only the front unit counts).</summary>
    public int attackStackHpPercent = 0;

    // ---- marching towards a target (action 0x4000)
    public int pathStayIfAreaValueAbove = 300;
    public int pathTechFactor = 5;
    public int pathBase = 30;
    public int pathStrongHp = 119;
    public int pathStrongBonus = 40;
    public int pathFullHealthBonus = 20;
    public int pathNavyBonus = 40;
    public int pathAssaultBonus = 20;
    public int pathCommanderBonus = 40;
    public int pathLevelBonus = 20;

    // ---- defending what matters (all 0 in classic)
    /// <summary>Defence weight of an own area: the sum of these for what the area is.</summary>
    public int defendCapital = 0, defendPort = 0, defendAirfield = 0, defendCity = 0, defendHome = 0;
    /// <summary>Threat = enemy hp around / own hp in the area, capped at this (in percent, 300 = 3x).</summary>
    public int threatCapPercent = 300;
    /// <summary>Staying in a threatened own area: + defence weight * threat * this / 100.</summary>
    public int defendStayPercent = 0;
    /// <summary>Moving into a threatened own area: + defence weight * threat * this / 100.</summary>
    public int defendReinforcePercent = 0;
    /// <summary>Taking the last unit out of a threatened own capital: - this.</summary>
    public int capitalLastUnitPenalty = 0;

    // ---- cards
    /// <summary>
    /// Random card category bands (percent rolls above each threshold): installation, air strike,
    /// construction, assault artillery, destroyer; below the last: infantry.
    /// </summary>
    public int[] cardBands = { 92, 79, 72, 59, 49 };
    /// <summary>No card when money or industry fell below this percent of the turn's start (classic 25).</summary>
    public int cardReservePercent = 25;
    /// <summary>Always buy when money and industry exceed this multiple of the price.</summary>
    public int cardAlwaysBuyMultiple = 4;
    /// <summary>Refusal chance (percent) with money up to 2x, up to 3x, above 3x the price.</summary>
    public int[] cardRefusePercent = { 70, 45, 20 };

    public int AreaTypeValue(int typeId) => typeId >= 0 && typeId < areaTypeValue.Length ? areaTypeValue[typeId] : 0;
    public int InstallationValue(int id) => id >= 0 && id < installationValue.Length ? installationValue[id] : 0;
    public int CardBand(int i) => i < cardBands.Length ? cardBands[i] : -1;
    public int CardRefuse(int i) => i < cardRefusePercent.Length ? cardRefusePercent[i] : 0;
    public bool DefendsAreas => defendStayPercent != 0 || defendReinforcePercent != 0 || capitalLastUnitPenalty != 0;
    public override string ToString() => id;
}

/// <summary>
/// The AI profiles (<c>data/json/ai/profiles.json</c>) and the one in use. <c>default</c> names the
/// profile new games use; <c>--ai NAME</c> on the desktop overrides it (the regression tests pin
/// "classic"). A profile may name a <c>base</c> profile and list only what it changes.
/// </summary>
public static class AiProfiles
{
    public static readonly Dictionary<string, AiProfile> profiles = new(StringComparer.OrdinalIgnoreCase);
    public static string defaultId = "classic";
    /// <summary>Profile forced from the command line (null: the data's default).</summary>
    public static string Override;
    public static AiProfile Current { get; private set; } = new();

    private static readonly System.Reflection.FieldInfo[] s_fields =
        typeof(AiProfile).GetFields().Where(f => f.Name != "id" && f.Name != "description").ToArray();

    public static void LoadBuiltIn()
    {
        profiles.Clear();
        profiles["classic"] = new AiProfile { id = "classic", description = "The original game's AI." };
        defaultId = "classic";
        Select();
    }

    /// <summary>Makes the profile of the command line, else the data's default, current.</summary>
    public static AiProfile Select(string id = null)
    {
        id ??= Override ?? defaultId;
        Current = profiles.TryGetValue(id ?? "", out var p) ? p : (profiles.TryGetValue("classic", out var c) ? c : new AiProfile());
        return Current;
    }

    public static bool LoadJson()
    {
        LoadBuiltIn();
        var root = JsonData.LoadMerged(DataPaths.AiProfiles);
        if (root?.Obj("profiles") is not JsonObject list) return false;
        defaultId = root.Str("default", "classic");
        var pending = list.Where(kv => kv.Value is JsonObject).Select(kv => (kv.Key, (JsonObject)kv.Value)).ToList();
        // a profile may build on another one ("base"); resolve in dependency order
        for (int guard = 0; pending.Count > 0 && guard < 16; guard++)
        {
            foreach (var (key, o) in pending.ToList())
            {
                string baseId = o.Str("base", key.Equals("classic", StringComparison.OrdinalIgnoreCase) ? null : "classic");
                AiProfile baseProfile = null;
                if (baseId != null && !profiles.TryGetValue(baseId, out baseProfile)) continue;   // base not loaded yet
                var p = baseProfile != null ? Clone(baseProfile) : new AiProfile();
                p.id = key;
                p.description = o.Str("description", "");
                Apply(p, o);
                profiles[key] = p;
                pending.Remove((key, o));
            }
        }
        foreach (var (key, _) in pending) Log($"AI profile {key}: its base profile is missing");
        Select();
        return true;
    }

    private static AiProfile Clone(AiProfile source)
    {
        var p = new AiProfile();
        foreach (var f in s_fields)
        {
            object v = f.GetValue(source);
            f.SetValue(p, v is int[] a ? a.ToArray() : v);
        }
        return p;
    }

    private static void Apply(AiProfile p, JsonObject o)
    {
        foreach (var f in s_fields)
        {
            var node = o.Get(f.Name);
            if (node == null) continue;
            if (f.FieldType == typeof(int)) f.SetValue(p, o.Int(f.Name, (int)f.GetValue(p)));
            else if (f.FieldType == typeof(int[]) && node is JsonArray arr)
                f.SetValue(p, arr.Select(n => TryParseInt(n?.Text(), out int v) ? v : 0).ToArray());
        }
        // area type and installation values may also be given by name ({"capital": 250, ...})
        if (o.Obj("areaTypeValueByName") is JsonObject byType)
            foreach (var (name, v) in byType)
                foreach (var t in Terrain.AreaTypes.Where(t => t.name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    if (t.id >= 0 && t.id < 16)
                    {
                        if (p.areaTypeValue.Length <= t.id) Array.Resize(ref p.areaTypeValue, t.id + 1);
                        p.areaTypeValue[t.id] = TryParseInt(v?.Text(), out int n) ? n : 0;
                    }
    }

    public static JsonObject BuiltInJson()
    {
        LoadBuiltIn();
        var classic = new JsonObject { ["description"] = "The original game's AI: every number as in the original." };
        var reference = new AiProfile();
        foreach (var f in s_fields)
        {
            object v = f.GetValue(reference);
            classic[f.Name] = v is int[] a ? new JsonArray(a.Select(x => (JsonNode)x).ToArray()) : JsonValue.Create((int)v);
        }
        return new JsonObject
        {
            ["format"] = 1,
            ["about"] = "AI profiles. classic holds the original game's numbers; the others name a base and list what they change. " +
                        "default is the profile new games use (--ai NAME overrides it). Field reference: DATA_SCHEMA.md#ai.",
            ["default"] = "classic",
            ["profiles"] = new JsonObject { ["classic"] = classic },
        };
    }

    /// <summary>The classic profile (for --verify-data: it must stay the original's numbers).</summary>
    public static object Snapshot()
    {
        profiles.TryGetValue("classic", out var c);
        c ??= new AiProfile();
        return s_fields.ToDictionary(f => f.Name, f => f.GetValue(c) is int[] a ? string.Join(",", a) : f.GetValue(c)?.ToString());
    }
}
