namespace WC2;

// Conquest mode prefers the ai="0" countries as selectable player countries; when the scenario has
// none of them it falls back to every non-neutral country. The country button image uses the
// id first and the name when the id is missing.

/// <summary>Port of gui/menus/SelectCountry.cpp (GetBattleBelligerentList).</summary>
public static partial class BattleScenario
{
    /// <summary>
    /// Reads the belligerent list of a scenario (scenarios/*.json or the original XML). The first
    /// pass only collects the ai="0" countries; if none is found a second pass collects every
    /// non-neutral country. <paramref name="includeAi1"/> (IgnoreAI in the original interface) is
    /// kept for compatibility and unused.
    /// </summary>
    public static void GetBattleBelligerentList(string fileName, List<Belligerent> result, bool includeAi1)
    {
        var data = GetPreloadedBattleScenario(fileName);
        if (data == null)
        {
            data = new BattleScenarioData();
            if (!LoadBattleScenarioData(fileName, data)) return;
        }
        bool CountryFound = false;
        bool UseAlliance = false;
        do
        {
            // when the first pass found no selectable player country, UseAlliance is set and the list is scanned again
            foreach (var country in data.countries)
            {
                // alliances follow the original rule: a/b/c are the sides, anything else is neutral
                // (the remake decoded every letter here, so an "n" country could be offered as a player country)
                int allianceNum = country.alliance;
                bool InvolveCountry;
                if (UseAlliance)
                {
                    InvolveCountry = allianceNum != (int)NEUTRAL;
                }
                else
                {
                    // a missing ai attribute counts as 0 here (like the original QueryIntAttribute)
                    int ai = country.hasAi ? country.ai : 0;
                    // the first pass prefers player countries but must not add neutral ones either
                    InvolveCountry = ai == 0 && allianceNum != (int)NEUTRAL;
                }
                if (InvolveCountry)
                {
                    // Belligerent is a light weight snapshot for the selection screen, no CCountry is created
                    var item = new Belligerent();
                    item.name = country.hasName ? country.name : "";
                    item.id = country.id ?? "";
                    // common3 is the default commander so the selection screen always has a portrait
                    item.commander = country.hasCommander ? country.commander : "common3";
                    item.alliance = allianceNum;
                    result.Add(item);
                    CountryFound = true;
                }
            }
            if (UseAlliance)
                break;
            UseAlliance = true;
        } while (!CountryFound);
    }
}
