namespace WC2;

/// <summary>
/// Runtime state of a nation (CCountry.cpp). Card usage lives in GameUseCard.cs, GetCommanderLevel /
/// CanUseCommander in CCommander.cs. TurnBegin/TurnEnd (GameTurnBegin.cpp / GameTurnEnd.cpp) and
/// SaveCountry/LoadCountry (GameLoadSave.cpp) are kept at the end of this file so the CGameManager
/// files stay with the logic agent.
/// </summary>
public sealed partial class CCountry
{
    public int alliance;
    public DEFEAT_TYPE defeatType;
    public List<int> areaIdList = new();
    public List<int> capitalIdList = new();
    public int money;
    public int industry;
    public float taxRate;
    public string id = "";
    public string name = "";
    public byte r, g, b, a;
    public bool ai;
    public bool defeated;
    public CountryAction action = new();
    public float actionTimer;
    public float actionDelayTime;
    public bool actionWaitCamera;
    public int tech;
    public int techTurn;
    public int[] oldCardRound = new int[28];
    public List<int> aoeCardTargetIdList = new();
    /// <summary>Index into aoeCardTargetIdList (the original kept a list iterator; Count = end()).</summary>
    public int aoeCardCurTargetId;
    public int[] destroyCount = new int[10];
    public CommanderDef commanderDef;
    public int commanderTurn;
    public bool commanderAlive;
    public int[] warMedal = new int[6];
    public bool borrowedLoan;
    public CPlayer player;
    public int[] cardRound = new int[VALID_CARD_COUNT + 10];
    public int[] cardTimes = new int[VALID_CARD_COUNT + 10];
    /// <summary>Kill statistics of custom unit types, so dynamic ids never index the fixed array.</summary>
    public Dictionary<int, int> destroyCountByArmyType = new();

    /// <summary>The r,g,b,a bytes packed as the engine color 0xAABBGGRR (the original read *(int*)&amp;r).</summary>
    public uint ColorRGBA => (uint)r | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);

    public CCountry()
    {
        areaIdList.Clear();
        capitalIdList.Clear();
        aoeCardTargetIdList.Clear();
        destroyCountByArmyType.Clear();
    }

    // Move camera while ai is controlling player country
    public bool IsLocalPlayer() => !g_GameManager.AIAction;

    /// <summary>
    /// First adjacent area owned by this country. No caller at the moment, kept for the old logic.
    /// </summary>
    /// <param name="areaId">start area</param>
    /// <param name="hasArmy">the adjacent area must hold at least one unit</param>
    /// <returns>the area id or -1</returns>
    public int FindAdjacentAreaID(int areaId, bool hasArmy)
    {
        int adjacentAreaCount = g_Scene.GetNumAdjacentAreas(areaId);
        for (int index = 0; index < adjacentAreaCount; ++index)
        {
            CArea adjacentArea = g_Scene.GetAdjacentArea(areaId, index);
            if (adjacentArea.country == this && (!hasArmy || adjacentArea.armyCount > 0))
            {
                return adjacentArea.id;
            }
        }
        return -1;
    }

    /// <summary>First adjacent land area owned by this country (no caller at the moment).</summary>
    public int FindAdjacentLandAreaID(int areaId, bool hasArmy)
    {
        int adjacentAreaCount = g_Scene.GetNumAdjacentAreas(areaId);
        for (int index = 0; index < adjacentAreaCount; ++index)
        {
            CArea adjacentArea = g_Scene.GetAdjacentArea(areaId, index);
            if (adjacentArea.country == this &&
                !adjacentArea.info.sea &&
                (!hasArmy || adjacentArea.armyCount > 0))
            {
                return adjacentArea.id;
            }
        }
        return -1;
    }

    /// <summary>Number of airports of the country.</summary>
    public int GetNumAirport()
    {
        int airportCount = 0;
        foreach (int areaId in areaIdList)
        {
            CArea area = g_Scene.GetArea(areaId);
            if (area != null && area.info.constructionType == AIRPORT)
            {
                ++airportCount;
            }
        }
        return airportCount;
    }

    /// <summary>Distance from the unit position of an area to the nearest own airport (-1 without airport).</summary>
    public float GetMinDstToAirport(int areaId)
    {
        CArea targetArea = g_Scene.GetArea(areaId);
        float minimumDistanceSquared = -1.0f;

        foreach (int countryAreaId in areaIdList)
        {
            CArea countryArea = g_Scene.GetArea(countryAreaId);
            if (countryArea == null || countryArea.info.constructionType != AIRPORT)
            {
                continue;
            }

            float deltaX = (float)(targetArea.info.armyPos[0] - countryArea.info.constructionPos[0]);
            float deltaY = (float)(targetArea.info.armyPos[1] - countryArea.info.constructionPos[1]);
            float distanceSquared = deltaX * deltaX + deltaY * deltaY;
            if (minimumDistanceSquared < 0.0f || distanceSquared < minimumDistanceSquared)
            {
                minimumDistanceSquared = distanceSquared;
            }
        }

        if (minimumDistanceSquared > 0.0f)
        {
            return MathF.Sqrt(minimumDistanceSquared);
        }
        return minimumDistanceSquared;
    }

    /// <summary>Current target of an area card, -1 at the end of the list.</summary>
    public int GetCurCardTarget()
    {
        if (aoeCardCurTargetId < 0 || aoeCardCurTargetId >= aoeCardTargetIdList.Count)
        {
            return -1;
        }
        return aoeCardTargetIdList[aoeCardCurTargetId];
    }

    /// <summary>Advances the area card target to the next area.</summary>
    public void NextCardTarget()
    {
        if (aoeCardCurTargetId < aoeCardTargetIdList.Count)
        {
            ++aoeCardCurTargetId;
        }
    }

    /// <summary>Counts a destroyed unit of the given type.</summary>
    public void AddDestroy(int armyType)
    {
        if (armyType >= 0 && armyType < 10)
        {
            ++destroyCount[armyType];
            return;
        }
        if (armyType >= 10)
        {
            destroyCountByArmyType.TryGetValue(armyType, out int count);
            destroyCountByArmyType[armyType] = count + 1;
        }
    }

    /// <summary>The country loses an area.</summary>
    public void RemoveArea(int areaId)
    {
        g_GameManager.DisplayAreaChange = true;
        areaIdList.RemoveAll(x => x == areaId);
        if (Terrain.Area(g_Scene.GetArea(areaId).info.areaType).capital)
        {
            capitalIdList.RemoveAll(x => x == areaId);
        }
    }

    /// <summary>The country gains an area.</summary>
    public void AddArea(int areaId)
    {
        g_GameManager.DisplayAreaChange = true;
        if (!FindArea(areaId))
        {
            areaIdList.Add(areaId);
            if (Terrain.Area(g_Scene.GetArea(areaId).info.areaType).capital)
                capitalIdList.Add(areaId);
        }
    }

    /// <summary>Air strike radius of the country.</summary>
    public float AirstrikeRadius()
    {
        // original: 300 up to air force medal level 1, 400 above (data/medals.json airforce.radius*)
        if (GetWarMedalLevel(AIR_FORCE_MEDAL) < Medals.airStrikeRadiusFromLevel) return Medals.airStrikeRadius;
        return Medals.airStrikeRadiusUpgraded;
    }

    /// <summary>Collects one turn of taxes.</summary>
    public void CollectTaxes()
    {
        int sum_money = GetTaxes() + money;
        if (sum_money > GameRules.moneyCap) money = GameRules.moneyCap;
        else money = sum_money;
    }

    /// <summary>Collects one turn of industry.</summary>
    public void CollectIndustrys()
    {
        industry += GetIndustrys();
        if (industry > GameRules.industryCap) industry = GameRules.industryCap;
    }

    /// <summary>Whether the country is defeated (more rules may be added later).</summary>
    public bool IsConquested()
    {
        if (defeatType == CORE) return IfAllLandNormal();
        // original: an ARMY country survives while it owns land or keeps units in its sea areas
        if (defeatType == ARMY) return !IfExistLand() && !IfExistArmy();
        return !IfExistLand();
    }

    /// <summary>Whether the country still owns land.</summary>
    public bool IfExistLand()
    {
        foreach (int i in areaIdList)
        {
            CArea area_here = g_Scene.GetArea(i);
            if (!area_here.info.sea) return true;
        }
        return false;
    }

    /// <summary>Whether the country still has units.</summary>
    public bool IfExistArmy()
    {
        foreach (int i in areaIdList)
        {
            CArea area_here = g_Scene.GetArea(i);
            if (area_here.armyCount > 0) return true;
        }
        return false;
    }

    /// <summary>The country is conquered by byCountry.</summary>
    public void BeConquestedBy(CCountry byCountry)
    {
        // iterate a snapshot: SetAreaCountry may touch the area lists
        foreach (int i in areaIdList.ToList())
        {
            CArea area_here = g_Scene.GetArea(i);
            area_here.ClearAllArmy();
            if (!area_here.info.sea) g_Scene.SetAreaCountry(i, byCountry);
            else g_Scene.SetAreaCountry(i, null);
        }
        areaIdList.Clear();
        capitalIdList.Clear();
    }

    /// <summary>Whether every land area of the country is a normal area.</summary>
    public bool IfAllLandNormal()
    {
        foreach (int i in areaIdList)
        {
            CArea area_here = g_Scene.GetArea(i);
            if (!area_here.info.sea)
            {
                // capitals, ports and cities are key areas (terrain: keyArea)
                if (Terrain.Area(area_here.info.areaType).keyArea) return false;
            }
        }
        return true;
    }

    public void Init(string idIn, string nameIn)
    {
        areaIdList.Clear();
        capitalIdList.Clear();
        destroyCountByArmyType.Clear();
        id = idIn ?? "";
        name = nameIn ?? "";
        commanderTurn = 0;
        commanderAlive = false;
        commanderDef = null;
        money = 0;
        tech = 1;
        techTurn = 0;
        industry = 0;
        r = 255;
        g = 255;
        b = 255;
        ai = true;
        defeatType = LAND;
        defeated = false;
        borrowedLoan = false;
        taxRate = 1.0f;
        alliance = 1;
        action.actionType = CountryAction.NO_ACTION;
        actionWaitCamera = false;
        actionTimer = 0;
        actionDelayTime = 0;
        player = null;
        for (int i = 0; i < warMedal.Length; i++) warMedal[i] = 0;
        for (int i = 0; i < cardRound.Length; i++) cardRound[i] = 0;
        for (int i = 0; i < cardTimes.Length; i++) cardTimes[i] = 0;
        InitCountryCardRound();
    }

    /// <summary>Initialises every card cool-down of the country (may move to a card file later).</summary>
    public void InitCountryCardRound()
    {
        foreach (List<CARD_ID> i in CObjectDef.AllCardTab)
        {
            foreach (CARD_ID cardId in i)
            {
                CardDef card = CObjectDef.Instance().GetCardDef(cardId, this);
                if (card != null) cardRound[(int)cardId] = card.round;
                else cardRound[(int)cardId] = 0;
            }
        }
    }

    /// <summary>Executes the queued action.</summary>
    public void DoAction()
    {
        actionTimer = 0.0f;
        CArea StartArea = g_Scene.GetArea(action.startAreaId);
        CArea TargetArea = g_Scene.GetArea(action.targetAreaId);
        switch (action.actionType)
        {
            case CountryAction.MOVE_ACTION:
            {
                if (action.armyIndex > 0)
                {
                    // move to the front first so the occupation can trigger
                    StartArea.MoveArmyToFront(action.armyIndex, false);
                    action.armyIndex = 0;
                }
                using (PerfStats.Measure("act.moveArmy")) StartArea.MoveArmyTo(TargetArea);
                using (PerfStats.Measure("act.sound")) g_SoundRes.PlayCharSE(TargetArea.info.sea ? SHIP_WAV : MOVE_WAV);
                break;
            }

            case CountryAction.ATTACK_ACTION:
            {
                if (action.armyIndex > 0)
                {
                    StartArea.MoveArmyToFront(action.armyIndex, false);
                    action.armyIndex = 0;
                }
                CArmy Army = StartArea.GetArmy(0);
                if (Army.basicAbilities.combat.attack.mode == COMBAT_ATTACK_AIRSTRIKE)
                {
                    // payload and movement cost come from combat.attack.
                    // The unit still needs movement; attack-cost=0 means "clear the movement",
                    // so here it only counts as "can launch", CFight does the actual deduction.
                    ArmyCombatProfile attackProfile = Army.basicAbilities.combat.attack;
                    int attackCost = attackProfile.attackCost;
                    bool enoughMovement =
                        Army.movement > 0 &&
                        (attackCost <= 0 || Army.movement >= attackCost);
                    if (enoughMovement)
                    {
                        g_Scene.AircraftCarrierBomb(action.startAreaId, action.targetAreaId, (int)attackProfile.payloadType);
                    }
                    else
                    {
                        Army.active = false;
                    }
                    break;
                }
                if (StartArea.info.armyPos[0] >= TargetArea.info.armyPos[0])
                {
                    StartArea.SetArmyDir(0, -1.0f);
                    TargetArea.SetArmyDir(0, 1.0f);
                }
                else
                {
                    StartArea.SetArmyDir(0, 1.0f);
                    TargetArea.SetArmyDir(0, -1.0f);
                }
                // battle animation on? (off by default, recommended off for mods)
                bool NeedBattleAnimation = false;
                if (g_GameSettings.battleAnimation)
                {
                    if (StartArea.country == null || StartArea.country.ai)
                    {
                        if (TargetArea.country != null) NeedBattleAnimation = !TargetArea.country.ai;
                    }
                    else
                    {
                        NeedBattleAnimation = true;
                    }
                }
                if (Army.movement > 0)
                {
                    using (PerfStats.Measure("act.battle")) ((CGameState)CStateManager.Instance().GetStatePtr(GAME)).StartBattale(
                        action.startAreaId, action.targetAreaId, 1, NeedBattleAnimation);
                }
                else
                {
                    Army.active = false;
                }
                break;
            }

            case CountryAction.MOVE_FRONT_ACTION:
            {
                if (action.armyIndex > 0)
                {
                    TargetArea.MoveArmyToFront(action.armyIndex, false);
                }
                break;
            }

            case CountryAction.CARD_ACTION:
            {
                CardDef Def = CObjectDef.Instance().GetCardDef(action.cardId);
                // card branches
                if (action.cardId == AIRBORNE_FORCE_CARD)
                {
                    g_Scene.Airborne(action.targetAreaId);
                }
                else if (Def != null && Def.type == AIR_FORCE_TYPE_CARD)
                {
                    g_Scene.BombArea(action.targetAreaId,
                        (int)(action.cardId == AIR_STRIKE_CARD ? BOMBER_AIRSTRIKE :
                            (action.cardId == BOMBER_CARD ? BOMBER_BOMB : BOMBER_NUCLEAR_BOMB)));
                }
                else
                {
                    // ordinary cards
                    UseCard(CObjectDef.Instance().GetCardDef(action.cardId), action.targetAreaId, action.armyIndex);
                }
                break;
            }

            case CountryAction.CAMERA_ACTION:
                FinishAction();
                break;

            case CountryAction.NO_ACTION:
            case CountryAction.INACTIVATE_ACTION:
                break;
        }
    }

    public int GetArmyCount()
    {
        int total = 0;
        foreach (int i in areaIdList) total += g_Scene.GetArea(i).armyCount;
        return total;
    }

    public int GetAreaCount()
    {
        int total = 0;
        foreach (int i in areaIdList) total += g_Scene.GetArea(i).info.sea ? 0 : 1;
        return total;
    }

    /// <summary>Whether the country owns the area.</summary>
    public bool FindArea(int areaId)
    {
        foreach (int id_here in areaIdList)
        {
            if (id_here == areaId) return true;
        }
        return false;
    }

    /// <summary>Id of the most valuable area of the country, -1 without areas.</summary>
    public int GetHighestValueArea()
    {
        int maxValue = -1;
        int maxValueAreaId = -1;
        foreach (int i in areaIdList)
        {
            CArea areaHere = g_Scene.GetArea(i);
            if (areaHere.HasArmyCard(3)) return areaHere.id;
            int value_here = CActionAssist.Instance().calcAreaValue(areaHere);
            if (maxValue < 0 || value_here > maxValue)
            {
                maxValue = value_here;
                maxValueAreaId = i;
            }
        }
        return maxValueAreaId;
    }

    /// <summary>Queues (and, for the local player, executes) an action.</summary>
    public void Action(CountryAction actionIn)
    {
        action.actionType = actionIn.actionType;
        action.cardId = actionIn.cardId;
        action.startAreaId = actionIn.startAreaId;
        action.targetAreaId = actionIn.targetAreaId;
        action.armyIndex = actionIn.armyIndex;
        switch (action.actionType)
        {
            case CountryAction.MOVE_ACTION:
                if (IsLocalPlayer())
                {
                    DoAction();
                    return;
                }
                g_Scene.MoveCameraBetweenArea(action.startAreaId, action.targetAreaId);
                break;
            case CountryAction.ATTACK_ACTION:
                g_Scene.fightFlashAreaId[0] = action.startAreaId;
                g_Scene.fightFlashAreaId[1] = action.targetAreaId;
                if (IsLocalPlayer())
                {
                    DoAction();
                    return;
                }
                g_Scene.MoveCameraBetweenArea(action.startAreaId, action.targetAreaId);
                break;
            case CountryAction.INACTIVATE_ACTION:
                g_Scene.GetArea(action.targetAreaId).SetArmyActive(action.armyIndex, false);
                FinishAction();
                return;
            case CountryAction.MOVE_FRONT_ACTION:
            case CountryAction.CARD_ACTION:
                if (IsLocalPlayer())
                {
                    DoAction();
                    return;
                }
                g_Scene.MoveCameraToArea(action.targetAreaId);
                break;
            case CountryAction.CAMERA_ACTION:
                g_Scene.MoveCameraToArea(action.targetAreaId);
                break;
            default:
                return;
        }
        actionWaitCamera = true;
        actionDelayTime = 0.0f;
    }

    /// <summary>Advances the wait / execute / finish states of the current action.</summary>
    /// <param name="time">seconds since the last frame</param>
    public void Update(float time)
    {
        if (actionWaitCamera)
        {
            if (actionDelayTime <= 0.0f)
            {
                if (g_Scene.IsMoving()) return;
            }
            else
            {
                actionTimer += time;
                if (g_Scene.IsMoving() || actionTimer < actionDelayTime) return;
            }

            actionWaitCamera = false;
            using (PerfStats.Measure("game.doAction")) DoAction();
            return;
        }

        CountryAction.ACTION_TYPE actionType = action.actionType;
        actionTimer += time;

        if (actionType == CountryAction.MOVE_ACTION)
        {
            CArea targetArea = g_Scene.GetArea(action.targetAreaId);
            if (targetArea.armyMovingIn != null || actionTimer <= 0.6f) return;
            using (PerfStats.Measure("game.finish")) FinishAction();
            return;
        }

        if ((actionType == CountryAction.ATTACK_ACTION ||
             actionType == CountryAction.MOVE_FRONT_ACTION ||
             actionType == CountryAction.CARD_ACTION) &&
            actionTimer > 0.6f)
        {
            FinishAction();
        }
    }

    /// <summary>Ends the current action; an attack also clears the flashing of both areas.</summary>
    public void FinishAction()
    {
        if (action.actionType == CountryAction.ATTACK_ACTION)
        {
            g_Scene.fightFlashAreaId[0] = -1;
            g_Scene.fightFlashAreaId[1] = -1;
        }
        action.actionType = CountryAction.NO_ACTION;
    }

    /// <summary>Whether no action is pending.</summary>
    public bool IsActionFinish() => action.actionType == CountryAction.NO_ACTION;

    /// <summary>The commander died: sets the respawn turns and the retreat dialogue (single player).</summary>
    public void CommanderDie()
    {
        commanderAlive = false;

        if (!ai)
        {
            commanderTurn = GetCommanderAbility(GetCommanderLevel()).coolDown;
            if (g_GameManager.gameMode == CGameManager.MULTIPLAYER) return;

            int variant = (int)(lrand48() % 5);
            string dialogueKey = Sprintf("commander retreat %d", variant + 1);
            var gameState = (CGameState)CStateManager.Instance().GetStatePtr(GAME);
            gameState.ShowDialogue(dialogueKey, "Player", false);
            return;
        }

        commanderTurn = 5;
        if (g_GameManager.gameMode == CGameManager.MULTIPLAYER) return;

        string commanderName = GetCommanderName();
        if (commanderName != null)
        {
            int variant = (int)(lrand48() % 5);
            string dialogueKey = Sprintf("commander retreat %d", variant + 1);
            var gameState = (CGameState)CStateManager.Instance().GetStatePtr(GAME);
            gameState.ShowDialogue(dialogueKey, commanderName, false);
        }
    }

    /// <summary>Name of the commander, null without one.</summary>
    public string GetCommanderName() => commanderDef != null ? commanderDef.name : null;

    /// <summary>
    /// Sets the commander medals: AI countries read the commander definition, multiplayer players
    /// their player data, every other player country the local commander save.
    /// </summary>
    /// <param name="commanderName">AI commander definition name (unused for non AI countries)</param>
    public void SetCommander(string commanderName)
    {
        if (ai)
        {
            if (commanderName != null)
            {
                commanderDef = CObjectDef.Instance().GetCommanderDef(commanderName);
                if (commanderDef != null)
                {
                    for (int medalId = 0; medalId < 6; ++medalId)
                    {
                        warMedal[medalId] = commanderDef.medal[medalId];
                    }
                }
            }
            return;
        }

        if (g_GameManager.gameMode == CGameManager.MULTIPLAYER)
        {
            if (player != null && player.commanderData != null)
            {
                for (int medalId = 0; medalId < 6; ++medalId)
                {
                    warMedal[medalId] = player.commanderData.warMedal[medalId];
                }
            }
            return;
        }

        for (int medalId = 0; medalId < 6; ++medalId)
        {
            warMedal[medalId] = g_Commander.GetWarMedalLevel((WARMEDAL_ID)medalId);
        }
    }

    public void SetWarMedalLevel(WARMEDAL_ID warMedalId, int level) => warMedal[(int)warMedalId] = level;

    public int GetWarMedalLevel(WARMEDAL_ID warMedalId) => warMedal[(int)warMedalId];

    /// <summary>
    /// Taxes of the turn: the commerce medal adds 10% per level, then the tax rate is applied.
    /// </summary>
    public int GetTaxes()
    {
        int totalTaxes = 0;
        foreach (int areaId in areaIdList)
        {
            totalTaxes += g_Scene.GetArea(areaId).GetRealTax();
        }

        // commerce medal: +10% per level (data/medals.json); level * 10 * total / 100 is the original level * total / 10
        int commerceMedalLevel = GetWarMedalLevel(COMMERCE_MEDAL);
        if (commerceMedalLevel > 0)
        {
            totalTaxes += (commerceMedalLevel * Medals.commerceIncomePercentPerLevel * totalTaxes) / 100;
        }
        return (int)(totalTaxes * taxRate);
    }

    /// <summary>
    /// Industry of the turn: the commerce medal adds 10% per level, then the tax rate is applied.
    /// </summary>
    public int GetIndustrys()
    {
        int totalIndustry = 0;
        foreach (int areaId in areaIdList)
        {
            totalIndustry += g_Scene.GetArea(areaId).GetIndustry();
        }

        int commerceMedalLevel = GetWarMedalLevel(COMMERCE_MEDAL);
        if (commerceMedalLevel > 0)
        {
            totalIndustry += (commerceMedalLevel * Medals.commerceIncomePercentPerLevel * totalIndustry) / 100;
        }
        return (int)(totalIndustry * taxRate);
    }

    /// <summary>First capital of the country, -1 without one (no caller at the moment).</summary>
    public int GetFirstCapital()
    {
        if (capitalIdList.Count == 0) return -1;
        return capitalIdList[0];
    }

    /// <summary>
    /// Card table of this country, falling back to "others". The original left this commented out
    /// ("of little use"); ported from that commented code.
    /// </summary>
    public Dictionary<int, CardDef> GetThisCountryNewCardDefMap()
    {
        var map = CObjectDef.Instance().mapNewCardDef;
        if (map.TryGetValue(name, out ToNewCardDef own) && own != null) return own.mapCarddef;
        if (map.TryGetValue("others", out ToNewCardDef others) && others != null) return others.mapCarddef;
        return new Dictionary<int, CardDef>();
    }

    // ---- GameTurnBegin.cpp ---------------------------------------------------------------

    /// <summary>Country turn start; calls the turn start of every area.</summary>
    public void TurnBegin()
    {
        // research progress
        if (techTurn > 0)
        {
            techTurn--;
            if (techTurn == 0) tech++;
        }
        // taxes
        if (g_GameManager.currentTurnNumMinusOne > 0)
        {
            CollectTaxes();
            CollectIndustrys();
        }
        // area turn start
        foreach (int AreaID in areaIdList)
        {
            g_Scene.GetArea(AreaID).TurnBegin();
        }
    }

    // ---- GameTurnEnd.cpp -----------------------------------------------------------------

    /// <summary>Country turn end.</summary>
    public void TurnEnd()
    {
        // commander cool-down
        if (!commanderAlive && commanderTurn > 0) commanderTurn -= 1;
        // card cool-downs
        for (int i = 0; i < cardRound.Length; i++)
        {
            if (cardRound[i] > 0) cardRound[i] -= 1;
        }

        // card usage counters; this feature still needs UI work, better not use it yet
        for (int cardId = (int)INFANTRY_CARD; cardId < VALID_CARD_COUNT; cardId++)
        {
            CardDef card = CObjectDef.Instance().GetCardDef((CARD_ID)cardId);
            if (card == null) continue;

            // round 0 refills the uses every turn, a positive round does not;
            // round 1 and 0 behave slightly differently
            if (cardTimes[cardId] >= card.maxTimes && card.round > 0)
            {
                cardTimes[cardId] = 0;
            }
        }

        // area turn end
        foreach (int AreaID in areaIdList)
        {
            g_Scene.GetArea(AreaID).TurnEnd();
        }
    }

    // ---- GameLoadSave.cpp ----------------------------------------------------------------

    /// <summary>Stores the country (without its areas) into saveInfo.</summary>
    public void SaveCountry(SaveCountryInfo saveInfo)
    {
        saveInfo.money = money;
        saveInfo.industry = industry;
        saveInfo.tech = tech;
        saveInfo.techTurn = techTurn;
        saveInfo.ai = ai;
        Array.Copy(cardRound, saveInfo.cardRound, Math.Min(cardRound.Length, saveInfo.cardRound.Length));
        Array.Copy(cardTimes, saveInfo.cardTimes, Math.Min(cardTimes.Length, saveInfo.cardTimes.Length));
        saveInfo.id = id;
        saveInfo.name = name;
        saveInfo.r = r;
        saveInfo.g = g;
        saveInfo.b = b;
        saveInfo.a = a;
        saveInfo.alliance = alliance;
        saveInfo.defeatType = (int)defeatType;
        saveInfo.taxRate = taxRate;
        Array.Copy(destroyCount, saveInfo.destroyCount, destroyCount.Length);
        saveInfo.commanderName = commanderDef != null ? commanderDef.name : "";
        saveInfo.commanderTurn = commanderTurn;
        saveInfo.commanderAlive = commanderAlive;
        Array.Copy(warMedal, saveInfo.warMedal, warMedal.Length);
        saveInfo.borrowedLoan = borrowedLoan;
        saveInfo.defeated = defeated;
    }

    /// <summary>Restores the country (without its areas) from saveCountryInfo.</summary>
    public void LoadCountry(SaveCountryInfo saveCountryInfo)
    {
        money = saveCountryInfo.money;
        industry = saveCountryInfo.industry;
        tech = saveCountryInfo.tech;
        techTurn = saveCountryInfo.techTurn;
        ai = saveCountryInfo.ai;
        Array.Copy(saveCountryInfo.cardRound, cardRound, Math.Min(cardRound.Length, saveCountryInfo.cardRound.Length));
        Array.Copy(saveCountryInfo.cardTimes, cardTimes, Math.Min(cardTimes.Length, saveCountryInfo.cardTimes.Length));
        id = saveCountryInfo.id ?? "";
        name = saveCountryInfo.name ?? "";
        r = saveCountryInfo.r;
        g = saveCountryInfo.g;
        b = saveCountryInfo.b;
        a = saveCountryInfo.a;
        alliance = saveCountryInfo.alliance;
        defeatType = (DEFEAT_TYPE)saveCountryInfo.defeatType;
        taxRate = saveCountryInfo.taxRate;
        Array.Copy(saveCountryInfo.destroyCount, destroyCount, destroyCount.Length);
        SetCommander(saveCountryInfo.commanderName);
        commanderTurn = saveCountryInfo.commanderTurn;
        commanderAlive = saveCountryInfo.commanderAlive;
        Array.Copy(saveCountryInfo.warMedal, warMedal, warMedal.Length);
        borrowedLoan = saveCountryInfo.borrowedLoan;
        defeated = saveCountryInfo.defeated;
    }
}
