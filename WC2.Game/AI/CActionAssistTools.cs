namespace WC2;

/// <summary>Search tools of the AI (CActionAssistTools.cpp).</summary>
public sealed partial class CActionAssist
{
    /// <summary>File-local "visited" vector of the original.</summary>
    private static bool[] visited = Array.Empty<bool>();

    /// <summary>
    /// Breadth first path search; returns the next reachable area towards the found target.
    /// </summary>
    /// <param name="AreaID">area of the searching unit</param>
    /// <param name="ArmyIndex">index of the searching unit</param>
    /// <returns>the next area to act on towards the target</returns>
    public int searchNodeByID(int AreaID, int ArmyIndex)
    {
        // init the search space
        visited = new bool[g_Scene.areaCount];
        searchHead = -1;
        searchTail = 0;
        searchQueue[0].areaId = AreaID;
        searchQueue[0].previous = -1;
        // the start area is already visited
        visited[AreaID] = true;

        do
        {
            // final id from here
            int TargetID = searchNode(AreaID, ArmyIndex);
            if (TargetID != -1)
            {
                int i;
                // walk back to the first node whose previous is 0 (the start)
                for (i = searchTail; searchQueue[i].previous != 0; i = searchQueue[i].previous)
                    continue;
                return searchQueue[i].areaId;
            }
            // keep searching until the search space limit is reached
        } while (searchHead < searchTail && searchTail <= 510);
        return -1;
    }

    /// <summary>
    /// Searches a valid (enemy) target and returns its area id.
    /// </summary>
    /// <param name="areaId">area of the searching unit</param>
    /// <param name="armyIndex">index of the searching unit</param>
    /// <returns>a valid target id or -1</returns>
    public int searchNode(int areaId, int armyIndex)
    {
        searchHead++;
        int CurrentNodeAreaID = searchQueue[searchHead].areaId;
        int i;
        for (i = 0; i < g_Scene.GetNumAdjacentAreas(CurrentNodeAreaID); i++)
        {
            CArea SearchArea = g_Scene.GetAdjacentArea(CurrentNodeAreaID, i);
            // already visited
            if (visited[SearchArea.id])
                continue;
            // otherwise check whether the area can be moved to or attacked;
            // areaId is the one that matters here, not CurrentNodeAreaId
            if (!aiCheckMoveable(CurrentNodeAreaID, SearchArea.id, armyIndex, areaId)
                && !aiCheckAttackable(CurrentNodeAreaID, SearchArea.id, armyIndex, areaId))
                continue;

            // Allied land cannot be passed (kept as a comment in the original, players may still
            // occupy allied land; to be unified in aiCheckMoveable later).
            if (searchTail < 511)
                searchTail += 1;
            // maintain the reachability edges
            visited[SearchArea.id] = true;
            searchQueue[searchTail].areaId = SearchArea.id;
            searchQueue[searchTail].previous = searchHead;

            // is the node already a valid target? only enemies are final path targets
            if (!getAlliance(areaId, SearchArea.id, (int)ENEMY))
                continue;

            // the searching unit is a navy unit
            if (g_Scene.GetArea(areaId).GetArmy(armyIndex).IsNavy())
            {
                if (!SearchArea.info.sea)
                {
                    // 3 with enemies, otherwise 1: attack weighs most
                    if (SearchArea.armyCount > 0)
                        targetNodeType = 3;
                    else
                        targetNodeType = 1;
                    return SearchArea.id;
                }
                // valuable sea area: 2
                if (Terrain.Area(SearchArea.info.areaType).port)
                {
                    targetNodeType = 2;
                    return SearchArea.id;
                }
                continue;
            }
            // not at sea
            if (!SearchArea.info.sea)
            {
                // high value areas matter more
                if (calcAreaValue(SearchArea) > 80)
                    targetNodeType = 3;
                else
                    // attack over move
                    targetNodeType = (SearchArea.armyCount > 0) ? 2 : 1;
                return SearchArea.id;
            }
        }
        return -1;
    }

    /// <summary>
    /// Special search frame meant to replace most getNeighbor calls later.
    /// </summary>
    /// <param name="startAreaID">area of the attacking unit</param>
    /// <param name="lowest">closest attack range (steps, not a radius)</param>
    /// <param name="highest">farthest attack range</param>
    public static List<int> getSpecialRangeNeighbourIds(int startAreaID, int lowest, int highest)
    {
        return g_Scene.BFS(startAreaID, highest, (AreaBFSNode<int> node) =>
        {
            return node.depth >= lowest && node.depth <= highest;
        });
    }

    /// <summary>
    /// Multi purpose neighbour query; callers will move to small dedicated functions later.
    /// </summary>
    /// <param name="startAreaId">search start</param>
    /// <param name="mode">search mode, see the code comments</param>
    /// <param name="alliance">0, 1 or 2 for SELF, ENEMY, NOT_ALLY</param>
    /// <param name="range">search range, at most 2 in the original</param>
    /// <returns>a neighbour id or a value</returns>
    public int getNeighbor(int startAreaId, int mode, int alliance, int range)
    {
        int numAdjacentAreas = g_Scene.GetNumAdjacentAreas(startAreaId);
        Array.Fill(getNeighbourId, -1);
        int allianceEnum;
        if (alliance == 2)
        {
            allianceEnum = (int)NOT_ALLY;
        }
        else if (alliance == 1)
        {
            allianceEnum = (int)ENEMY;
        }
        else
        {
            allianceEnum = (int)SELF;
        }
        switch (mode)
        {
            case 2000:
            {
                // all sea neighbours of the area
                int count = 0;
                for (int i = 0; i < numAdjacentAreas; i++)
                {
                    CArea area = g_Scene.GetAdjacentArea(startAreaId, i);
                    if (area.info.sea)
                    {
                        count++;
                        getNeighbourId[i] = area.id;
                    }
                }
                return count;
            }
            case 2010:
            {
                // hp sum of the neighbours matching the alliance condition
                int value = 0;
                for (int i = 0; i < numAdjacentAreas; i++)
                {
                    CArea area = g_Scene.GetAdjacentArea(startAreaId, i);
                    if (!getAlliance(startAreaId, area.id, allianceEnum)) continue;

                    if (area.armyCount > 0)
                        value += calcAreaArmyHp(area, false);
                }
                return value;
            }
            case 2020:
            {
                int localMinHp = int.MaxValue;
                int selectedAreaId = -1;
                // minimum hp among the matching neighbours
                for (int i = 0; i < numAdjacentAreas; i++)
                {
                    CArea area = g_Scene.GetAdjacentArea(startAreaId, i);
                    if (!getAlliance(startAreaId, area.id, allianceEnum)) continue;

                    if (area.armyCount > 0)
                    {
                        int areaArmyHp = calcAreaArmyHp(area, false);
                        if (areaArmyHp < localMinHp)
                        {
                            localMinHp = areaArmyHp;
                            selectedAreaId = area.id;
                            getNeighbourId[0] = areaArmyHp;
                        }
                    }
                }
                return selectedAreaId;
            }
            case 2030:
            {
                // maximum area army hp among the neighbours
                int curMaxArmyHp = 0;
                for (int i = 0; i < numAdjacentAreas; i++)
                {
                    CArea area = g_Scene.GetAdjacentArea(startAreaId, i);
                    if (!getAlliance(startAreaId, area.id, allianceEnum)) continue;
                    if (area.armyCount > 0)
                    {
                        int adjacentAreaArmyHp = calcAreaArmyHp(area, false);
                        if (adjacentAreaArmyHp > curMaxArmyHp)
                        {
                            curMaxArmyHp = adjacentAreaArmyHp;
                            getNeighbourId[0] = area.id;
                        }
                    }
                }
                return curMaxArmyHp;
            }
            case 2060:
            {
                // Probably not greedy enough, but nobody uses this case; keyed on the area value.
                int selectedId = -1;
                for (int i = 0; i < numAdjacentAreas; i++)
                {
                    CArea area = g_Scene.GetAdjacentArea(startAreaId, i);
                    if (!getAlliance(startAreaId, area.id, allianceEnum)) continue;
                    int localValue = calcAreaValue(area);
                    if (localValue > 0)
                    {
                        selectedId = area.id;
                        getNeighbourId[0] = localValue;
                    }
                }
                return selectedId;
            }
            case 2100:
            {
                // to be changed later, or handled specially at every 2100 call site
                List<int> bfsForAllInRange = getSpecialRangeNeighbourIds(startAreaId, 1, range);
                for (int i = 0; i < 50; i++)
                {
                    if (i >= bfsForAllInRange.Count) break;
                    getNeighbourId[i] = bfsForAllInRange[i];
                }
                bfsForAllInRange.Clear();
                return 0;
            }
            default:
                break;
        }
        if (mode != 2050 && mode != 2040 && mode != 2070 && mode != 2080)
            return -1;
        for (int i = 0; i < numAdjacentAreas; i++)
        {
            CArea area = g_Scene.GetAdjacentArea(startAreaId, i);

            if (!getAlliance(startAreaId, area.id, allianceEnum))
                continue;
            switch (mode)
            {
                case 2040:
                    // return when enemies exist
                    if (area.armyCount != 0)
                        getNeighbourId[0] = area.id;
                    break;
                case 2050:
                    // empty neighbour areas
                    if (area.armyCount == 0)
                        getNeighbourId[0] = area.id;
                    break;

                case 2070:
                    if (area.armyCount != 0)
                    {
                        if (area.info.sea)
                        {
                            // Units around that trigger the fort second counter. The old logic
                            // approximated this with IsNavy(); now the combat trait is read directly
                            // so custom units can declare it explicitly.
                            CArmy army = area.GetArmy(0);
                            if (army != null && army.basicAbilities != null &&
                                (army.basicAbilities.combat.traits & ARMY_COMBAT_TRAIT_RECV_FORT_SECOND_COUNTER) != 0)
                            {
                                return area.id;
                            }
                        }
                    }
                    break;
                default:
                    // 2080: artillery search, used to decide forts and radars. This needs a strict
                    // definition of what forts / radars protect against (see the CFight reductions).
                    if (area.armyCount != 0 && !area.info.sea)
                    {
                        CArmy topArmy = area.GetArmy(0);
                        if (topArmy != null)
                        {
                            // new rule: units one step away that can attack us without a counter
                            if (topArmy.basicAbilities.combat.attack.range.shape == COMBAT_RANGE_GRAPH &&
                                topArmy.basicAbilities.combat.attack.range.minRange <= 1000 &&
                                topArmy.basicAbilities.combat.attack.range.maxRange > 0 &&
                                topArmy.basicAbilities.combat.attack.engagementMode == COMBAT_ENGAGEMENT_PROJECTILE)
                            {
                                return area.id;
                            }
                            // the old logic looked for artillery on top of the stack
                        }
                    }
                    break;
            }
        }
        // not found
        return -1;
    }
}
