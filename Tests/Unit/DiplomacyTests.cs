using System.Text.Json.Nodes;

namespace WC2.Tests;

/// <summary>
/// Factions and relations of a scenario (Game/Data/Diplomacy.cs, DATA_SCHEMA.md#scenarios): sides beyond
/// the original a/b/c, selectors, rounds, the last matching relation winning; the original scenarios
/// (no relations) unchanged; and the sample total conversion samples/mods/world-1962.
/// </summary>
[Collection("assets")]
public class DiplomacyTests
{
    /// <summary>Countries of a scenario root, with the alliance numbers the loader gives them.</summary>
    private static Dictionary<string, CCountry> Install(JsonObject root)
    {
        var diplomacy = ScenarioDiplomacy.Read(root);
        var countries = new Dictionary<string, CCountry>();
        foreach (var c in root.Arr("countries").Objects())
        {
            string id = c.Str("id");
            int side = diplomacy.ReadCountry(id, c);
            countries[id] = new CCountry { id = id, name = c.Str("name") ?? id, alliance = side > 0 ? side : BattleScenario.ParseAlliance(c.Str("alliance")) };
        }
        Diplomacy.Set(diplomacy);
        return countries;
    }

    [Fact]
    public void WithoutRelationsEverythingIsAsInTheOriginal()
    {
        var c = Install(Support.Json("""
            { "countries": [ { "id": "de", "alliance": "a" }, { "id": "gb", "alliance": "b" }, { "id": "ch", "alliance": "n" } ] }
            """).AsObject());
        Assert.True(Diplomacy.Current.IsEmpty);
        Assert.Equal(1, c["de"].alliance);
        Assert.Equal((int)SPECIAL_ALLIENCE_TYPE.NEUTRAL, c["ch"].alliance);
        foreach (var a in c.Values)
            foreach (var b in c.Values)
            {
                Assert.False(Diplomacy.AtPeace(a, b, 1));
                Assert.False(Diplomacy.AtLastingPeace(a, b, 1));
            }
        Assert.Null(Diplomacy.SideColor(1));
        Assert.Null(Diplomacy.NextAnnouncement());
        Diplomacy.Clear();
    }

    [Fact]
    public void FactionsAreSidesAndRelationsDecideWarAndPeaceByRound()
    {
        var c = Install(Support.Json("""
            {
              "factions": [ { "id": "north", "color": [10, 20, 30], "tags": ["bloc"] }, { "id": "south" }, { "id": "calm" } ],
              "relations": [
                { "between": ["north", "south"], "state": "peace", "until": 5, "announce": { "end": "war text", "commander": "Guderian" } },
                { "between": ["calm", "*"], "state": "peace" },
                { "between": ["tag:raider", "*"], "state": "war" },
                { "between": ["country:s2", "north"], "state": "war", "from": 3 }
              ],
              "countries": [
                { "id": "n1", "faction": "north" }, { "id": "n2", "faction": "north" },
                { "id": "s1", "faction": "south" }, { "id": "s2", "faction": "south" },
                { "id": "c1", "faction": "calm" }, { "id": "r1", "faction": "calm", "tags": ["raider"] },
                { "id": "old", "alliance": "a" }
              ]
            }
            """).AsObject());
        // each faction is a side of its own after the original four numbers; members are allies
        Assert.Equal(Diplomacy.FirstFactionSide, c["n1"].alliance);
        Assert.Equal(c["n1"].alliance, c["n2"].alliance);
        Assert.Equal(Diplomacy.FirstFactionSide + 1, c["s1"].alliance);
        Assert.Equal(1, c["old"].alliance);
        Assert.Equal(0xe21e140au, Diplomacy.SideColor(c["n1"].alliance));
        Assert.True(Diplomacy.HasTag(c["n2"], "bloc"));          // a faction's tags are its members'
        Assert.False(Diplomacy.AtPeace(c["n1"], c["n2"], 1));    // allies, never "at peace"

        // north and south: peace in rounds 1-4, war from round 5
        Assert.True(Diplomacy.AtPeace(c["n1"], c["s1"], 1));
        Assert.True(Diplomacy.AtPeace(c["s1"], c["n2"], 4));
        Assert.False(Diplomacy.AtPeace(c["n1"], c["s1"], 5));
        Assert.False(Diplomacy.AtLastingPeace(c["n1"], c["s1"], 1));
        // a later, more specific relation wins: s2 fights north from round 3
        Assert.True(Diplomacy.AtPeace(c["s2"], c["n1"], 2));
        Assert.False(Diplomacy.AtPeace(c["s2"], c["n1"], 3));
        // calm is at peace with everyone for good, except its raider (a tag rule after it)
        Assert.True(Diplomacy.AtLastingPeace(c["c1"], c["old"], 1));
        Assert.True(Diplomacy.AtLastingPeace(c["c1"], c["s2"], 40));
        Assert.False(Diplomacy.AtPeace(c["r1"], c["s1"], 1));
        // an original side without a relation is at war with the factions
        Assert.False(Diplomacy.AtPeace(c["old"], c["n1"], 1));
        Diplomacy.Clear();
    }

    private static JsonObject SampleScenario()
    {
        string path = Path.Combine(Support.Repo, "samples", "mods", "world-1962", "data", "json", "scenarios", "conquest_12.json");
        return JsonData.Parse(File.ReadAllText(path)).AsObject();
    }

    [Theory]
    // the blocs: the Pacific truce ends in round 10, the Atlantic one in 15, the Axis splits in 25
    [InlineData("ja", "am", 9, true)]
    [InlineData("ja", "am", 10, false)]
    [InlineData("de", "gb", 14, true)]
    [InlineData("es", "pt", 15, false)]
    [InlineData("de", "ja", 24, true)]
    [InlineData("it", "man", 25, false)]
    // the Russian successor states fight the Reich and each other, not the Atlantic or the Sphere ...
    [InlineData("sov", "msk", 1, false)]
    [InlineData("sib", "sov", 1, false)]
    [InlineData("sib", "ja", 1, true)]
    [InlineData("ala", "am", 30, true)]
    // ... except the Far Eastern Republic, at war with Japan, as is the Republic of China
    [InlineData("fer", "ja", 1, false)]
    [InlineData("roc", "nkg", 1, false)]
    [InlineData("roc", "de", 1, true)]
    // the Non-Aligned are at peace with everyone, always
    [InlineData("in", "ja", 40, true)]
    [InlineData("ch", "de", 40, true)]
    [InlineData("tib", "roc", 40, true)]
    public void TheSampleWorldOf1962HasItsWarsAndTruces(string a, string b, int round, bool peace)
    {
        var c = Install(SampleScenario());
        Assert.Equal(peace, Diplomacy.AtPeace(c[a], c[b], round));
        Diplomacy.Clear();
    }

    [Fact]
    public void TheSampleIsComplete()
    {
        var root = SampleScenario();
        var c = Install(root);
        Assert.Equal(10, Diplomacy.Current.factions.Count);
        // every country belongs to a faction, and the factions are distinct sides
        Assert.All(c.Values, x => Assert.True(x.alliance >= Diplomacy.FirstFactionSide));
        Assert.Equal(10, c.Values.Select(x => x.alliance).Distinct().Count());
        // the Non-Aligned need not be defeated, the blocs must
        Assert.True(Diplomacy.AtLastingPeace(c["se"], c["de"], 1));
        Assert.False(Diplomacy.AtLastingPeace(c["am"], c["de"], 1));
        // every area names a country of the scenario
        var ids = c.Keys.ToHashSet();
        Assert.All(root.Arr("areas").Objects(), a => Assert.Contains(a.Str("country"), ids));
        // a new country (art = its id, unknown to the game) is registered and brings its pictures
        string mod = Path.Combine(Support.Repo, "samples", "mods", "world-1962");
        var registered = JsonData.Parse(File.ReadAllText(Path.Combine(mod, "data", "json", "countries", "countries.json")))["countries"]!.AsObject();
        var builtIn = CountryDefs.BuiltInJson()["countries"]!.AsObject();
        foreach (var x in c.Values.Where(x => !builtIn.ContainsKey(x.name)))
        {
            Assert.True(registered.ContainsKey(x.name), x.name + " is not registered");
            foreach (var picture in new[] { $"flag_{x.name}.png", $"button_{x.name}@2x.png", $"sflag_{x.name}@2x.png" })
                Assert.True(File.Exists(Path.Combine(mod, picture)), picture + " is missing");
        }
        Diplomacy.Clear();
    }

    private static string War1756 => Path.Combine(Support.Repo, "mods", "seven-years-war-1756");

    private static JsonObject War1756Scenario() =>
        JsonData.Parse(File.ReadAllText(Path.Combine(War1756, "data", "json", "scenarios", "conquest_11.json"))).AsObject();

    [Theory]
    // Britain and France are at war in the colonies from the start; Prussia strikes in round 3, Russia joins
    // in 5, Sweden in 6, Spain in 12; the neutral powers stay at peace with everyone
    [InlineData("gb", "fr", 1, false)]
    [InlineData("pr", "hab", 2, true)]
    [InlineData("pr", "sax", 3, false)]
    [InlineData("gb", "ru", 4, true)]
    [InlineData("pr", "ru", 5, false)]
    [InlineData("pr", "se", 5, true)]
    [InlineData("han", "se", 6, false)]
    [InlineData("gb", "es", 11, true)]
    [InlineData("gb", "es", 12, false)]
    [InlineData("ott", "ru", 30, true)]
    [InlineData("qin", "gb", 30, true)]
    [InlineData("dkn", "se", 30, true)]
    [InlineData("plc", "pr", 30, true)]
    public void TheSevenYearsWarBreaksOutRoundByRound(string a, string b, int round, bool peace)
    {
        var c = Install(War1756Scenario());
        Assert.Equal(peace, Diplomacy.AtPeace(c[a], c[b], round));
        Diplomacy.Clear();
    }

    [Fact]
    public void TheSevenYearsWarFliesItsOwnFlags()
    {
        var root = War1756Scenario();
        var c = Install(root);
        // the neutral powers need not fall; the enemy alliance must
        Assert.True(Diplomacy.AtLastingPeace(c["ott"], c["gb"], 1));
        Assert.False(Diplomacy.AtLastingPeace(c["fr"], c["gb"], 1));
        // a country with the game's art and a flag of the period flies it under this scenario's own key, so the
        // game's flag of that art stays everywhere else; every flag has its three pictures in the mod
        var builtIn = CountryDefs.BuiltInJson()["countries"]!.AsObject();
        foreach (var country in root.Arr("countries").Objects())
        {
            string art = country.Str("name"), key = country.Str("flag") ?? art;
            if (country.Str("flag") is string flag) Assert.False(builtIn.ContainsKey(flag), flag + " would replace the game's flag");
            else Assert.False(builtIn.ContainsKey(art), art + " has no flag of the period");
            foreach (var picture in new[] { $"flag_{key}.png", $"button_{key}@2x.png", $"sflag_{key}@2x.png" })
                Assert.True(File.Exists(Path.Combine(War1756, picture)), picture + " is missing");
        }
        Diplomacy.Clear();
    }
}
