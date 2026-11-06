namespace WC2;

/// <summary>Card purchase decisions of the AI (CActionAssistBuyCard.cpp).</summary>
public sealed partial class CActionAssist
{
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
                if (!Terrain.Area(curArea.info.areaType).port)
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
            bool basicConstructionCheck;
            if (navy)
            {
                basicConstructionCheck =
                    curCountry.CheckCardTargetArea(CObjectDef.Instance().GetCardDef(DESTROYER_CARD), areaId);
            }
            else
            {
                basicConstructionCheck =
                    curCountry.CheckCardTargetArea(CObjectDef.Instance().GetCardDef(INFANTRY_CARD), areaId) ||
                    curCountry.CheckCardTargetArea(CObjectDef.Instance().GetCardDef(ARTILLERY_CARD), areaId);
            }
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
        List<CARD_ID> candidates = CObjectDef.Instance().GetTroopCardList(curCountry.name, navy);
        if (candidates == null) return (int)INVALID_CARD;

        // descending walk
        foreach (CARD_ID cardId in candidates)
        {
            if ((int)cardId < 0 || (int)cardId >= VALID_CARD_COUNT) continue;
            // explicitly unusable card
            if (!g_NationInfo.canUseCard[(int)cardId]) continue;

            CardDef card = CObjectDef.Instance().GetCardDef(cardId, curCountry);
            if (card == null) continue;
            // the AI wants to build and the area allows it
            if (isBuyCard(card) && curCountry.CheckCardTargetArea(card, targetAreaId)) return (int)cardId;
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
        CARD_ID selectedFortCardId;
        int choice = GetAIRand() % 100;
        // choose first so that even the anti-aircraft gun gets a chance
        if (choice <= 29)
        {
            selectedFortCardId = ENTRENCHMENT_CARD;
        }
        else if (choice <= 59)
        {
            selectedFortCardId = LAND_FORT_CARD;
        }
        else if (choice >= 80)
        {
            selectedFortCardId = RADAR_CARD;
        }
        else
        {
            selectedFortCardId = ANTIAIRCRAFT_CARD;
        }
        int globalBenifit = -1;
        int cardUsingAreaId = -1;
        CCountry curCountry = CActionAI.Instance().currentCountry;
        foreach (int areaId in curCountry.areaIdList)
        {
            CArea area = g_Scene.GetArea(areaId);
            if (area.installationType != NO_INSTALLATION) continue;
            if (area.info.sea) continue;
            int areaArmyCnt = area.armyCount;
            if (areaArmyCnt == 0) continue;

            int benefit = 4 * area.GetCityLevel() + 10 * areaArmyCnt + 5 * area.GetIndustryLevel();
            switch (selectedFortCardId)
            {
                case ENTRENCHMENT_CARD:
                    // 2010: hp sum of the surrounding enemies, hence the division
                    // (could be refined, e.g. more value against non-tank units)
                    benefit += getNeighbor(areaId, 2010, 1, 1) / 10;
                    break;
                case LAND_FORT_CARD:
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
                case ANTIAIRCRAFT_CARD:
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
                CardDef cardDef = CObjectDef.Instance().GetCardDef(selectedFortCardId);
                if (isBuyCard(cardDef) &&
                    curCountry.CheckCardTargetArea(cardDef, areaId))
                {
                    globalBenifit = benefit;
                    cardUsingAreaId = areaId;
                }
            }
        }

        if (cardUsingAreaId == -1) return false;
        CardDef tgtCard = CObjectDef.Instance().GetCardDef(selectedFortCardId);
        if (tgtCard == null) return false;
        actionNode.cardId = selectedFortCardId;
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
        CARD_ID selectedConstructionCardId;
        if (choice <= 54)
        {
            selectedConstructionCardId = CITY_CARD;
        }
        else if (choice >= 90)
        {
            selectedConstructionCardId = AIRPORT_CARD;
        }
        else
        {
            selectedConstructionCardId = INDUSTRY_CARD;
        }
        if (!g_NationInfo.canUseCard[(int)selectedConstructionCardId]) return false;

        int globalBenefit = -1;
        int selectedAreaId = -1;
        foreach (int areaId in curCountry.areaIdList)
        {
            CArea area = g_Scene.GetArea(areaId);
            if (area.info.sea) continue;
            int localBenefit = 0;
            if (selectedConstructionCardId == AIRPORT_CARD)
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
            else if (selectedConstructionCardId == INDUSTRY_CARD)
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
            int coe = selectedConstructionCardId == AIRPORT_CARD ? -1 : 1;
            // capital 30, large city 20, normal city 10 (terrain: ai.construction)
            localBenefit += Terrain.Area(area.info.areaType).aiConstructionValue * coe;
            if (globalBenefit < localBenefit)
            {
                CardDef carddef = CObjectDef.Instance().GetCardDef(selectedConstructionCardId);

                if (isBuyCard(carddef) && curCountry.CheckCardTargetArea(carddef, areaId))
                {
                    globalBenefit = localBenefit;
                    selectedAreaId = areaId;
                }
            }
        }

        if (selectedAreaId == -1) return false;
        CardDef constructionCard = CObjectDef.Instance().GetCardDef(selectedConstructionCardId);
        if (constructionCard == null) return false;
        actionNode.cardId = selectedConstructionCardId;
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
        if (choice <= 5)
        {
            // air strike / bombing: 60%
            bool cannotAirStrike = !g_NationInfo.canUseCard[(int)AIR_STRIKE_CARD];
            bool cannotBomb = !g_NationInfo.canUseCard[(int)BOMBER_CARD];
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
                    curSuitableCardId = AIR_STRIKE_CARD;
                }
                else if (cannotBomb)
                {
                    // original: when the bomber is unavailable the target is still worth an air strike
                    if (cannotAirStrike) continue;
                    localBenefit = 50;
                    curSuitableCardId = AIR_STRIKE_CARD;
                }
                else
                {
                    // otherwise bombing
                    localBenefit = 60;
                    curSuitableCardId = BOMBER_CARD;
                }
                // anti-aircraft guns remove the benefit
                // (terrain: installation ai.airStrikePenalty, area ai.airStrike, rules.ai level weights)
                localBenefit -= Terrain.Installation(neighbour.installationType).aiAirStrikePenalty;
                localBenefit += Terrain.Area(neighbour.info.areaType).aiAirStrikeValue;
                localBenefit += Terrain.Rules.aiAirStrikeCityLevelWeight * neighbour.GetCityLevel()
                              + Terrain.Rules.aiAirStrikeIndustryLevelWeight * neighbour.GetIndustryLevel();
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
            if (!g_NationInfo.canUseCard[(int)NUCLEAR_BOMB_CARD]) return false;
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
                    // capital 80, large city 75, normal city / port 70, other 60 (terrain: ai.nuclear)
                    localBenefit = Terrain.Area(neighbour.info.areaType).aiNuclearValue;
                    localBenefit += Terrain.Rules.aiAirStrikeCityLevelWeight * neighbour.GetCityLevel()
                                  + Terrain.Rules.aiAirStrikeIndustryLevelWeight * neighbour.GetIndustryLevel();
                    if (nuclearBombBenefit < localBenefit)
                    {
                        CardDef card = CObjectDef.Instance().GetCardDef(NUCLEAR_BOMB_CARD);
                        if (isBuyCard(card) && curCountry.CheckCardTargetArea(card, neighbourId))
                        {
                            nuclearBombBenefit = localBenefit;
                            airTargetAreaId = neighbourId;
                            airCardId = NUCLEAR_BOMB_CARD;
                        }
                    }
                }
            }
        }
        else
        {
            // airborne
            if (!g_NationInfo.canUseCard[(int)AIRBORNE_FORCE_CARD]) return false;
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
                // capital 360, large city 340, normal city 310, port 280, other 260 (terrain: ai.airborne)
                localBenefit = Terrain.Area(neighbour.info.areaType).aiAirborneValue;
                localBenefit += Terrain.Rules.aiAirborneCityLevelWeight * neighbour.GetCityLevel()
                              + Terrain.Rules.aiAirborneIndustryLevelWeight * neighbour.GetIndustryLevel();
                if (airbourneBenefit < localBenefit)
                {
                    CardDef card = CObjectDef.Instance().GetCardDef(AIRBORNE_FORCE_CARD);
                    if (isBuyCard(card) && curCountry.CheckCardTargetArea(card, neighbourId))
                    {
                        airTargetAreaId = neighbourId;
                        airbourneBenefit = localBenefit;
                        airCardId = AIRBORNE_FORCE_CARD;
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
            if (atkBenefit > globalBenefit && g_NationInfo.canUseCard[(int)ASSAULT_ART_CARD])
            {
                CardDef card = CObjectDef.Instance().GetCardDef(ASSAULT_ART_CARD);
                if (isBuyCard(card) && curCountry.CheckCardTargetArmy(card, areaId, 0))
                {
                    globalBenefit = atkBenefit;
                    tgtAreaId = areaId;
                    selectedCardId = ASSAULT_ART_CARD;
                }
            }

            // enemies strong, we are weak: defence tactics
            int defendBenefit = overallNeighbourVal - areaArmyHp;
            if (randomBuff >= 15 && randomBuff <= 29)
            {
                // raise the defence tendency (original bands are disjoint: 0-14 / 15-29 / 30-49)
                defendBenefit += 50;
            }
            if (defendBenefit > globalBenefit && g_NationInfo.canUseCard[(int)DEFEND_ART_CARD])
            {
                CardDef card = CObjectDef.Instance().GetCardDef(DEFEND_ART_CARD);
                if (isBuyCard(card) && curCountry.CheckCardTargetArmy(card, areaId, 0))
                {
                    globalBenefit = defendBenefit;
                    tgtAreaId = areaId;
                    selectedCardId = DEFEND_ART_CARD;
                }
            }

            int carrierBenefit = 80 * seaBuffedNeighbourVal;
            // high carrier tendency
            if (randomBuff >= 30 && randomBuff <= 49)
                carrierBenefit += 3 * totalSeaAreaCount;
            if (carrierBenefit > globalBenefit && g_NationInfo.canUseCard[(int)CARRIER_CARD])
            {
                CardDef card = CObjectDef.Instance().GetCardDef(CARRIER_CARD);
                if (isBuyCard(card) && curCountry.CheckCardTargetArmy(card, areaId, 0))
                {
                    globalBenefit = carrierBenefit;
                    tgtAreaId = areaId;
                    selectedCardId = CARRIER_CARD;
                }
            }
            // no random bonus for the commander; more likely on land
            int cmdBenefit = areaArmyHp;
            if (!area.info.sea)
                cmdBenefit *= 3;
            if (cmdBenefit > globalBenefit && g_NationInfo.canUseCard[(int)COMMANDER_CARD])
            {
                CardDef card = CObjectDef.Instance().GetCardDef(COMMANDER_CARD);
                if (isBuyCard(card) && curCountry.CheckCardTargetArmy(card, areaId, 0))
                {
                    globalBenefit = cmdBenefit;
                    tgtAreaId = areaId;
                    selectedCardId = COMMANDER_CARD;
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
        // plenty of money: yes (classic: more than 4x the price; data/json/ai/profiles.json)
        var P = AiProfiles.Current;
        if (g_NationInfo.money > P.cardAlwaysBuyMultiple * price && g_NationInfo.industry > P.cardAlwaysBuyMultiple * industry)
            return true;
        int standard;
        if (g_NationInfo.money <= 2 * price || g_NationInfo.industry <= 2 * industry)
        {
            standard = P.CardRefuse(0);   // classic 70
        }
        else if (g_NationInfo.money <= 3 * price || g_NationInfo.industry <= 3 * industry)
        {
            standard = P.CardRefuse(1);   // classic 45
        }
        else
        {
            standard = P.CardRefuse(2);   // classic 20
        }
        return choice > standard;
    }
}
