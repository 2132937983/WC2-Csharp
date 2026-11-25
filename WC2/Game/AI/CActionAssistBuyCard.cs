namespace WC2;

/// <summary>Card purchase decisions of the AI (CActionAssistBuyCard.cpp).</summary>
public sealed partial class CActionAssist
{
    private IEnumerable<CardDef> GetAICards(CCountry country) =>
        CObjectDef.Instance().GetCardDefs(country)
            .Where(card => country.CanBuyCard(card))
            .OrderByDescending(card => card.aiWeight);

    private CardDef FindConstructionCard(CCountry country, CONSTRUCTION_TYPE construction) =>
        GetAICards(country).FirstOrDefault(card => card.effects.OfType<AreaStructureEffectDef>()
            .Any(effect => effect.slot == AreaStructureSlot.CONSTRUCTION && effect.structure == construction));

    private CardDef FindInstallationCard(CCountry country, INSTALLATION_TYPE installation) =>
        GetAICards(country).FirstOrDefault(card => card.effects.OfType<AreaStructureEffectDef>()
            .Any(effect => effect.slot == AreaStructureSlot.INSTALLATION && effect.installation == installation));

    private CardDef FindBuffCard(CCountry country, UnitBuffKind buff) =>
        GetAICards(country).FirstOrDefault(card => card.effects.OfType<UnitBuffEffectDef>()
            .Any(effect => effect.buff == buff));

    private CardDef FindStrikeCard(CCountry country, COMBAT_AIRSTRIKE_PAYLOAD payload) =>
        GetAICards(country).FirstOrDefault(card => card.effects.OfType<StrategicStrikeEffectDef>()
            .Any(effect => effect.payload == payload));

    private CardDef FindAirborneCard(CCountry country) =>
        GetAICards(country).FirstOrDefault(card => card.effects.OfType<AirborneDeployEffectDef>().Any());

    /// <summary>Legacy special card category; the original placeholder never buys anything.</summary>
    public bool purChaseSpecialCard()
    {
        return false;
    }

    /// <summary>
    /// Decides whether to use a unit card and which draft card. Also updates actionNode, which is the
    /// real return value.
    /// </summary>
    /// <param name="navy">whether a navy unit is drafted</param>
    public bool purChaseDraftCard(bool navy)
    {
        CActionAI ai = CActionAI.Instance();
        CCountry curCountry = ai.currentCountry;
        int globalBenefit = -1; // global benefit
        int selectedAreaId = -1;
        foreach (int areaId in curCountry.areaIdList)
        {
            CArea curArea = g_Scene.GetArea(areaId);
            if (navy)
            {
                // navy can only be drafted in ports
                if (curArea.info.areaType != PORT)
                {
                    continue;
                }
            }
            else
            {
                // land units cannot be drafted at sea
                if (curArea.info.sea)
                {
                    continue;
                }
            }
            // area value
            int areaValue = calcAreaValue(curArea);
            // hp sum of all enemies around
            int enemyThreatenVal = getNeighbor(areaId, 2010, 1, 1);
            int importance = areaValue + enemyThreatenVal;
            // original rates the garrison by the hp of the whole stack
            int curAreaArmyHp = calcAreaArmyHp(curArea, false);
            // importance *= 10;  (depends on the mod's hp scale: with 10x hp the AI would only draft infantry)
            if (curAreaArmyHp >= importance)
            {
                // unimportant area: the units there already protect it
                continue;
            }

            // the original draws the random number only inside the band that needs it
            if ((float)curAreaArmyHp >= 0.75 * (float)importance)
            {
                // small threat: mostly ignored, sometimes considered
                if (GetAIRand() % 100 <= 80) continue;
            }
            else if ((float)curAreaArmyHp >= 0.50 * (float)importance)
            {
                if (GetAIRand() % 100 <= 50) continue;
            }
            else if ((float)curAreaArmyHp >= 0.25 * (float)importance)
            {
                if (GetAIRand() % 100 <= 20) continue;
            }
            // threat check passed: check the simplest card of the faction
            // (a more general "basic card" abstraction is planned)
            bool basicConstructionCheck = CObjectDef.Instance().GetCardDefs(curCountry)
                .Where(card => card.type == (navy ? NAVY_TYPE_CARD : ARMY_TYPE_CARD) &&
                               card.effects.OfType<DraftArmyEffectDef>().Any())
                .Any(card => curCountry.CheckCardTargetArea(card, areaId));
            if (basicConstructionCheck)
            {
                int curDiff = importance - curAreaArmyHp;
                if (globalBenefit < curDiff)
                {
                    // update the global benefit
                    globalBenefit = curDiff;
                    selectedAreaId = areaId;
                }
            }
        }

        if (selectedAreaId == -1) return false; // nowhere to use the card
        int draftId = calcDraftType(navy, selectedAreaId, curCountry);
        // must be set explicitly; to become -1 later
        if (draftId == (int)INVALID_CARD)
        {
            return false;
        }
        CARD_ID draftType = CObjectDef.TurnIntToRealCardId(draftId);

        CardDef tgtCard = CObjectDef.Instance().GetCardDef(draftType);
        if (tgtCard == null) return false;
        actionNode.cardId = draftType;
        actionNode.actionType = ActionNode.CARD_NODE;
        actionNode.startAreaId = selectedAreaId;
        actionNode.targetAreaId = selectedAreaId;
        return true;
    }

    /// <summary>
    /// Picks the draft card by walking the price ordered candidates.
    /// </summary>
    /// <param name="navy">whether a navy unit is drafted</param>
    /// <param name="targetAreaId">area where the unit is drafted</param>
    /// <param name="curCountry">acting country</param>
    /// <returns>the draft card type</returns>
    public int calcDraftType(bool navy, int targetAreaId, CCountry curCountry)
    {
        IEnumerable<CardDef> candidates = CObjectDef.Instance().GetCardDefs(curCountry)
            .Where(card => card.effects.OfType<DraftArmyEffectDef>().Any() &&
                           (card.type == (navy ? NAVY_TYPE_CARD : ARMY_TYPE_CARD)))
            .OrderByDescending(card => card.price)
            .ThenByDescending(card => card.industry)
            .ThenByDescending(card => card.tech);

        // Preserve the original descending price/industry/tech walk, but derive candidates from
        // their draft effects instead of the legacy troop-card ID table.
        foreach (CardDef card in candidates)
        {
            if (!g_NationInfo.canUseCard.TryGetValue(card.key, out bool canUse) || !canUse) continue;
            // the AI wants to build and the area allows it
            if (isBuyCard(card) && curCountry.CheckCardTargetArea(card, targetAreaId)) return (int)card.id;
        }
        return (int)INVALID_CARD;
    }

    /// <summary>
    /// Installation cards: fort, anti-aircraft, entrenchment and radar. Custom installation cards
    /// can be added here.
    /// </summary>
    /// <returns>whether a card was chosen</returns>
    public bool purChaseFortCard()
    {
        CCountry curCountry = CActionAI.Instance().currentCountry;
        CardDef selectedFortCard;
        int choice = GetAIRand() % 100;
        // choose first so that even the anti-aircraft gun gets a chance
        if (choice <= 29)
        {
            selectedFortCard = FindInstallationCard(curCountry, ENTRENCHMENT);
        }
        else if (choice <= 59)
        {
            selectedFortCard = FindInstallationCard(curCountry, FORT);
        }
        else if (choice >= 80)
        {
            selectedFortCard = FindInstallationCard(curCountry, RADAR);
        }
        else
        {
            selectedFortCard = FindInstallationCard(curCountry, ANTIAIRCRAFT);
        }
        if (selectedFortCard == null || !isBuyCard(selectedFortCard) || selectedFortCard.effects.Count != 1 ||
            selectedFortCard.effects[0] is not AreaStructureEffectDef installationEffect ||
            installationEffect.slot != AreaStructureSlot.INSTALLATION)
            return false;
        INSTALLATION_TYPE selectedInstallation = installationEffect.installation;
        int globalBenifit = -1;
        int cardUsingAreaId = -1;
        foreach (int areaId in curCountry.areaIdList)
        {
            CArea area = g_Scene.GetArea(areaId);
            if (area.installationType != NO_INSTALLATION) continue;
            if (area.info.sea) continue;
            int areaArmyCnt = area.armyCount;
            if (areaArmyCnt == 0) continue;

            int benefit = 4 * area.GetCityLevel() + 10 * areaArmyCnt + 5 * area.GetIndustryLevel();
            switch (selectedInstallation)
            {
                case ENTRENCHMENT:
                    // 2010: hp sum of the surrounding enemies, hence the division
                    // (could be refined, e.g. more value against non-tank units)
                    benefit += getNeighbor(areaId, 2010, 1, 1) / 10;
                    break;
                case FORT:
                    // navy around: forts counter navy twice, more value
                    if (getNeighbor(areaId, 2070, 1, 1) != -1)
                    {
                        benefit += 50;
                    }
                    // artillery around
                    if (getNeighbor(areaId, 2080, 1, 1) + 1 > 0)
                    {
                        benefit += 50;
                    }
                    break;
                case ANTIAIRCRAFT:
                    // what is an anti-aircraft gun worth?
                    break;
                default:
                    // radar: artillery within two steps (radars also stop rockets, to be studied)
                    if (getNeighbor(areaId, 2080, 1, 1) + 1 > 0)
                    {
                        benefit += 100;
                    }
                    break;
            }
            if (globalBenifit < benefit)
            {
                if (isBuyCard(selectedFortCard) &&
                    curCountry.CheckCardTargetArea(selectedFortCard, areaId))
                {
                    globalBenifit = benefit;
                    cardUsingAreaId = areaId;
                }
            }
        }

        if (cardUsingAreaId == -1) return false;
        actionNode.cardId = selectedFortCard.id;
        actionNode.actionType = ActionNode.CARD_NODE;
        actionNode.startAreaId = cardUsingAreaId;
        actionNode.targetAreaId = cardUsingAreaId;
        actionNode.armyIndex = 0;
        return true;
    }

    /// <summary>
    /// Construction cards: city, airport and industry. Custom facility cards can be added here.
    /// </summary>
    /// <returns>whether a construction card is bought</returns>
    public bool purChaseConstructCard()
    {
        CCountry curCountry = CActionAI.Instance().currentCountry;
        int choice = GetAIRand() % 100;
        CardDef selectedConstructionCard;
        if (choice <= 54)
        {
            selectedConstructionCard = FindConstructionCard(curCountry, CITY);
        }
        else if (choice >= 90)
        {
            selectedConstructionCard = FindConstructionCard(curCountry, AIRPORT);
        }
        else
        {
            selectedConstructionCard = FindConstructionCard(curCountry, INDUSTRY);
        }
        if (selectedConstructionCard == null || !isBuyCard(selectedConstructionCard) ||
            selectedConstructionCard.effects.Count != 1 ||
            selectedConstructionCard.effects[0] is not AreaStructureEffectDef constructionEffect)
            return false;
        CONSTRUCTION_TYPE selectedConstruction = constructionEffect.structure;

        int globalBenefit = -1;
        int selectedAreaId = -1;
        foreach (int areaId in curCountry.areaIdList)
        {
            CArea area = g_Scene.GetArea(areaId);
            if (area.info.sea) continue;
            int localBenefit = 0;
            if (selectedConstruction == AIRPORT)
            {
                // airports only on empty land
                if (area.info.constructionType != NO_CONSTRUCTION) continue;
                if (area.armyCount > 0)
                {
                    localBenefit = 10;
                }
                else
                {
                    localBenefit = 0;
                }
            }
            else if (selectedConstruction == INDUSTRY)
            {
                // existing construction on the area?
                if (area.info.constructionType != NO_CONSTRUCTION)
                {
                    // not an industry: skip
                    if (area.info.constructionType != INDUSTRY)
                    {
                        continue;
                    }
                    else
                    {
                        // original: weighted by the city level of the area type (capitals are
                        // never upgraded, large city 30, city 20, else 0)
                        int cityLevelHere = area.GetCityLevel();
                        if (cityLevelHere == 3) continue;
                        localBenefit = cityLevelHere == 2 ? 30 : cityLevelHere == 1 ? 20 : 0;
                    }
                }
                else
                {
                    localBenefit = 10;
                }
                // units make building safer
                if (area.armyCount > 0)
                {
                    localBenefit += 10;
                }
            }
            else
            {
                if (area.info.constructionType != NO_CONSTRUCTION)
                {
                    // only cities can be built further
                    if (area.info.constructionType != CITY)
                    {
                        continue;
                    }
                    else
                    {
                        int areaCityLevel = area.GetCityLevel();
                        if (areaCityLevel >= 4) continue;

                        if (areaCityLevel == 3)
                        {
                            localBenefit = 45;
                        }
                        else if (areaCityLevel < 1)
                        {
                            // actually an exceptional case
                            localBenefit = 0;
                        }
                        else
                        {
                            localBenefit = (areaCityLevel + 1) * 10;
                        }
                    }
                }
                else
                {
                    localBenefit = 10;
                }
                // units make building safer
                if (area.armyCount > 0)
                {
                    localBenefit += 15;
                }
            }
            // added coefficient to stop the AI from building airports in capitals
            int coe = selectedConstruction == AIRPORT ? -1 : 1;
            switch (area.info.areaType)
            {
                case CAPITAL:
                    localBenefit += 30 * coe;
                    break;
                case LARGE_CITY:
                    localBenefit += 20 * coe;
                    break;
                case NORMAL_CITY:
                    localBenefit += 10 * coe;
                    break;
                default:
                    break;
            }
            if (globalBenefit < localBenefit)
            {
                if (isBuyCard(selectedConstructionCard) &&
                    curCountry.CheckCardTargetArea(selectedConstructionCard, areaId))
                {
                    globalBenefit = localBenefit;
                    selectedAreaId = areaId;
                }
            }
        }

        if (selectedAreaId == -1) return false;
        actionNode.cardId = selectedConstructionCard.id;
        actionNode.actionType = ActionNode.CARD_NODE;
        actionNode.startAreaId = selectedAreaId;
        actionNode.targetAreaId = selectedAreaId;
        actionNode.armyIndex = 0;
        return true;
    }

    /// <summary>
    /// Air force cards: air strike / bomber, nuclear bomb and airborne.
    /// </summary>
    /// <returns>whether a card is used</returns>
    public bool purChaseAirStrikeCard()
    {
        // how the AI uses the air force cards
        CCountry curCountry = CActionAI.Instance().currentCountry;
        int airTargetAreaId = -1;
        int choice = GetAIRand() % 10;
        // choose the card first, then search per card
        CARD_ID airCardId = INVALID_CARD;
        CardDef airStrikeCard = FindStrikeCard(curCountry, COMBAT_AIRSTRIKE_PAYLOAD.COMBAT_AIRSTRIKE_PAYLOAD_AIRSTRIKE);
        CardDef bomberCard = FindStrikeCard(curCountry, COMBAT_AIRSTRIKE_PAYLOAD.COMBAT_AIRSTRIKE_PAYLOAD_BOMBER);
        CardDef nuclearCard = FindStrikeCard(curCountry, COMBAT_AIRSTRIKE_PAYLOAD.COMBAT_AIRSTRIKE_PAYLOAD_NUCLEAR);
        CardDef airborneCard = FindAirborneCard(curCountry);
        if (choice <= 5)
        {
            // air strike / bombing: 60%
            bool cannotAirStrike = airStrikeCard == null || !isBuyCard(airStrikeCard);
            bool cannotBomb = bomberCard == null || !isBuyCard(bomberCard);
            if (cannotAirStrike && cannotBomb)
            {
                // neither card is usable
                return false;
            }
            int normalAirAtkBenefit = -1;
            foreach (int areaId in curCountry.areaIdList)
            {
                int neighbourArmiesHp = getNeighbor(areaId, 2030, 1, 1);
                int neighbourId = getNeighbourId[0];
                // The original was odd here (getNeighbourId held the value); the logic was rewritten.
                // Only areas bordering us are air struck, chosen by the neighbour hp.
                if (neighbourId == -1) continue;
                CArea neighbour = g_Scene.GetArea(neighbourId);
                if (neighbour == null) return false;

                if (neighbour.info.sea)
                {
                    // ships have more hp, so halve it
                    neighbourArmiesHp /= 2;
                }

                // enemies with 99 hp or less are not worth an air strike / bombing
                if (neighbourArmiesHp <= 99) continue;
                int localBenefit;
                CARD_ID curSuitableCardId;
                if (neighbourArmiesHp <= 199)
                {
                    // more than 99 but below 200: air strike
                    if (cannotAirStrike) continue;
                    localBenefit = 50;
                    curSuitableCardId = airStrikeCard.id;
                }
                else if (cannotBomb)
                {
                    // original: when the bomber is unavailable the target is still worth an air strike
                    if (cannotAirStrike) continue;
                    localBenefit = 50;
                    curSuitableCardId = airStrikeCard.id;
                }
                else
                {
                    // otherwise bombing
                    localBenefit = 60;
                    curSuitableCardId = bomberCard.id;
                }
                // anti-aircraft guns remove the benefit
                if (neighbour.installationType == ANTIAIRCRAFT)
                    localBenefit -= 100;
                switch (neighbour.info.areaType)
                {
                    case CAPITAL:
                        localBenefit += 20;
                        break;
                    case LARGE_CITY:
                        localBenefit += 15;
                        break;
                    case NORMAL_CITY:
                    case PORT:
                        localBenefit += 10;
                        break;
                    default:
                        break;
                }
                localBenefit += 6 * neighbour.GetCityLevel() + 8 * neighbour.GetIndustryLevel();
                // update area and card choice
                if (normalAirAtkBenefit < localBenefit)
                {
                    CardDef card = CObjectDef.Instance().GetCardDef(curSuitableCardId);
                    // willing and able to air strike / bomb
                    if (isBuyCard(card) && curCountry.CheckCardTargetArea(card, neighbourId))
                    {
                        normalAirAtkBenefit = localBenefit;
                        airTargetAreaId = neighbourId;
                        airCardId = curSuitableCardId;
                    }
                }
            }
        }
        else if (choice > 7)
        {
            // nuclear bomb
            if (nuclearCard == null || !isBuyCard(nuclearCard)) return false;
            int nuclearBombBenefit = -1;
            foreach (int areaId in curCountry.areaIdList)
            {
                int neighbourArmiesHp = getNeighbor(areaId, 2030, 1, 1);
                int neighbourId = getNeighbourId[0];
                if (neighbourId == -1) continue;

                CArea neighbour = g_Scene.GetArea(neighbourId);
                if (neighbour == null) return false;
                int localBenefit;
                // at least 300 hp required; sea units are not special cased here
                if (neighbourArmiesHp >= 300)
                {
                    // area weights like the air strike, capital and large city weigh more
                    switch (neighbour.info.areaType)
                    {
                        case CAPITAL:
                            localBenefit = 80;
                            break;
                        case LARGE_CITY:
                            localBenefit = 75;
                            break;
                        case NORMAL_CITY:
                            localBenefit = 70;
                            break;
                        default:
                            localBenefit = neighbour.info.areaType == PORT ? 70 : 60;
                            break;
                    }
                    localBenefit += 6 * neighbour.GetCityLevel() + 8 * neighbour.GetIndustryLevel();
                    if (nuclearBombBenefit < localBenefit)
                    {
                        CardDef card = nuclearCard;
                        if (isBuyCard(card) && curCountry.CheckCardTargetArea(card, neighbourId))
                        {
                            nuclearBombBenefit = localBenefit;
                            airTargetAreaId = neighbourId;
                            airCardId = nuclearCard.id;
                        }
                    }
                }
            }
        }
        else
        {
            // airborne
            if (airborneCard == null || !isBuyCard(airborneCard)) return false;
            int airbourneBenefit = -1;
            foreach (int areaId in curCountry.areaIdList)
            {
                // empty areas adjacent to the country
                getNeighbor(areaId, 2050, 1, 1);
                int neighbourId = getNeighbourId[0];
                // no such area
                if (neighbourId == -1) continue;
                CArea neighbour = g_Scene.GetArea(neighbourId);
                if (neighbour == null) return false;
                if (neighbour.info.sea) continue;
                int localBenefit;
                switch (neighbour.info.areaType)
                {
                    case CAPITAL:
                        localBenefit = 360;
                        break;
                    case LARGE_CITY:
                        localBenefit = 340;
                        break;
                    case NORMAL_CITY:
                        localBenefit = 310;
                        break;
                    default:
                        localBenefit = neighbour.info.areaType == PORT ? 280 : 260;
                        break;
                }
                localBenefit += 40 * neighbour.GetCityLevel() + 80 * neighbour.GetIndustryLevel();
                if (airbourneBenefit < localBenefit)
                {
                    CardDef card = airborneCard;
                    if (isBuyCard(card) && curCountry.CheckCardTargetArea(card, neighbourId))
                    {
                        airTargetAreaId = neighbourId;
                        airbourneBenefit = localBenefit;
                        airCardId = airborneCard.id;
                    }
                }
            }
        }

        if (airCardId == INVALID_CARD) return false;
        if (airTargetAreaId == -1) return false;
        CardDef airCard = CObjectDef.Instance().GetCardDef(airCardId);
        if (airCard == null) return false;

        actionNode.cardId = airCardId;
        actionNode.actionType = ActionNode.CARD_NODE;
        actionNode.startAreaId = airTargetAreaId;
        actionNode.targetAreaId = airTargetAreaId;
        actionNode.armyIndex = 0;
        return true;
    }

    /// <summary>
    /// Online strategy deciding assault / defence tactics, commander and carrier. Custom unit
    /// enhancement cards can get their AI logic here.
    /// </summary>
    /// <returns>whether a unit enhancement card is used</returns>
    public bool purChaseCardsCard()
    {
        CARD_ID selectedCardId = INVALID_CARD;
        CCountry curCountry = CActionAI.Instance().currentCountry;
        int randomBuff = GetAIRand() % 50;
        int globalBenefit = -1;
        int tgtAreaId = -1;
        foreach (int areaId in curCountry.areaIdList)
        {
            CArea area = g_Scene.GetArea(areaId);
            if (area.armyCount == 0) continue;
            int overallNeighbourVal = getNeighbor(areaId, 2010, 1, 1);
            int areaArmyHp = calcAreaArmyHp(area, true);
            int seaBuffedNeighbourVal;
            if (!area.info.sea)
            {
                // land area: count the sea neighbours, gives the carrier its value
                seaBuffedNeighbourVal = getNeighbor(areaId, 2000, 0, 1);
            }
            else
            {
                seaBuffedNeighbourVal = 0;
            }

            // we are strong, enemies weak: assault tactics
            int atkBenefit = areaArmyHp - overallNeighbourVal;
            if (randomBuff <= 14)
            {
                // raise the assault tendency
                atkBenefit += 50;
            }
            CardDef assaultCard = FindBuffCard(curCountry, UnitBuffKind.ASSAULT);
            if (atkBenefit > globalBenefit && assaultCard != null && isBuyCard(assaultCard))
            {
                if (curCountry.CheckCardTargetArmy(assaultCard, areaId, 0))
                {
                    globalBenefit = atkBenefit;
                    tgtAreaId = areaId;
                    selectedCardId = assaultCard.id;
                }
            }

            // enemies strong, we are weak: defence tactics
            int defendBenefit = overallNeighbourVal - areaArmyHp;
            if (randomBuff >= 15 && randomBuff <= 29)
            {
                // raise the defence tendency (original bands are disjoint: 0-14 / 15-29 / 30-49)
                defendBenefit += 50;
            }
            CardDef defenceCard = FindBuffCard(curCountry, UnitBuffKind.DEFENCE);
            if (defendBenefit > globalBenefit && defenceCard != null && isBuyCard(defenceCard))
            {
                if (curCountry.CheckCardTargetArmy(defenceCard, areaId, 0))
                {
                    globalBenefit = defendBenefit;
                    tgtAreaId = areaId;
                    selectedCardId = defenceCard.id;
                }
            }

            int carrierBenefit = 80 * seaBuffedNeighbourVal;
            // high carrier tendency
            if (randomBuff >= 30 && randomBuff <= 49)
                carrierBenefit += 3 * totalSeaAreaCount;
            CardDef carrierCard = FindBuffCard(curCountry, UnitBuffKind.CARRIER);
            if (carrierBenefit > globalBenefit && carrierCard != null && isBuyCard(carrierCard))
            {
                if (curCountry.CheckCardTargetArmy(carrierCard, areaId, 0))
                {
                    globalBenefit = carrierBenefit;
                    tgtAreaId = areaId;
                    selectedCardId = carrierCard.id;
                }
            }
            // no random bonus for the commander; more likely on land
            int cmdBenefit = areaArmyHp;
            if (!area.info.sea)
                cmdBenefit *= 3;
            CardDef commanderCard = FindBuffCard(curCountry, UnitBuffKind.COMMANDER);
            if (cmdBenefit > globalBenefit && commanderCard != null && isBuyCard(commanderCard))
            {
                if (curCountry.CheckCardTargetArmy(commanderCard, areaId, 0))
                {
                    globalBenefit = cmdBenefit;
                    tgtAreaId = areaId;
                    selectedCardId = commanderCard.id;
                }
            }
        }
        if (selectedCardId == INVALID_CARD) return false;
        CardDef troop = CObjectDef.Instance().GetCardDef(selectedCardId);
        if (troop == null) return false;
        actionNode.cardId = selectedCardId;
        actionNode.actionType = ActionNode.CARD_NODE;
        actionNode.startAreaId = tgtAreaId;
        actionNode.targetAreaId = tgtAreaId;
        actionNode.armyIndex = 0;
        return true;
    }

    /// <summary>
    /// Random willingness to buy a card, graded by economy versus card price.
    /// </summary>
    /// <param name="card">card definition</param>
    /// <returns>whether the AI wants to buy the card</returns>
    public bool isBuyCard(CardDef card)
    {
        int choice = GetAIRand() % 100;
        int price = card.price;
        int industry = card.industry;
        // not enough money: no
        if (g_NationInfo.money <= price || g_NationInfo.industry <= industry)
            return false;
        // plenty of money: yes
        if (g_NationInfo.money > 4 * price && g_NationInfo.industry > 4 * industry)
            return true;
        int standard;
        if (g_NationInfo.money <= 2 * price || g_NationInfo.industry <= 2 * industry)
        {
            standard = 70;
        }
        else if (g_NationInfo.money <= 3 * price || g_NationInfo.industry <= 3 * industry)
        {
            standard = 45;
        }
        else
        {
            standard = 20;
        }
        return choice > standard;
    }
}
