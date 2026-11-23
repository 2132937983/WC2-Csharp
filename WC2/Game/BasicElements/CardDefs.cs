namespace WC2;

/// <summary>Card definitions loaded from carddef.xml (CardDefs.cpp).</summary>
public sealed partial class CObjectDef
{
    /// <summary>
    /// Fixed order of the five buy-card tabs: land, navy, air force, development, strategy.
    /// This is AllCardTab of GUICardTab.cpp; it lives here because CObjectDef and CCountry need it
    /// before the GUI is ported.
    /// </summary>
    public static readonly List<CARD_ID>[] AllCardTab =
    {
        new() { INFANTRY_CARD, ARMOUR_CARD, ARTILLERY_CARD, ROCKET_CARD, TANK_CARD, HEAVY_TANK_CARD, ELITE_INFANTRY_CARD },
        new() { DESTROYER_CARD, CRUISER_CARD, BATTLE_SHIP_CARD, AIRCRAFT_CARRIER_CARD },
        new() { AIR_STRIKE_CARD, BOMBER_CARD, AIRBORNE_FORCE_CARD, NUCLEAR_BOMB_CARD },
        new() { CITY_CARD, INDUSTRY_CARD, AIRPORT_CARD, LAND_FORT_CARD, ENTRENCHMENT_CARD, ANTIAIRCRAFT_CARD, RADAR_CARD },
        new() { RESEARCH_CARD, CARRIER_CARD, ASSAULT_ART_CARD, DEFEND_ART_CARD, COMMANDER_CARD, SUPPLY_LINE_CARD, ACE_FORCES_CARD },
    };

    private static readonly List<CARD_ID> s_emptyTroopCardList = new();

    // 用于ai用卡的排序
    private struct CardSortInfo
    {
        public CARD_ID id;
        public int price;
        public int industry;
        public int tech;
    }

    /// <summary>
    /// Releases the XML card definitions and the per country card tables.
    /// Every card belongs to the country table that defined it.
    /// </summary>
    public void ReleaseCardDef()
    {
        foreach (var countryEntry in mapNewCardDef)
        {
            ToNewCardDef countryCards = countryEntry.Value;
            if (countryCards == null) continue;
            countryCards.mapCarddef.Clear();
        }
        mapNewCardDef.Clear();
    }

    /// <summary>
    /// Target type used by the card selection GUI.
    /// </summary>
    /// <returns>0 for the research card, 5 for the unit targeted tactic cards, 1 otherwise</returns>
    public int GetCardTargetType(CardDef card)
    {
        if (card.effects.OfType<ResearchEffectDef>().Any()) return 0;
        if (card.effects.OfType<DraftArmyEffectDef>().Any()) return 1;
        if (card.effects.Any(effect => effect is StrategicStrikeEffectDef or AirborneDeployEffectDef)) return 1;
        BuffArmyEffectDef buffArmy = card.effects.OfType<BuffArmyEffectDef>().FirstOrDefault();
        if (buffArmy != null)
            return buffArmy.target == BuffArmyTarget.SELECTED ? 5 : 1;

        CARD_ID cardId = card.id;
        if (cardId == RESEARCH_CARD) return 0;
        if ((int)cardId - (int)RESEARCH_CARD >= 4) return 1;
        return 5;
    }

    /// <summary>Reads the card properties from carddef.xml (see the xml for the format).</summary>
    public void LoadCardDef()
    {
        var Xml = new TiXmlDocument(GetPath("carddef.xml", null));
        ReleaseCardDef();
        if (Xml.LoadFile(TIXML_ENCODING_UNKNOWN))
        {
            TiXmlElement XmlElement = Xml.FirstChildElement("cards");
            if (XmlElement != null)
            {
                for (TiXmlElement ListElement = XmlElement.FirstChildElement();
                     ListElement != null; ListElement = ListElement.NextSiblingElement())
                {
                    // "call" only to keep it apart from the card name; deserves a better name
                    string CountryName = ListElement.Attribute("call");
                    if (CountryName != null)
                    {
                        InitCountryCardDefMap(CountryName);
                        for (TiXmlElement CardListElement = ListElement.FirstChildElement();
                             CardListElement != null;
                             CardListElement = CardListElement.NextSiblingElement())
                        {
                            string CardName = CardListElement.Attribute("name");
                            if (CardName != null)
                            {
                                var tmp_card_def = new CardDef();
                                tmp_card_def.name = CardName;
                                int Id;
                                if (CardListElement.QueryIntAttribute("legacy-id", out Id) != TIXML_SUCCESS &&
                                    CardListElement.QueryIntAttribute("id", out Id) != TIXML_SUCCESS) Id = 0;
                                tmp_card_def.id = (CARD_ID)Id;
                                tmp_card_def.key = CardListElement.Attribute("key") ?? $"legacy-{Id}";
                                string ArmyType = CardListElement.Attribute("type");
                                if (ArmyType != null)
                                {
                                    if (ArmyType == "navy") tmp_card_def.type = NAVY_TYPE_CARD;
                                    else if (ArmyType == "airforce") tmp_card_def.type = AIR_FORCE_TYPE_CARD;
                                    else if (ArmyType == "development") tmp_card_def.type = DEVELOPMENT_TYPE_CARD;
                                    else if (ArmyType == "strategy") tmp_card_def.type = STRATEGY_TYPE_CARD;
                                    else tmp_card_def.type = ARMY_TYPE_CARD;
                                }
                                else
                                {
                                    tmp_card_def.type = ARMY_TYPE_CARD;
                                }
                                int Price, Industry, Round, Tech;
                                int RelatedArmyId, MaxTimes;
                                if (CardListElement.QueryIntAttribute("price", out Price) != TIXML_SUCCESS) Price = 0;
                                if (CardListElement.QueryIntAttribute("industry", out Industry) != TIXML_SUCCESS) Industry = 0;
                                if (CardListElement.QueryIntAttribute("round", out Round) != TIXML_SUCCESS) Round = 0;
                                if (CardListElement.QueryIntAttribute("tech", out Tech) != TIXML_SUCCESS) Tech = 0;
                                // related unit id, unused for now
                                if (CardListElement.QueryIntAttribute("relatedArmyId", out RelatedArmyId) != TIXML_SUCCESS)
                                    RelatedArmyId = (int)tmp_card_def.id;
                                // max uses; better not set until the UI supports it
                                if (CardListElement.QueryIntAttribute("maxTimes", out MaxTimes) != TIXML_SUCCESS) MaxTimes = 0;
                                tmp_card_def.price = Price;
                                tmp_card_def.industry = Industry;
                                tmp_card_def.round = Round;
                                tmp_card_def.tech = Tech;
                                tmp_card_def.relatedArmyId = RelatedArmyId;
                                tmp_card_def.maxTimes = MaxTimes;
                                string priceScaling = CardListElement.Attribute("price-scaling");
                                if (priceScaling != null && priceScaling != "commander-level")
                                    throw CardConfigError(CountryName, tmp_card_def, $"unknown price-scaling '{priceScaling}'");
                                string cooldownMode = CardListElement.Attribute("cooldown-mode");
                                if (cooldownMode != null && cooldownMode != "commander")
                                    throw CardConfigError(CountryName, tmp_card_def, $"unknown cooldown-mode '{cooldownMode}'");
                                tmp_card_def.commanderPriceScaling = priceScaling == "commander-level";
                                tmp_card_def.commanderCooldown = cooldownMode == "commander";
                                string Image = CardListElement.Attribute("image");
                                if (Image != null) tmp_card_def.image = Image;
                                else tmp_card_def.image = "card_infantry.png";
                                string Intro = CardListElement.Attribute("intro");
                                // the original tests Image here (sic); a missing intro falls back to the default text
                                if (Image != null) tmp_card_def.intro = Intro ?? "Infantry Intro";
                                else tmp_card_def.intro = "Infantry Intro";
                                ParseCardCosts(CardListElement, tmp_card_def, CountryName);
                                ParseCardEffects(CardListElement, tmp_card_def, CountryName);
                                string tmp_char = CountryName;
                                AddNewCardDefToMap(tmp_card_def, tmp_char);
                            }
                        }
                    }
                }
            }
        }
        RebuildTroopCardLists();
    }

    // 解析卡牌开销字段
    // 支持配置不同的研发增长开销
    private static void ParseCardCosts(TiXmlElement cardElement, CardDef card, string countryName)
    {
        TiXmlElement costsElement = cardElement.FirstChildElement("costs");
        if (costsElement == null) return;

        var seen = new HashSet<CardResourceType>();
        for (TiXmlElement element = costsElement.FirstChildElement("resource");
             element != null; element = element.NextSiblingElement("resource"))
        {
            CardResourceType resource = element.Attribute("type")?.ToLowerInvariant() switch
            {
                "money" => CardResourceType.Money,
                "industry" => CardResourceType.Industry,
                string value => throw CardConfigError(countryName, card, $"unknown cost resource '{value}'"),
                null => throw CardConfigError(countryName, card, "cost resource requires type"),
            };
            if (!seen.Add(resource))
                throw CardConfigError(countryName, card, $"duplicate cost resource '{resource}'");
            // growth 是针对tech的
            // 目前只支持(tech-1)作为唯一的growth乘系数
            if (element.QueryIntAttribute("base", out int baseAmount) != TIXML_SUCCESS)
                throw CardConfigError(countryName, card, $"cost resource '{resource}' requires base");
            if (element.QueryIntAttribute("growth", out int growth) != TIXML_SUCCESS) growth = 0;
            card.costs.Add(new CardResourceCostDef
            {
                resource = resource,
                baseAmount = baseAmount,
                growthPerTechLevel = growth,
            });
            if (resource == CardResourceType.Money) card.price = baseAmount;
            else if (resource == CardResourceType.Industry) card.industry = baseAmount;
        }
        if (card.costs.Count == 0)
            throw CardConfigError(countryName, card, "costs cannot be empty");
    }

    // 解析卡牌效果
    private static void ParseCardEffects(TiXmlElement cardElement, CardDef card, string countryName)
    {
        TiXmlElement effectsElement = cardElement.FirstChildElement("effects");
        if (effectsElement != null)
        {
            for (TiXmlElement effectElement = effectsElement.FirstChildElement("effect");
                 effectElement != null;
                 effectElement = effectElement.NextSiblingElement("effect"))
            {
                string type = effectElement.Attribute("type");
                if (string.Equals(type, "area-structure", StringComparison.OrdinalIgnoreCase))
                    card.effects.Add(ParseAreaStructureEffect(effectElement, countryName, card));
                else if (string.Equals(type, "buff-army", StringComparison.OrdinalIgnoreCase))
                    card.effects.Add(ParseBuffArmyEffect(effectElement, countryName, card));
                else if (string.Equals(type, "research", StringComparison.OrdinalIgnoreCase))
                    card.effects.Add(ParseResearchEffect(effectElement, countryName, card));
                else if (string.Equals(type, "draft-army", StringComparison.OrdinalIgnoreCase))
                    card.effects.Add(ParseDraftArmyEffect(effectElement, countryName, card));
                else if (string.Equals(type, "strategic-strike", StringComparison.OrdinalIgnoreCase))
                    card.effects.Add(ParseStrategicStrikeEffect(effectElement, countryName, card));
                else if (string.Equals(type, "airborne-deploy", StringComparison.OrdinalIgnoreCase))
                    card.effects.Add(ParseAirborneDeployEffect(effectElement, countryName, card));
                else
                    throw CardConfigError(countryName, card, $"unknown effect type '{type}'");
            }
        }

        // 和展示（目前只有音效）相关的卡牌效果
        TiXmlElement presentationElement = cardElement.FirstChildElement("presentation");
        if (presentationElement != null)
        {
            string sound = presentationElement.Attribute("sound") ?? "";
            if (sound.Length > 0 && sound != "build" && sound != "buff" &&
                sound != "supply" && sound != "level-up")
                throw CardConfigError(countryName, card, $"unknown presentation sound '{sound}'");
            card.presentation = new CardPresentationDef
            {
                sound = sound,
            };
        }

        bool hasCommanderEffect = card.effects.OfType<UnitBuffEffectDef>()
            .Any(effect => effect.buff == UnitBuffKind.COMMANDER);
        if (hasCommanderEffect && (!card.commanderPriceScaling || !card.commanderCooldown))
            throw CardConfigError(countryName, card,
                "commander buff-army requires price-scaling='commander-level' and cooldown-mode='commander'");
        if (!hasCommanderEffect && (card.commanderPriceScaling || card.commanderCooldown))
            throw CardConfigError(countryName, card,
                "commander price/cooldown attributes require a commander buff-army effect");

        int researchEffectCount = card.effects.Count(effect => effect is ResearchEffectDef);
        if (researchEffectCount > 1)
            throw CardConfigError(countryName, card, "a card may contain only one research effect");
        if (researchEffectCount > 0 && card.type != STRATEGY_TYPE_CARD)
            throw CardConfigError(countryName, card, "research effect requires a strategy card");

        int draftEffectCount = card.effects.Count(effect => effect is DraftArmyEffectDef);
        if (draftEffectCount > 0 && (draftEffectCount != 1 || card.effects.Count != 1))
            throw CardConfigError(countryName, card,
                "a draft card must contain exactly one draft-army effect");
        if ((card.type == ARMY_TYPE_CARD || card.type == NAVY_TYPE_CARD) &&
            card.effects.Count > 0 && draftEffectCount != 1)
            throw CardConfigError(countryName, card,
                "army and navy cards require a single draft-army effect");

        int airMissionCount = card.effects.Count(effect =>
            effect is StrategicStrikeEffectDef or AirborneDeployEffectDef);
        if (airMissionCount > 0 && (card.type != AIR_FORCE_TYPE_CARD ||
            airMissionCount != 1 || card.effects.Count != 1))
            throw CardConfigError(countryName, card,
                "an airforce mission card must contain exactly one air mission effect");
    }

    private static CardEffectDef ParseBuffArmyEffect(
        TiXmlElement element, string countryName, CardDef card)
    {
        // buff目前分为三种；增加固定buff（攻防、运输船、指挥官）、回血和升级
        return element.Attribute("operation")?.ToLowerInvariant() switch
        {
            "add-buff" => ParseUnitBuffEffect(element, countryName, card),
            "health" => ParseArmyHealthEffect(element, countryName, card),
            "level" => ParseArmyLevelEffect(element, countryName, card),
            string value => throw CardConfigError(countryName, card,
                $"invalid buff-army operation '{value}'"),
            null => throw CardConfigError(countryName, card, "buff-army requires operation"),
        };
    }

    private static UnitBuffEffectDef ParseUnitBuffEffect(
        TiXmlElement element, string countryName, CardDef card)
    {
        // 增加固定buff；目前只支持几种类别
        UnitBuffKind buff = element.Attribute("buff")?.ToLowerInvariant() switch
        {
            "carrier" => UnitBuffKind.CARRIER,
            "assault" => UnitBuffKind.ASSAULT,
            "defence" => UnitBuffKind.DEFENCE,
            "commander" => UnitBuffKind.COMMANDER,
            string value => throw CardConfigError(countryName, card, $"invalid unit buff '{value}'"),
            null => throw CardConfigError(countryName, card, "buff-army add-buff requires buff"),
        };
        var effect = new UnitBuffEffectDef
        {
            type = "buff-army",
            buff = buff,
            target = ParseBuffArmyTarget(element.Attribute("target"), countryName, card),
        };
        // 统一的条件解析函数
        ParseEffectConditions(element, effect, countryName, card);
        return effect;
    }

    private static ArmyHealthEffectDef ParseArmyHealthEffect(
        TiXmlElement element, string countryName, CardDef card)
    {
        // 回血量：支持绝对值和百分比
        string amountText = element.Attribute("amount");
        bool percent = amountText?.EndsWith("%", StringComparison.Ordinal) == true;
        string numberText = percent ? amountText.Substring(0, amountText.Length - 1) : amountText;
        if (!int.TryParse(numberText, out int amount) || amount == 0 ||
            amount < -16384 || amount > 16383)
            throw CardConfigError(countryName, card,
                $"invalid buff-army health amount '{amountText}' (expected -16384..16383, optionally followed by %)");

        var effect = new ArmyHealthEffectDef
        {
            type = "buff-army",
            target = ParseBuffArmyTarget(element.Attribute("target"), countryName, card),
            packedAmount = (ushort)((amount & 0x7fff) | (percent ? 0x8000 : 0)),
        };
        ParseEffectConditions(element, effect, countryName, card);
        return effect;
    }

    private static ArmyLevelEffectDef ParseArmyLevelEffect(
        TiXmlElement element, string countryName, CardDef card)
    {
        //等级；+-inf就是直接满级/消除所有等级
        string levelsText = element.Attribute("levels")?.ToLowerInvariant();
        sbyte levels;
        if (levelsText == "+inf") levels = ArmyLevelEffectDef.ToMaximum;
        else if (levelsText == "-inf") levels = ArmyLevelEffectDef.ToMinimum;
        else if (!sbyte.TryParse(levelsText, out levels) || levels == 0 ||
                 levels == sbyte.MinValue || levels == sbyte.MaxValue)
            throw CardConfigError(countryName, card,
                $"invalid buff-army level value '{levelsText}' (expected -127..-1, +1..+126, +inf or -inf)");

        var effect = new ArmyLevelEffectDef
        {
            type = "buff-army",
            target = ParseBuffArmyTarget(element.Attribute("target"), countryName, card),
            levels = levels,
        };
        ParseEffectConditions(element, effect, countryName, card);
        return effect;
    }

    // 研发类的效果
    // 研发卡牌的冷却不跟一般的冷却走
    private static ResearchEffectDef ParseResearchEffect(
        TiXmlElement element, string countryName, CardDef card)
    {
        if (!XmlValueParser.ParseUInt8Attribute(element, "turns", out byte turns))
            throw CardConfigError(countryName, card, "research turns must be 0..255");
        if (!XmlValueParser.ParseUInt8Attribute(element, "max-level", out byte maxLevel) || maxLevel == 0)
            throw CardConfigError(countryName, card, "research max-level must be 1..255");
        var effect = new ResearchEffectDef
        {
            type = "research",
            turns = turns,
            maxLevel = maxLevel,
        };
        ParseEffectConditions(element, effect, countryName, card);
        return effect;
    }

    // 招募（非伞兵）
    // 招募是跟id走的
    private static DraftArmyEffectDef ParseDraftArmyEffect(
        TiXmlElement element, string countryName, CardDef card)
    {
        string unit = element.Attribute("unit")?.ToLowerInvariant();
        if (!armydef_internal.TryResolveArmyTypeReference(unit, out int armyTypeId))
            throw CardConfigError(countryName, card, $"unknown draft army type '{unit}'");
        if (card.type != ARMY_TYPE_CARD && card.type != NAVY_TYPE_CARD)
            throw CardConfigError(countryName, card, "draft-army effect requires an army or navy card");
        card.relatedArmyId = armyTypeId;
        var effect = new DraftArmyEffectDef { type = "draft-army", armyTypeId = armyTypeId };
        ParseEffectConditions(element, effect, countryName, card);
        return effect;
    }

    // 空袭卡（不包括空降）
    // 这里沿用了armydef定义中的payload
    private static StrategicStrikeEffectDef ParseStrategicStrikeEffect(
        TiXmlElement element, string countryName, CardDef card)
    {
        string payloadText = element.Attribute("payload")?.ToLowerInvariant();
        if (!armydef_internal.TryParseAirstrikePayload(payloadText,
                out COMBAT_AIRSTRIKE_PAYLOAD payload))
            throw CardConfigError(countryName, card,
                $"unknown strike payload '{payloadText}'");
        if (card.type != AIR_FORCE_TYPE_CARD)
            throw CardConfigError(countryName, card, "strategic-strike requires an airforce card");
        var effect = new StrategicStrikeEffectDef { type = "strategic-strike", payload = payload };
        ParseEffectConditions(element, effect, countryName, card);
        return effect;
    }

    // 解析空降
    // 可以空降任何单位——这样可以拿去做特招
    // none的意思是“空降但不占领”，感觉以后可以拿来做一些特殊的整活能力
    private static AirborneDeployEffectDef ParseAirborneDeployEffect(
        TiXmlElement element, string countryName, CardDef card)
    {
        string unit = element.Attribute("unit")?.ToLowerInvariant();
        if (!armydef_internal.TryResolveArmyTypeReference(unit, out int armyTypeId))
            throw CardConfigError(countryName, card, $"unknown airborne army type '{unit}'");
        string occupation = element.Attribute("occupation") ?? "capture";
        if (occupation != "capture" && occupation != "none")
            throw CardConfigError(countryName, card, $"unknown airborne occupation '{occupation}'");
        if (card.type != AIR_FORCE_TYPE_CARD)
            throw CardConfigError(countryName, card, "airborne-deploy requires an airforce card");
        var effect = new AirborneDeployEffectDef
        {
            type = "airborne-deploy", armyTypeId = armyTypeId, capture = occupation == "capture",
        };
        ParseEffectConditions(element, effect, countryName, card);
        return effect;
    }

    // 目前只支持3种target： selected：选中的单位
    // front： 前置单位（也就是说，你用这张卡时点击侧边栏，会发生切换置顶单位的动作）
    // all：所有单位
    private static BuffArmyTarget ParseBuffArmyTarget(
        string value, string countryName, CardDef card) => value?.ToLowerInvariant() switch
        {
            "selected" => BuffArmyTarget.SELECTED,
            "front" => BuffArmyTarget.FRONT,
            "all" => BuffArmyTarget.ALL,
            _ => throw CardConfigError(countryName, card, $"invalid buff-army target '{value}'"),
        };

    //解析用卡条件
    private static void ParseEffectConditions(
        TiXmlElement effectElement, CardEffectDef effect, string countryName, CardDef card)
    {
        TiXmlElement conditionsElement = effectElement.FirstChildElement("conditions");
        if (conditionsElement == null) return;

        for (TiXmlElement element = conditionsElement.FirstChildElement();
             element != null; element = element.NextSiblingElement())
            effect.conditions.Add(ParseConditionNode(element, null, countryName, card));
    }

    private enum CardConditionScope { TARGET_ARMY, TARGET_AREA, SOURCE_COUNTRY }

    private static CardConditionDef ParseConditionNode(
        TiXmlElement element, CardConditionScope? scope, string countryName, CardDef card)
    {
        string nodeName = element.Value()?.ToLowerInvariant();
        if (nodeName == "condition")
        {
            // 条件必须嵌套在目标域内
            if (scope == null)
                throw CardConfigError(countryName, card,
                    "condition requires a target-army, target-area or source-country scope");
            // 解析真正的确定条件
            return ParseScopedCondition(element, scope.Value, countryName, card);
        }

        // 如果父级是target-area
        if (nodeName == "army" && scope == CardConditionScope.TARGET_AREA)
        {
            // 对于地块上的单位，支持三种匹配模式：all：所有，any：任意，front：顶部
            string target = element.Attribute("target")?.ToLowerInvariant();
            TargetAreaArmyMode areaArmyMode = target switch
            {
                "front" => TargetAreaArmyMode.FRONT,
                "any" => TargetAreaArmyMode.ANY,
                "all" => TargetAreaArmyMode.ALL,
                _ => throw CardConfigError(countryName, card,
                    $"invalid target-area army target '{target}'"),
            };
            if (element.Attribute("match") != null)
                throw CardConfigError(countryName, card,
                    "target-area army uses target='front|any|all'; match is not supported");
            var areaArmy = new TargetAreaArmyConditionDef
            {
                type = "target-area-army",
                mode = areaArmyMode,
            };
            for (TiXmlElement child = element.FirstChildElement();
                 child != null; child = child.NextSiblingElement())
                 // 进一步解析底层条件
                areaArmy.children.Add(ParseConditionNode(
                    child, CardConditionScope.TARGET_ARMY, countryName, card));
            if (areaArmy.children.Count == 0)
                throw CardConfigError(countryName, card, "target-area army condition cannot be empty");
            return areaArmy;
        }

        // 目前的三类基本范围
        if (nodeName == "target-army" || nodeName == "target-area" || nodeName == "source-country")
        {
            CardConditionScope childScope = nodeName switch
            {
                "target-army" => CardConditionScope.TARGET_ARMY,
                "target-area" => CardConditionScope.TARGET_AREA,
                _ => CardConditionScope.SOURCE_COUNTRY,
            };
            var scopedGroup = new CardConditionGroupDef
            {
                type = nodeName,
                mode = CardConditionGroupMode.ALL,
            };
            for (TiXmlElement child = element.FirstChildElement();
                 child != null; child = child.NextSiblingElement())
                scopedGroup.children.Add(ParseConditionNode(child, childScope, countryName, card));
            if (scopedGroup.children.Count == 0)
                throw CardConfigError(countryName, card, $"condition scope '{nodeName}' cannot be empty");
            return scopedGroup;
        }

        // 底层条件解析
        //与或非
        CardConditionGroupMode mode = nodeName switch
        {
            "all" => CardConditionGroupMode.ALL,
            "any" => CardConditionGroupMode.ANY,
            "not" => CardConditionGroupMode.NOT,
            _ => throw CardConfigError(countryName, card, $"unknown condition node '{nodeName}'"),
        };
        var group = new CardConditionGroupDef { type = nodeName, mode = mode };
        for (TiXmlElement child = element.FirstChildElement();
             child != null; child = child.NextSiblingElement())
            group.children.Add(ParseConditionNode(child, scope, countryName, card));

        if (group.children.Count == 0)
            throw CardConfigError(countryName, card, $"condition group '{nodeName}' cannot be empty");
        if (mode == CardConditionGroupMode.NOT && group.children.Count != 1)
            throw CardConfigError(countryName, card, "condition group 'not' requires exactly one child");
        return group;
    }

    // 解析实际的条件
    // 上面那个函数主要是解析条件域
    private static CardConditionDef ParseScopedCondition(
        TiXmlElement element, CardConditionScope scope, string countryName, CardDef card)
    {
        string property = element.Attribute("property")?.ToLowerInvariant();
        string operation = element.Attribute("operator")?.ToLowerInvariant();
        string value = element.Attribute("value")?.ToLowerInvariant();
        if (scope == CardConditionScope.TARGET_ARMY)
        {
            // 这个和armydef的属性强绑定
            if (property == "navy" && (operation == "equal" || operation == "not-equal") &&
                (value == "true" || value == "false"))
                return new TargetArmyNavyConditionDef
                {
                    type = "target-army-navy",
                    expected = (value == "true") == (operation == "equal"),
                };
            // 是否有攻/防/运输船等
            if (property == "buff" && (operation == "has" || operation == "lacks"))
                return new TargetArmyBuffConditionDef
                {
                    type = "target-army-buff",
                    buff = ParseUnitBuffKind(value, countryName, card),
                    mustHave = operation == "has",
                };
            // 单位等级检查
            if (property == "level")
                return new TargetArmyLevelConditionDef
                {
                    type = "target-army-level",
                    operation = ParseNumericConditionOperator(operation, countryName, card),
                    valueIsMaximum = value == "max",
                    value = value == "max" ? 0 : ParseConditionInt(value, "level", countryName, card),
                };
            // Concrete ArmyDef id; unlike unit-type, this identifies one registered army definition.
            // 针对的是infantry、artillery这些id，可以填名称，也可以填数字id
            if (property == "army-id" && (operation == "equal" || operation == "not-equal"))
                return new TargetArmyIdConditionDef
                {
                    type = "target-army-id",
                    armyId = armydef_internal.TryResolveArmyTypeReference(value, out int armyId)
                        ? armyId
                        : throw CardConfigError(countryName, card, $"unknown army id '{value}'"),
                    mustEqual = operation == "equal",
                };
            // unit-type：定义的单位属性
            if (property == "unit-type" && (operation == "equal" || operation == "not-equal") &&
                XmlValueParser.ParseUInt8Text(value, out byte unitType))
                return new TargetArmyUnitTypeConditionDef
                {
                    type = "target-army-unit-type",
                    unitType = unitType,
                    mustEqual = operation == "equal",
                };
            // 生命值
            if (property == "health")
                return ParseArmyHealthCondition(operation, value, countryName, card);
            throw CardConfigError(countryName, card,
                $"invalid target-army condition property='{property}' operator='{operation}' value='{value}'");
        }

        // 地块条件
        if (scope == CardConditionScope.TARGET_AREA)
        {
            AreaLevelKind? kind = property switch
            {
                "city-level" => AreaLevelKind.CITY,
                "industry-level" => AreaLevelKind.INDUSTRY,
                "airport-level" => AreaLevelKind.AIRPORT,
                "port-level" => AreaLevelKind.PORT,
                _ => null,
            };
            if (kind.HasValue)
                return new TargetAreaLevelConditionDef
                {
                    type = "target-area-level",
                    kind = kind.Value,
                    operation = ParseNumericConditionOperator(operation, countryName, card),
                    value = ParseConditionInt(value, property, countryName, card),
                };
            throw CardConfigError(countryName, card,
                $"invalid target-area condition property='{property}' operator='{operation}' value='{value}'");
        }

        // 国家数据统计——针对指挥官
        if (property == "buff-count" && operation == "less-than")
        {
            if (!int.TryParse(value, out int lessThan) || lessThan < 1)
                throw CardConfigError(countryName, card,
                    "source-country buff-count requires a positive less-than value");
            return new SourceCountryBuffCountConditionDef
            {
                type = "source-country-buff-count",
                buff = ParseUnitBuffKind(element.Attribute("buff"), countryName, card),
                lessThan = lessThan,
            };
        }
        throw CardConfigError(countryName, card,
            $"invalid source-country condition property='{property}' operator='{operation}' value='{value}'");
    }

    private static NumericConditionOperator ParseNumericConditionOperator(
        string value, string countryName, CardDef card) => value switch
        {
            "less-than" => NumericConditionOperator.LESS_THAN,
            "less-than-or-equal" => NumericConditionOperator.LESS_THAN_OR_EQUAL,
            "equal" => NumericConditionOperator.EQUAL,
            "greater-than-or-equal" => NumericConditionOperator.GREATER_THAN_OR_EQUAL,
            "greater-than" => NumericConditionOperator.GREATER_THAN,
            _ => throw CardConfigError(countryName, card, $"invalid numeric operator '{value}'"),
        };

    private static int ParseConditionInt(string value, string property, string countryName, CardDef card)
    {
        if (!int.TryParse(value, out int result) || result < 0)
            throw CardConfigError(countryName, card,
                $"condition '{property}' requires a non-negative integer value");
        return result;
    }

    private static TargetArmyHealthConditionDef ParseArmyHealthCondition(
        string operationText, string valueText, string countryName, CardDef card)
    {
        NumericConditionOperator operation = operationText switch
        {
            "less-than" => NumericConditionOperator.LESS_THAN,
            "less-than-or-equal" => NumericConditionOperator.LESS_THAN_OR_EQUAL,
            "equal" => NumericConditionOperator.EQUAL,
            "greater-than-or-equal" => NumericConditionOperator.GREATER_THAN_OR_EQUAL,
            "greater-than" => NumericConditionOperator.GREATER_THAN,
            _ => throw CardConfigError(countryName, card,
                $"invalid health operator '{operationText}'"),
        };
        var condition = new TargetArmyHealthConditionDef
        {
            type = "target-army-health",
            operation = operation,
        };
        if (valueText == "max")
        {
            condition.valueIsMaximum = true;
            return condition;
        }

        // 设置回血百分比还是数值
        condition.valueIsPercent = valueText?.EndsWith("%", StringComparison.Ordinal) == true;
        string numberText = condition.valueIsPercent
            ? valueText.Substring(0, valueText.Length - 1) : valueText;
        if (!short.TryParse(numberText, out condition.value))
            throw CardConfigError(countryName, card, $"invalid health value '{valueText}'");
        return condition;
    }

    private static UnitBuffKind ParseUnitBuffKind(string value, string countryName, CardDef card) =>
        value?.ToLowerInvariant() switch
        {
            "carrier" => UnitBuffKind.CARRIER,
            "assault" => UnitBuffKind.ASSAULT,
            "defence" => UnitBuffKind.DEFENCE,
            "commander" => UnitBuffKind.COMMANDER,
            _ => throw CardConfigError(countryName, card, $"invalid condition buff '{value}'"),
        };

    // 地块建设/工事效果解析
    private static AreaStructureEffectDef ParseAreaStructureEffect(
        TiXmlElement element, string countryName, CardDef card)
    {
        string slot = element.Attribute("slot");
        string operationText = element.Attribute("operation");
        var effect = new AreaStructureEffectDef { type = "area-structure" };
        if (string.Equals(slot, "installation", StringComparison.OrdinalIgnoreCase))
        {
            effect.slot = AreaStructureSlot.INSTALLATION;
            if (string.Equals(operationText, "set", StringComparison.OrdinalIgnoreCase))
            {
                effect.operation = CardEffectOperation.SET;
                effect.installation = ParseInstallationType(
                    element.Attribute("structure"), countryName, card);
                if (element.Attribute("amount") != null || element.Attribute("initial-level") != null)
                    throw CardConfigError(countryName, card, "installation set cannot specify amount or initial-level");
                return effect;
            }
            if (string.Equals(operationText, "clear", StringComparison.OrdinalIgnoreCase))
            {
                effect.operation = CardEffectOperation.CLEAR;
                if (element.Attribute("structure") != null || element.Attribute("amount") != null ||
                    element.Attribute("initial-level") != null)
                    throw CardConfigError(countryName, card, "installation clear cannot specify structure, amount or initial-level");
                return effect;
            }
            throw CardConfigError(countryName, card, $"unsupported installation operation '{operationText}'");
        }
        if (!string.Equals(slot, "construction", StringComparison.OrdinalIgnoreCase))
            throw CardConfigError(countryName, card, $"invalid area-structure slot '{slot}'");

        effect.slot = AreaStructureSlot.CONSTRUCTION;
        if (string.Equals(operationText, "clear", StringComparison.OrdinalIgnoreCase))
        {
            effect.operation = CardEffectOperation.CLEAR;
            if (element.Attribute("structure") != null || element.Attribute("amount") != null)
                throw CardConfigError(countryName, card, "clear cannot specify structure or amount");
            return effect;
        }
        // 调整等级
        if (!string.Equals(operationText, "adjust", StringComparison.OrdinalIgnoreCase))
            throw CardConfigError(countryName, card, $"unsupported construction operation '{operationText}'");

        effect.operation = CardEffectOperation.ADJUST;
        effect.structure = ParseConstructionType(element.Attribute("structure"), countryName, card);
        string initialLevel = element.Attribute("initial-level") ?? "area-default";
        if (!string.Equals(initialLevel, "area-default", StringComparison.OrdinalIgnoreCase))
            throw CardConfigError(countryName, card, $"unsupported initial-level '{initialLevel}'");

        string amount = element.Attribute("amount");
        if (amount == "+inf")
        {
            effect.toMaximum = true;
        }
        else if (amount == "-inf")
        {
            effect.operation = CardEffectOperation.CLEAR;
        }
        else if (!int.TryParse(amount, out effect.levelDelta) || effect.levelDelta == 0)
        {
            throw CardConfigError(countryName, card, $"invalid amount '{amount}'");
        }
        return effect;
    }

    private static CONSTRUCTION_TYPE ParseConstructionType(string value, string countryName, CardDef card) =>
        value?.ToLowerInvariant() switch
        {
            "city" => CONSTRUCTION_TYPE.CITY,
            "industry" => CONSTRUCTION_TYPE.INDUSTRY,
            "airport" => CONSTRUCTION_TYPE.AIRPORT,
            _ => throw CardConfigError(countryName, card, $"invalid construction structure '{value}'"),
        };

    private static INSTALLATION_TYPE ParseInstallationType(string value, string countryName, CardDef card) =>
        value?.ToLowerInvariant() switch
        {
            "fort" => INSTALLATION_TYPE.FORT,
            "entrenchment" => INSTALLATION_TYPE.ENTRENCHMENT,
            "antiaircraft" => INSTALLATION_TYPE.ANTIAIRCRAFT,
            "radar" => INSTALLATION_TYPE.RADAR,
            _ => throw CardConfigError(countryName, card, $"invalid installation structure '{value}'"),
        };

    private static InvalidDataException CardConfigError(string countryName, CardDef card, string message) =>
        new($"carddef.xml: country={countryName} card={card.key}: {message}");

    public void InitCountryCardDefMap(string countryName)
    {
        if (countryName == null) return;
        if (mapNewCardDef.ContainsKey(countryName)) return;
        mapNewCardDef[countryName] = new ToNewCardDef();
    }

    public void AddNewCardDefToMap(CardDef cardDef, string countryName)
    {
        if (cardDef == null) return;

        if (!mapNewCardDef.TryGetValue(countryName, out ToNewCardDef countryCards) || countryCards == null)
        {
            countryCards = new ToNewCardDef();
            mapNewCardDef[countryName] = countryCards;
        }

        // replaces an existing card of the same id
        countryCards.mapCarddef[(int)cardDef.id] = cardDef;
    }

    public CardDef GetNewCardDef(CARD_ID cardDefID, string CountryName)
    {
        if (CountryName != null &&
            mapNewCardDef.TryGetValue(CountryName, out ToNewCardDef countryCards) && countryCards != null)
        {
            if (countryCards.mapCarddef.TryGetValue((int)cardDefID, out CardDef card)) return card;
        }

        if (!mapNewCardDef.TryGetValue("others", out ToNewCardDef defaultCards) || defaultCards == null) return null;

        if (!defaultCards.mapCarddef.TryGetValue((int)cardDefID, out CardDef defaultCard)) return null;

        return defaultCard;
    }

    /// <summary>
    /// Builds the AI drafting priority (by price) of every country.
    /// A country table only holds overrides, missing cards still fall back to "others" via GetNewCardDef.
    /// </summary>
    public void RebuildTroopCardLists()
    {
        foreach (var countryEntry in mapNewCardDef)
        {
            ToNewCardDef countryCards = countryEntry.Value;
            if (countryCards == null) continue;

            for (int category = 0; category < 2; ++category)
            {
                var cards = new List<CardSortInfo>(AllCardTab[category].Count);

                foreach (CARD_ID cardId in AllCardTab[category])
                {
                    CardDef cardDef = GetNewCardDef(cardId, countryEntry.Key);
                    if (cardDef == null) continue;

                    cards.Add(new CardSortInfo { id = cardId, price = cardDef.price, industry = cardDef.industry, tech = cardDef.tech });
                }

                cards.Sort((left, right) =>
                {
                    if (left.price != right.price) return right.price.CompareTo(left.price);
                    if (left.industry != right.industry) return right.industry.CompareTo(left.industry);
                    return right.tech.CompareTo(left.tech);
                });

                List<CARD_ID> order = countryCards.troopOrder[category];
                order.Clear();
                foreach (CardSortInfo card in cards) order.Add(card.id);
            }
        }
    }

    /// <summary>The read-only drafting order held by CObjectDef; without a country table the default one is used.</summary>
    public List<CARD_ID> GetTroopCardList(string countryName, bool navy)
    {
        ToNewCardDef countryCards = null;
        if (countryName == null || !mapNewCardDef.TryGetValue(countryName, out countryCards) || countryCards == null)
            mapNewCardDef.TryGetValue("others", out countryCards);

        if (countryCards != null) return countryCards.troopOrder[navy ? 1 : 0];

        return s_emptyTroopCardList;
    }

    public CardDef GetCardDef(CARD_ID cardId, CCountry country)
    {
        if (country == null) return GetNewCardDef(cardId, "others");
        return GetNewCardDef(cardId, country.name);
    }

    public CardDef GetCardDef(CARD_ID cardId)
    {
        CCountry country_now = g_GameManager.GetCurCountry();
        return GetCardDef(cardId, country_now);
    }

    public static CARD_ID TurnIntToRealCardId(int num_in)
    {
        if (num_in <= 0) return INFANTRY_CARD;
        return (CARD_ID)num_in;
    }
}
