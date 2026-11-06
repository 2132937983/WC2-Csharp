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
                                if (CardListElement.QueryIntAttribute("id", out Id) != TIXML_SUCCESS) Id = 0;
                                tmp_card_def.id = (CARD_ID)Id;
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
                                string Image = CardListElement.Attribute("image");
                                if (Image != null) tmp_card_def.image = Image;
                                else tmp_card_def.image = "card_infantry.png";
                                string Intro = CardListElement.Attribute("intro");
                                // the original tests Image here (sic); a missing intro falls back to the default text
                                if (Image != null) tmp_card_def.intro = Intro ?? "Infantry Intro";
                                else tmp_card_def.intro = "Infantry Intro";
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
