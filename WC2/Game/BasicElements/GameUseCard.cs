namespace WC2;

/// <summary>Card usage and card target checks of a country (GameUseCard.cpp).</summary>
public sealed partial class CCountry
{
    /// <summary>Industry price of a card, including XML-defined technology growth.</summary>
    public int GetCardIndustry(CardDef card)
    {
        CardResourceCostDef cost = card.costs.FirstOrDefault(
            item => item.resource == CardResourceType.Industry);
        if (cost != null) return GetScaledResourceCost(cost);
        // fall back
        if (card.id == RESEARCH_CARD) return card.industry * tech;
        return card.industry;
    }

    /// <summary>Whether the industry suffices for the card.</summary>
    public bool IsEnoughIndustry(CardDef card) => GetCardIndustry(card) <= industry;

    /// <summary>Money price of a card, including XML-defined technology growth.</summary>
    public int GetCardPrice(CardDef card)
    {
        CardResourceCostDef cost = card.costs.FirstOrDefault(
            item => item.resource == CardResourceType.Money);
        int cardMoney = cost != null ? GetScaledResourceCost(cost) : card.price;
        if (cost == null && card.id == RESEARCH_CARD) return cardMoney * tech;
        // 指挥官造价目前还不能定制
        if (card.commanderPriceScaling || card.id == COMMANDER_CARD)
            return cardMoney + 5 * GetCommanderLevel();
        return cardMoney;
    }

    // 可变价格
    private int GetScaledResourceCost(CardResourceCostDef cost)
    {
        long value = (long)cost.baseAmount +
            (long)cost.growthPerTechLevel * Math.Max(0, tech - 1);
        return (int)Math.Clamp(value, int.MinValue, int.MaxValue);
    }

    /// <summary>Whether the money suffices for the card.</summary>
    public bool IsEnoughMoney(CardDef card) => GetCardPrice(card) <= money;

    /// <summary>Cool-down of a card.</summary>
    public int GetCardRounds(CARD_ID cardId) => cardRound[(int)cardId];

    /// <summary>Core card usage logic.</summary>
    /// <param name="cardDef">the card</param>
    /// <param name="targetAreaId">target area id</param>
    /// <param name="armyIndex">target unit index in the target area</param>
    public void UseCard(CardDef cardDef, int targetAreaId, int armyIndex)
    {
        if (cardDef == null) return;
        int CardPrice = GetCardPrice(cardDef);
        int CardIndustry = GetCardIndustry(cardDef);
        // only when affordable
        if (CardPrice > money || CardIndustry > industry) return;
        // locked card
        if (!IsCardUnlock(cardDef)) return;
        // card still cooling down
        bool isResearch = cardDef.effects.OfType<ResearchEffectDef>().Any();
        if (!isResearch && GetCardRounds(cardDef.id) > 0) return;

        // target area or null
        CArea targetArea = targetAreaId >= 0 ? g_Scene.GetArea(targetAreaId) : null;
        if (cardDef.type == ARMY_TYPE_CARD || cardDef.type == NAVY_TYPE_CARD)
        {
            if (targetArea == null) return;
            CArmy pArmy;
            if (cardDef.effects.Count > 0)
            {
                CardEffectResult result = CardEffectExecutor.Apply(
                    CreateCardEffectContext(cardDef, targetAreaId, armyIndex));
                if (!result.Success) return;
                pArmy = targetArea.armyDrafting;
            }
            else
            {
                if (!CheckCardTargetArea(cardDef, targetAreaId)) return;
                pArmy = targetArea.DraftArmy(cardDef.relatedArmyId);
            }
            if (pArmy == null) return;
            string draftedArmyType = pArmy.basicAbilities.armyType;
            // navy medal bonus
            int NavyMedalLevel = GetWarMedalLevel(NAVY_MEDAL);
            if (NavyMedalLevel > 0)
            {
                // land units get a transport ship
                if (cardDef.type == ARMY_TYPE_CARD) pArmy.AddCard(CARRIER);
                if ((NavyMedalLevel > 1 &&
                     (draftedArmyType == "destroyer" || draftedArmyType == "cruiser")) ||
                    (NavyMedalLevel > 2 &&
                     (draftedArmyType == "battleship" || draftedArmyType == "aircraftcarrier")))
                    pArmy.Upgrade();
            }

            // infantry medal bonus
            if (draftedArmyType == "infantry")
            {
                int InfantryMedalLevel = GetWarMedalLevel(INFANTRY_MEDAL);
                if (InfantryMedalLevel > 0)
                {
                    pArmy.Upgrade();
                    if (InfantryMedalLevel > 1)
                    {
                        pArmy.Upgrade();
                        if (InfantryMedalLevel > 2)
                        {
                            pArmy.AddCard(ASSAULT);
                            pArmy.AddCard(DEFENCE);
                        }
                    }
                }
            }

            // artillery medal bonus
            int ArtilleryMedalLevel = GetWarMedalLevel(ARTILLERY_MEDAL);
            if (ArtilleryMedalLevel > 0)
            {
                if (draftedArmyType == "artillery") pArmy.Upgrade();
                if (ArtilleryMedalLevel > 1)
                {
                    if (draftedArmyType == "rocket") pArmy.Upgrade();
                    if (ArtilleryMedalLevel > 2 &&
                        (draftedArmyType == "artillery" || draftedArmyType == "rocket"))
                        pArmy.AddCard(DEFENCE);
                }
            }

            // armour medal bonus
            int ArmourMedalLevel = GetWarMedalLevel(ARMOUR_MEDAL);
            if (ArmourMedalLevel > 0)
            {
                if (draftedArmyType == "panzer") pArmy.Upgrade();
                if (ArmourMedalLevel > 1)
                {
                    if (draftedArmyType == "tank") pArmy.Upgrade();
                    if (ArmourMedalLevel > 2 && draftedArmyType == "heavytank") pArmy.Upgrade();
                }
            }
        }
        else if (cardDef.type == DEVELOPMENT_TYPE_CARD)
        {
            if (!CheckCardTargetArea(cardDef, targetAreaId)) return;
            if (targetArea == null) return;
            if (cardDef.effects.Count > 0)
            {
                CardEffectResult result = CardEffectExecutor.Apply(CreateCardEffectContext(cardDef, targetAreaId, armyIndex));
                if (!result.Success) return;
            }
            else
            {
                switch (cardDef.id)
                {
                    case CITY_CARD:
                        targetArea.Construct((int)CITY);
                        break;
                    case INDUSTRY_CARD:
                        targetArea.Construct((int)INDUSTRY);
                        break;
                    case AIRPORT_CARD:
                        targetArea.Construct((int)AIRPORT);
                        break;
                    case LAND_FORT_CARD:
                        targetArea.installationType = FORT;
                        break;
                    case ENTRENCHMENT_CARD:
                        targetArea.installationType = ENTRENCHMENT;
                        break;
                    case ANTIAIRCRAFT_CARD:
                        targetArea.installationType = ANTIAIRCRAFT;
                        break;
                    case RADAR_CARD:
                        targetArea.installationType = RADAR;
                        break;
                    default:
                        return;
                }
            }
            if (cardDef.presentation?.sound == "build" || cardDef.effects.Count == 0)
                g_SoundRes.PlayCharSE(BUILD_WAV);
        }
        else if (cardDef.type == STRATEGY_TYPE_CARD)
        {
            // strategy cards
            if (cardDef.effects.Count > 0)
            {
                CardEffectResult result = CardEffectExecutor.Apply(
                    CreateCardEffectContext(cardDef, targetAreaId, armyIndex));
                if (!result.Success) return;
                switch (cardDef.presentation?.sound)
                {
                    case "buff": g_SoundRes.PlayCharSE(BUFF_WAV); break;
                    case "supply": g_SoundRes.PlayCharSE(SUPPLY_WAV); break;
                    case "level-up": g_SoundRes.PlayCharSE(LV_UP_WAV); break;
                }
            }
            else if (cardDef.id == RESEARCH_CARD)
            {
                if (tech > 4 || techTurn > 0) return;
                g_SoundRes.PlayCharSE(LV_UP_WAV);
                // research duration
                techTurn = 3;
            }
            else if (cardDef.id == COMMANDER_CARD)
            {
                if (!CheckCardTargetArmy(cardDef, targetAreaId, armyIndex)) return;
                if (targetArea == null) return;
                commanderAlive = true;
                targetArea.AddArmyCard(armyIndex, (int)COMMANDER);
                CArmy targetArmy = targetArea.army[armyIndex];
                CommanderAbility Ability = GetCommanderAbility(GetCommanderLevel());
                if (targetArmy == null) return;
                g_SoundRes.PlayCharSE(BUFF_WAV);

                // the commander heals instantly
                targetArmy.AddStrength(Ability.armyAbility.instantRecovery);
                targetArmy.ResetMaxStrength(false);

                // an AI commander talks when used
                if (ai && g_GameManager.gameMode != CGameManager.MULTIPLAYER)
                {
                    string CommanderName = GetCommanderName();
                    if (CommanderName != null)
                    {
                        string s = Sprintf("commander return %ld", (long)(GetRand() % 5 + 1));
                        var GameState = (CGameState)CStateManager.Instance().GetStatePtr(GAME);
                        GameState.ShowDialogue(s, CommanderName, false);
                    }
                }
            }
            else if (cardDef.id == ACE_FORCES_CARD)
            {
                // ace forces
                if (targetArea == null) return;
                CArmy army = targetArea.GetArmy(0);
                if (army == null) return;
                army.Upgrade();
                g_SoundRes.PlayCharSE(LV_UP_WAV);
            }
            else if (cardDef.id == SUPPLY_LINE_CARD)
            {
                // supply line: full heal
                if (targetArea == null) return;
                g_SoundRes.PlayCharSE(SUPPLY_WAV);
                for (int i = 0; i < targetArea.armyCount; i++)
                {
                    targetArea.RevertArmyStrength(i);
                }
            }
            else if (cardDef.id == CARRIER_CARD || cardDef.id == ASSAULT_ART_CARD ||
                     cardDef.id == DEFEND_ART_CARD)
            {
                if (!CheckCardTargetArmy(cardDef, targetAreaId, armyIndex)) return;
                // transport ship, assault tactic, defence tactic
                if (targetArea == null) return;
                switch (cardDef.id)
                {
                    case CARRIER_CARD:
                        targetArea.AddArmyCard(armyIndex, (int)CARRIER);
                        break;
                    case ASSAULT_ART_CARD:
                        targetArea.AddArmyCard(armyIndex, (int)ASSAULT);
                        break;
                    case DEFEND_ART_CARD:
                        targetArea.AddArmyCard(armyIndex, (int)DEFENCE);
                        break;
                    default:
                        return;
                }
                g_SoundRes.PlayCharSE(BUFF_WAV);
            }
        }
        else if (cardDef.type == AIR_FORCE_TYPE_CARD && GetCardRounds(cardDef.id) == 0)
        {
            if (targetArea == null) return;
            if (!CheckCardTargetArea(cardDef, targetAreaId)) return;
            if (cardDef.effects.Count > 0)
            {
                CardEffectResult result = CardEffectExecutor.Apply(
                    CreateCardEffectContext(cardDef, targetAreaId, armyIndex));
                if (!result.Success) return;
            }
            else if (cardDef.id == AIRBORNE_FORCE_CARD)
            {
                // airborne card
                CCountry targetCountry = targetArea.country;
                // another owner: occupy the area
                if (targetCountry != this)
                {
                    if (targetCountry != null)
                    {
                        targetCountry.RemoveArea(targetAreaId);
                    }
                    AddArea(targetAreaId);
                    targetArea.country = this;
                    // conquered?
                    if (targetCountry != null && targetCountry.IsConquested())
                        targetCountry.BeConquestedBy(this);
                }
                targetArea.DraftArmy((int)INFANTRY);
            }
            // air force medal > 2 allows unlimited air force cards (except airborne), see postCardUsage
        }
        postCardUsage(cardDef, CardPrice, CardIndustry);
    }

    /// <summary>The common bookkeeping after a card was used.</summary>
    public void postCardUsage(CardDef cardDef, int cardPrice, int cardIndustry)
    {
        // Use the price captured before applying effects; immediate research may change tech.
        money -= cardPrice;
        industry -= cardIndustry;

        // usage count of this turn
        cardTimes[(int)cardDef.id]++;
        if (cardDef.effects.OfType<ResearchEffectDef>().Any()) return;
        // the commander cool-down is driven by commanderTurn, not by the card use
        if (cardDef.commanderCooldown || cardDef.id == COMMANDER_CARD) return;

        // cool-down update
        int newRound = cardDef.round;

        // a card with a usage limit needs an extra cool-down computation
        if (cardDef.maxTimes > 0)
        {
            if (cardTimes[(int)cardDef.id] >= cardDef.maxTimes)
            {
                // uses exhausted: at least one turn of cool-down even without a configured one
                newRound = newRound > 0 ? newRound : 1;
            }
            else
            {
                // no cool-down
                newRound = 0;
            }
        }

        // non airborne air force cards: free of cool-down with a high air force medal
        bool isAirborneDeploy = cardDef.effects.OfType<AirborneDeployEffectDef>().Any();
        if (cardDef.type == AIR_FORCE_TYPE_CARD &&
            !isAirborneDeploy && cardDef.id != AIRBORNE_FORCE_CARD)
        {
            newRound = GetWarMedalLevel(AIR_FORCE_MEDAL) > 2 ? 0 : newRound;
        }

        cardRound[(int)cardDef.id] = newRound;
    }

    /// <summary>Whether the card is unlocked by the country tech level.</summary>
    public bool IsCardUnlock(CardDef card) => card.tech <= tech;

    /// <summary>Whether the card can be used on the area.</summary>
    public bool CheckCardTargetArea(CardDef cardDef, int areaId)
    {
        CArea area = g_Scene.GetArea(areaId);
        if (area == null) return false;

        if (!area.info.enable) return false;

        if (!IsCardUnlock(cardDef)) return false;

        int card_ID = (int)cardDef.id;
        if (cardDef.type == NAVY_TYPE_CARD)
        {
            if (cardDef.effects.Count > 0)
                return CardEffectExecutor.CanApply(
                    CreateCardEffectContext(cardDef, areaId, -1), out _);
            // navy: own port at sea (the "not sea" case is reserved for land ships, if ever)
            // fall back检查——适用于没有应用card effect的版本
            if (area.country != this || area.info.areaType != PORT || !area.info.sea)
            {
                return false;
            }
            return area.armyCount < AREA_NAVY_LIMIT; // navy capacity of the area
        }

        if (cardDef.type == AIR_FORCE_TYPE_CARD)
        {
            if (cardDef.effects.Count > 0)
                return CardEffectExecutor.CanApply(
                    CreateCardEffectContext(cardDef, areaId, -1), out _);
            // air force: an airport is required
            // fall back检查——适用于没有应用card effect的版本
            if (GetNumAirport() <= 0)
            {
                return false;
            }
            if ((card_ID >= (int)AIR_STRIKE_CARD && card_ID <= (int)BOMBER_CARD) ||
                (card_ID == (int)NUCLEAR_BOMB_CARD))
            {
                // not airborne
                if (area.country == this) return false;
                if (area.armyCount <= 0) return false;
                // the target must be within the radius and not at zero distance
                float dstToAirport = GetMinDstToAirport(areaId);
                if (dstToAirport > 0)
                {
                    return dstToAirport < AirstrikeRadius();
                }
                return false;
            }
            else if (card_ID == (int)AIRBORNE_FORCE_CARD)
            {
                // no airborne landing at sea
                if (area.info.sea) return false;
                float dstToAirport = GetMinDstToAirport(areaId);
                if (dstToAirport > 0)
                {
                    if (dstToAirport < AirstrikeRadius())
                    {
                        if (this == area.country) return area.armyCount < 4;
                        return area.armyCount == 0;
                    }
                }
                return false;
            }
        }
        if (cardDef.type == DEVELOPMENT_TYPE_CARD)
        {
            if (cardDef.effects.Count > 0)
            {
                return CardEffectExecutor.CanApply(
                    CreateCardEffectContext(cardDef, areaId, -1), out _);
            }

            // development: own land only
            if (area.country != this) return false;

            if (area.info.sea) return false;

            if (card_ID >= (int)LAND_FORT_CARD && card_ID <= (int)RADAR_CARD)
            {
                return area.installationType == NO_INSTALLATION;
            }

            if (card_ID >= (int)CITY_CARD && card_ID <= (int)AIRPORT_CARD)
            {
                return area.CanConstruct(card_ID - (int)CITY_CARD + 1);
            }
            return false;
        }

        if (cardDef.type == STRATEGY_TYPE_CARD)
        {
            // strategy
            if (cardDef.effects.Count > 0)
            {
                // Area target arrows follow the top unit. A target="selected" card validates the
                // actual sidebar selection separately in CheckCardTargetArmy / UseCard.
                return CardEffectExecutor.CanApply(
                    CreateCardEffectContext(cardDef, areaId, 0), out _);
            }
            if (card_ID == (int)RESEARCH_CARD)
            {
                return false;
            }
            if (card_ID >= (int)CARRIER_CARD && card_ID <= (int)COMMANDER_CARD)
            {
                // besides CheckCardTargetArmy a check here is useful too
                if (area.country != this || area.armyCount <= 0)
                {
                    // no unit or not ours
                    return false;
                }
                int specialCardType;
                if (card_ID == (int)CARRIER_CARD)
                {
                    CArmy army_top = area.GetArmy(0);
                    if (army_top.IsNavy())
                    {
                        // a navy unit cannot take a transport ship
                        return false;
                    }
                    specialCardType = 2;
                }
                else if (card_ID == (int)ASSAULT_ART_CARD)
                {
                    specialCardType = 0;
                }
                else if (card_ID == (int)DEFEND_ART_CARD)
                {
                    specialCardType = 1;
                }
                else if (card_ID == (int)COMMANDER_CARD)
                {
                    specialCardType = 3;
                }
                else
                {
                    // other cards, to be added
                    return false;
                }
                return !area.HasArmyCard(0, specialCardType);
            }
            else if (card_ID == (int)SUPPLY_LINE_CARD)
            {
                // any unit below full health
                if (area.country == this && area.armyCount > 0)
                {
                    for (int i = 0; i < area.armyCount; i++)
                    {
                        CArmy army_tmp = area.GetArmy(i);
                        if (army_tmp.hp < army_tmp.GetMaxStrength()) return true;
                    }
                }
            }
            else if (card_ID == (int)ACE_FORCES_CARD)
            {
                // usable below max level
                if (area.country == this && area.armyCount > 0)
                {
                    return area.GetArmy(0).level < 4;
                }
            }
            return false;
        }

        if (cardDef.type == ARMY_TYPE_CARD)
        {
            if (cardDef.effects.Count > 0)
                return CardEffectExecutor.CanApply(
                    CreateCardEffectContext(cardDef, areaId, -1), out _);
            // fall back到传统的招募检查
            if (area.armyCount >= AREA_RENDER_ARMY_LIMIT) return false;
            if (area.info.sea) return false;
            if (area.country != this) return false;
            return CountryTroopSummonConditionCheck(cardDef, areaId);
        }

        return false;
    }

    private CardEffectContext CreateCardEffectContext(CardDef card, int areaId, int armyIndex) => new()
    {
        sourceCountry = this,
        card = card,
        targetArea = areaId >= 0 ? g_Scene.GetArea(areaId) : null,
        targetAreaId = areaId,
        targetArmyIndex = armyIndex,
    };

    /// <summary>Whether a unit enhancement card can be used on the unit.</summary>
    /// <param name="cardDef">the card</param>
    /// <param name="areaId">area id</param>
    /// <param name="armyIndex">target unit index</param>
    public bool CheckCardTargetArmy(CardDef cardDef, int areaId, int armyIndex)
    {
        CArea pArea = g_Scene.GetArea(areaId);
        if (pArea == null) return false;
        if (!pArea.info.enable) return false;

        CCountry areaOwner = pArea.country;
        if (areaOwner != this) return false;

        CArmy targetArmy = pArea.GetArmy(armyIndex);
        if (targetArmy == null) return false;

        if (cardDef.effects.Count > 0)
        {
            return CardEffectExecutor.CanApply(
                CreateCardEffectContext(cardDef, areaId, armyIndex), out _);
        }

        // fall back处理
        switch (cardDef.id)
        {
            case CARRIER_CARD:
                if (!targetArmy.IsNavy() && (targetArmy.cards & 4) == 0) return true;
                return false;
            case ASSAULT_ART_CARD:
                return (targetArmy.cards & 1) == 0;
            case DEFEND_ART_CARD:
                return (targetArmy.cards & 2) == 0;
            case COMMANDER_CARD:
                return CanUseCommander(cardDef) && (targetArmy.cards & 8) == 0;
            default:
                return false;
        }
    }

    /// <summary>Country specific troop card checks can go here.</summary>
    public bool CountryTroopSummonConditionCheck(CardDef cardDef, int TargetAreaID)
    {
        if (cardDef.type != ARMY_TYPE_CARD) return false;
        return CountryTroopSummonCommonCheck(cardDef, TargetAreaID);
    }

    /// <summary>Fallback check: the common troop card conditions.</summary>
    public static bool CountryTroopSummonCommonCheck(CardDef cardDef, int TargetAreaID)
    {
        CArea area_in = g_Scene.GetArea(TargetAreaID);
        if (cardDef.id == INFANTRY_CARD)
        {
            // infantry
            return area_in.GetCityLevel() > 2;
        }
        else if (cardDef.id == ARMOUR_CARD || cardDef.id == ARTILLERY_CARD)
        {
            // panzer, artillery
            return area_in.GetIndustryLevel() > 0;
        }
        else if (cardDef.id == ROCKET_CARD || cardDef.id == HEAVY_TANK_CARD)
        {
            // rocket, heavy tank
            return area_in.GetIndustryLevel() > 2;
        }
        else if (cardDef.id == TANK_CARD)
        {
            // tank
            return area_in.GetIndustryLevel() > 1;
        }
        else if (cardDef.id == ELITE_INFANTRY_CARD)
        {
            // elite infantry
            return (area_in.GetCityLevel() > 3) || (area_in.GetIndustryLevel() > 0);
        }
        return false;
    }

    public bool CanBuyCard(CardDef Card)
    {
        if (Card == null) return false;
        ResearchEffectDef research = Card.effects.OfType<ResearchEffectDef>().FirstOrDefault();
        // locked card
        if (!IsCardUnlock(Card)
            // or the commander card while the commander is in use
            || ((Card.commanderCooldown || Card.id == COMMANDER_CARD) && !CanUseCommander(Card)))
        {
            return false;
        }
        // the research card at tech > 4 or while researching
        if (research != null)
        {
            if (!CardEffectExecutor.CanApply(new CardEffectContext
                {
                    sourceCountry = this,
                    card = Card,
                    targetAreaId = -1,
                    targetArmyIndex = -1,
                }, out _)) return false;
        }
        else if (Card.id == RESEARCH_CARD)
        {
            if ((tech > 4) || (techTurn > 0)) return false;
        }
        else
        {
            // any other card still cooling down
            if (cardRound[(int)Card.id] > 0) return false;
        }
        return IsEnoughMoney(Card) && IsEnoughIndustry(Card);
    }

    /// <summary>Marks the valid target areas (arrows) of a card.</summary>
    public void SetCardTargets(CardDef card)
    {
        CARD_TYPE cardType = card.type;

        if (cardType == AIR_FORCE_TYPE_CARD)
        {
            // airborne uses the green arrow, air strike and bombing the yellow one
            int targetType = card.effects.OfType<AirborneDeployEffectDef>().Any() ? 1 : 2;
            // Candidates come from the airport radius query; CheckCardTargetArea remains the
            // final card-definition and condition check.
            var candidateIds = new HashSet<int>();
            foreach (int airportAreaId in areaIdList)
            {
                CArea airport = g_Scene.GetArea(airportAreaId);
                if (airport == null || airport.info.constructionType != AIRPORT) continue;
                // the old air strike test is centred on the airport constructionPos while the spatial
                // index buckets by armyPos; the larger candidate radius only avoids missing border
                // targets, the exact distance is re-checked by CheckCardTargetArea
                float offsetX = (float)(airport.info.constructionPos[0] - airport.info.armyPos[0]);
                float offsetY = (float)(airport.info.constructionPos[1] - airport.info.armyPos[1]);
                float candidateRange = AirstrikeRadius() + MathF.Sqrt(offsetX * offsetX + offsetY * offsetY);
                List<int> rangeAreas = g_Scene.GetAreasInCircleRange(airportAreaId, 0.0f, candidateRange, false);
                foreach (int rangeAreaId in rangeAreas) candidateIds.Add(rangeAreaId);
            }
            foreach (int areaId in candidateIds)
            {
                CArea area = g_Scene.GetArea(areaId);
                if (area != null && CheckCardTargetArea(card, areaId))
                {
                    // valid: set the arrow type
                    area.targetType = targetType;
                }
            }
        }
        else
        {
            // every other card iterates the own areas
            foreach (int areaId in areaIdList)
            {
                CArea area = g_Scene.GetArea(areaId);
                if (CheckCardTargetArea(card, areaId))
                {
                    area.targetType = 1;
                }
            }
        }
    }
}
