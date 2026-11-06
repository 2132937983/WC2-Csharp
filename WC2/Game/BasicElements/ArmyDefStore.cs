namespace WC2;

/// <summary>Storage and lookup of the army definitions (ArmyDefStore.cpp).</summary>
public sealed partial class CObjectDef
{
    /// <summary>Releases every army definition and the per country tables.</summary>
    public void ReleaseArmyDef()
    {
        foreach (var countryEntry in mapArmyDefList)
        {
            ArmyDefList armyDefList = countryEntry.Value;
            if (armyDefList == null) continue;
            armyDefList.def.Clear();
        }
        mapArmyDefList.Clear();
    }

    /// <summary>Fills a list with the fallback ("others") definitions. May improve in the future.</summary>
    public void SetDefaultList(ArmyDefList given_list)
    {
        if (given_list == null) return;
        if (!mapArmyDefList.TryGetValue("others", out ArmyDefList fallback) || fallback == null) return;
        given_list.def = new Dictionary<int, ArmyDef>(fallback.def);
    }

    /// <summary>The ArmyDef (basic abilities) of armyType for the country, falling back to "others".</summary>
    /// <param name="armyType">unit type id</param>
    /// <param name="countryName">country name</param>
    public ArmyDef GetArmyDef(int armyType, string countryName)
    {
        mapArmyDefList.TryGetValue("others", out ArmyDefList fallbackCountry);

        static ArmyDef FindInList(ArmyDefList list, int armyType)
        {
            if (list == null) return null;
            return list.def.TryGetValue(armyType, out ArmyDef def) ? def : null;
        }

        if (countryName != null)
        {
            if (mapArmyDefList.TryGetValue(countryName, out ArmyDefList country))
            {
                ArmyDef result = FindInList(country, armyType);
                if (result != null) return result;
            }
        }
        return fallbackCountry == null ? null : FindInList(fallbackCountry, armyType);
    }
}
