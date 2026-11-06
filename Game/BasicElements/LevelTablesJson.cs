using System.Text.Json.Nodes;

namespace WC2;

/// <summary>
/// Unit levels ("levels" in data/units.json) and commander levels ("levels" in data/commanders.json).
/// <code>
/// "levels": [ { "attack": 0, "defence": 0, "turnRecovery": 0, "instantRecovery": 0, "maxHpPercent": 0, "expToNext": 90 }, ... ]   // units, 5 levels
/// "levels": [ { "attack": 0, ..., "maxHpPercent": 5, "coolDown": 4, "medalsToNext": 5 }, ... ]                                   // commanders, 15 levels
/// </code>
/// Missing levels and fields keep the built-in values.
/// </summary>
public static class LevelTablesJson
{
    public static void ReadUnitLevels(JsonArray levels)
    {
        if (levels == null) { CArmy.SetLevelTable(null, null); return; }
        var builtIn = CArmy.BuiltInLevels();
        var builtInExp = CArmy.BuiltInLevelUpExperience();
        var table = new List<ArmyAbility>();
        var exp = new List<int>();
        int i = 0;
        foreach (var o in levels.Objects())
        {
            var b = i < builtIn.Length ? builtIn[i] : new ArmyAbility();
            table.Add(ReadAbility(o, b));
            exp.Add(o.Int("expToNext", i < builtInExp.Length ? builtInExp[i] : 0));
            i++;
        }
        CArmy.SetLevelTable(table, exp);
    }

    public static void ReadCommanderLevels(JsonArray levels)
    {
        if (levels == null) { CCommander.SetLevelTable(null, null); return; }
        var builtIn = CCommander.BuiltInLevels();
        var builtInCosts = CCommander.BuiltInUpgradeMedalCosts();
        var table = new List<CommanderAbility>();
        var costs = new List<int>();
        int i = 0;
        foreach (var o in levels.Objects())
        {
            var b = i < builtIn.Length ? builtIn[i] : new CommanderAbility();
            table.Add(new CommanderAbility { armyAbility = ReadAbility(o, b.armyAbility), coolDown = o.Int("coolDown", b.coolDown) });
            costs.Add(o.Int("medalsToNext", i < builtInCosts.Length ? builtInCosts[i] : 0));
            i++;
        }
        CCommander.SetLevelTable(table, costs);
    }

    private static ArmyAbility ReadAbility(JsonObject o, ArmyAbility b) => new(
        o.Int("attack", b.attack),
        o.Int("defence", b.defence),
        o.Int("turnRecovery", b.turnRecovery),
        o.Int("instantRecovery", b.instantRecovery),
        o.Int("maxHpPercent", b.maxHpPercentage));

    public static JsonArray UnitLevelsJson()
    {
        var levels = CArmy.BuiltInLevels();
        var exp = CArmy.BuiltInLevelUpExperience();
        var arr = new JsonArray();
        for (int i = 0; i < levels.Length; i++)
        {
            var o = AbilityJson(levels[i]);
            if (i < exp.Length) o["expToNext"] = exp[i];
            arr.Add(o);
        }
        return arr;
    }

    public static JsonArray CommanderLevelsJson()
    {
        var levels = CCommander.BuiltInLevels();
        var costs = CCommander.BuiltInUpgradeMedalCosts();
        var arr = new JsonArray();
        for (int i = 0; i < levels.Length; i++)
        {
            var o = AbilityJson(levels[i].armyAbility);
            o["coolDown"] = levels[i].coolDown;
            if (i < costs.Length) o["medalsToNext"] = costs[i];
            arr.Add(o);
        }
        return arr;
    }

    private static JsonObject AbilityJson(ArmyAbility a) => new()
    {
        ["attack"] = a.attack,
        ["defence"] = a.defence,
        ["turnRecovery"] = a.turnRecovery,
        ["instantRecovery"] = a.instantRecovery,
        ["maxHpPercent"] = a.maxHpPercentage,
    };

    /// <summary>Current tables, for --verify-data.</summary>
    public static object Snapshot() => new
    {
        units = CArmy.LevelTable.ToList(),
        exp = CArmy.LevelUpExperience.ToList(),
        commanders = CCommander.LevelTable.ToList(),
        medals = CCommander.UpgradeMedalCosts.ToList(),
    };
}
