using System.Text.Json.Nodes;

namespace WC2;

/// <summary>A side of a scenario beyond the original three (scenario JSON <c>factions</c>).</summary>
public sealed class FactionDef
{
    public string id = "";
    /// <summary>The alliance number of the members (<see cref="Diplomacy.FirstFactionSide"/> and up).</summary>
    public int side;
    /// <summary>String table key of the faction's name (shown nowhere yet but kept for menus and saves).</summary>
    public string name = "";
    /// <summary>Unit base colour (0xAABBGGRR) or null for the alliance colour table.</summary>
    public uint? color;
    public string leader = "";
    public List<string> tags = new();
}

/// <summary>
/// War or peace between two groups of countries in a span of rounds (scenario JSON <c>relations</c>).
/// A group is a selector: <c>*</c> (everyone), <c>tag:&lt;tag&gt;</c>, <c>faction:&lt;id&gt;</c>,
/// <c>country:&lt;id&gt;</c>, or a bare id (a faction when one has that id, otherwise a country).
/// </summary>
public sealed class RelationDef
{
    public string a = "*";
    public string b = "*";
    public bool peace;
    /// <summary>First round (1-based) the relation holds.</summary>
    public int from = 1;
    /// <summary>The round it ends (exclusive); 0 = never.</summary>
    public int until;
    /// <summary>String table keys of the dialogues shown when the relation begins / ends, and the
    /// commander whose portrait speaks them.</summary>
    public string announceStart = "";
    public string announceEnd = "";
    public string commander = "";

    public bool ActiveAt(int round) => round >= from && (until <= 0 || round < until);
}

/// <summary>The diplomacy data of one scenario: factions, the tags and faction of every country, relations.</summary>
public sealed class ScenarioDiplomacy
{
    public List<FactionDef> factions = new();
    public Dictionary<string, string> countryFaction = new();
    public Dictionary<string, List<string>> countryTags = new();
    public List<RelationDef> relations = new();

    public bool IsEmpty => factions.Count == 0 && relations.Count == 0 && countryTags.Count == 0;

    public FactionDef Faction(string id) => factions.FirstOrDefault(f => f.id == id);

    /// <summary>
    /// Reads <c>factions</c> and <c>relations</c> of a scenario root:
    /// <code>
    /// "factions": [ { "id": "pact", "name": "faction pact", "color": [90, 90, 100], "leader": "de", "tags": ["bloc"] } ],
    /// "relations": [ { "between": ["pact", "atlantic"], "state": "peace", "from": 1, "until": 15,
    ///                  "announce": { "start": "text key", "end": "text key", "commander": "Guderian" } } ]
    /// </code>
    /// Country entries carry <c>"faction"</c> and <c>"tags"</c>; <see cref="ReadCountry"/> reads them.
    /// </summary>
    public static ScenarioDiplomacy Read(JsonObject root)
    {
        var d = new ScenarioDiplomacy();
        int side = Diplomacy.FirstFactionSide;
        foreach (var f in root.Arr("factions").Objects())
        {
            string id = f.Str("id");
            if (string.IsNullOrEmpty(id) || d.Faction(id) != null) continue;
            var def = new FactionDef { id = id, side = side++, name = f.Str("name") ?? id, leader = f.Str("leader") ?? "" };
            if (f.Arr("color") is JsonArray rgb && rgb.Count >= 3)
            {
                uint r = Byte(rgb[0]), g = Byte(rgb[1]), b = Byte(rgb[2]);
                def.color = (0xe2u << 24) | (b << 16) | (g << 8) | r;
            }
            foreach (var t in Strings(f.Arr("tags"))) if (!string.IsNullOrEmpty(t)) def.tags.Add(t);
            d.factions.Add(def);
        }
        foreach (var r in root.Arr("relations").Objects())
        {
            var between = Strings(r.Arr("between")).ToList();
            if (between.Count != 2) continue;
            var rel = new RelationDef
            {
                a = between[0], b = between[1],
                peace = (r.Str("state") ?? "war") == "peace",
                from = Math.Max(1, r.Int("from", 1)),
                until = Math.Max(0, r.Int("until", 0)),
            };
            if (r.Obj("announce") is JsonObject an)
            {
                rel.announceStart = an.Str("start") ?? "";
                rel.announceEnd = an.Str("end") ?? "";
                rel.commander = an.Str("commander") ?? "";
            }
            d.relations.Add(rel);
        }
        return d;
    }

    private static IEnumerable<string> Strings(JsonArray a) => a == null ? Enumerable.Empty<string>() : a.Select(n => n?.Text()).Where(t => t != null);

    private static uint Byte(JsonNode n) => (uint)Math.Clamp(int.TryParse(n?.Text(), out int v) ? v : 0, 0, 255);

    /// <summary>The faction and tags of one country entry; returns the alliance number of its faction (0: none).</summary>
    public int ReadCountry(string countryId, JsonObject c)
    {
        var tags = Strings(c.Arr("tags")).Where(t => !string.IsNullOrEmpty(t)).ToList();
        if (tags.Count > 0) countryTags[countryId] = tags;
        string faction = c.Str("faction");
        if (string.IsNullOrEmpty(faction)) return 0;
        var def = Faction(faction);
        if (def == null) return 0;
        countryFaction[countryId] = faction;
        return def.side;
    }
}

/// <summary>
/// The diplomacy of the running match (DATA_SCHEMA.md#scenarios, "Factions and relations"). The
/// original game knows three sides (alliance a, b, c), at war with each other, and neutral countries.
/// A scenario may add any number of factions (each one more side) and relations: war or peace between
/// two groups of countries for a span of rounds. Countries at peace cannot attack each other or enter
/// each other's areas, the AI does not count them as enemies, they do not encircle each other, and a
/// country at lasting peace with the player is not needed for victory. Members of one side are always
/// allies. Without relations every check returns at once, so the classic game is unchanged.
/// </summary>
public static class Diplomacy
{
    /// <summary>Alliance number of the first faction: 1-3 are a/b/c, 4 is neutral (original values).</summary>
    public const int FirstFactionSide = 5;

    private static ScenarioDiplomacy current = new();
    private static readonly Dictionary<(CCountry, CCountry), bool> cache = new();
    private static int cacheRound = int.MinValue;
    private static readonly Queue<(string text, string commander)> announcements = new();
    private static int announcedRound;

    public static ScenarioDiplomacy Current => current;

    /// <summary>Installs the diplomacy of a scenario (null clears it).</summary>
    public static void Set(ScenarioDiplomacy diplomacy, int announcedUpToRound = 0)
    {
        current = diplomacy ?? new ScenarioDiplomacy();
        cache.Clear();
        cacheRound = int.MinValue;
        announcements.Clear();
        announcedRound = announcedUpToRound;
    }

    public static void Clear() => Set(null);

    /// <summary>The round number the player sees (1-based).</summary>
    private static int Round => g_GameManager != null ? g_GameManager.currentTurnNumMinusOne + 1 : 1;

    /// <summary>Whether two countries of different sides are at peace in the current round.</summary>
    public static bool AtPeace(CCountry a, CCountry b) => AtPeace(a, b, Round);

    /// <summary>Whether two countries of different sides are at peace in a round (1-based).</summary>
    public static bool AtPeace(CCountry a, CCountry b, int round)
    {
        if (current.relations.Count == 0 || a == null || b == null || a == b || a.alliance == b.alliance) return false;
        if (round != cacheRound) { cache.Clear(); cacheRound = round; }
        if (cache.TryGetValue((a, b), out bool peace)) return peace;
        peace = PeaceAt(a, b, round);
        cache[(a, b)] = peace;
        cache[(b, a)] = peace;
        return peace;
    }

    /// <summary>Whether the two countries stay at peace for good (no relation turns them to war later).</summary>
    public static bool AtLastingPeace(CCountry a, CCountry b) => AtLastingPeace(a, b, Round);

    public static bool AtLastingPeace(CCountry a, CCountry b, int round)
    {
        if (current.relations.Count == 0 || a == null || b == null || a.alliance == b.alliance) return false;
        if (!PeaceAt(a, b, round)) return false;
        // every round where some relation starts or ends from now on
        foreach (var r in current.relations)
            foreach (int change in new[] { r.from, r.until })
                if (change > round && !PeaceAt(a, b, change)) return false;
        return true;
    }

    /// <summary>The last relation matching the pair that holds in the round decides (default: war).</summary>
    private static bool PeaceAt(CCountry a, CCountry b, int round)
    {
        bool peace = false;
        foreach (var r in current.relations)
        {
            if (!r.ActiveAt(round)) continue;
            if ((Matches(r.a, a) && Matches(r.b, b)) || (Matches(r.a, b) && Matches(r.b, a))) peace = r.peace;
        }
        return peace;
    }

    /// <summary>Whether a selector (<see cref="RelationDef"/>) names the country.</summary>
    public static bool Matches(string selector, CCountry c)
    {
        if (c == null || string.IsNullOrEmpty(selector)) return false;
        if (selector == "*") return true;
        if (selector.StartsWith("tag:", StringComparison.Ordinal))
            return HasTag(c, selector.Substring(4));
        if (selector.StartsWith("faction:", StringComparison.Ordinal))
            return FactionOf(c) == selector.Substring(8);
        if (selector.StartsWith("country:", StringComparison.Ordinal))
            return c.id == selector.Substring(8);
        return current.Faction(selector) != null ? FactionOf(c) == selector : c.id == selector;
    }

    public static string FactionOf(CCountry c) => c != null && current.countryFaction.TryGetValue(c.id, out var f) ? f : null;

    /// <summary>Tags of the country; a country also carries the tags of its faction.</summary>
    public static bool HasTag(CCountry c, string tag)
    {
        if (c == null) return false;
        if (current.countryTags.TryGetValue(c.id, out var tags) && tags.Contains(tag)) return true;
        string faction = FactionOf(c);
        return faction != null && current.Faction(faction) is FactionDef f && f.tags.Contains(tag);
    }

    /// <summary>The unit base colour of a faction side, or null (the alliance colour table).</summary>
    public static uint? SideColor(int side)
    {
        if (side < FirstFactionSide) return null;
        foreach (var f in current.factions) if (f.side == side) return f.color;
        return null;
    }

    /// <summary>
    /// The next relation change to announce in the current round (one per call, so the dialogues show
    /// one after another), or null. Each round's changes are queued once.
    /// </summary>
    public static (string text, string commander)? NextAnnouncement()
    {
        int round = Round;
        if (round > announcedRound)
        {
            announcedRound = round;
            foreach (var r in current.relations)
            {
                if (r.from == round && r.from > 1 && r.announceStart != "") announcements.Enqueue((r.announceStart, r.commander));
                if (r.until == round && r.announceEnd != "") announcements.Enqueue((r.announceEnd, r.commander));
            }
        }
        return announcements.Count > 0 ? announcements.Dequeue() : null;
    }

    /// <summary>Scenario JSON alliance text of a country: its faction id or the a/b/c/n letter.</summary>
    public static string AllianceText(CCountry c)
    {
        if (FactionOf(c) is string faction) return faction;
        return c.alliance switch { 1 => "a", 2 => "b", 3 => "c", _ => "n" };
    }
}
