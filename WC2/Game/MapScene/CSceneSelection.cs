namespace WC2;

/// <summary>Area selection, targets and combat range queries (CSceneSelection.cpp).</summary>
public sealed partial class CScene
{
    /// <summary>Computes and stores the target arrow vertices between two areas.</summary>
    /// <param name="startAreaId">Arrow start area.</param>
    /// <param name="targetAreaId">Arrow end area.</param>
    public void CreateArrow(int startAreaId, int targetAreaId)
    {
        float vectorX = 0.0f;
        float vectorY = 0.0f;
        CArea startArea = GetArea(startAreaId);
        CArea targetArea = GetArea(targetAreaId);
        int startX = startArea.info.armyPos[0];
        int startY = startArea.info.armyPos[1];
        int endX = targetArea.info.armyPos[0];
        int endY = targetArea.info.armyPos[1];
        int yDif = endY - startY;
        int xDif = endX - startX;
        float distance = InvSqrt((float)(xDif * xDif) + (float)(yDif * yDif));
        vectorX = xDif * distance;
        vectorY = yDif * distance;
        // hgeVector::Rotate(1.5708f): rotate the unit direction by 90 degrees.
        {
            const float a = 1.5708f;
            float rx = vectorX * MathF.Cos(a) - vectorY * MathF.Sin(a);
            float ry = vectorX * MathF.Sin(a) + vectorY * MathF.Cos(a);
            vectorX = rx;
            vectorY = ry;
        }

        targetArea.arrowStartOffset[0] = startX + vectorX * 22.0f;
        targetArea.arrowStartOffset[1] = startY + vectorY * 22.0f;
        targetArea.arrowStartOffsetMinus[0] = startX - vectorX * 22.0f;
        targetArea.arrowStartOffsetMinus[1] = startY - vectorY * 22.0f;
        targetArea.arrowEndOffset[0] = endX + vectorX * 22.0f;
        targetArea.arrowEndOffset[1] = endY + vectorY * 22.0f;
        targetArea.arrowEndOffsetMinus[0] = endX - vectorX * 22.0f;
        targetArea.arrowEndOffsetMinus[1] = endY - vectorY * 22.0f;
        arrowTimer = 0.2f;
    }

    /// <summary>Converts the range stored in the army xml to a topological distance (1000 = one cell).</summary>
    private static float GraphRangeToCells(float range)
    {
        // 1000 always means one cell; the old cell-count format is no longer accepted.
        if (!float.IsFinite(range))
            return range;
        return range / 1000.0f;
    }

    /// <summary>Circle ranges are configured as 1000 = map distance 100; the spatial index uses map units.</summary>
    private static float CircleRangeToMapDistance(float range)
    {
        return range / 10.0f;
    }

    /// <summary>
    /// Generates the candidate target areas from the attack mode and range configuration of a unit.
    /// Graph ranges use BFS, circle ranges use the CScene spatial index; only spatial reachability is
    /// computed here, not alliances, unit state or whether the target is garrisoned.
    /// </summary>
    /// <param name="startAreaId">Area of the attacking unit.</param>
    /// <param name="profile">Attack profile of the unit.</param>
    /// <param name="country">Country used to resolve radius-source="country".</param>
    /// <returns>Ids of the areas inside the range.</returns>
    public List<int> GetCombatTargetAreas(int startAreaId, ArmyCombatProfile profile, CCountry country)
    {
        // Invalid start area.
        if (startAreaId < 0 || startAreaId >= areaList.Count || GetArea(startAreaId) == null)
            return new List<int>();

        ArmyCombatRange range = profile.range;

        // Circle radius: use the circle query.
        if (range.shape == COMBAT_RANGE_CIRCLE)
        {
            float maxDistance = range.maxRange;
            if (range.radiusFromCountry && country != null)
                maxDistance = country.AirstrikeRadius();
            float minDistance = CircleRangeToMapDistance(range.minRange);
            if (!range.radiusFromCountry)
                maxDistance = CircleRangeToMapDistance(maxDistance);
            return GetAreasInCircleRange(startAreaId, minDistance, maxDistance, range.maxInclusive);
        }

        // Otherwise topological.
        float minCells = Math.Max(0.0f, GraphRangeToCells(range.minRange));
        float maxCells = GraphRangeToCells(range.maxRange);
        int minDepth = float.IsFinite(minCells)
                       ? (int)MathF.Ceiling(minCells)
                       : Math.Max(0, GetNumAreas()) + 1;
        int maxDepth;
        if (float.IsFinite(maxCells))
        {
            maxDepth = (int)MathF.Ceiling(Math.Max(0.0f, maxCells));
            if (!range.maxInclusive && maxDepth > 0 && maxDepth >= maxCells)
                --maxDepth;
        }
        else
        {
            maxDepth = Math.Max(0, GetNumAreas());
        }

        // BFS: collect every area inside the topological attack range.
        return BFS(startAreaId, maxDepth, (AreaBFSNode<int> node) =>
        {
            if (node.depth == 0 || node.depth < minDepth || node.depth > maxDepth)
                return false;
            return true;
        });
    }

    /// <summary>
    /// Shortest topological distance between two areas (number of adjacency edges); -1 when invalid or unreachable.
    /// BFS reaches the target first with the shortest distance.
    /// </summary>
    public int GetTopologicalDistance(int startAreaId, int targetAreaId)
    {
        int areaCount = GetNumAreas();
        if (startAreaId < 0 || targetAreaId < 0 ||
            startAreaId >= areaCount || targetAreaId >= areaCount ||
            GetArea(startAreaId) == null ||
            GetArea(targetAreaId) == null)
        {
            return -1;
        }
        if (startAreaId == targetAreaId)
            return 0;

        int distance = -1;
        int maxDepth = Math.Max(0, areaCount);
        BFS(startAreaId, maxDepth, (AreaBFSNode<int> node) =>
        {
            if (node.id == targetAreaId && distance < 0)
                distance = node.depth;
            return false;
        });
        return distance;
    }

    /// <summary>Computes the moveable and attackable targets of the selected area.</summary>
    /// <param name="startArea">Currently selected start area.</param>
    public void SetSelAreaTargets(CArea startArea)
    {
        if (startArea == null || startArea.armyCount <= 0 || !startArea.IsActive() ||
            startArea.country == null || startArea.country.ai || g_GameManager.AIAction)
        {
            return;
        }

        CArmy army = startArea.GetArmy(0);
        if (army == null || army.basicAbilities == null)
            return;

        int adjacentCount = GetNumAdjacentAreas(startArea.id);
        for (int i = 0; i < adjacentCount; ++i)
        {
            CArea targetArea = GetAdjacentArea(startArea.id, i);
            if (targetArea == null)
                continue;

            // Move arrow. Not tied to unit traits yet: everybody still moves to adjacent areas.
            if (CheckMoveable(startArea.id, targetArea.id, 0))
            {
                targetArea.targetType = 3;
                CreateArrow(startArea.id, targetArea.id);
            }
        }

        // The attack mode decides the target arrow: direct attacks use type0 (targetType 4);
        // projectile/harassment use type1 and air strikes type2, both targetType 2, so they
        // share the yellow ranged arrow.
        ArmyCombatProfile attack = army.basicAbilities.combat.attack;
        List<int> attackTargets = GetCombatTargetAreas(startArea.id, attack, startArea.country);
        foreach (int targetAreaId in attackTargets)
        {
            CArea targetArea = GetArea(targetAreaId);
            if (targetArea == null ||
                !CActionAssist.Instance().aiCheckAttackable(startArea.id, targetAreaId, 0, startArea.id))
            {
                continue;
            }
            if (attack.mode == COMBAT_ATTACK_DIRECT)
            {
                targetArea.targetType = 4;
                CreateArrow(startArea.id, targetAreaId);
            }
            else
            {
                targetArea.targetType = 2;
            }
        }
    }

    /// <summary>Checks whether the army can move to the target area.</summary>
    public bool CheckMoveable(int StartAreaID, int TargetAreaID, int ArmyIndex)
    {
        if (!CheckAdjacent(StartAreaID, TargetAreaID))
            return false;
        return CActionAssist.Instance().aiCheckMoveable(StartAreaID, TargetAreaID, ArmyIndex, StartAreaID);
    }

    /// <summary>
    /// Checks whether the army can attack the target area based on the map relation between the
    /// two areas (defined in fights/FightAssist.cpp in the C++).
    /// </summary>
    /// <param name="StartAreaID">Attacking area.</param>
    /// <param name="TargetAreaID">Attacked area.</param>
    /// <param name="ArmyIndex">Index of the unit in the stack.</param>
    public bool CheckAttackable(int StartAreaID, int TargetAreaID, int ArmyIndex)
    {
        if (!CActionAssist.Instance().aiCheckAttackable(StartAreaID, TargetAreaID, ArmyIndex, StartAreaID))
            return false;
        CArea StartArea = g_Scene.GetArea(StartAreaID);
        if (StartArea == null || StartArea.GetArmy(ArmyIndex) == null ||
            StartArea.GetArmy(ArmyIndex).basicAbilities == null ||
            StartArea.country == null)
        {
            return false;
        }
        ArmyCombatProfile attack = StartArea.GetArmy(ArmyIndex).basicAbilities.combat.attack;
        List<int> targets = GetCombatTargetAreas(StartAreaID, attack, StartArea.country);
        return targets.Contains(TargetAreaID);
    }

    /// <summary>Clears the target marks of every area.</summary>
    public void ClearTargets()
    {
        int validAreaCount = Math.Min(areaCount, areaList.Count);
        for (int areaId = 0; areaId < validAreaCount; ++areaId)
        {
            CArea area = areaList[areaId];
            if (area != null)
                area.targetType = 0;
        }
    }

    /// <summary>Cancels the current area selection.</summary>
    public void UnselectArea()
    {
        if (areaSelected == null)
            return;

        areaSelected.selected = false;
        areaSelected = null;
        ClearTargets();
    }

    /// <summary>Selects an area and refreshes the target marks.</summary>
    public void SelectArea(CArea area)
    {
        if (area == null)
            return;

        UnselectArea();
        areaSelected = area;
        area.selected = true;
        SetSelAreaTargets(area);
    }

    /// <summary>Selects an area by id and refreshes the target marks.</summary>
    public void SelectArea(int areaId)
    {
        if (areaId < 0 || areaId >= areaList.Count)
            return;
        SelectArea(GetArea(areaId));
    }

    /// <summary>Recomputes the target marks of the selected area.</summary>
    public void ResetTarget()
    {
        ClearTargets();
        if (areaSelected != null)
            SetSelAreaTargets(areaSelected);
    }

    /// <summary>Currently selected area, or null.</summary>
    public CArea GetSelectedArea()
    {
        return areaSelected;
    }
}
