namespace WC2;

/// <summary>
/// A map region: owner, buildings, installation and up to four unit stacks (CArea.cpp).
/// The army handling lives in AreaArmy.cs, the animation/rendering in MapScene/CAreaPresentation.cs.
/// TurnBegin/TurnEnd (GameTurnBegin.cpp / GameTurnEnd.cpp) and SaveAera/LoadArea (GameLoadSave.cpp)
/// are kept at the end of this file so the CGameManager files stay with the logic agent.
/// </summary>
public sealed partial class CArea
{
    public int id;
    public AreaInfo info = new();
    public INSTALLATION_TYPE installationType;
    public CCountry country;
    public CArmy[] army = new CArmy[4];
    public int armyCount;
    public CArmy armyDrafting;
    public CArmy armyMovingIn;
    public CArmy armyMovingToFront;
    public float draftingArmyOffset;
    public float[] movingInArmyOffset = new float[2];
    public float[] movingInArmySpeed = new float[2];
    public float movingArmyToFrontAngle;
    public bool selected;
    public bool movingInArmyOccupy;
    public bool movingInArmyComplain;
    public string complainCommanderName;
    public int targetType;
    public float[] arrowEndOffset = new float[2];
    public float[] arrowEndOffsetMinus = new float[2];
    public float[] arrowStartOffset = new float[2];
    public float[] arrowStartOffsetMinus = new float[2];

    /// <summary>Constructs the area with no armies, no owner and zeroed arrow offsets.</summary>
    public CArea() { }

    /// <summary>
    /// Initialises the area from the map definition and clears every army / runtime state it held before.
    /// </summary>
    /// <param name="idIn">area id</param>
    /// <param name="infoIn">area type, tax and unit / building positions</param>
    public void Init(int idIn, AreaInfo infoIn)
    {
        id = idIn;
        info.CopyFrom(infoIn);
        info.enable = true;
        info.sea = false;
        info.constructionType = NO_CONSTRUCTION;
        info.constructionLevel = 0;

        installationType = NO_INSTALLATION;
        country = null;

        for (int armyIndex = 0; armyIndex < AREA_TROOP_LIMIT; ++armyIndex)
            army[armyIndex] = null;

        armyCount = 0;
        armyDrafting = null;
        armyMovingIn = null;
        armyMovingToFront = null;
        selected = false;
        movingInArmyOccupy = false;
        targetType = 0;
    }

    /// <summary>
    /// Whether a construction of the given type (city, industry, airport) can be built here.
    /// </summary>
    public bool CanConstruct(int constructionType)
    {
        if (info.sea) return false;
        if (info.constructionType == NO_CONSTRUCTION) return true;
        if (constructionType == (int)CITY)
        {
            if (info.constructionLevel < MAX_CITY_LEVEL && info.constructionType == CITY) return true;
        }
        else if (constructionType == (int)INDUSTRY)
        {
            if (info.constructionLevel < MAX_INDUSTRY_LEVEL && info.constructionType == INDUSTRY) return true;
        }
        else if (constructionType == (int)AIRPORT)
        {
            if (info.constructionLevel < MAX_AIRPORT_LEVEL) return true;
        }
        // none of the conditions above holds
        return false;
    }

    /// <summary>Builds a city, industry or airport here (or upgrades the existing building by one level).</summary>
    public void Construct(int constructionType)
    {
        // an existing building simply gains a level
        if (info.constructionType != NO_CONSTRUCTION)
        {
            info.constructionLevel += 1;
            return;
        }
        // by default every building starts at level 1
        info.constructionLevel = 1;
        if (constructionType == 1) info.constructionType = CITY;
        else if (constructionType == 2) info.constructionType = INDUSTRY;
        else if (constructionType == 3) info.constructionType = AIRPORT;
        // but the following area types are special
        switch (info.areaType)
        {
            case CAPITAL:
                if (info.constructionType == CITY) info.constructionLevel = 4;
                else if (info.constructionType == INDUSTRY) info.constructionLevel = 3;
                break;
            case LARGE_CITY:
                if (info.constructionType == CITY) info.constructionLevel = 3;
                else if (info.constructionType == INDUSTRY) info.constructionLevel = 2;
                break;
            case NORMAL_CITY:
                if (info.constructionType == CITY) info.constructionLevel = 2;
                else if (info.constructionType == INDUSTRY) info.constructionLevel = 1;
                break;
            default:
                break;
        }
    }

    /// <summary>Whether this area is encircled.</summary>
    public bool CheckEncirclement()
    {
        if (country == null) return false;
        int adjacentNum = g_Scene.GetNumAdjacentAreas(id);
        for (int i = 0; i < adjacentNum; ++i)
        {
            CArea area = g_Scene.GetAdjacentArea(id, i);
            // an adjacent area without owner breaks the encirclement
            if (area.country == null) return false;
            // a capital / large city only counts when it has units
            if (area.info.areaType == CAPITAL || area.info.areaType == LARGE_CITY)
            {
                if (area.country.alliance == country.alliance ||
                    area.country.alliance == (int)NEUTRAL || area.armyCount == 0)
                    return false;
            }
            else if (area.country.alliance == country.alliance ||
                     area.country.alliance == (int)NEUTRAL)
            {
                // otherwise the area only needs to be occupied
                // (the original behaviour would add "|| area->ArmyCount == 0" here)
                return false;
            }
        }
        return true;
    }

    /// <summary>Applies (or lifts) the encirclement effect on the units of this area.</summary>
    public void Encirclement()
    {
        int num = armyCount;
        if (CheckEncirclement())
        {
            // 1 = upset morale
            for (int i = 0; i < num; ++i) army[i].SetMorale((int)MORALE_UPSET);
        }
        else
        {
            for (int i = 0; i < num; ++i)
            {
                if (army[i].morale == 1 && army[i].moraleUpTurn < 1)
                    army[i].SetMorale((int)MORALE_NORMAL);
            }
        }
    }

    public int GetCityLevel()
    {
        int cityLevel = 0;
        // capitals, large cities and normal cities have a default city level
        if (info.areaType == CAPITAL) cityLevel = 3;
        else if (info.areaType == LARGE_CITY) cityLevel = 2;
        else if (info.areaType == NORMAL_CITY) cityLevel = 1;
        // a city construction overrides the default when it is higher
        if (info.constructionType == CITY && cityLevel < info.constructionLevel) return info.constructionLevel;
        return cityLevel;
    }

    public int GetIndustryLevel()
    {
        int industryLevel = 0;
        // capitals and large cities have a default industry level
        if (info.areaType == CAPITAL) industryLevel = 2;
        else if (info.areaType == LARGE_CITY) industryLevel = 1;
        // an industry construction overrides the default when it is higher
        if (info.constructionType == INDUSTRY && industryLevel < info.constructionLevel) return info.constructionLevel;
        return industryLevel;
    }

    /// <summary>Raw tax income of the area (before the tax rate).</summary>
    public int GetRealTax() => 5 * GetCityLevel() + info.tax;

    /// <summary>Raw industry income of the area.</summary>
    public int GetIndustry() => 5 * GetIndustryLevel();

    /// <summary>Destroys every construction here (only the nuclear bomb calls this).</summary>
    public void DestroyConstruction()
    {
        info.constructionLevel = 0;
        info.constructionType = NO_CONSTRUCTION;
    }

    /// <summary>Lowers the construction level by one.</summary>
    public void ReduceConstructionLevel()
    {
        if (info.constructionType != NO_CONSTRUCTION)
        {
            // only an existing building can be degraded; -1 first, then the area type specific checks
            info.constructionLevel -= 1;
            if (info.constructionLevel == 0)
            {
                info.constructionType = NO_CONSTRUCTION;
                return;
            }
            switch (info.areaType)
            {
                case CAPITAL:
                    // the original tested ==, this port tests <= (same for industry)
                    // the hard coded 3 / 2 levels should become constants one day
                    if ((info.constructionType == CITY && info.constructionLevel <= 3) ||
                        (info.constructionType == INDUSTRY && info.constructionLevel <= 2))
                    {
                        info.constructionLevel = 0;
                        info.constructionType = NO_CONSTRUCTION;
                    }
                    break;
                case LARGE_CITY:
                    if ((info.constructionType == CITY && info.constructionLevel <= 2) ||
                        (info.constructionType == INDUSTRY && info.constructionLevel <= 1))
                    {
                        info.constructionLevel = 0;
                        info.constructionType = NO_CONSTRUCTION;
                    }
                    break;
                case NORMAL_CITY:
                    if (info.constructionType == CITY && info.constructionLevel <= 1)
                    {
                        info.constructionLevel = 0;
                        info.constructionType = NO_CONSTRUCTION;
                    }
                    break;
                default:
                    break;
            }
        }
    }

    // ---- GameTurnBegin.cpp ---------------------------------------------------------------

    /// <summary>Turn start of the area: calls the turn start of every unit (before the AI prepares its actions).</summary>
    public void TurnBegin()
    {
        for (int i = 0; i < armyCount; i++)
        {
            CArmy army_tmp = GetArmy(i);
            // unit turn start
            army_tmp.TurnBegin();
            if (info.sea && army_tmp.movement > 1)
            {
                // the original limits every unit at sea (ships included) to one movement point
                army_tmp.movement = 1;
            }
        }
    }

    // ---- GameTurnEnd.cpp -----------------------------------------------------------------

    /// <summary>Turn end of the area: unit healing etc.</summary>
    public void TurnEnd()
    {
        int cityLevel = GetCityLevel();
        int industryLevel = GetIndustryLevel();
        int level = 0;
        int baseReplenishment = 0;
        if (cityLevel > industryLevel) level = cityLevel;
        else level = industryLevel;
        // an airport counts as level 1 (original)
        if (info.constructionType == AIRPORT && level < 1) level = 1;

        // healing granted by the area to units that still have movement
        switch (info.areaType)
        {
            case CAPITAL: baseReplenishment = 9; break;
            case LARGE_CITY: baseReplenishment = 7; break;
            case NORMAL_CITY: baseReplenishment = 5; break;
            case PORT: baseReplenishment = 6; break;
            default: baseReplenishment = 3; break;
        }
        baseReplenishment += 3 * level;
        for (int i = 0; i < armyCount; ++i)
        {
            int replenishment = baseReplenishment;
            CArmy theArmy = army[i];
            if (theArmy.movement > 0)
            {
                // a navy unit in a port
                if (info.areaType == PORT && theArmy.IsNavy())
                {
                    // heals twice as much
                    replenishment *= 2;
                }
                theArmy.AddStrength(replenishment);
            }
            replenishment = GetArmyAbility(theArmy.level).turnRecovery;
            if (theArmy.HasCard(COMMANDER))
            {
                // with a commander use the max of the commander and unit level recovery
                int commanderLevel = country.GetCommanderLevel();
                int commanderReplenishment = GetCommanderAbility(commanderLevel).armyAbility.turnRecovery;
                if (replenishment <= commanderReplenishment) replenishment = commanderReplenishment;
            }

            theArmy.AddStrength(replenishment);

            // unit turn end
            theArmy.TurnEnd();
        }
    }

    // ---- GameLoadSave.cpp ----------------------------------------------------------------

    /// <summary>Stores the area state (buildings, installation and every army) into areaInfo.</summary>
    public void SaveAera(SaveAreaInfo areaInfo)
    {
        areaInfo.id = id;
        areaInfo.constructionType = (int)info.constructionType;
        areaInfo.constructionLevel = info.constructionLevel;
        areaInfo.installationType = (int)installationType;
        areaInfo.armyCount = 0;
        for (int i = 0; i < areaInfo.army.Length; i++) areaInfo.army[i] = new SaveArmyInfo();

        // the special (animating) armies first
        if (armyDrafting != null) SaveArmyEntry(areaInfo, armyDrafting);
        if (armyMovingIn != null) SaveArmyEntry(areaInfo, armyMovingIn);
        if (armyMovingToFront != null) SaveArmyEntry(areaInfo, armyMovingToFront);
        // then every stationed army
        foreach (CArmy Army in army)
        {
            if (Army != null) SaveArmyEntry(areaInfo, Army);
        }
    }

    /// <summary>areaInfo-&gt;army[areaInfo-&gt;armyCount++] with a bound check (the C array has four slots).</summary>
    private static void SaveArmyEntry(SaveAreaInfo areaInfo, CArmy armyIn)
    {
        if (areaInfo.armyCount >= areaInfo.army.Length) return;
        armyIn.SaveArmy(areaInfo.army[areaInfo.armyCount++]);
    }

    /// <summary>Restores the area state from saveAreaInfo (the owner must already be set).</summary>
    public void LoadArea(SaveAreaInfo saveAreaInfo)
    {
        id = saveAreaInfo.id;
        info.constructionType = (CONSTRUCTION_TYPE)saveAreaInfo.constructionType;
        info.constructionLevel = saveAreaInfo.constructionLevel;
        installationType = (INSTALLATION_TYPE)saveAreaInfo.installationType;
        if (country != null)
        {
            // rebuild the armies; the ArmyDef is looked up by id so new ArmyDef fields need no save handling
            for (int i = 0; i < saveAreaInfo.armyCount; i++)
            {
                ArmyDef def = CObjectDef.Instance().GetArmyDef(saveAreaInfo.army[i].id, country.name);
                if (def == null) continue;
                var Army = new CArmy();
                Army.Init(def, country);
                Army.LoadArmy(saveAreaInfo.army[i]);
                AddArmy(Army, true);
            }
        }
    }
}
