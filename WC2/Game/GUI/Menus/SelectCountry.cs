namespace WC2;

// Conquest mode prefers the ai="0" countries as selectable player countries; when the XML has
// none of them it falls back to every non-neutral country. The country button image uses the
// id first and the name when the id is missing.

/// <summary>Port of gui/menus/SelectCountry.cpp (GetBattleBelligerentList).</summary>
public static partial class BattleScenario
{
    /// <summary>
    /// Reads the belligerent list of a battle XML. The first pass only collects the ai="0"
    /// countries; if none is found a second pass collects every non-neutral country.
    /// <paramref name="includeAi1"/> (IgnoreAI in the original interface) is kept for
    /// compatibility and unused.
    /// </summary>
    public static void GetBattleBelligerentList(string fileName, List<Belligerent> result, bool includeAi1)
    {
        var xml = new TiXmlDocument(GetPath(fileName, null));
        if (xml.LoadFile(TIXML_ENCODING_UNKNOWN))
        {
            TiXmlNode battle = xml.FirstChild("battle");
            if (battle != null)
            {
                bool CountryFound = false;
                bool UseAlliance = false;
                // battle may hold several lists (country, area, dialogue ...); only name="country" is used here
                for (TiXmlElement list = battle.FirstChildElement(); list != null; list = list.NextSiblingElement())
                {
                    string name = list.Attribute("name");
                    if (name == null || name != "country")
                        continue;
                    do
                    {
                        // when the first pass found no selectable player country, UseAlliance is set and the same list is scanned again
                        for (TiXmlElement country = list.FirstChildElement(); country != null; country = country.NextSiblingElement())
                        {
                            bool InvolveCountry;
                            int allianceNum;
                            string alliance = country.Attribute("alliance");
                            char allianceChar = string.IsNullOrEmpty(alliance) ? '\0' : alliance[0];
                            bool isNeutralAlliance =
                                    alliance == null ||
                                    (allianceChar >= 'a' && allianceChar <= 'z' &&
                                     allianceChar - 'a' + 1 == (int)NEUTRAL);
                            if (isNeutralAlliance || allianceChar < 'a' || allianceChar > 'z')
                            {
                                // missing or invalid alliance letters are treated as neutral
                                allianceNum = (int)NEUTRAL;
                            }
                            else
                            {
                                allianceNum = allianceChar - 'a' + 1;
                            }

                            if (UseAlliance)
                            {
                                InvolveCountry = allianceNum != (int)NEUTRAL;
                            }
                            else
                            {
                                // missing attribute keeps ai = 0 (like the original QueryIntAttribute)
                                country.QueryIntAttribute("ai", out int ai);
                                // the first pass prefers player countries but must not add neutral ones either
                                InvolveCountry = ai == 0 && allianceNum != (int)NEUTRAL;
                            }
                            if (InvolveCountry)
                            {
                                // Belligerent is a light weight snapshot for the selection screen, no CCountry is created
                                var item = new Belligerent();
                                item.name = country.Attribute("name") ?? "";
                                item.id = country.Attribute("id") ?? "";
                                string commander = country.Attribute("commander");
                                if (commander != null)
                                    item.commander = commander;
                                else
                                    // common3 is the default commander so the selection screen always has a portrait
                                    item.commander = "common3";
                                item.alliance = allianceNum;
                                result.Add(item);
                                CountryFound = true;
                            }
                        }
                        if (UseAlliance)
                            break;
                        UseAlliance = true;
                    } while (!CountryFound);
                    break;
                }
            }
        }
    }
}
