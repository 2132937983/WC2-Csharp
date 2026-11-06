namespace WC2;

/// <summary>Attack flow and result application (FightLogics.cpp). CGameState::StartBattale lives with the GUI port.</summary>
public sealed partial class CFight
{
    /// <summary>
    /// Attacker dice result at the given index. Not called by the current project; kept for
    /// completeness of the original CFight data queries.
    /// </summary>
    public int GetAttackerResult(int index)
    {
        return startArmyDices[index];
    }

    /// <summary>Defender dice result at the given index (compatibility query, see GetAttackerResult).</summary>
    public int GetDefenderResult(int index)
    {
        return targetArmyDices[index];
    }

    /// <summary>
    /// Damage pre-allocation and counter attack computation of a normal attack.
    /// </summary>
    /// <param name="StartAreaId">area of the attacking unit</param>
    /// <param name="TargetAreaId">area of the attacked unit</param>
    public void NormalAttack(int StartAreaId, int TargetAreaId)
    {
        CArea startArea = g_Scene.GetArea(StartAreaId);
        CArea targetArea = g_Scene.GetArea(TargetAreaId);
        CArmy startArmy = startArea.GetArmy(0);
        CArmy targetArmy = targetArea.GetArmy(0);
        startAreaId = StartAreaId;
        targetAreaId = TargetAreaId;
        topologicalDistance = g_Scene.GetTopologicalDistance(StartAreaId, TargetAreaId);

        // decide here whether a counter attack is triggered
        CheckCounter(startArmy, targetArmy, targetArea);

        // the following block computes the base damage rolled between min and max attack
        startArmyNumDices = startArmy.GetNumDices();
        startArmyDices.Clear();
        startArmyReceiveDamage = 0;
        startArmyNumDices1 = startArmy.GetNumDices();
        startArmyMaxAttack = startArmy.basicAbilities.maxAttack;
        startArmyMinAttack = startArmy.basicAbilities.minAttack;

        targetArmyReceiveDamage = 0;
        targetArmyNumDices = targetArmy.GetNumDices();
        targetArmyDices.Clear();
        targetArmyMaxAttack = targetArmy.basicAbilities.maxAttack;
        targetArmyMinAttack = targetArmy.basicAbilities.minAttack;
        targetArmyNumDices1 = targetArmy.GetNumDices();

        float atkDmgRate = 1.0f, counterDmgRate = 1.0f;
        // extra attack / defence buffs of attacker and defender
        bool radarHalve;
        (startArmyExtraAttack, startArmyDefence, atkDmgRate, radarHalve) =
            GetAttackDmgParameters(startArmy, targetArmy, startArea, targetArea);
        (targetArmyExtraAttack, targetArmyDefence, counterDmgRate) =
            GetCounterDmgParameters(startArmy, targetArmy, startArea, targetArea);

        // the original rolls the attacker's dice first, then the defender's
        int targetReceiveDmg = RowDicesForDamage(startArmyMinAttack, startArmyMaxAttack,
            startArmyNumDices1, startArmyExtraAttack, startArmyDices);
        int starterReceiveDmg = RowDicesForDamage(targetArmyMinAttack, targetArmyMaxAttack,
            targetArmyNumDices1, targetArmyExtraAttack, targetArmyDices);
        if (canCounter)
        {
            startArmyReceiveDamage = starterReceiveDmg - startArmyNumDices1 * startArmyDefence;
            if (float.IsInfinity(counterDmgRate))
            {
                startArmyReceiveDamage = startArmy.hp;
            }
            else
            {
                startArmyReceiveDamage = (int)(startArmyReceiveDamage * counterDmgRate);
            }
            if (startArmyReceiveDamage <= 0)
            {
                startArmyReceiveDamage = 1;
            }
        }

        targetArmyReceiveDamage = targetReceiveDmg - targetArmyDefence * targetArmyNumDices1;
        // a radar halves the raw damage of distant fire before any rate (integer, as in the original)
        if (radarHalve) targetArmyReceiveDamage /= 2;
        if (float.IsInfinity(atkDmgRate))
        {
            targetArmyReceiveDamage = targetArmy.hp;
        }
        else
        {
            targetArmyReceiveDamage = (int)(targetArmyReceiveDamage * atkDmgRate);
        }
        if (targetArmyReceiveDamage <= 0)
        {
            targetArmyReceiveDamage = 1;
        }
        if (secondAttackIndex == 1)
        {
            if (startArmySecondAttack)
            {
                startArmyReceiveDamage = 0;
            }
            else if (targetArmySecondAttack)
            {
                // original assigns the bool to the int
                targetArmyReceiveDamage = startArmySecondAttack ? 1 : 0;
            }
        }

        // not an air strike: normal attack type
        airStrikeType = 0;
        airStrikePayload = 0;
        specialAttackFromUnit = false;
        // normal attacks never read this field, keep the sentinel anyway
        airStrikeAttackCost = -1;
        startArmySecondAttack = false;
        targetArmySecondAttack = false;

        // battle animation data, kept for later
        startArmyNumDicesLost = startArmyNumDices - startArmy.GetNumDicesIfLostStrength(startArmyReceiveDamage);
        targetArmyNumDicesLost = targetArmyNumDices - targetArmy.GetNumDicesIfLostStrength(targetArmyReceiveDamage);
    }

    /// <summary>Applies the fight result.</summary>
    public void ApplyResult()
    {
        CArea startArea = null;
        int targetArmyId;
        bool constructionReduced = false;

        if (targetAreaId < 0)
        {
            return;
        }
        CArea targetArea = g_Scene.GetArea(targetAreaId);
        if (targetArea == null)
        {
            return;
        }
        if (airStrikeType == 0)
        {
            // this branch applies a normal attack result
            startArea = g_Scene.GetArea(startAreaId);
            CArmy startArmy = startArea.GetArmy(0);
            ArmyCombatRules startCombatRules = startArmy.basicAbilities.combat;

            // Independent of unit survival, so it runs before the damage is applied
            // (a "settlement independent" trigger).
            // The construction level damage is no longer keyed on the battleship id but read from
            // the attacking unit's numeric traits: constructionLevelDegrade is the maximum number of
            // levels removed per trigger, constructionLevelDegradeChance the chance in percent.
            int constructionLevelDegrade = startCombatRules.constructionLevelDegrade;
            int constructionLevelChance = startCombatRules.constructionLevelDegradeChance;
            bool constructionTriggered =
                constructionLevelDegrade > 0 && constructionLevelChance > 0 &&
                (constructionLevelChance >= 100 || (GetRand() % 100) < constructionLevelChance);
            if (constructionTriggered)
            {
                for (int damageIndex = 0; damageIndex < constructionLevelDegrade; ++damageIndex)
                {
                    targetArea.ReduceConstructionLevel();
                }
                constructionReduced = true;
            }

            bool startArmyKilled = ApplyNormalAttackDmg(startArea, startArmyReceiveDamage, targetArea, startArmy);

            CArmy targetArmy = targetArea.GetArmy(0);
            bool targetArmyKilled = ApplyNormalAttackDmg(targetArea, targetArmyReceiveDamage, startArea, targetArmy);

            bool targetAreaClear = false;
            // the target gains experience when it survives
            if (!targetArmyKilled)
            {
                targetArmy.AddExp(startArmyReceiveDamage);
            }
            else
            {
                targetAreaClear = targetArea.armyCount == 0;
            }

            // settlement triggers of the attacking unit
            if (!startArmyKilled)
            {
                startArmy.AddExp(targetArmyReceiveDamage);
                if (secondAttackIndex == 0)
                {
                    // entrenchments clear the movement
                    uint combatTraits = startArmy.basicAbilities.combat.traits;
                    bool willClearMovement =
                        Terrain.Installation(targetArea.installationType).stopsAttackers &&
                        (combatTraits & ARMY_COMBAT_TRAIT_CROSS_ENTRENCHMENT) == 0;

                    // Movement restored after a kill (more precisely: this attack's cost is waived),
                    // now driven by the configured trait. Original tanks / heavy tanks default to 1.
                    int movementRestore = startArmy.basicAbilities.combat.movementRestoreAfterKill;
                    bool willRestoreMovement = movementRestore > 0
                        && targetArmyKilled && (targetArea.armyCount > 0);
                    if (willClearMovement)
                    {
                        startArmy.movement = 0;
                    }
                    else
                    {
                        // Pay the attack cost first, then give back the configured movement.
                        // movementRestoreAfterKill=1 equals the original "free attack", larger values
                        // express extra recovery but never exceed the unit's maximum.
                        startArmy.movement--;
                        if (willRestoreMovement)
                        {
                            startArmy.movement += movementRestore;
                            if (startArmy.movement > startArmy.basicAbilities.movement)
                            {
                                startArmy.movement = startArmy.basicAbilities.movement;
                            }
                        }
                    }

                    // Fort second attack. Original rule: a navy unit attacking a non-rocket unit in a fort.
                    // Now driven by the attacker's recv_fort_second_counter trait; only when a counter
                    // actually happened and never from a second attack itself.
                    if (!targetArmyKilled
                        && Terrain.Installation(targetArea.installationType).secondCounter
                        && counterOccurred
                        && secondAttackIndex == 0
                        && (startArmy.basicAbilities.combat.traits & ARMY_COMBAT_TRAIT_RECV_FORT_SECOND_COUNTER) != 0)
                    {
                        targetArmySecondAttack = true;
                    }
                }
            }

            // effects on the emptied target area, in the original order: construction, installation
            // roll, then (if the attacker survived) occupation and the breakthrough roll
            if (targetAreaClear)
            {
                if (!constructionReduced)
                {
                    targetArea.ReduceConstructionLevel();
                }
                if (GetRand() % 100 <= 49)
                {
                    targetArea.installationType = NO_INSTALLATION;
                }
                if (!startArmyKilled)
                {
                    // Trait: whether the unit occupies the area after killing its last defender.
                    if ((startArmy.basicAbilities.combat.traits & ARMY_COMBAT_TRAIT_CANNOT_OCCUPY) == 0 &&
                        startArea.info.sea == targetArea.info.sea)
                    {
                        // Units without CANNOT_OCCUPY occupy by default; can-occupy="false" and
                        // traits="cannot-occupy" both set this negative trait. Land/sea mismatches
                        // still cannot be occupied directly.
                        startArea.OccupyArea(targetArea);
                    }
                    if (GetRand() % 100 <= 29)
                    {
                        // high morale
                        startArmy.Breakthrough();
                    }
                }
            }

            // multiplayer is not considered for now, the code is only kept here
            if (g_GameManager.gameMode != CGameManager.MULTIPLAYER)
            {
                if (startArea.country != null)
                {
                    if (!startArea.country.ai)
                    {
                        if (CActionAI.Instance().getMedal(targetArmyReceiveDamage, targetArea))
                        {
                            if (startArea.country.IsLocalPlayer())
                            {
                                g_Commander.medal++;
                                g_GameManager.randomRewardMedal++;
                            }
                            g_Scene.GainMedal((float)startArea.info.armyPos[0], (float)startArea.info.armyPos[1]);
                            g_SoundRes.PlayCharSE(LV_UP_WAV);
                        }
                    }
                }

                if (targetArea.country != null)
                {
                    if (!targetArea.country.ai)
                    {
                        // original: the attacked area's type applies to the defender's roll as well
                        if (CActionAI.Instance().getMedal(startArmyReceiveDamage, targetArea))
                        {
                            if (targetArea.country.IsLocalPlayer())
                            {
                                g_Commander.medal++;
                                g_GameManager.randomRewardMedal++;
                            }
                            g_Scene.GainMedal((float)targetArea.info.armyPos[0], (float)targetArea.info.armyPos[1]);
                            g_SoundRes.PlayCharSE(LV_UP_WAV);
                        }
                    }
                }
            }
        }
        else if (airStrikeType == 2)
        {
            // bombing
            for (int i = targetArea.armyCount - 1; i >= 0; i--)
            {
                CArmy tmp_army = targetArea.GetArmy(i);
                targetArmyId = tmp_army.basicAbilities.id;
                // damage falls off by 20% per stack layer (integer arithmetic of the original)
                if (targetArea.LostArmyStrength(i, targetArmyReceiveDamage + i * targetArmyReceiveDamage / -5))
                {
                    // destroyed: add a destroy record
                    airStrikeCountry.AddDestroy(targetArmyId);
                }
            }
            if (targetArea.armyCount == 0)
            {
                g_Scene.AdjacentAreasEncirclement(targetAreaId);
            }
            // anti-aircraft guns protect the building from bombing (terrain: protectsFromBombing)
            if (!Terrain.Installation(targetArea.installationType).protectsFromBombing)
            {
                targetArea.ReduceConstructionLevel();
            }
        }
        else if (airStrikeType == 3)
        {
            // nuclear bomb, destroys everything
            for (int j = 0; j < targetArea.armyCount; j++)
            {
                CArmy pArmy = targetArea.GetArmy(j);
                airStrikeCountry.AddDestroy(pArmy.basicAbilities.id);
            }
            targetArea.DestroyConstruction();
            targetArea.installationType = NO_INSTALLATION;
            targetArea.ClearAllArmy();
            g_Scene.AdjacentAreasEncirclement(targetAreaId);
        }
        else
        {
            // normal air strike: apply the damage first
            CArmy targetArmy = targetArea.GetArmy(0);
            targetArmyId = targetArmy.basicAbilities.id;
            if (targetArea.LostArmyStrength(0, targetArmyReceiveDamage))
            {
                if (targetArea.armyCount == 0)
                {
                    g_Scene.AdjacentAreasEncirclement(targetAreaId);
                }
                airStrikeCountry.AddDestroy(targetArmyId);
            }
        }

        // Unit air strikes handle experience, rewards and movement after the payload settlement.
        // The old airStrikeType==4 convention is gone; cards never enter this branch.
        if (specialAttackFromUnit)
        {
            startArea = g_Scene.GetArea(startAreaId);
            if (startArea != null)
            {
                CArmy startArmy = startArea.GetArmy(0);
                if (startArmy != null)
                {
                    // The original added startArmyReceiveDamage as experience, but that value is always 0
                    // for air strikes (no counter), so there was no gain. We give a little experience here.
                    startArmy.AddExp(targetArmyReceiveDamage);
                    if (airStrikeAttackCost <= 0)
                    {
                        startArmy.movement = 0;
                    }
                    else
                    {
                        startArmy.movement = Math.Max(0, startArmy.movement - airStrikeAttackCost);
                    }

                    if (g_GameManager.gameMode != CGameManager.MULTIPLAYER &&
                        startArea.country != null &&
                        !startArea.country.ai &&
                        CActionAI.Instance().getMedal(targetArmyReceiveDamage, targetArea))
                    {
                        if (startArea.country.IsLocalPlayer())
                        {
                            g_Commander.medal++;
                            g_GameManager.randomRewardMedal++;
                        }
                        g_Scene.GainMedal((float)startArea.info.armyPos[0], (float)startArea.info.armyPos[1]);
                        g_SoundRes.PlayCharSE(LV_UP_WAV);
                    }
                }
            }
        }

        RenderAttackResults(startArea, targetArea);
    }

    /// <summary>Shows the floating damage texts after a fight.</summary>
    /// <param name="startArea">attacking area (may be null for card air strikes)</param>
    /// <param name="targetArea">attacked area</param>
    public void RenderAttackResults(CArea startArea, CArea targetArea)
    {
        string tmpChar;
        if (secondAttackIndex == 1)
        {
            if (startArea != null && startArmyReceiveDamageFirst > 0)
            {
                ecEffectManager.Instance().AddEffect("effect_exp.xml", true)
                    ?.FireAt((float)startArea.info.armyPos[0], (float)startArea.info.armyPos[1], 0.0f);
                tmpChar = Sprintf("-%d", startArmyReceiveDamageFirst);
                g_FightTextMgr.AddText((float)startArea.info.armyPos[0], (float)startArea.info.armyPos[1] - 30.0f, tmpChar, 0xFF0000FF);
            }
            if (targetArmyReceiveDamageFirst > 0)
            {
                ecEffectManager.Instance().AddEffect("effect_exp.xml", true)
                    ?.FireAt((float)targetArea.info.armyPos[0], (float)targetArea.info.armyPos[1], 0.0f);
                tmpChar = Sprintf("-%d", targetArmyReceiveDamageFirst);
                g_FightTextMgr.AddText((float)targetArea.info.armyPos[0], (float)targetArea.info.armyPos[1] - 30.0f, tmpChar, 0xFF0000FF);
            }
        }
        if (!startArmySecondAttack && !targetArmySecondAttack)
        {
            if (startArea != null && startArmyReceiveDamage > 0)
            {
                ecEffectManager.Instance().AddEffect("effect_exp.xml", true)
                    ?.FireAt((float)startArea.info.armyPos[0], (float)startArea.info.armyPos[1], 0.0f);
                tmpChar = Sprintf("-%d", startArmyReceiveDamage);
                g_FightTextMgr.AddText((float)startArea.info.armyPos[0], (float)startArea.info.armyPos[1] - 30.0f + (float)secondAttackIndex * 18.0f, tmpChar, 0xFF0000FF);
            }
            if (targetArmyReceiveDamage > 0)
            {
                ecEffectManager.Instance().AddEffect("effect_exp.xml", true)
                    ?.FireAt((float)targetArea.info.armyPos[0], (float)targetArea.info.armyPos[1], 0.0f);
                tmpChar = Sprintf("-%d", targetArmyReceiveDamage);
                g_FightTextMgr.AddText((float)targetArea.info.armyPos[0], (float)targetArea.info.armyPos[1] - 30.0f + (float)secondAttackIndex * 18.0f, tmpChar, 0xFF0000FF);
            }
            if (airStrikeType == 3)
            {
                ecEffectManager.Instance().AddEffect("effect_nuclearbomb.xml", true)
                    ?.FireAt((float)targetArea.info.armyPos[0], (float)targetArea.info.armyPos[1], 0.0f);
            }
            g_SoundRes.PlayCharSE(EXP_WAV);
        }
    }

    /// <summary>Runs the first attack.</summary>
    /// <param name="StartAreaID">attacking area id</param>
    /// <param name="TargetAreaID">attacked area id</param>
    public void FirstAttack(int StartAreaID, int TargetAreaID)
    {
        secondAttackIndex = 0;
        NormalAttack(StartAreaID, TargetAreaID);
        startArmyReceiveDamageFirst = startArmyReceiveDamage;
        targetArmyReceiveDamageFirst = targetArmyReceiveDamage;
    }

    public void SecondAttack()
    {
        secondAttackSideIndex = startArmySecondAttack ? 0 : 1;
        secondAttackIndex = 1;
        NormalAttack(startAreaId, targetAreaId);
    }
}
