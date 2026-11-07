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
    /// <summary>Scale of the units, flag and buildings drawn in this area (1; smaller in a small area of a mesh map).</summary>
    public float unitScale = 1f;

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
        // maximum levels come from the terrain definitions (city 4, industry 3, airport 1)
        if (constructionType == (int)CITY)
        {
            if (info.constructionLevel < Terrain.Construction(CITY).maxLevel && info.constructionType == CITY) return true;
        }
        else if (constructionType == (int)INDUSTRY)
        {
            if (info.constructionLevel < Terrain.Construction(INDUSTRY).maxLevel && info.constructionType == INDUSTRY) return true;
        }
        else if (constructionType == (int)AIRPORT)
        {
            if (info.constructionLevel < Terrain.Construction(AIRPORT).maxLevel) return true;
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
        if (constructionType == 1) info.constructionType = CITY;
        else if (constructionType == 2) info.constructionType = INDUSTRY;
        else if (constructionType == 3) info.constructionType = AIRPORT;
        // A new building starts one level above what the area already has: level 1 in a plain area,
        // a city in a capital at 4 (inherent city level 3), an industry there at 3, and so on. This is
        // exactly the original per area type table, expressed through the terrain definitions.
        info.constructionLevel = 1 + Terrain.Area(info.areaType).InherentLevel(info.constructionType);
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
            // a capital / large city only counts when it has units (terrain: encirclementNeedsGarrison)
            if (Terrain.Area(area.info.areaType).encirclementNeedsGarrison)
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
        // capitals, large cities and normal cities have an inherent city level (terrain: cityLevel)
        int cityLevel = Terrain.Area(info.areaType).cityLevel;
        // a city construction overrides the default when it is higher
        if (info.constructionType == CITY && cityLevel < info.constructionLevel) return info.constructionLevel;
        return cityLevel;
    }

    public int GetIndustryLevel()
    {
        // capitals and large cities have an inherent industry level (terrain: industryLevel)
        int industryLevel = Terrain.Area(info.areaType).industryLevel;
        // an industry construction overrides the default when it is higher
        if (info.constructionType == INDUSTRY && industryLevel < info.constructionLevel) return info.constructionLevel;
        return industryLevel;
    }

    /// <summary>Raw tax income of the area (before the tax rate).</summary>
    public int GetRealTax() => Terrain.Construction(CITY).perLevel * GetCityLevel() + info.tax;

    /// <summary>Raw industry income of the area.</summary>
    public int GetIndustry() => Terrain.Construction(INDUSTRY).perLevel * GetIndustryLevel();

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
            // A building that falls back to the area's inherent level is gone (a capital city at 3, its
            // industry at 2, a large city at 2 / 1, a normal city at 1): the original per area type
            // table (it tested ==, this port <=), expressed through the terrain definitions.
            if (info.constructionLevel <= Terrain.Area(info.areaType).InherentLevel(info.constructionType))
            {
                info.constructionLevel = 0;
                info.constructionType = NO_CONSTRUCTION;
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
            int seaMaxMovement = Terrain.Rules.seaMaxMovement;
            if (info.sea && army_tmp.movement > seaMaxMovement)
            {
                // the original limits every unit at sea (ships included) to one movement point
                army_tmp.movement = seaMaxMovement;
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
        // an airport counts as level 1 (original; terrain: healingLevel)
        int healingLevel = Terrain.Construction(info.constructionType).healingLevel;
        if (level < healingLevel) level = healingLevel;

        // healing granted by the area to units that still have movement
        // (capital 9, large city 7, normal city 5, port 6, other 3, plus 3 per level; terrain: healing)
        AreaTypeDef areaTypeDef = Terrain.Area(info.areaType);
        baseReplenishment = areaTypeDef.healing + Terrain.Rules.healingPerLevel * level;
        for (int i = 0; i < armyCount; ++i)
        {
            int replenishment = baseReplenishment;
            CArmy theArmy = army[i];
            if (theArmy.movement > 0)
            {
                // a navy unit in a port heals twice as much (terrain: navyHealingMultiplier)
                if (theArmy.IsNavy())
                {
                    replenishment *= areaTypeDef.navyHealingMultiplier;
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
