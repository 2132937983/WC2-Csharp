using System.Globalization;
using System.Text.Json.Nodes;

namespace WC2;

/// <summary>
/// Exports the running battle as a scenario JSON (the format of scenarios/*.json). The pause box
/// Save button writes it to the documents folder as game.json; copied into a mod as
/// scenarios/&lt;name&gt;.json it replaces (or adds) a scenario.
/// </summary>
public sealed partial class CGameManager
{
    public void SaveBattleJson(string fileName)
    {
        var countries = new JsonArray();
        for (int i = 0; i < GetNumCountries(); i++)
        {
            CCountry c = GetCountryByIndex(i);
            var o = new JsonObject { ["id"] = c.id, ["name"] = c.name };
            if (!string.IsNullOrEmpty(c.flag)) o["flag"] = c.flag;
            if (!string.IsNullOrEmpty(c.GetCommanderName())) o["commander"] = c.GetCommanderName();
            o["ai"] = c.ai ? 1 : 0;
            // a faction member names its faction, the others the a/b/c/n side
            if (Diplomacy.FactionOf(c) is string faction) o["faction"] = faction;
            else o["alliance"] = Diplomacy.AllianceText(c);
            if (Diplomacy.Current.countryTags.TryGetValue(c.id, out var tags))
                o["tags"] = new JsonArray(tags.Select(t => (JsonNode)t).ToArray());
            o["defeated"] = c.defeatType switch { DEFEAT_TYPE.ARMY => "army", DEFEAT_TYPE.CORE => "core", _ => "land" };
            o["money"] = c.money;
            o["industry"] = c.industry;
            o["techLevel"] = c.tech;
            o["taxFactor"] = JsonData.Value(c.taxRate.ToString("R", CultureInfo.InvariantCulture));
            o["r"] = c.r;
            o["g"] = c.g;
            o["b"] = c.b;
            o["a"] = c.a;
            countries.Add(o);
        }

        var areas = new JsonArray();
        for (int i = 0; i < g_Scene.GetNumAreas(); i++)
        {
            CArea area = g_Scene.GetArea(i);
            if (area == null || !area.info.enable || area.country == null) continue;
            var o = new JsonObject { ["id"] = area.id, ["country"] = area.country.id };
            string construction = area.info.constructionType switch
            {
                CONSTRUCTION_TYPE.CITY => "city",
                CONSTRUCTION_TYPE.INDUSTRY => "industry",
                CONSTRUCTION_TYPE.AIRPORT => "airport",
                _ => null,
            };
            if (construction != null)
            {
                o["construction"] = construction;
                o["level"] = area.info.constructionLevel;
            }
            string installation = Terrain.Installation(area.installationType).name;
            if (area.installationType != INSTALLATION_TYPE.NO_INSTALLATION) o["installation"] = installation;
            var units = new JsonArray();
            for (int j = 0; j < area.armyCount; j++)
            {
                CArmy army = area.GetArmy(j);
                string typeName = armydef_internal.GetArmyTypeName(army.basicAbilities.id);
                if (string.IsNullOrEmpty(typeName)) typeName = army.basicAbilities.name;
                if (string.IsNullOrEmpty(typeName)) typeName = "infantry";
                units.Add(new JsonObject { ["type"] = typeName, ["level"] = army.level, ["cards"] = army.cards });
            }
            if (units.Count > 0) o["units"] = units;
            areas.Add(o);
        }

        var dialogues = new JsonArray();
        for (int i = 0; i < GetNumDialogues(); i++)
        {
            DialogueDef d = GetDialogueByIndex(i);
            dialogues.Add(new JsonObject { ["commander"] = d.commander, ["index"] = d.index, ["atRound"] = d.atRound, ["left"] = d.left });
        }

        var root = new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Battle exported from a running game (turn " + (currentTurnNumMinusOne + 1) + ", " + battleFileName + "). Copy it into a mod as scenarios/<name>.json to play it. Field reference: DATA_SCHEMA.md#scenarios.",
            ["map"] = mapId,
        };
        if (!string.IsNullOrEmpty(areasEnable)) root["areasEnable"] = areasEnable;
        var diplomacy = Diplomacy.Current;
        if (diplomacy.factions.Count > 0)
        {
            root["factions"] = new JsonArray(diplomacy.factions.Select(f =>
            {
                var o = new JsonObject { ["id"] = f.id, ["name"] = f.name };
                if (f.color is uint c) o["color"] = new JsonArray((int)(c & 0xff), (int)((c >> 8) & 0xff), (int)((c >> 16) & 0xff));
                if (f.leader != "") o["leader"] = f.leader;
                if (f.tags.Count > 0) o["tags"] = new JsonArray(f.tags.Select(t => (JsonNode)t).ToArray());
                return (JsonNode)o;
            }).ToArray());
        }
        if (diplomacy.relations.Count > 0)
        {
            root["relations"] = new JsonArray(diplomacy.relations.Select(r =>
            {
                var o = new JsonObject { ["between"] = new JsonArray(r.a, r.b), ["state"] = r.peace ? "peace" : "war" };
                if (r.from > 1) o["from"] = r.from;
                if (r.until > 0) o["until"] = r.until;
                if (r.announceStart != "" || r.announceEnd != "")
                    o["announce"] = new JsonObject { ["start"] = r.announceStart, ["end"] = r.announceEnd, ["commander"] = r.commander };
                return (JsonNode)o;
            }).ToArray());
        }
        root["countries"] = countries;
        root["areas"] = areas;
        if (dialogues.Count > 0) root["dialogues"] = dialogues;
        string path = GetPath(fileName, null);
        JsonData.Write(path, root, inlineDepth: 2);
        Log("Battle exported: " + path);
    }
}
