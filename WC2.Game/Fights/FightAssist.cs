namespace WC2;

/// <summary>Counter attack rules, buff parameters and dice helpers (CFight part of FightAssist.cpp).</summary>
public sealed partial class CFight
{
    /// <summary>
    /// Whether a distance falls into the configured counter range.
    /// graph uses the theoretical 1000 = one step unit; circle uses map coordinates, so the
    /// XML 1000 = 100.0 configuration is converted to map units before comparing.
    /// </summary>
    private static bool IsWithinCounterRange(ArmyCombatRange range, float distance, CCountry defenderCountry)
    {
        float minRange = (float)range.minRange;
        float maxRange = (float)range.maxRange;
        if (range.shape == COMBAT_RANGE_CIRCLE)
        {
            minRange /= 10.0f;
            if (range.radiusFromCountry && defenderCountry != null)
            {
                maxRange = defenderCountry.AirstrikeRadius();
            }
            else
            {
                maxRange /= 10.0f;
            }
        }

        if (maxRange < minRange || distance < minRange)
        {
            return false;
        }
        return range.maxInclusive ? distance <= maxRange : distance < maxRange;
    }

    /// <summary>
    /// Decides whether the defender can counter from the engagement mode and its counter range.
    /// Close engagement uses distance 0; projectile fire uses the topological distance or the map
    /// distance depending on the defender's range shape. Active air strikes never trigger a counter.
    /// </summary>
    /// <param name="startArmy">attacking unit</param>
    /// <param name="targetArmy">target unit</param>
    /// <param name="targetArea">defender area</param>
    public void CheckCounter(CArmy startArmy, CArmy targetArmy, CArea targetArea)
    {
        canCounter = false;
        counterOccurred = false;
        if (startArmy == null || targetArmy == null || targetArea == null ||
            startArmy.basicAbilities == null ||
            targetArmy.basicAbilities == null)
        {
            return;
        }

        ArmyCombatProfile attack = startArmy.basicAbilities.combat.attack;
        ArmyCombatProfile counter = targetArmy.basicAbilities.combat.counter;
        ArmyCombatRange counterRange = counter.range.Clone();

        // A fort raises the defender's maximum counter range to one step. Only the copy used by this
        // fight changes, not the unit definition and not the minimum range. Circle ranges keep their
        // own unit conversion; the correction value is always 1000.
        int minCounterRange = Terrain.Installation(targetArea.installationType).minCounterRange; // fort: 1000
        if (minCounterRange > 0 && counterRange.maxRange < minCounterRange)
        {
            counterRange.maxRange = (ushort)Math.Min(minCounterRange, ushort.MaxValue);
        }

        // air strikes do not counter; normal fights only use the defender's range configuration
        if (attack.mode == COMBAT_ATTACK_AIRSTRIKE)
        {
            return;
        }

        float engagementDistance = 0.0f;
        if (attack.engagementMode == COMBAT_ENGAGEMENT_PROJECTILE)
        {
            if (counterRange.shape == COMBAT_RANGE_GRAPH)
            {
                int topologicalDistance = this.topologicalDistance;
                if (topologicalDistance < 0)
                {
                    topologicalDistance = g_Scene.GetTopologicalDistance(startAreaId, targetAreaId);
                }
                if (topologicalDistance < 0)
                {
                    return;
                }

                float attackMaxRange = (float)attack.range.maxRange;
                if (attack.range.radiusFromCountry && startArmy.country != null)
                {
                    // the airport radius is in map units; convert to the 1000 = one step scale
                    attackMaxRange = startArmy.country.AirstrikeRadius() * 10.0f;
                }
                engagementDistance = Math.Min(attackMaxRange, (float)topologicalDistance * 1000.0f);
            }
            else
            {
                // circle counter range: the engagement distance is the plain map distance
                engagementDistance = g_Scene.GetTwoAreasDistance(startAreaId, targetAreaId);
            }
        }

        canCounter = IsWithinCounterRange(counterRange, engagementDistance, targetArea.country);
        // a second attack is an independent attack flow, not a counter of this normal attack
        counterOccurred = canCounter && !startArmySecondAttack && !targetArmySecondAttack;
    }

    /// <summary>
    /// Extra attack and defence of a unit.
    /// </summary>
    /// <param name="area">area of the unit: fort, entrenchment and capital give 1 defence</param>
    /// <param name="army">the unit: tactics cards, morale, level and commander apply</param>
    /// <returns>(extraAttack, defence)</returns>
    public (int, int) GetArmyExtraAttackDefence(CArea area, CArmy army)
    {
        int armyDefence = 0;
        int armyExtraAttack = 0;
        // fort, entrenchment and capital give extra defence; the larger of the installation's and the
        // area type's value applies (all three are 1, so a fort in a capital still gives 1)
        armyDefence += Math.Max(Terrain.Installation(area.installationType).defence, Terrain.Area(area.info.areaType).defence);
        // tactics
        if (army.HasCard(DEFENCE))
        {
            armyDefence++;
        }
        if (army.HasCard(ASSAULT))
        {
            armyExtraAttack++;
        }

        // morale
        if (army.morale == (int)MORALE_UPSET)
        {
            armyExtraAttack--;
        }
        else if (army.morale == (int)MORALE_HIGH)
        {
            armyExtraAttack++;
        }

        // unit level and commander
        ArmyAbility tmpArmyAbility = GetArmyAbility(army.level);
        if (!army.HasCard(COMMANDER))
        {
            armyExtraAttack += tmpArmyAbility.attack;
            armyDefence += tmpArmyAbility.defence;
        }
        else
        {
            CommanderAbility tmpCommanderAbility = GetCommanderAbility(area.country.GetCommanderLevel());
            if (tmpCommanderAbility.armyAbility.attack < tmpArmyAbility.attack)
            {
                armyExtraAttack += tmpArmyAbility.attack;
            }
            else
            {
                armyExtraAttack += tmpCommanderAbility.armyAbility.attack;
            }

            if (tmpCommanderAbility.armyAbility.defence < tmpArmyAbility.defence)
            {
                armyDefence += tmpArmyAbility.defence;
            }
            else
            {
                armyDefence += tmpCommanderAbility.armyAbility.defence;
            }
        }
        return (armyExtraAttack, armyDefence);
    }

    /// <summary>
    /// Rolls the dice for the total damage output.
    /// </summary>
    /// <param name="minAtk">minimum attack</param>
    /// <param name="maxAtk">maximum attack</param>
    /// <param name="numDices">number of dice</param>
    /// <param name="extraAtk">extra attack</param>
    /// <param name="dices">rolled dice (used by the battle animation)</param>
    /// <returns>damage dealt (before the enemy defence)</returns>
    public int RowDicesForDamage(int minAtk, int maxAtk, int numDices, int extraAtk, List<int> dices)
    {
        int sum = 0;
        for (int i = 0; i < numDices; i++)
        {
            int dice = GetRand() % (maxAtk - minAtk + 1) + minAtk;
            dices.Add(dice);
            sum += dice + extraAtk;
        }
        return sum;
    }

    /// <summary>
    /// Parameters of the attack damage: attacker extra attack, attacker defence and the final damage rate.
    /// </summary>
    /// <param name="startArmy">attacking unit</param>
    /// <param name="targetArmy">attacked unit</param>
    /// <param name="startArea">attacking area</param>
    /// <param name="targetArea">attacked area; a radar reduces the damage</param>
    /// <returns>(extra attack, defence, final rate, whether a radar halves the raw damage)</returns>
    public (int, int, float, bool) GetAttackDmgParameters(CArmy startArmy, CArmy targetArmy, CArea startArea, CArea targetArea)
    {
        int extraAtk, defense;
        (extraAtk, defense) = GetArmyExtraAttackDefence(startArea, startArmy);
        float dmgRate = 1.0f;
        // A radar weakens projectile fire coming from distant areas. The check uses the real
        // topological distance between the areas, so it does not matter whether the attacker is
        // configured with a graph or a circle range; close engagement never triggers it.
        // The original halves the raw damage as an integer before any rate, so the flag is
        // returned separately instead of being folded into dmgRate.
        ArmyCombatProfile attackProfile = startArmy.basicAbilities.combat.attack;
        bool radarHalve = Terrain.Installation(targetArea.installationType).halvesRangedDamage
            && attackProfile.engagementMode != COMBAT_ENGAGEMENT_CLOSE
            && startArmy.basicAbilities.combat.attack.range.maxRange >= 1000;
        // New system: unit type damage relations (attack phase) and weapon/armor relations.
        dmgRate *= GetCombatDamageRate(
            startArmy.basicAbilities.combat,
            targetArmy.basicAbilities.unitType,
            COMBAT_DAMAGE_ATTACK);
        dmgRate *= GetWeaponArmorDamageRate(
            CObjectDef.Instance().weaponArmorDamageRates,
            startArmy.basicAbilities.weaponType,
            targetArmy.basicAbilities.armorType);
        return (extraAtk, defense, dmgRate, radarHalve);
    }

    /// <summary>
    /// Parameters of the counter damage: defender extra attack, defender defence and the final damage rate.
    /// </summary>
    /// <returns>(extra attack, defence, final rate which may be below 1.0)</returns>
    public (int, int, float) GetCounterDmgParameters(CArmy startArmy, CArmy targetArmy, CArea startArea, CArea targetArea)
    {
        int extraAtk, defense;
        (extraAtk, defense) = GetArmyExtraAttackDefence(targetArea, targetArmy);
        float dmgRate = 1.0f;

        // counter phase unit type relations and weapon/armor relations
        dmgRate *= GetCombatDamageRate(
            targetArmy.basicAbilities.combat,
            startArmy.basicAbilities.unitType,
            COMBAT_DAMAGE_COUNTER);
        dmgRate *= GetWeaponArmorDamageRate(
            CObjectDef.Instance().weaponArmorDamageRates,
            targetArmy.basicAbilities.weaponType,
            startArmy.basicAbilities.armorType);
        return (extraAtk, defense, dmgRate);
    }

    /// <summary>
    /// Applies damage only; death triggers are left to the caller.
    /// </summary>
    /// <param name="dmgDstArea">area receiving the damage</param>
    /// <param name="damage">damage value</param>
    /// <param name="dmgSrcArea">area the damage comes from</param>
    /// <param name="dmgDstArmy">unit receiving the damage</param>
    /// <returns>whether the unit died</returns>
    public bool ApplyNormalAttackDmg(CArea dmgDstArea, int damage, CArea dmgSrcArea, CArmy dmgDstArmy)
    {
        int dmgDstArmyType = dmgDstArmy.basicAbilities.id;
        bool death = dmgDstArea.LostArmyStrength(0, damage);
        if (death)
        {
            if (dmgDstArea.armyCount <= 0)
            {
                g_Scene.AdjacentAreasEncirclement(dmgDstArea.id);
            }
            dmgSrcArea.country.AddDestroy(dmgDstArmyType);
        }
        return death;
    }
}
