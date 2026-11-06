using System.Text.Json.Nodes;

namespace WC2;

/// <summary>An area type of the map data (normal, capital, port, large city, normal city, or a modded one).</summary>
public sealed class AreaTypeDef
{
    public string name = "";
    public int id;
    /// <summary>City level the area has without any building (a capital is a level 3 city).</summary>
    public int cityLevel;
    /// <summary>Industry level the area has without any building.</summary>
    public int industryLevel;
    /// <summary>Strength every unit with movement left regains at the end of the turn (before the level bonus).</summary>
    public int healing = 3;
    /// <summary>Multiplier of that healing for navy units (ports repair ships twice as fast).</summary>
    public int navyHealingMultiplier = 1;
    /// <summary>Extra defence of units standing here (the larger of this and the installation's value applies).</summary>
    public int defence;
    /// <summary>Counts as a capital of its owner.</summary>
    public bool capital;
    /// <summary>Ships can be drafted here and the port is drawn.</summary>
    public bool port;
    /// <summary>Counts for the "core" defeat type: a country that holds no key area on land is defeated.</summary>
    public bool keyArea;
    /// <summary>An enemy area of this type only closes an encirclement when units garrison it.</summary>
    public bool encirclementNeedsGarrison;
    /// <summary>Added to the medal roll of fights in this area.</summary>
    public int medalBonus;
    // AI target values
    public int aiConstructionValue;
    public int aiAirStrikeValue;
    public int aiNuclearValue = 60;
    public int aiAirborneValue = 260;

    /// <summary>Inherent level of a construction type in this area (0 for airports and nothing).</summary>
    public int InherentLevel(CONSTRUCTION_TYPE construction) => construction switch
    {
        CONSTRUCTION_TYPE.CITY => cityLevel,
        CONSTRUCTION_TYPE.INDUSTRY => industryLevel,
        _ => 0,
    };
}

/// <summary>A building type: city, industry, airport.</summary>
public sealed class ConstructionDef
{
    public string name = "";
    public int id;
    public int maxLevel;
    /// <summary>Income per level: tax for cities, industry for industries.</summary>
    public int perLevel;
    /// <summary>The area counts at least this level for unit healing (an airport counts as level 1).</summary>
    public int healingLevel;
}

/// <summary>A defensive installation: fort, entrenchment, anti-aircraft gun, radar.</summary>
public sealed class InstallationDef
{
    public string name = "";
    public int id;
    /// <summary>Extra defence of units standing here.</summary>
    public int defence;
    /// <summary>The defenders' counter range is raised to at least this value (1000 = one area).</summary>
    public int minCounterRange;
    /// <summary>A unit attacking into this area loses all its movement unless it has the cross-entrenchment trait.</summary>
    public bool stopsAttackers;
    /// <summary>Units with the recv_fort_second_counter trait take a second counter attack here.</summary>
    public bool secondCounter;
    /// <summary>Ranged fire (projectile engagement, range of at least one area) does half damage here.</summary>
    public bool halvesRangedDamage;
    /// <summary>Percentage of air strike and bombing damage that gets through.</summary>
    public int airStrikeDamagePercent = 100;
    /// <summary>Bombing does not damage the area's building.</summary>
    public bool protectsFromBombing;
    /// <summary>Subtracted from the AI's air strike value of the area.</summary>
    public int aiAirStrikePenalty;
}

/// <summary>Terrain-wide numbers.</summary>
public sealed class TerrainRules
{
    /// <summary>Extra healing per city / industry level of the area.</summary>
    public int healingPerLevel = 3;
    /// <summary>Movement of every unit at sea at the start of its turn is limited to this.</summary>
    public int seaMaxMovement = 1;
    public int aiAirStrikeCityLevelWeight = 6;
    public int aiAirStrikeIndustryLevelWeight = 8;
    public int aiAirborneCityLevelWeight = 40;
    public int aiAirborneIndustryLevelWeight = 80;
}

/// <summary>
/// Terrain definitions (<c>data/terrain.json</c>): area types, constructions, installations and a
/// few terrain-wide numbers. The rules ask these definitions instead of testing for "capital",
/// "port", "fort" in code, so new area types and different values can come from data and mods.
/// The built-in table reproduces the original constants exactly; the JSON file starts as a copy of it.
/// </summary>
public static class Terrain
{
    private static readonly Dictionary<int, AreaTypeDef> s_areaTypes = new();
    private static readonly Dictionary<string, AreaTypeDef> s_areaTypesByName = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, ConstructionDef> s_constructions = new();
    private static readonly Dictionary<int, InstallationDef> s_installations = new();
    private static AreaTypeDef s_normal;
    private static readonly ConstructionDef s_noConstruction = new() { name = "none" };
    private static readonly InstallationDef s_noInstallation = new() { name = "none" };

    public static TerrainRules Rules { get; private set; } = new();
    public static IEnumerable<AreaTypeDef> AreaTypes => s_areaTypes.Values;
    public static IEnumerable<ConstructionDef> Constructions => s_constructions.Values;
    public static IEnumerable<InstallationDef> Installations => s_installations.Values;

    static Terrain() => LoadBuiltIn();

    public static AreaTypeDef Area(AREA_TYPE type) => s_areaTypes.TryGetValue((int)type, out var d) ? d : s_normal;
    public static ConstructionDef Construction(CONSTRUCTION_TYPE type) => s_constructions.TryGetValue((int)type, out var d) ? d : s_noConstruction;
    public static InstallationDef Installation(INSTALLATION_TYPE type) => s_installations.TryGetValue((int)type, out var d) ? d : s_noInstallation;

    /// <summary>Area type by its name in the map data ("capital", "large city", ...); unknown names are normal areas.</summary>
    public static AREA_TYPE ParseAreaType(string name) =>
        name != null && s_areaTypesByName.TryGetValue(name, out var d) ? (AREA_TYPE)d.id : AREA_TYPE.NORMAL;

    public static string AreaTypeName(AREA_TYPE type) => Area(type).name;

    /// <summary>The original values.</summary>
    public static void LoadBuiltIn()
    {
        s_areaTypes.Clear();
        s_areaTypesByName.Clear();
        s_constructions.Clear();
        s_installations.Clear();
        AddArea(new AreaTypeDef { name = "normal", id = 0, healing = 3, aiNuclearValue = 60, aiAirborneValue = 260 });
        AddArea(new AreaTypeDef
        {
            name = "capital", id = 1, cityLevel = 3, industryLevel = 2, healing = 9, defence = 1, capital = true, keyArea = true,
            encirclementNeedsGarrison = true, medalBonus = 12, aiConstructionValue = 30, aiAirStrikeValue = 20, aiNuclearValue = 80, aiAirborneValue = 360,
        });
        AddArea(new AreaTypeDef
        {
            name = "port", id = 2, healing = 6, navyHealingMultiplier = 2, port = true, keyArea = true,
            aiAirStrikeValue = 10, aiNuclearValue = 70, aiAirborneValue = 280,
        });
        AddArea(new AreaTypeDef
        {
            name = "large city", id = 3, cityLevel = 2, industryLevel = 1, healing = 7, keyArea = true, encirclementNeedsGarrison = true,
            medalBonus = 8, aiConstructionValue = 20, aiAirStrikeValue = 15, aiNuclearValue = 75, aiAirborneValue = 340,
        });
        AddArea(new AreaTypeDef
        {
            name = "normal city", id = 4, cityLevel = 1, healing = 5, keyArea = true,
            medalBonus = 4, aiConstructionValue = 10, aiAirStrikeValue = 10, aiNuclearValue = 70, aiAirborneValue = 310,
        });
        AddConstruction(new ConstructionDef { name = "city", id = (int)CONSTRUCTION_TYPE.CITY, maxLevel = 4, perLevel = 5 });
        AddConstruction(new ConstructionDef { name = "industry", id = (int)CONSTRUCTION_TYPE.INDUSTRY, maxLevel = 3, perLevel = 5 });
        AddConstruction(new ConstructionDef { name = "airport", id = (int)CONSTRUCTION_TYPE.AIRPORT, maxLevel = 1, healingLevel = 1 });
        AddInstallation(new InstallationDef { name = "fort", id = (int)INSTALLATION_TYPE.FORT, defence = 1, minCounterRange = 1000, secondCounter = true });
        AddInstallation(new InstallationDef { name = "entrenchment", id = (int)INSTALLATION_TYPE.ENTRENCHMENT, defence = 1, stopsAttackers = true });
        AddInstallation(new InstallationDef
        {
            name = "antiaircraft", id = (int)INSTALLATION_TYPE.ANTIAIRCRAFT, airStrikeDamagePercent = 30, protectsFromBombing = true, aiAirStrikePenalty = 100,
        });
        AddInstallation(new InstallationDef { name = "radar", id = (int)INSTALLATION_TYPE.RADAR, halvesRangedDamage = true, airStrikeDamagePercent = 50 });
        Rules = new TerrainRules();
    }

    private static void AddArea(AreaTypeDef d)
    {
        s_areaTypes[d.id] = d;
        s_areaTypesByName[d.name] = d;
        if (d.id == 0) s_normal = d;
    }

    private static void AddConstruction(ConstructionDef d) => s_constructions[d.id] = d;
    private static void AddInstallation(InstallationDef d) => s_installations[d.id] = d;

    // ------------------------------------------------------------------------------------
    // JSON
    // ------------------------------------------------------------------------------------

    /// <summary>Loads data/terrain.json over the built-in table; returns false when there is no file.</summary>
    public static bool LoadJson()
    {
        var root = JsonData.LoadMerged(DataPaths.Terrain);
        LoadBuiltIn();
        if (root == null) return false;
        foreach (var (name, o) in root.Obj("areaTypes").Entries())
        {
            int id = o.Int("id", -1);
            var d = id >= 0 && s_areaTypes.TryGetValue(id, out var existing) && existing.name == name ? existing : new AreaTypeDef();
            d.name = name;
            d.id = id >= 0 ? id : NextId(s_areaTypes.Keys, 5);
            d.cityLevel = o.Int("cityLevel", d.cityLevel);
            d.industryLevel = o.Int("industryLevel", d.industryLevel);
            d.healing = o.Int("healing", d.healing);
            d.navyHealingMultiplier = o.Int("navyHealingMultiplier", d.navyHealingMultiplier);
            d.defence = o.Int("defence", d.defence);
            d.capital = o.Bool("capital", d.capital);
            d.port = o.Bool("port", d.port);
            d.keyArea = o.Bool("keyArea", d.keyArea);
            d.encirclementNeedsGarrison = o.Bool("encirclementNeedsGarrison", d.encirclementNeedsGarrison);
            d.medalBonus = o.Int("medalBonus", d.medalBonus);
            var ai = o.Obj("ai");
            d.aiConstructionValue = ai.Int("construction", d.aiConstructionValue);
            d.aiAirStrikeValue = ai.Int("airStrike", d.aiAirStrikeValue);
            d.aiNuclearValue = ai.Int("nuclear", d.aiNuclearValue);
            d.aiAirborneValue = ai.Int("airborne", d.aiAirborneValue);
            AddArea(d);
        }
        foreach (var (name, o) in root.Obj("constructions").Entries())
        {
            var d = s_constructions.Values.FirstOrDefault(c => c.name == name) ?? new ConstructionDef { name = name, id = o.Int("id", 0) };
            d.maxLevel = o.Int("maxLevel", d.maxLevel);
            d.perLevel = o.Int("perLevel", d.perLevel);
            d.healingLevel = o.Int("healingLevel", d.healingLevel);
            if (d.id > 0) AddConstruction(d);
        }
        foreach (var (name, o) in root.Obj("installations").Entries())
        {
            var d = s_installations.Values.FirstOrDefault(c => c.name == name) ?? new InstallationDef { name = name, id = o.Int("id", 0) };
            d.defence = o.Int("defence", d.defence);
            d.minCounterRange = o.Int("minCounterRange", d.minCounterRange);
            d.stopsAttackers = o.Bool("stopsAttackers", d.stopsAttackers);
            d.secondCounter = o.Bool("secondCounter", d.secondCounter);
            d.halvesRangedDamage = o.Bool("halvesRangedDamage", d.halvesRangedDamage);
            d.airStrikeDamagePercent = o.Int("airStrikeDamagePercent", d.airStrikeDamagePercent);
            d.protectsFromBombing = o.Bool("protectsFromBombing", d.protectsFromBombing);
            d.aiAirStrikePenalty = o.Obj("ai").Int("airStrikePenalty", d.aiAirStrikePenalty);
            if (d.id > 0) AddInstallation(d);
        }
        var rules = root.Obj("rules");
        var r = new TerrainRules();
        r.healingPerLevel = rules.Int("healingPerLevel", r.healingPerLevel);
        r.seaMaxMovement = rules.Int("seaMaxMovement", r.seaMaxMovement);
        var aiRules = rules.Obj("ai");
        r.aiAirStrikeCityLevelWeight = aiRules.Int("airStrikeCityLevelWeight", r.aiAirStrikeCityLevelWeight);
        r.aiAirStrikeIndustryLevelWeight = aiRules.Int("airStrikeIndustryLevelWeight", r.aiAirStrikeIndustryLevelWeight);
        r.aiAirborneCityLevelWeight = aiRules.Int("airborneCityLevelWeight", r.aiAirborneCityLevelWeight);
        r.aiAirborneIndustryLevelWeight = aiRules.Int("airborneIndustryLevelWeight", r.aiAirborneIndustryLevelWeight);
        Rules = r;
        return true;
    }

    private static int NextId(IEnumerable<int> used, int start)
    {
        var set = new HashSet<int>(used);
        int id = start;
        while (set.Contains(id)) id++;
        return id;
    }

    /// <summary>The built-in table as JSON (the starting point of data/terrain.json).</summary>
    public static JsonObject BuiltInJson()
    {
        LoadBuiltIn();
        var areas = new JsonObject();
        foreach (var d in s_areaTypes.Values.OrderBy(d => d.id))
        {
            areas[d.name] = new JsonObject
            {
                ["id"] = d.id,
                ["cityLevel"] = d.cityLevel,
                ["industryLevel"] = d.industryLevel,
                ["healing"] = d.healing,
                ["navyHealingMultiplier"] = d.navyHealingMultiplier,
                ["defence"] = d.defence,
                ["capital"] = d.capital,
                ["port"] = d.port,
                ["keyArea"] = d.keyArea,
                ["encirclementNeedsGarrison"] = d.encirclementNeedsGarrison,
                ["medalBonus"] = d.medalBonus,
                ["ai"] = new JsonObject
                {
                    ["construction"] = d.aiConstructionValue,
                    ["airStrike"] = d.aiAirStrikeValue,
                    ["nuclear"] = d.aiNuclearValue,
                    ["airborne"] = d.aiAirborneValue,
                },
            };
        }
        var constructions = new JsonObject();
        foreach (var d in s_constructions.Values.OrderBy(d => d.id))
            constructions[d.name] = new JsonObject { ["id"] = d.id, ["maxLevel"] = d.maxLevel, ["perLevel"] = d.perLevel, ["healingLevel"] = d.healingLevel };
        var installations = new JsonObject();
        foreach (var d in s_installations.Values.OrderBy(d => d.id))
        {
            installations[d.name] = new JsonObject
            {
                ["id"] = d.id,
                ["defence"] = d.defence,
                ["minCounterRange"] = d.minCounterRange,
                ["stopsAttackers"] = d.stopsAttackers,
                ["secondCounter"] = d.secondCounter,
                ["halvesRangedDamage"] = d.halvesRangedDamage,
                ["airStrikeDamagePercent"] = d.airStrikeDamagePercent,
                ["protectsFromBombing"] = d.protectsFromBombing,
                ["ai"] = new JsonObject { ["airStrikePenalty"] = d.aiAirStrikePenalty },
            };
        }
        var r = Rules;
        return new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Terrain definitions: area types (as named in data/json/maps/<id>/areas.json), buildings, installations and terrain-wide numbers. These values reproduce the original game; field reference: DATA_SCHEMA.md#terrain.",
            ["areaTypes"] = areas,
            ["constructions"] = constructions,
            ["installations"] = installations,
            ["rules"] = new JsonObject
            {
                ["healingPerLevel"] = r.healingPerLevel,
                ["seaMaxMovement"] = r.seaMaxMovement,
                ["ai"] = new JsonObject
                {
                    ["airStrikeCityLevelWeight"] = r.aiAirStrikeCityLevelWeight,
                    ["airStrikeIndustryLevelWeight"] = r.aiAirStrikeIndustryLevelWeight,
                    ["airborneCityLevelWeight"] = r.aiAirborneCityLevelWeight,
                    ["airborneIndustryLevelWeight"] = r.aiAirborneIndustryLevelWeight,
                },
            },
        };
    }

    /// <summary>Snapshot of the current tables for --verify-data.</summary>
    public static object Snapshot() => new
    {
        areas = s_areaTypes.OrderBy(k => k.Key).Select(k => k.Value).ToList(),
        constructions = s_constructions.OrderBy(k => k.Key).Select(k => k.Value).ToList(),
        installations = s_installations.OrderBy(k => k.Key).Select(k => k.Value).ToList(),
        rules = Rules,
    };
}
