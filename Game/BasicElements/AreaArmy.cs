namespace WC2;

/// <summary>Operations on the armies of an area (AreaArmy.cpp).</summary>
public sealed partial class CArea
{
    /// <summary>Drafts a unit in this area.</summary>
    /// <param name="armyType">unit type to draft</param>
    public CArmy DraftArmy(int armyType)
    {
        // part of the "max units per area" rule, wrapped in a constant
        // a future "volume" rule would need the armydef here
        if (armyCount >= AREA_RENDER_ARMY_LIMIT) return null;
        // no owner, no drafting
        if (country == null) return null;

        ArmyDef armyDef = CObjectDef.Instance().GetArmyDef(armyType, country.name);
        if (armyDef == null) return null;

        var pArmy = new CArmy();
        pArmy.Init(armyDef, country);
        // whether a freshly drafted unit has movement can be changed here
        pArmy.active = true;
        pArmy.movement = pArmy.basicAbilities.movement;

        armyDrafting = pArmy;
        draftingArmyOffset = -60.0f;
        g_SoundRes.PlayCharSE(DRAFT_WAV);
        return pArmy;
    }

    /// <summary>Heals one army of the area.</summary>
    public void AddArmyStrength(int armyIndex, int hp)
    {
        int num = armyCount;
        if (armyIndex < num)
        {
            if (army[armyIndex] != null) army[armyIndex].AddStrength(hp);
        }
    }

    /// <summary>
    /// Adds a tactic card to a unit: 0 assault, 1 defence, 2 carrier, 3 commander.
    /// </summary>
    public void AddArmyCard(int armyIndex, int cardId)
    {
        if (armyIndex < armyCount)
        {
            if (army[armyIndex] != null) army[armyIndex].cards |= 1 << cardId;
        }
    }

    /// <summary>Removes every army of the area.</summary>
    public void ClearAllArmy()
    {
        int num = armyCount;
        for (int i = num; i > 0; i--)
        {
            army[i - 1] = null;
        }
        armyCount = 0;
    }

    /// <summary>Removes a tactic card from a unit: 0 assault, 1 defence, 2 carrier, 3 commander.</summary>
    public void DelArmyCard(int armyIndex, int cardId)
    {
        if (armyIndex < armyCount)
        {
            if (army[armyIndex] != null) army[armyIndex].cards &= ~(1 << cardId);
        }
    }

    /// <summary>Whether the unit at armyIndex holds the card (bit index) cardId.</summary>
    public bool HasArmyCard(int armyIndex, int cardId)
    {
        if (armyIndex < 0 || armyIndex >= armyCount || cardId < 0 || cardId >= 32) return false;
        CArmy a = army[armyIndex];
        if (a == null) return false;
        return (((uint)a.cards >> cardId) & 1U) != 0;
    }

    /// <summary>Whether any unit of the area holds the card (bit index) cardId.</summary>
    public bool HasArmyCard(int cardId)
    {
        if (cardId < 0 || cardId >= 32) return false;
        for (int armyIndex = 0; armyIndex < armyCount; ++armyIndex)
        {
            if (HasArmyCard(armyIndex, cardId)) return true;
        }
        return false;
    }

    /// <summary>Restores the unit to its current max strength.</summary>
    public void RevertArmyStrength(int armyIndex)
    {
        if (armyIndex < 0 || armyIndex >= armyCount) return;
        CArmy a = army[armyIndex];
        if (a != null) a.hp = a.GetMaxStrength();
    }

    /// <summary>Sets the construction type and level, clamped to the maximum of that type.</summary>
    public void SetConstruction(int constructionType, int constructionLevel)
    {
        info.constructionType = (CONSTRUCTION_TYPE)constructionType;
        info.constructionLevel = constructionLevel;

        switch ((CONSTRUCTION_TYPE)constructionType)
        {
            case NO_CONSTRUCTION:
                info.constructionLevel = 0;
                break;
            case CITY:
                if (constructionLevel > Terrain.Construction(CITY).maxLevel) info.constructionLevel = Terrain.Construction(CITY).maxLevel;
                break;
            case INDUSTRY:
                if (constructionLevel > Terrain.Construction(INDUSTRY).maxLevel) info.constructionLevel = Terrain.Construction(INDUSTRY).maxLevel;
                break;
            case AIRPORT:
                if (constructionLevel > Terrain.Construction(AIRPORT).maxLevel) info.constructionLevel = Terrain.Construction(AIRPORT).maxLevel;
                break;
            default:
                break;
        }
    }

    /// <summary>Upgrades the unit at armyIndex.</summary>
    public void UpgradeArmy(int armyIndex)
    {
        if (armyIndex < 0 || armyIndex >= armyCount) return;
        CArmy a = army[armyIndex];
        if (a != null) a.Upgrade();
    }

    /// <summary>Sets the movement of every unit of the area.</summary>
    public void SetAllArmyMovement(int movement)
    {
        for (int i = 0; i < armyCount; ++i)
        {
            CArmy a = army[i];
            if (a != null) a.movement = movement;
        }
    }

    /// <summary>Whether a unit with movement left and active flag exists here.</summary>
    public bool IsActive()
    {
        for (int i = 0; i < armyCount; ++i)
        {
            CArmy a = army[i];
            if (a != null && a.movement > 0 && a.active) return true;
        }
        return false;
    }

    /// <summary>Sets the active flag of the unit at armyIndex.</summary>
    public void SetArmyActive(int armyIndex, bool active)
    {
        if (armyIndex < 0 || armyIndex >= armyCount) return;
        CArmy a = army[armyIndex];
        if (a != null) a.active = active;
    }

    /// <summary>Active flag of the unit at armyIndex (false when the index is invalid).</summary>
    public bool IsArmyActive(int armyIndex)
    {
        if (armyIndex < 0 || armyIndex >= armyCount) return false;
        CArmy a = army[armyIndex];
        return a != null && a.active;
    }

    /// <summary>Unit at the given stack position (0 = top).</summary>
    public CArmy GetArmy(int armyIndex)
    {
        // the original only tested ArmyIndex > armyCount; the extra array bound keeps it safe in C#
        if (armyCount < 0 || armyIndex > armyCount || armyIndex < 0 || armyIndex >= army.Length) return null;
        return army[armyIndex];
    }

    /// <summary>Moves the top unit of this area to targetArea.</summary>
    public void MoveArmyTo(CArea targetArea)
    {
        bool willOccupy = false;
        bool willComplain = false;
        string commander = null;
        if (armyCount <= 0) return;
        // the target has an owner
        if (targetArea.country != null)
        {
            commander = targetArea.country.GetCommanderName();
        }
        // the top unit has movement and the target has less than four units
        if (army[0].movement > 0 && targetArea.armyCount < AREA_RENDER_ARMY_LIMIT)
        {
            // same owner
            if (country == targetArea.country)
            {
                // neither occupation nor commander complaint
                willComplain = false;
            }
            else
            {
                willOccupy = true;
                // the target has an owner
                CCountry oldOwner = targetArea.country;
                if (oldOwner != null)
                {
                    // original: only a human moving into an AI ally's area may trigger the
                    // commander complaint (50 %)
                    willComplain = oldOwner.ai && !country.ai && country.alliance == oldOwner.alliance
                                   && (GetRand() % 2) != 0;
                    // the old owner loses the area
                    oldOwner.RemoveArea(targetArea.id);
                }
                else
                {
                    willComplain = false;
                }
                // the occupying country gains the area
                country.AddArea(targetArea.id);
                // change the owner of the target
                targetArea.country = country;
                // the previous owner may have lost its last area (the original checks the loser)
                if (oldOwner != null && oldOwner.IsConquested())
                {
                    oldOwner.BeConquestedBy(country);
                }
            }
            CArmy pArmy = army[0];

            // remove the top unit here
            RemoveArmy(pArmy);
            targetArea.SetMoveInArmy(this, pArmy, willOccupy, willComplain, commander);

            // land to land costs one movement point; any move touching the sea clears it (original)
            if (!targetArea.info.sea && !info.sea) pArmy.movement -= 1;
            else pArmy.movement = 0;

            if (armyCount == 0)
            {
                // recompute the encirclement of this area and its neighbours
                g_Scene.AdjacentAreasEncirclement(id);
            }
        }
    }

    /// <summary>Sets up the unit that moves into this area.</summary>
    /// <param name="origin">area the unit comes from</param>
    /// <param name="armyIn">the moving unit</param>
    /// <param name="willOccupy">the area is newly occupied</param>
    /// <param name="willComplain">a commander complaint is triggered</param>
    /// <param name="complainCommander">the complaining commander</param>
    public void SetMoveInArmy(CArea origin, CArmy armyIn, bool willOccupy, bool willComplain, string complainCommander)
    {
        movingInArmyOccupy = willOccupy;
        armyMovingIn = armyIn;
        movingInArmyComplain = willComplain;
        // reset on every move so a stale commander of a previous unit is never reused
        complainCommanderName = null;
        if (complainCommander != null) complainCommanderName = complainCommander;
        movingInArmyOffset[0] = (float)(origin.info.armyPos[0] - info.armyPos[0]);
        movingInArmyOffset[1] = (float)(origin.info.armyPos[1] - info.armyPos[1]);
        movingInArmySpeed[0] = movingInArmyOffset[0] * (-4.0f);
        movingInArmySpeed[1] = movingInArmyOffset[1] * (-4.0f);

        if (origin.info.armyPos[0] > info.armyPos[0]) armyIn.direction = -1.0f;
        else if (origin.info.armyPos[0] < info.armyPos[0]) armyIn.direction = 1.0f;
    }

    public int GetArmyIdx(CArmy armyIn)
    {
        for (int i = 0; i < armyCount; ++i)
        {
            if (armyIn == army[i]) return i;
        }
        return -1;
    }

    /// <summary>Removes the unit from the stack when it belongs to this area.</summary>
    public void RemoveArmy(CArmy armyIn)
    {
        int item = GetArmyIdx(armyIn);
        // only when the unit is here
        if (item < 0) return;
        // shift the following units forward
        int max = armyCount - 1;
        for (int i = item; i < armyCount - 1; ++i)
        {
            army[i] = army[i + 1];
        }
        army[max] = null;
        armyCount -= 1;
    }

    /// <summary>Whether the area can take one more unit.</summary>
    public bool CanContainArmy(CArmy armyIn)
    {
        if (armyCount >= AREA_RENDER_ARMY_LIMIT) return false;

        if (!info.sea && armyIn.IsNavy()) return false;
        // could become CanEnterSea / CanEnterLand or CanEnterAreaType one day
        if (info.sea && (!armyIn.IsNavy()) && (!armyIn.HasCard(CARRIER))) return false;
        int i, TroopCount = 0, NavyCount = 0;
        for (i = 0; i < armyCount; i++)
        {
            if (GetArmy(i).IsNavy()) NavyCount++;
            else TroopCount++;
        }
        if (armyIn.IsNavy()) NavyCount++;
        else TroopCount++;

        // special land / navy capacity rules
        // a future version may use unit "volume" instead
        if (NavyCount > AREA_NAVY_LIMIT) return false;
        if (NavyCount == AREA_NAVY_LIMIT && TroopCount > 0) return false;
        if (TroopCount > AREA_TROOP_LIMIT) return false;
        return true;
    }

    /// <summary>
    /// The top unit of this area occupies targetArea.
    /// An "area occupied" trigger could be fired from here one day.
    /// </summary>
    public void OccupyArea(CArea targetArea)
    {
        CCountry targetCountry = targetArea.country;

        // the occupying unit
        CArmy theArmy = army[0];

        if (armyCount > 0)
        {
            bool isOccupying = false;
            if (country != targetCountry)
            {
                // an unowned area has no previous owner to update (original else branch)
                targetCountry?.RemoveArea(targetArea.id);
                country.AddArea(targetArea.id);
                targetArea.country = country;
                if (targetCountry != null && targetCountry.IsConquested())
                {
                    // conquered
                    targetCountry.BeConquestedBy(country);
                }
                // enemy territory: occupation state
                isOccupying = true;
            }
            RemoveArmy(theArmy);

            targetArea.SetMoveInArmy(this, theArmy, isOccupying, false, null);
            if (armyCount == 0)
            {
                // the unit layout of this area changed: recompute the encirclement around it
                g_Scene.AdjacentAreasEncirclement(id);
            }
        }
    }

    /// <summary>Moves the unit at armyIndex to the top of the stack.</summary>
    /// <param name="armyIndex">stack position of the unit</param>
    /// <param name="instantly">whether the change is animated (see the original semantics)</param>
    public void MoveArmyToFront(int armyIndex, bool instantly)
    {
        if (armyIndex > 0)
        {
            // the original re-inserts a unit that is still flying to the front before validating the index
            CArmy army_to_front = armyMovingToFront;
            if (army_to_front != null)
            {
                AddArmy(army_to_front, false);
                armyMovingToFront = null;
            }
            if (armyIndex < armyCount)
            {
                CArmy tmp_remove = army[armyIndex];
                if (tmp_remove != null)
                {
                    if (instantly)
                    {
                        RemoveArmy(army[armyIndex]);
                        armyMovingToFront = tmp_remove;
                        movingArmyToFrontAngle = 0.0f;
                    }
                    else
                    {
                        for (int i = armyIndex - 1; i >= 0; i--)
                        {
                            army[i + 1] = army[i];
                        }
                        army[0] = tmp_remove;
                    }
                }
            }
        }
    }

    /// <summary>Sum of the unit hp of the area.</summary>
    /// <param name="TopOnly">only the top unit</param>
    public int calcAreaArmyHp(bool TopOnly)
    {
        if (armyCount <= 0) return 0;
        CArmy curArmy;
        if (TopOnly)
        {
            curArmy = GetArmy(0);
            return curArmy != null ? curArmy.hp : -1;
        }
        int s = 0;
        for (int i = 0; i < armyCount; i++)
        {
            curArmy = GetArmy(i);
            if (curArmy == null) return -1;
            s += curArmy.hp;
        }
        return s;
    }

    /// <summary>Adds a unit to the area.</summary>
    /// <param name="armyIn">the unit</param>
    /// <param name="bottom">whether it goes to the bottom of the stack</param>
    public void AddArmy(CArmy armyIn, bool bottom)
    {
        int num = armyCount;
        // room for one more unit
        if (num <= 3)
        {
            // at the bottom (0 is the top)
            if (bottom)
            {
                army[num] = armyIn;
            }
            else
            {
                // otherwise shift everything down (the original started at num + 1, which is a no-op slot)
                for (int i = num; i > 0; i--)
                {
                    army[i] = army[i - 1];
                }
                // the new unit becomes the top
                army[0] = armyIn;
            }
            // one more unit
            armyCount = num + 1;
        }
    }

    /// <summary>
    /// A unit of this area loses hp; includes LostStrength and the death handling.
    /// </summary>
    /// <param name="armyIndex">stack position (0 = top)</param>
    /// <param name="damage">damage taken</param>
    /// <returns>whether the unit died</returns>
    public bool LostArmyStrength(int armyIndex, int damage)
    {
        // valid index, unit exists and the unit died
        if (armyIndex < armyCount && army[armyIndex] != null && army[armyIndex].LostStrength(damage))
        {
            CArmy theArmy = army[armyIndex];
            // the unit carried the commander
            if ((theArmy.cards & 8) != 0)
            {
                // the commander dies
                country?.CommanderDie();
            }
            // remove the unit from the area
            RemoveArmy(theArmy);
            if (info.sea && armyCount == 0)
            {
                if (country != null)
                {
                    bool isConquested = country.IsConquested();
                    if (isConquested) country.BeConquestedBy(null);
                }
            }
            return true;
        }
        return false;
    }

    /// <summary>Sets the facing of a unit.</summary>
    public void SetArmyDir(int armyIndex, float direction)
    {
        if (armyIndex < armyCount) army[armyIndex].direction = direction;
    }

    /// <summary>Moves a unit (by reference) to the top; the original never called this.</summary>
    public void MoveArmyToFront(CArmy armyIn, bool instantly)
    {
        int idx = GetArmyIdx(armyIn);
        if (idx >= 0) MoveArmyToFront(idx, instantly);
    }

    /// <summary>Poisons every unit (never called in WC2, kept from the original code base).</summary>
    public void AllArmyPoisoning()
    {
        foreach (CArmy cArmy in army)
        {
            cArmy?.Poisoning();
        }
    }
}
