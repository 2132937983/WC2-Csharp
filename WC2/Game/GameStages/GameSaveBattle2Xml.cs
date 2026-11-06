namespace WC2;

/// <summary>Writes the current battle layout to a battle XML (GameSaveBattle2Xml.cpp).</summary>
public sealed partial class CGameManager
{
    /// <summary>
    /// Saves the current unit layout to an XML file.
    /// </summary>
    /// <param name="fileName">file name</param>
    public void SaveBattle(string fileName)
    {
        var new_xml = new TiXmlDocument(GetPath(fileName, null));
        var battle_info = new TiXmlElement("battle");

        // map and area enable data
        battle_info.SetAttribute("map", mapId);
        if (!string.IsNullOrEmpty(areasEnable))
        {
            battle_info.SetAttribute("areasenable", areasEnable);
        }
        var country_list = new TiXmlElement("list");
        // country data
        country_list.SetAttribute("name", "country");
        for (int i = 0; i < GetNumCountries(); i++)
        {
            CCountry this_country = GetCountryByIndex(i);
            var new_country = new TiXmlElement("country");
            new_country.SetAttribute("id", this_country.id);
            new_country.SetAttribute("name", this_country.name);
            if (!string.IsNullOrEmpty(this_country.GetCommanderName()))
            {
                new_country.SetAttribute("commander", this_country.GetCommanderName());
            }

            new_country.SetAttribute("ai", this_country.ai ? 1 : 0);
            new_country.SetAttribute("defeated", (this_country.defeatType == ARMY) ? "army" : "land");
            new_country.SetDoubleAttribute("taxfactor", this_country.taxRate);
            new_country.SetAttribute("money", this_country.money);
            new_country.SetAttribute("industry", this_country.industry);
            new_country.SetAttribute("techlevel", this_country.tech);
            string alliance_this;
            if (this_country.alliance == 1)
            {
                alliance_this = "a";
            }
            else if (this_country.alliance == 2)
            {
                alliance_this = "b";
            }
            else if (this_country.alliance == 3)
            {
                alliance_this = "c";
            }
            else
            {
                alliance_this = "n";
            }
            new_country.SetAttribute("alliance", alliance_this);
            new_country.SetAttribute("r", this_country.r);
            new_country.SetAttribute("g", this_country.g);
            new_country.SetAttribute("b", this_country.b);
            new_country.SetAttribute("a", this_country.a);
            country_list.LinkEndChild(new_country);
        }
        battle_info.LinkEndChild(country_list);

        // area data
        var area_list = new TiXmlElement("list");
        area_list.SetAttribute("name", "area");
        for (int i = 0; i < g_Scene.GetNumAreas(); i++)
        {
            CArea area_here = g_Scene.GetArea(i);
            if (area_here.info.enable && area_here.country != null)
            {
                var new_area = new TiXmlElement("area");
                new_area.SetAttribute("id", area_here.id);
                new_area.SetAttribute("construction", (area_here.info.constructionType == CITY) ? "city" :
                                                      (area_here.info.constructionType == INDUSTRY) ? "industry" :
                                                      (area_here.info.constructionType == AIRPORT) ? "airport" :
                                                      "none");
                new_area.SetAttribute("level", area_here.info.constructionLevel);
                new_area.SetAttribute("installation", (area_here.installationType == ENTRENCHMENT) ? "entrenchment" :
                                                      (area_here.installationType == FORT) ? "fort" :
                                                      (area_here.installationType == ANTIAIRCRAFT) ? "antiaircraft" :
                                                      (area_here.installationType == RADAR) ? "radar" :
                                                      "none");
                new_area.SetAttribute("country", area_here.country.id);

                // units of the area
                for (int j = 0; j < area_here.armyCount; j++)
                {
                    var new_army = new TiXmlElement("army");
                    CArmy army_here = area_here.GetArmy(j);
                    string armyTypeName = armydef_internal.GetArmyTypeName(army_here.basicAbilities.id);
                    if (string.IsNullOrEmpty(armyTypeName))
                    {
                        // old definitions or externally created ArmyDefs may be unregistered: use their raw name
                        armyTypeName = army_here.basicAbilities.name;
                    }
                    if (string.IsNullOrEmpty(armyTypeName))
                    {
                        armyTypeName = "infantry";
                    }
                    new_army.SetAttribute("type", armyTypeName);
                    new_army.SetAttribute("level", army_here.level);
                    new_army.SetAttribute("cards", army_here.cards);
                    new_area.LinkEndChild(new_army);
                }
                area_list.LinkEndChild(new_area);
            }
        }
        battle_info.LinkEndChild(area_list);
        var dialogue_list = new TiXmlElement("list");
        dialogue_list.SetAttribute("name", "dialogue");
        // dialogue data (cannot be changed in game anyway)
        for (int i = 0; i < GetNumDialogues(); i++)
        {
            DialogueDef dialogue_here = GetDialogueByIndex(i);
            var new_dialogue = new TiXmlElement("dialogue");
            new_dialogue.SetAttribute("commander", dialogue_here.commander);
            new_dialogue.SetAttribute("index", dialogue_here.index);
            new_dialogue.SetAttribute("atround", dialogue_here.atRound);

            new_dialogue.SetAttribute("left", dialogue_here.left ? 1 : 0);
            dialogue_list.LinkEndChild(new_dialogue);
        }
        battle_info.LinkEndChild(dialogue_list);
        new_xml.LinkEndChild(battle_info);
        new_xml.SaveFile();
    }
}
