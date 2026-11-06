namespace WC2;

/// <summary>AI main decision loop (CActionAI.cpp).</summary>
public sealed partial class CActionAI
{
    private static CActionAI _instance;
    public static CActionAI Instance() => _instance ??= new CActionAI();

    public string currentCountryName = "";
    public CCountry currentCountry;
    public int turnBeginMoneyQuarter;
    public int turnBeginIndustryQuarter;
    public int aiProgressPercentage;
    public string currentCountryNameShort = "";
    public int totalAiArmyAreaCount;
    public int turnBeginActiveAreaCount;
    public bool x2C, x2D, x2E, x2F;
    public bool action;
    public int currentCountryIndex;

    public CActionAI()
    {
        setActionNodeClear();
        currentCountryName = "";
        currentCountryIndex = -1;
    }

    public void InitAI()
    {
        totalAiArmyAreaCount = 0;
        turnBeginActiveAreaCount = 0;
        CActionAssist.Instance().totalSeaAreaCount = 0;
        int i;
        // refresh the statistics
        for (i = 0; i < g_Scene.areaList.Count; i++)
        {
            CArea area = g_Scene.GetArea(i);
            if (area.info.enable)
            {
                if (area.info.sea)
                    CActionAssist.Instance().totalSeaAreaCount += 1;
                CCountry country = area.country;
                if (country != null && country.ai && area.armyCount > 0)
                    totalAiArmyAreaCount += 1;
            }
        }
        aiProgressPercentage = 1;
    }

    /// <summary>
    /// Medal drop chance.
    /// </summary>
    /// <param name="damage">damage dealt</param>
    /// <param name="TargetArea">attacked area</param>
    /// <returns>whether a medal is gained</returns>
    public bool getMedal(int damage, CArea TargetArea)
    {
        // original (inlined in CFight::ApplyResult): AI random stream, no medal below 20 damage,
        // strict thresholds; the caller passes the attacked area for both the attacker's and the
        // defender's roll
        if (damage < 20) return false;
        int r = GetAIRand() % 100;
        if (TargetArea != null)
        {
            // capital +12, large city +8, normal city +4 (terrain: medalBonus)
            r += Terrain.Area(TargetArea.info.areaType).medalBonus;
        }
        if (damage <= 24) return r > 95;
        if (damage <= 29) return r > 91;
        if (damage <= 34) return r > 87;
        return r > 82;
    }

    /// <summary>
    /// Checks the "constant within the turn" information to decide whether a card is usable this turn.
    /// </summary>
    /// <param name="cardId">card id</param>
    /// <returns>whether the card can be used</returns>
    public bool detectCards(CARD_ID cardId)
    {
        CardDef cardDef = CObjectDef.Instance().GetCardDef(cardId);
        if (cardDef == null) return false;

        // unlocked (tech requirement)?
        CCountry curCountry = currentCountry;
        bool unlockedCard = curCountry.IsCardUnlock(cardDef);
        if (!unlockedCard) return false;

        // economy at turn start: if it cannot be used now it cannot be used later either
        if (cardDef.price > curCountry.money || cardDef.industry > curCountry.industry) return false;

        // the commander needs a special check
        if (cardId == COMMANDER_CARD)
        {
            return currentCountry.CanUseCommander();
        }

        // every card may have a cool down now
        return currentCountry.GetCardRounds(cardId) == 0 && unlockedCard;
    }

    /// <summary>Refreshes the g_NationInfo statistics.</summary>
    public void analyseCompositePower()
    {
        CCountry country = currentCountry;
        g_NationInfo.money = country.money;
        g_NationInfo.industry = country.industry;
        g_NationInfo.tech = country.tech;
        for (int cardId = (int)INFANTRY_CARD; cardId < VALID_CARD_COUNT; cardId++)
        {
            g_NationInfo.canUseCard[cardId] = detectCards(CObjectDef.TurnIntToRealCardId(cardId));
        }
    }

    /// <summary>
    /// Main frame of the AI decision.
    /// </summary>
    /// <param name="countryIndex">index of the country in the list</param>
    /// <param name="CGameManagerx38">unknown, probably legacy</param>
    /// <returns>a usable action node, or null when the action is invalid or nothing worthwhile is left</returns>
    public ActionNode setCpuDriverbyId(int countryIndex, int CGameManagerx38)
    {
        // initialisation before one decision
        action = false;
        CActionAssist.Instance().actionToNextAreaTargetIdList.Clear();
        setActionNodeClear();

        // current country
        CCountry curCountry = g_GameManager.GetCountryByIndex(countryIndex);
        currentCountry = curCountry;
        if (curCountry == null || curCountry.IsConquested())
        {
            // safety check
            CActionAssist.Instance().actionNode.actionType = ActionNode.INV_NODE;
            return null;
        }
        else
        {
            currentCountryName = currentCountry.name;
            // AI progress bar
            getAiPercent(countryIndex);

            // first action of a new country: remember money and industry for later card decisions
            if (currentCountryIndex != countryIndex)
            {
                currentCountryIndex = countryIndex;
                turnBeginMoneyQuarter = currentCountry.money / 4;
                turnBeginIndustryQuarter = currentCountry.industry / 4;
            }

            // basic situation of the country, including usable cards
            analyseCompositePower();

            // pick a card category at random; the lowest card id of the category stands for it
            int choice = GetAIRand() % 100;
            CARD_ID cardId;
            if (choice > 92)
            {
                // installation cards
                cardId = LAND_FORT_CARD;
            }
            else if (choice > 79)
            {
                cardId = AIR_STRIKE_CARD;
            }
            else if (choice > 72)
            {
                // construction cards
                cardId = CITY_CARD;
            }
            else if (choice > 59)
            {
                cardId = ASSAULT_ART_CARD;
            }
            else if (choice > 49)
            {
                cardId = DESTROYER_CARD;
            }
            else
            {
                cardId = INFANTRY_CARD;
            }

            // try to buy a card
            purChaseCard(cardId);
            // try to move a unit to the top
            sortArmyToFront();
            // try to move or attack
            moveAndAttack();
            if (action)
                return CActionAssist.Instance().actionNode;
            // original: forget the working country when it has nothing left to do
            currentCountryIndex = -1;
            return null;
        }
    }

    public void setActionNodeClear()
    {
        CActionAssist actionAssist = CActionAssist.Instance();
        actionAssist.actionNode.cardId = INVALID_CARD;
        actionAssist.actionNode.x1888 = 0;
        actionAssist.actionNode.armyIndex = -1;
        actionAssist.actionNode.startAreaId = -1;
        actionAssist.searchHead = -1;
        actionAssist.actionNode.actionType = ActionNode.CLEAR_NODE;
    }

    /// <summary>Decides whether a unit of some area should be moved to the top of its stack.</summary>
    public void sortArmyToFront()
    {
        // a unit with another action keeps moving; do not swap it
        if (action) return;
        CCountry curCountry = CActionAI.Instance().currentCountry;
        foreach (int areaId in curCountry.areaIdList)
        {
            CArea area = g_Scene.GetArea(areaId);
            // Too few units: no stack adjustment needed. The original did not consider sea areas
            // either (fixed here, old conditions kept as comments):
            // bool needCheckMoveFront = area.armyCount > 1 && !area.info.sea && area.IsActive();
            // bool needCheckMoveFront = area.armyCount > 1 && area.IsActive();
            bool needCheckMoveFront = area.armyCount > 1;

            if (needCheckMoveFront)
            {
                int candidatePos = 1;
                int selectedPos = -1;
                CArmy topArmy = area.GetArmy(0);
                int curArmyHp = topArmy.hp;
                int rawArmyHp = curArmyHp;
                // pick the lower unit with the most hp
                while (candidatePos < area.armyCount)
                {
                    if (area.IsArmyActive(candidatePos))
                    {
                        CArmy nextArmy = area.GetArmy(candidatePos);
                        if (nextArmy.hp > curArmyHp)
                        {
                            selectedPos = candidatePos;
                            curArmyHp = nextArmy.hp;
                        }
                    }
                    ++candidatePos;
                }
                if (selectedPos != -1)
                {
                    // even when found, swapping is a matter of probability
                    int choice = GetAIRand() % 100;
                    bool whetherMoveFront;
                    switch (area.armyCount)
                    {
                        case 2:
                            whetherMoveFront = choice <= 60;
                            break;
                        case 3:
                            whetherMoveFront = choice <= 70;
                            break;
                        case 4:
                            whetherMoveFront = choice <= 80;
                            break;
                        default:
                            // exceptional case
                            whetherMoveFront = false;
                            break;
                    }
                    if (!whetherMoveFront) continue;
                    int boost = curArmyHp - rawArmyHp;
                    bool finallyShouldMoveFront = boost > 29 && boost >= choice;
                    // select the unit to move up
                    if (finallyShouldMoveFront)
                    {
                        CActionAssist actionAssist = CActionAssist.Instance();
                        actionAssist.actionNode.cardId = INVALID_CARD;
                        actionAssist.actionNode.actionType = ActionNode.MOVE_FRONT_NODE;
                        actionAssist.actionNode.startAreaId = area.id;
                        actionAssist.actionNode.targetAreaId = area.id;
                        actionAssist.actionNode.x1888 = 0;
                        actionAssist.actionNode.armyIndex = selectedPos;
                        action = true;
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Index of the entry with the highest targetValue in the candidate list (the caller checks for emptiness).
    /// </summary>
    public int getMaxId()
    {
        // the original compares signed (cmp/jge in the binary): a negative score never wins and
        // the first node (the "stay" node) is kept when every candidate is <= 0
        int curMax = 0;
        int idx = 0;
        List<NODE> vec = CActionAssist.Instance().actionToNextAreaTargetIdList;
        for (int i = 0; i < vec.Count; ++i)
        {
            if (curMax < vec[i].targetValue)
            {
                curMax = vec[i].targetValue;
                idx = i;
            }
        }
        return idx;
    }

    /// <summary>
    /// Tries to set up the move / attack logic for a given area.
    /// </summary>
    /// <param name="inArea">area to check for units wanting to act</param>
    /// <returns>index of the unit within the area</returns>
    public int setArmyAction(CArea inArea)
    {
        int areaId = inArea.id;
        int inArmyCount = inArea.armyCount;
        int selectedArmyIdx = -1;
        for (int i = 0; i < inArmyCount; i++)
        {
            CArmy army = inArea.GetArmy(i);
            if (army.active)
            {
                selectedArmyIdx = i;
            }
        }
        if (selectedArmyIdx != -1)
        {
            CActionAssist actionAssist = CActionAssist.Instance();
            // stay still?
            actionAssist.actionToNextArea(0x1000, areaId, selectedArmyIdx, 1);
            // move?
            actionAssist.actionToNextArea(0x2000, areaId, selectedArmyIdx, 1);
            // attack?
            actionAssist.actionToNextArea(0x3000, areaId, selectedArmyIdx, 1);
            // implicit path finding, then take the first area towards the found target
            actionAssist.actionToNextArea(0x4000, areaId, selectedArmyIdx, 1);
        }
        return selectedArmyIdx;
    }

    /// <summary>
    /// Updates the AI progress bar information.
    /// </summary>
    /// <param name="countryIndex">index of the current country</param>
    public void getAiPercent(int countryIndex)
    {
        int numAreas = g_Scene.GetNumAreas();
        int thisCountryNowActiveAreaCount = 0;
        // original: the active areas are counted only on the first call for a country, which also
        // makes it the working country here (so setCpuDriverbyId never refreshes the money
        // quarters and its purchase gate stays inert); later calls contribute nothing
        if (currentCountryIndex != countryIndex)
        {
            currentCountryIndex = countryIndex;
            for (int i = 0; i < numAreas; i++)
            {
                CArea area = g_Scene.GetArea(i);
                bool aiAreaActive = area.country != null && area.country.ai
                                    && area.info.enable && area.IsActive();
                if (aiAreaActive)
                {
                    turnBeginActiveAreaCount++;
                    thisCountryNowActiveAreaCount++;
                }
            }
        }

        // number of progress blocks
        int progressedAreaCount = turnBeginActiveAreaCount - thisCountryNowActiveAreaCount;
        int aiProgressBlocks = (int)(((progressedAreaCount / (1.0 * totalAiArmyAreaCount)) * 10.0) + 1);
        if (aiProgressBlocks > 10)
            aiProgressBlocks = 10;
        aiProgressPercentage = aiProgressBlocks;
        if (aiProgressPercentage <= 0)
            aiProgressPercentage = 1;
        currentCountryNameShort = currentCountryName.Length >= 2
            ? currentCountryName.Substring(0, 2)
            : currentCountryName;
    }

    /// <summary>
    /// Decides whether to use the card category given by its id and which card of the category.
    /// </summary>
    /// <param name="card">card id standing for a category</param>
    /// <returns>whether a card is used (the return value is actually unused)</returns>
    public bool purChaseCard(CARD_ID card)
    {
        bool willUseCard;
        string aiCardUsingStr;
        // an action is already pending, do not check any further
        if (action) return false;

        // the country is short of money
        CCountry country = currentCountry;
        if (country.money < turnBeginMoneyQuarter || country.industry < turnBeginIndustryQuarter)
            return false;
        aiCardUsingStr = Sprintf("--PurChaseCard AI[%d]---\n", (int)card);

        // category specific checks
        if (card == AIR_STRIKE_CARD)
        {
            willUseCard = CActionAssist.Instance().purChaseAirStrikeCard();
        }
        else if (card == LAND_FORT_CARD)
        {
            willUseCard = CActionAssist.Instance().purChaseFortCard();
        }
        else if (card == ASSAULT_ART_CARD)
        {
            // unit enhancement tactics cards
            willUseCard = CActionAssist.Instance().purChaseCardsCard();
        }
        else if (card == CITY_CARD)
        {
            // construction cards
            willUseCard = CActionAssist.Instance().purChaseConstructCard();
        }
        else if (card == INFANTRY_CARD || card == DESTROYER_CARD)
        {
            // unit cards
            willUseCard = CActionAssist.Instance().purChaseDraftCard(card == DESTROYER_CARD);
        }
        else
        {
            return true;
        }

        if (willUseCard)
        {
            action = true;
            return true;
        }
        return true;
    }

    /// <summary>Decides the unit move / attack action (through actionNode).</summary>
    public void moveAndAttack()
    {
        if (action) return;
        CCountry curCountry = currentCountry;
        // all own areas
        foreach (int areaId in curCountry.areaIdList)
        {
            // clear the search list
            CActionAssist.Instance().actionToNextAreaTargetIdList.Clear();
            CArea area = g_Scene.GetArea(areaId);
            if (area.IsActive() && area.armyCount > 0)
            {
                // the active unit of the area that got actions assigned
                int activeArmyIdx = setArmyAction(area);
                if (activeArmyIdx != -1)
                {
                    CActionAssist actionAssist = CActionAssist.Instance();
                    if (actionAssist.actionToNextAreaTargetIdList.Count != 0)
                    {
                        // there are worthwhile actions: take the best one as move or attack
                        int maxValueNodeId = getMaxId();
                        CArea maxValArea = g_Scene.GetArea(
                            actionAssist.actionToNextAreaTargetIdList[maxValueNodeId].actionToNextAreaTargetId);
                        if (maxValArea.armyCount <= 0 || maxValArea.country == area.country)
                        {
                            // friendly or empty: move
                            actionAssist.actionNode.actionType = ActionNode.MOVE_NODE;
                        }
                        else
                        {
                            // occupied and not friendly: attack
                            actionAssist.actionNode.actionType = ActionNode.ATTACK_NODE;
                        }
                        actionAssist.actionNode.startAreaId = area.id;
                        actionAssist.actionNode.targetAreaId = maxValArea.id;
                        actionAssist.actionNode.x1888 = 0;
                        actionAssist.actionNode.armyIndex = activeArmyIdx;
                    }
                    else
                    {
                        // nothing to do: stay still
                        actionAssist.actionNode.cardId = INVALID_CARD;
                        actionAssist.actionNode.actionType = ActionNode.MOVE_NODE;
                        actionAssist.actionNode.armyIndex = activeArmyIdx;
                        actionAssist.actionNode.startAreaId = area.id;
                        actionAssist.actionNode.targetAreaId = area.id;
                        actionAssist.actionNode.x1888 = 0;
                    }
                    action = true;
                    return;
                }
            }
        }
    }

    /// <summary>DEPRECATED_EMPTY in the original.</summary>
    public int getNumArmyMove(int a, int b) => 0;
}
