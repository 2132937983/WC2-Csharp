using System.Text.Json.Nodes;

namespace WC2;

/// <summary>
/// Global rule numbers (<c>data/rules.json</c>) that were constants in the code. The built-in values
/// reproduce the original game.
/// <code>
/// { "combat": { "diceThresholds": [50, 25, 15, 5] }, "economy": { "moneyCap": 99999, "industryCap": 99999 } }
/// </code>
/// </summary>
public static class GameRules
{
    /// <summary>
    /// Strength percentages: a unit rolls one die plus one per threshold its strength is above
    /// (above 50 % five dice, above 25 % four, above 15 % three, above 5 % two, otherwise one).
    /// </summary>
    public static int[] diceThresholds = { 50, 25, 15, 5 };
    public static int moneyCap = 99999;
    public static int industryCap = 99999;

    public static void LoadBuiltIn()
    {
        diceThresholds = new[] { 50, 25, 15, 5 };
        moneyCap = 99999;
        industryCap = 99999;
    }

    /// <summary>Number of combat dice for a strength percentage.</summary>
    public static int Dice(int strengthPercent)
    {
        int dice = 1;
        foreach (int threshold in diceThresholds)
            if (strengthPercent > threshold) dice++;
        return dice;
    }

    public static bool LoadJson()
    {
        var root = JsonData.LoadMerged(DataPaths.Rules);
        LoadBuiltIn();
        if (root == null) return false;
        if (root.Obj("combat")?.Arr("diceThresholds") is JsonArray thresholds)
            diceThresholds = thresholds.Select(t => TryParseInt(t?.Text(), out int v) ? v : int.MaxValue).Where(v => v != int.MaxValue).ToArray();
        var economy = root.Obj("economy");
        moneyCap = economy.Int("moneyCap", moneyCap);
        industryCap = economy.Int("industryCap", industryCap);
        return true;
    }

    public static JsonObject BuiltInJson()
    {
        LoadBuiltIn();
        return new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Global rule numbers (the values reproduce the original game). Field reference: DATA_SCHEMA.md#rules.",
            ["combat"] = new JsonObject { ["diceThresholds"] = new JsonArray(diceThresholds.Select(t => (JsonNode)t).ToArray()) },
            ["economy"] = new JsonObject { ["moneyCap"] = moneyCap, ["industryCap"] = industryCap },
        };
    }

    public static object Snapshot() => new { dice = diceThresholds.ToList(), moneyCap, industryCap };
}
