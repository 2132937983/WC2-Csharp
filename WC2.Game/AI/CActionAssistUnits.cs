namespace WC2;

/// <summary>Unit evaluation helpers of the AI (CActionAssistUnits.cpp).</summary>
public sealed partial class CActionAssist
{
    /// <summary>
    /// Uses the official CFight rules to find out whether the target can counter the AI's attack.
    /// A temporary CFight is used; no damage is applied, only range, engagement mode and fort correction.
    /// </summary>
    private static bool CanCounterForAI(int startAreaId, int targetAreaId,
        CArmy startArmy, CArmy targetArmy, CArea targetArea)
    {
        if (startArmy == null || targetArmy == null || targetArea == null)
        {
            return false;
        }

        var counterCheck = new CFight();
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
        // original: plain (NORMAL) areas count 1, capital 250, large city 80, city 150, port 80
        int[] TypeAdd = { 1, 250, 80, 150, 80 };
        int[] InstalltionAdd = { 0, 20, 15, 10 };
        return (area.GetRealTax() * 2 + area.GetIndustry() * 3 +
                (((int)area.info.areaType < 5) ? TypeAdd[(int)area.info.areaType] : 0) +
                (((int)area.installationType < 4) ? InstalltionAdd[(int)area.installationType] : 0));
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
        // shared values
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
                    targetNode.targetValue /= (2 * armyCountHere + 1);
                }
                else if (neighboursEnemyHealthSum > areaInArmyHp)
                {
                    // enemies around are stronger than this area's units
                    targetNode.targetValue *= 2;
                }
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
                            targetNode.targetValue += targetAreaValue / 2;
                        }
                    }
                    else if (getAlliance(areaId, targetArea.id, (int)ENEMY))
                    {
                        // enemy territory
                        if (neighboursEnemyHealthSum != 0)
                        {
                            // other enemies around: hp difference plus army count times target value
                            targetNode.targetValue += (areaInArmyHp +
                                                       armyCountHere * targetAreaValue
                                                       - neighboursEnemyHealthSum + 80);
                        }
                        else
                        {
                            // otherwise only army count and target value
                            targetNode.targetValue += (targetAreaValue +
                                                       armyCountHere * targetAreaValue
                                                       + 80);
                        }
                        if ((!targetArea.info.sea) && ifAreaInSea)
                        {
                            // landing is favoured
                            targetNode.targetValue += 80;
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
                                                       60);
                        }
                        else
                        {
                            targetNode.targetValue += (targetAreaValue +
                                                       armyCountHere * targetAreaValue + 60);
                        }
                        if ((!targetArea.info.sea) && ifAreaInSea)
                        {
                            targetNode.targetValue += 80;
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
                        targetNode.targetValue += 40;
                    }
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
                                extraBenefit = 120;
                            }
                            else if (neighboursEnemyHealthSum < areaInArmyHp)
                            {
                                armyCountCoe = armyCountHere;
                                extraBenefit = 120;
                            }
                        }
                        else
                        {
                            if (getAlliance(areaId, i, (int)NOT_ALLY))
                            {
                                if (neighboursEnemyHealthSum > areaInArmyHp)
                                {
                                    armyCountCoe = armyCountHere - 1;
                                    extraBenefit = 40;
                                }
                                else if (neighboursEnemyHealthSum < areaInArmyHp)
                                {
                                    armyCountCoe = armyCountHere;
                                    extraBenefit = 40;
                                }
                            }
                        }

                        // original: coe * (own hp + area value - target hp) + extra; nothing at all when
                        // the surrounding strengths are equal (coe and extra stay 0)
                        targetNode.targetValue += armyCountCoe * (armyIn.hp + calcAreaValue(targetArea) - targetArmy.hp)
                                                  + extraBenefit;

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
                                targetNode.targetValue -= 90;
                            }
                            else
                            {
                                targetNode.targetValue += 50;
                            }
                        }
                        // tactics influence; cards is a bit set (1 << ARMY_CARD), the original masks 1/2/8/4
                        if ((armyIn.cards & (1 << (int)ASSAULT)) != 0)
                            targetNode.targetValue += 20;
                        if ((targetArmy.cards & (1 << (int)DEFENCE)) != 0)
                            targetNode.targetValue -= 20;
                        if ((armyIn.cards & (1 << (int)COMMANDER)) != 0)
                            targetNode.targetValue += 30;
                        // carriers are money too
                        if ((targetArmy.cards & (1 << (int)CARRIER)) != 0)
                            targetNode.targetValue += 5;

                        targetNode.targetIsPlayer = targetArea.country != null && !targetArea.country.ai;
                        if (targetNode.targetIsPlayer)
                        {
                            // the AI prefers hitting the player
                            targetNode.targetValue += 40;
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
                if (areaInValue > 300 && armyCountHere == 1) break;
                // the country is never null here; higher tech countries are more willing to path find
                targetNode.targetValue = (5 * countryIn.tech + 30) * armyCountHere;
                // hp tank
                if (armyIn.hp > 119) targetNode.targetValue += 40;

                // full health raises the tendency
                if (armyIn.IsFullHealth())
                {
                    targetNode.targetValue += 20;
                }
                // navy moves rather than stays
                if (armyIn.IsNavy() || (armyIn.cards & (1 << (int)CARRIER)) != 0)
                {
                    targetNode.targetValue += 40;
                }

                // individual unit strength (bit masks, see 0x3000)
                if ((armyIn.cards & (1 << (int)ASSAULT)) != 0)
                    targetNode.targetValue += 20;
                if ((armyIn.cards & (1 << (int)COMMANDER)) != 0)
                    targetNode.targetValue += 40;
                targetNode.targetValue += 20 * armyIn.level;
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
