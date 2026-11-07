namespace WC2;

/// <summary>Unit evaluation helpers of the AI (CActionAssistUnits.cpp).</summary>
public sealed partial class CActionAssist
{
    /// <summary>
    /// Uses the official CFight rules to find out whether the target can counter the AI's attack.
    /// A temporary CFight is used; no damage is applied, only range, engagement mode and fort correction.
    /// </summary>
    private static CFight s_counterCheck;

    private static bool CanCounterForAI(int startAreaId, int targetAreaId,
        CArmy startArmy, CArmy targetArmy, CArea targetArea)
    {
        if (startArmy == null || targetArmy == null || targetArea == null)
        {
            return false;
        }

        // one reused CFight (the check sets every input and CheckCounter resets its outputs)
        var counterCheck = s_counterCheck ??= new CFight();
        counterCheck.startAreaId = startAreaId;
        counterCheck.targetAreaId = targetAreaId;
        counterCheck.topologicalDistance = g_Scene.GetTopologicalDistance(startAreaId, targetAreaId);
        counterCheck.secondAttackIndex = 0;
        counterCheck.startArmySecondAttack = false;
        counterCheck.targetArmySecondAttack = false;
        counterCheck.CheckCounter(startArmy, targetArmy, targetArea);
        return counterCheck.canCounter;
    }

    /// <summary>Combined damage rate when the AI unit attacks the target.</summary>
    private static float GetAiAttackDamageRate(CArmy attacker, CArmy target)
    {
        if (attacker == null || target == null ||
            attacker.basicAbilities == null ||
            target.basicAbilities == null)
        {
            return 1.0f;
        }

        float unitRate = GetCombatDamageRate(
            attacker.basicAbilities.combat,
            target.basicAbilities.unitType,
            COMBAT_DAMAGE_ATTACK);
        float weaponRate = GetWeaponArmorDamageRate(
            CObjectDef.Instance().weaponArmorDamageRates,
            attacker.basicAbilities.weaponType,
            target.basicAbilities.armorType);
        if (float.IsInfinity(unitRate) || float.IsInfinity(weaponRate))
        {
            return float.PositiveInfinity;
        }
        return unitRate * weaponRate;
    }

    /// <summary>
    /// Attack tendency added for the combined damage rate: 1.0 is neutral, bonuses add a positive
    /// tendency and reductions a negative one.
    /// </summary>
    private static int AddAiDamageTendency(float damageRate)
    {
        // no usable rate: keep the base score instead of treating a broken config as an extreme advantage
        if (float.IsNaN(damageRate))
        {
            return 0;
        }
        if (float.IsInfinity(damageRate))
        {
            return 40000;
        }
        else
        {
            // 1.0 is the neutral point: every point away from it changes the score by 400.
            // Negative tendencies are scaled down so the AI does not drop a target over a small reduction.
            int tendency = (int)((damageRate - 1.0f) * 400.0f);
            if (tendency < 0)
            {
                tendency = Math.Min(-1, tendency / 10);
            }
            return tendency;
        }
    }

    /// <summary>
    /// Whether the owner of the area is AI controlled. Areas without an owner count as AI, like the
    /// original treats neutral / unowned areas.
    /// </summary>
    public bool isAI(int areaId)
    {
        CCountry country = g_Scene.GetArea(areaId).country;
        return country != null ? country.ai : true;
    }

    /// <summary>Area value used by the AI decisions.</summary>
    public int calcAreaValue(CArea area)
    {
        if (area == null)
            return -1;
        // original: tax * 2 + industry * 3 + area type (normal 1, capital 250, port 80, large city 150,
        // normal city 80) + installation (fort 20, entrenchment 15, anti-aircraft 10); data/json/ai/profiles.json
        var P = AiProfiles.Current;
        return area.GetRealTax() * P.taxWeight + area.GetIndustry() * P.industryWeight +
               P.AreaTypeValue((int)area.info.areaType) + P.InstallationValue((int)area.installationType);
    }

    /// <summary>
    /// Improved profiles: how much an own area is worth defending (capital, port, airfield, city,
    /// home area weights of the profile) times the threat (enemy hp around / own hp, capped), times
    /// <paramref name="percent"/> / 100. 0 for areas of other countries and when no enemy is around.
    /// </summary>
    private int DefenceBonus(CArea area, int enemyHp, int ownHp, int percent)
    {
        if (area == null || enemyHp <= 0 || percent == 0) return 0;
        var P = AiProfiles.Current;
        var type = Terrain.Area(area.info.areaType);
        int weight = 0;
        if (type.capital) weight += P.defendCapital;
        if (type.port) weight += P.defendPort;
        if (area.info.constructionType == AIRPORT && area.info.constructionLevel > 0) weight += P.defendAirfield;
        if (type.cityLevel > 0 || (area.info.constructionType == CITY && area.info.constructionLevel > 0)) weight += P.defendCity;
        if (area.country != null && CActionAI.Instance().IsHomeArea(area)) weight += P.defendHome;
        if (weight == 0) return 0;
        int threatPercent = Math.Min(P.threatCapPercent, enemyHp * 100 / Math.Max(1, ownHp));
        return weight * threatPercent / 100 * percent / 100;
    }

    /// <summary>The unit is the last one in its own capital and enemies are around.</summary>
    private static bool LeavesCapitalOpen(CArea area, int armyCountHere, int enemyHpAround) =>
        armyCountHere <= 1 && enemyHpAround > 0 && Terrain.Area(area.info.areaType).capital;

    /// <summary>
    /// Hp of the enemy units next to an area (mode 2010 of getNeighbor, without touching the shared
    /// neighbour array the move evaluation iterates).
    /// </summary>
    private int EnemyHpAround(int areaId)
    {
        int value = 0;
        int count = g_Scene.GetNumAdjacentAreas(areaId);
        for (int i = 0; i < count; i++)
        {
            CArea area = g_Scene.GetAdjacentArea(areaId, i);
            if (area == null || area.armyCount <= 0) continue;
            if (getAlliance(areaId, area.id, (int)ENEMY)) value += calcAreaArmyHp(area, false);
        }
        return value;
    }

    /// <summary>sub_61504: forwards to CArea.calcAreaArmyHp.</summary>
    public static int calcAreaArmyHp(CArea area, bool TopOnly)
    {
        if (area == null)
            return -1;
        return area.calcAreaArmyHp(TopOnly);
    }

    /// <summary>
    /// Basic AI checks whether the unit of the given area can attack the target area.
    /// Adjacency is not required here; the commented checks moved to CScene.
    /// </summary>
    /// <param name="StartAreaID">unused, like in aiCheckMoveable</param>
    /// <param name="TargetAreaID">attacked area id</param>
    /// <param name="ArmyIndex">index of the attacking unit</param>
    /// <param name="ArmyAreaID">area of the attacking unit</param>
    /// <returns>whether the target can be attacked</returns>
    public bool aiCheckAttackable(int StartAreaID, int TargetAreaID, int ArmyIndex, int ArmyAreaID)
    {
        // allies cannot be attacked
        if (getAlliance(ArmyAreaID, TargetAreaID, (int)ALLY))
            return false;
        CArea ArmyArea = g_Scene.GetArea(ArmyAreaID);
        // nor countries at peace (scenario relations)
        if (Diplomacy.AtPeace(ArmyArea.country, g_Scene.GetArea(TargetAreaID).country))
            return false;
        // invalid index, for start and target alike
        if (ArmyArea.armyCount <= ArmyIndex)
            return false;
        CArea TargetArea = g_Scene.GetArea(TargetAreaID);
        if (TargetArea.armyCount <= 0)
            return false;
        CArmy Army = ArmyArea.GetArmy(ArmyIndex);
        // the unit itself cannot attack without movement
        if (!Army.active || Army.movement <= 0)
        {
            return false;
        }
        return Army.movement > 0;
    }

    /// <summary>
    /// AI check whether a unit can move to the target area. Assumes a move costs 1 movement.
    /// Adjacency is not required here.
    /// </summary>
    /// <param name="StartAreaID">unused (inlined in the original)</param>
    /// <param name="TargetAreaID">target area id</param>
    /// <param name="ArmyIndex">index of the moving unit in its area</param>
    /// <param name="ArmyAreaID">the real area of the moving unit</param>
    /// <returns>whether the unit can move there</returns>
    public bool aiCheckMoveable(int StartAreaID, int TargetAreaID, int ArmyIndex, int ArmyAreaID)
    {
        CArea armyArea = g_Scene.GetArea(ArmyAreaID);
        CArea targetArea = g_Scene.GetArea(TargetAreaID);
        // foreign area with units
        if (armyArea.country != targetArea.country && targetArea.armyCount != 0)
            return false;
        // the areas of a country at peace stay closed (scenario relations)
        if (Diplomacy.AtPeace(armyArea.country, targetArea.country))
            return false;
        if (armyArea.country.ai && getAlliance(ArmyAreaID, TargetAreaID, (int)ALLY)
            && !getAlliance(ArmyAreaID, TargetAreaID, (int)SELF) && !targetArea.info.sea)
        {
            return false;
        }
        // invalid index
        if (armyArea.armyCount <= ArmyIndex)
            return false;
        CArmy Army = armyArea.GetArmy(ArmyIndex);
        // the unit cannot move (terrain support would go here)
        if (Army.movement <= 0)
            return false;
        // whether the target area has room for another unit
        return targetArea.CanContainArmy(Army);
    }

    /// <summary>
    /// Evaluates the given mode and pushes the best node into actionToNextAreaTargetIdList.
    /// </summary>
    /// <param name="mode">0x1000 stay, 0x2000 move, 0x3000 attack, 0x4000 path finding</param>
    /// <param name="areaId">evaluated area id</param>
    /// <param name="armyIndex">index of the unit in the area stack</param>
    /// <param name="unused">unknown fourth parameter</param>
    public void actionToNextArea(int mode, int areaId, int armyIndex, int unused)
    {
        // shared values (every number below comes from the AI profile, data/json/ai/profiles.json)
        var P = AiProfiles.Current;
        CArea areaIn = g_Scene.GetArea(areaId);
        CArmy armyIn = areaIn.army[armyIndex];
        int armyCountHere = areaIn.armyCount;
        int areaInValue = calcAreaValue(areaIn);
        int areaInArmyHp = calcAreaArmyHp(areaIn, false);
        int neighboursEnemyHealthSum = getNeighbor(areaId, 2010, 1, 1);
        bool ifAreaInSea = areaIn.info.sea;
        targetNode.actionToNextAreaArmyIndex = armyIndex;
        CCountry countryIn = areaIn.country;
        switch (mode)
        {
            case 0x1000:
            {
                // the action target is the area itself: the reserve stays
                targetNode.targetValue = 0;
                targetNode.actionToNextAreaTargetId = areaId;

                // based on the area value and the army count
                int areaValueArmyCountDiff = areaInValue / 100 - armyCountHere;
                if (areaValueArmyCountDiff < 0)
                {
                    // the garrison matters more: lower the base value
                    targetNode.targetValue = areaInValue / (1 - areaValueArmyCountDiff);
                }
                else
                {
                    // the area value matters more (garrison too weak for the value): raise it
                    targetNode.targetValue = (areaValueArmyCountDiff + 1) * areaInValue;
                }

                if (neighboursEnemyHealthSum == 0)
                {
                    // no enemies around: staying is much less likely
                    targetNode.targetValue /= (P.stayNoEnemyDivisorPerUnit * armyCountHere + 1);
                }
                else if (neighboursEnemyHealthSum > areaInArmyHp)
                {
                    // enemies around are stronger than this area's units
                    targetNode.targetValue *= P.stayThreatenedMultiplier;
                }
                // improved profiles: hold threatened capitals, ports, airfields, cities and home areas
                if (P.defendStayPercent != 0)
                    targetNode.targetValue += DefenceBonus(areaIn, neighboursEnemyHealthSum, areaInArmyHp, P.defendStayPercent);
                bool ifPlayerPlaying = false;
                if (countryIn != null)
                {
                    ifPlayerPlaying = !(countryIn.ai);
                }
                targetNode.targetIsPlayer = ifPlayerPlaying;
                actionToNextAreaTargetIdList.Add(targetNode);
                break;
            }
            case 0x2000:
            {
                // move evaluation: all adjacent areas (the 2100 call will become a direct vector later)
                getNeighbor(areaId, 2100, 0, 1);
                // every found neighbour
                foreach (int i in getNeighbourId)
                {
                    targetNode.targetValue = 0;
                    if (i < 0) continue;
                    // can the unit move there?
                    bool canMove = g_Scene.CheckMoveable(areaId, i, armyIndex);
                    if (!canMove) continue;
                    CArea targetArea = g_Scene.GetArea(i);
                    if (targetArea == null) continue;
                    // the AI cannot occupy allied land areas
                    targetNode.actionToNextAreaTargetId = i;
                    int targetAreaValue = calcAreaValue(targetArea);
                    if (getAlliance(areaId, targetArea.id, (int)SELF))
                    {
                        // own territory
                        if (targetArea.armyCount == 0 && areaIn.armyCount > 1)
                        {
                            // empty target and more than one unit here (defenders remain after the move)
                            targetNode.targetValue += targetAreaValue * P.moveEmptyOwnAreaPercent / 100;
                        }
                        // improved profiles: reinforce a threatened capital, port, airfield, city or home area
                        if (P.defendReinforcePercent != 0)
                            targetNode.targetValue += DefenceBonus(targetArea, EnemyHpAround(targetArea.id),
                                calcAreaArmyHp(targetArea, false), P.defendReinforcePercent);
                    }
                    else if (getAlliance(areaId, targetArea.id, (int)ENEMY))
                    {
                        // enemy territory
                        if (neighboursEnemyHealthSum != 0)
                        {
                            // other enemies around: hp difference plus army count times target value
                            targetNode.targetValue += (areaInArmyHp +
                                                       armyCountHere * targetAreaValue
                                                       - neighboursEnemyHealthSum + P.moveEnemyBonus);
                        }
                        else
                        {
                            // otherwise only army count and target value
                            targetNode.targetValue += (targetAreaValue +
                                                       armyCountHere * targetAreaValue
                                                       + P.moveEnemyBonus);
                        }
                        if ((!targetArea.info.sea) && ifAreaInSea)
                        {
                            // landing is favoured
                            targetNode.targetValue += P.landingBonus;
                        }
                    }
                    else if (getAlliance(areaId, targetArea.id, (int)NOT_ALLY))
                    {
                        // neutral / unowned areas: lower occupation tendency
                        if (neighboursEnemyHealthSum != 0)
                        {
                            targetNode.targetValue += (areaInArmyHp +
                                                       armyCountHere * targetAreaValue
                                                       - neighboursEnemyHealthSum +
                                                       P.moveNeutralBonus);
                        }
                        else
                        {
                            targetNode.targetValue += (targetAreaValue +
                                                       armyCountHere * targetAreaValue + P.moveNeutralBonus);
                        }
                        if ((!targetArea.info.sea) && ifAreaInSea)
                        {
                            targetNode.targetValue += P.landingBonus;
                        }
                    }

                    // hp ratio of the unit as coefficient
                    float coefficient = armyIn.CalcHpRate();
                    if (coefficient >= 0.5)
                    {
                        coefficient = 1.0f;
                    }

                    targetNode.targetIsPlayer = targetArea.country != null && !targetArea.country.ai;
                    // the AI prefers player areas (the decompiled code is odd, this follows the semantics)
                    if (targetNode.targetIsPlayer)
                    {
                        targetNode.targetValue += P.movePlayerBonus;
                    }
                    // improved profiles: do not take the last unit out of a threatened capital
                    if (P.capitalLastUnitPenalty != 0 && LeavesCapitalOpen(areaIn, armyCountHere, neighboursEnemyHealthSum))
                        targetNode.targetValue -= P.capitalLastUnitPenalty;
                    // original: (int)(rate * value); units below half strength are scaled, not zeroed
                    targetNode.targetValue = (int)(coefficient * targetNode.targetValue);
                    actionToNextAreaTargetIdList.Add(targetNode);
                }
                break;
            }
            case 0x3000:
            {
                // attack nodes. Target search uses the unit's combat range configuration (see CScene
                // and CFight); no fixed length neighbour array so large ranges are not cut at 50.
                List<int> combatTargets = g_Scene.GetCombatTargetAreas(
                    areaId, armyIn.basicAbilities.combat.attack, areaIn.country);
                foreach (int i in combatTargets)
                {
                    targetNode.targetValue = 0;
                    // really attackable?
                    if (i < 0) continue;
                    if (!g_Scene.CheckAttackable(areaId, i, armyIndex)) continue;
                    targetNode.actionToNextAreaTargetId = i;
                    CArea targetArea = g_Scene.GetArea(i);
                    if (targetArea.armyCount > 0)
                    {
                        // never attack allies (aiCheckAttackable already checked, kept anyway)
                        if (getAlliance(areaId, i, (int)ALLY)) continue;
                        CArmy targetArmy = targetArea.GetArmy(0);

                        // the original lowered the chance of attacking friends a lot; now they are just skipped
                        int extraBenefit = 0;
                        int armyCountCoe = 0;
                        if (getAlliance(areaId, i, (int)ENEMY))
                        {
                            if (neighboursEnemyHealthSum > areaInArmyHp)
                            {
                                armyCountCoe = armyCountHere - 1;
                                extraBenefit = P.attackEnemyExtra;
                            }
                            else if (neighboursEnemyHealthSum < areaInArmyHp)
                            {
                                armyCountCoe = armyCountHere;
                                extraBenefit = P.attackEnemyExtra;
                            }
                        }
                        else
                        {
                            if (getAlliance(areaId, i, (int)NOT_ALLY))
                            {
                                if (neighboursEnemyHealthSum > areaInArmyHp)
                                {
                                    armyCountCoe = armyCountHere - 1;
                                    extraBenefit = P.attackNeutralExtra;
                                }
                                else if (neighboursEnemyHealthSum < areaInArmyHp)
                                {
                                    armyCountCoe = armyCountHere;
                                    extraBenefit = P.attackNeutralExtra;
                                }
                            }
                        }

                        // original: coe * (own hp + area value - target hp) + extra; nothing at all when
                        // the surrounding strengths are equal (coe and extra stay 0)
                        targetNode.targetValue += armyCountCoe * (armyIn.hp + calcAreaValue(targetArea) - targetArmy.hp)
                                                  + extraBenefit;
                        // improved profiles: the units behind the front defender count too
                        if (P.attackStackHpPercent != 0)
                            targetNode.targetValue -= P.attackStackHpPercent * (calcAreaArmyHp(targetArea, false) - targetArmy.hp) / 100;

                        // Unit type and weapon/armor relations; composite weapon types already take
                        // the highest rate inside GetWeaponArmorDamageRate(). Only a tendency is added,
                        // the score is not multiplied.
                        targetNode.targetValue += AddAiDamageTendency(GetAiAttackDamageRate(armyIn, targetArmy));

                        // The no-counter bonus comes from the combat rules (default 300, artillery 200
                        // in armydef.xml); it reuses the official CFight counter check.
                        bool canCounter = CanCounterForAI(areaId, i, armyIn, targetArmy, targetArea);
                        if (!canCounter)
                        {
                            targetNode.targetValue += armyIn.basicAbilities.combat.aiNoCounterBonus;
                        }

                        if (areaIn.info.sea && armyIn.IsNavy() && !targetArea.info.sea)
                        {
                            // navy gets the fort second counter
                            if (targetArea.installationType == FORT)
                            {
                                targetNode.targetValue -= P.navyFortPenalty;
                            }
                            else
                            {
                                targetNode.targetValue += P.navyShoreBonus;
                            }
                        }
                        // tactics influence; cards is a bit set (1 << ARMY_CARD), the original masks 1/2/8/4
                        if ((armyIn.cards & (1 << (int)ASSAULT)) != 0)
                            targetNode.targetValue += P.assaultBonus;
                        if ((targetArmy.cards & (1 << (int)DEFENCE)) != 0)
                            targetNode.targetValue -= P.targetDefencePenalty;
                        if ((armyIn.cards & (1 << (int)COMMANDER)) != 0)
                            targetNode.targetValue += P.commanderBonus;
                        // carriers are money too
                        if ((targetArmy.cards & (1 << (int)CARRIER)) != 0)
                            targetNode.targetValue += P.carrierTargetBonus;

                        targetNode.targetIsPlayer = targetArea.country != null && !targetArea.country.ai;
                        if (targetNode.targetIsPlayer)
                        {
                            // the AI prefers hitting the player
                            targetNode.targetValue += P.attackPlayerBonus;
                        }
                        targetNode.targetValue = (int)(targetNode.targetValue * armyIn.CalcHpRate());
                        actionToNextAreaTargetIdList.Add(targetNode);
                    }
                }

                break;
            }
            case 0x4000:
            {
                // path finding, then move towards the first reachable step
                if (areaInValue > P.pathStayIfAreaValueAbove && armyCountHere == 1) break;
                // the country is never null here; higher tech countries are more willing to path find
                targetNode.targetValue = (P.pathTechFactor * countryIn.tech + P.pathBase) * armyCountHere;
                // hp tank
                if (armyIn.hp > P.pathStrongHp) targetNode.targetValue += P.pathStrongBonus;

                // full health raises the tendency
                if (armyIn.IsFullHealth())
                {
                    targetNode.targetValue += P.pathFullHealthBonus;
                }
                // navy moves rather than stays
                if (armyIn.IsNavy() || (armyIn.cards & (1 << (int)CARRIER)) != 0)
                {
                    targetNode.targetValue += P.pathNavyBonus;
                }

                // individual unit strength (bit masks, see 0x3000)
                if ((armyIn.cards & (1 << (int)ASSAULT)) != 0)
                    targetNode.targetValue += P.pathAssaultBonus;
                if ((armyIn.cards & (1 << (int)COMMANDER)) != 0)
                    targetNode.targetValue += P.pathCommanderBonus;
                targetNode.targetValue += P.pathLevelBonus * armyIn.level;
                // improved profiles: do not take the last unit out of a threatened capital
                if (P.capitalLastUnitPenalty != 0 && LeavesCapitalOpen(areaIn, armyCountHere, neighboursEnemyHealthSum))
                    targetNode.targetValue -= P.capitalLastUnitPenalty;
                // surrounding enemies
                if (neighboursEnemyHealthSum != 0)
                {
                    if (areaInArmyHp < neighboursEnemyHealthSum)
                        targetNode.targetValue /= 2;
                }
                else
                {
                    targetNode.targetValue *= 2;
                }

                float coefficient = armyIn.CalcHpRate();
                targetNode.targetValue = (int)(targetNode.targetValue * coefficient);
                targetNode.targetValue -= 100;

                if (targetNode.targetValue > 0)
                {
                    // first step towards the found target (terrain / movement changes affect this too)
                    int firstNeighbourToFoundTarget = searchNodeByID(areaId, armyIndex);
                    if (firstNeighbourToFoundTarget != -1)
                    {
                        targetNode.targetValue += 10 * targetNodeType + 10;
                        targetNode.actionToNextAreaTargetId = firstNeighbourToFoundTarget;
                        CArea targetArea = g_Scene.GetArea(firstNeighbourToFoundTarget);
                        targetNode.targetIsPlayer = targetArea.country != null && !targetArea.country.ai;
                        actionToNextAreaTargetIdList.Add(targetNode);
                        return;
                    }
                }
                break;
            }
            default:
                break;
        }
    }

    /// <summary>
    /// Alliance relation between two areas.
    /// </summary>
    /// <param name="areaId1">first area id</param>
    /// <param name="areaId2">second area id</param>
    /// <param name="allianceType">an ALLIANCE_TYPE value</param>
    public bool getAlliance(int areaId1, int areaId2, int allianceType)
    {
        CArea area1 = g_Scene.GetArea(areaId1);
        CArea area2 = g_Scene.GetArea(areaId2);
        CCountry area1Country = area1.country;
        CCountry area2Country = area2.country;
        int area1Alley = (area1Country == null) ? (int)NO_BELONGING : area1Country.alliance;
        int area2Alley = (area2Country == null) ? (int)NO_BELONGING : area2Country.alliance;

        // countries at peace (scenario relations) are neither enemies nor land to take
        if (((ALLIANCE_TYPE)allianceType == ENEMY || (ALLIANCE_TYPE)allianceType == NOT_ALLY) &&
            Diplomacy.AtPeace(area1Country, area2Country))
        {
            return false;
        }

        switch ((ALLIANCE_TYPE)allianceType)
        {
            case ALLY:
                if (area1Alley == area2Alley)
                {
                    // an alliance must exist to be "allied"
                    return area1Alley != (int)NO_BELONGING;
                }
                return false;
            case NOT_ALLY:
                if (area1Alley == area2Alley)
                {
                    return area1Alley == (int)NO_BELONGING;
                }
                return true;
            case ENEMY:
                if (area1Alley != area2Alley || area1Alley == (int)NO_BELONGING)
                {
                    // Different alliances, and the other one must exist and not be neutral to be "enemy".
                    // Biased: even an unowned area1 sees an owned area2 as enemy.
                    return area2Alley != (int)NEUTRAL && area2Alley != (int)NO_BELONGING;
                }
                return false;
            case SELF:
                // SELF means own territory
                if (area1Country == area2Country)
                {
                    return area1Country != null;
                }
                return false;
            default:
                return false;
        }
    }
}
