using System.Globalization;
using System.Text.Json;

namespace WC2;

/// <summary>
/// Writes the rule-relevant game state as stable, indented JSON: turn, commander, every country and
/// every area with its units. Golden tests compare these dumps between builds, so presentation-only
/// fields (timers, offsets, selection, animation) are left out on purpose.
/// </summary>
public static class GameStateDigest
{
    public static string Build()
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            var gm = g_GameManager;
            w.WriteStartObject();
            w.WriteString("mode", gm.gameMode.ToString());
            w.WriteString("battle", gm.battleFileName ?? "");
            w.WriteNumber("turn", gm.currentTurnNumMinusOne + 1);
            w.WriteNumber("currentCountryIndex", gm.currentCountryIndex);
            w.WriteBoolean("result", gm.result);
            w.WriteBoolean("resultWin", gm.resultWin);
            w.WriteNumber("randSeed", GetRandSeed());
            w.WriteNumber("aiRandSeed", GetAIRandSeed());

            w.WritePropertyName("commander");
            w.WriteStartObject();
            w.WriteNumber("level", g_Commander.level);
            w.WriteNumber("medal", g_Commander.medal);
            WriteInts(w, "warMedal", g_Commander.warMedal);
            WriteInts(w, "campaignProgress", g_Commander.campaignProgress);
            w.WriteEndObject();

            w.WritePropertyName("countries");
            w.WriteStartArray();
            foreach (var c in gm.allCountry)
            {
                w.WriteStartObject();
                w.WriteString("id", c.id);
                w.WriteNumber("alliance", c.alliance);
                w.WriteString("defeatType", c.defeatType.ToString());
                w.WriteBoolean("ai", c.ai);
                w.WriteBoolean("defeated", c.defeated);
                w.WriteNumber("money", c.money);
                w.WriteNumber("industry", c.industry);
                w.WriteString("taxRate", c.taxRate.ToString("R", CultureInfo.InvariantCulture));
                w.WriteNumber("tech", c.tech);
                w.WriteNumber("techTurn", c.techTurn);
                w.WriteString("commander", c.commanderDef?.name ?? "");
                w.WriteNumber("commanderTurn", c.commanderTurn);
                w.WriteBoolean("commanderAlive", c.commanderAlive);
                w.WriteBoolean("borrowedLoan", c.borrowedLoan);
                WriteInts(w, "areas", c.areaIdList);
                WriteInts(w, "capitals", c.capitalIdList);
                WriteInts(w, "cardRound", c.cardRound);
                WriteInts(w, "cardTimes", c.cardTimes);
                WriteInts(w, "warMedal", c.warMedal);
                WriteInts(w, "destroyCount", c.destroyCount);
                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WritePropertyName("areas");
            w.WriteStartArray();
            foreach (var area in g_Scene.areaList)
            {
                if (area == null) continue;
                bool interesting = area.country != null || area.armyCount > 0 || area.info.constructionLevel > 0
                                   || area.installationType != INSTALLATION_TYPE.NO_INSTALLATION;
                if (!interesting) continue;
                w.WriteStartObject();
                w.WriteNumber("id", area.id);
                w.WriteString("owner", area.country?.id ?? "");
                w.WriteString("construction", area.info.constructionType.ToString());
                w.WriteNumber("level", area.info.constructionLevel);
                w.WriteString("installation", area.installationType.ToString());
                w.WritePropertyName("units");
                w.WriteStartArray();
                for (int i = 0; i < area.armyCount && i < area.army.Length; i++)
                {
                    var a = area.army[i];
                    if (a == null) continue;
                    w.WriteStartObject();
                    w.WriteNumber("type", a.basicAbilities?.id ?? -1);
                    w.WriteString("country", a.country?.id ?? "");
                    w.WriteNumber("hp", a.hp);
                    w.WriteNumber("maxHp", a.maxHp);
                    w.WriteNumber("movement", a.movement);
                    w.WriteNumber("level", a.level);
                    w.WriteNumber("exp", a.exp);
                    w.WriteNumber("morale", a.morale);
                    w.WriteNumber("cards", a.cards);
                    w.WriteBoolean("active", a.active);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static void Write(string path)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, Build() + "\n", new UTF8Encoding(false));
        Log("State dump saved: " + path);
    }

    private static void WriteInts(Utf8JsonWriter w, string name, IEnumerable<int> values)
    {
        w.WritePropertyName(name);
        w.WriteStartArray();
        if (values != null) foreach (int v in values) w.WriteNumberValue(v);
        w.WriteEndArray();
    }
}
