using System.Text.Json.Nodes;

namespace WC2;

/// <summary>A unit drafted with a card gets level-ups and tactic cards when the owner's war medal is high enough.</summary>
public sealed class DraftBonusDef
{
    public WARMEDAL_ID medal;
    /// <summary>The bonus applies from this medal level on.</summary>
    public int minLevel = 1;
    /// <summary>Only cards of this type (null = any).</summary>
    public CARD_TYPE? cardType;
    /// <summary>Only these card ids (null = any).</summary>
    public List<int> cards;
    public int upgrades;
    public List<ARMY_CARD> addCards = new();
}

/// <summary>
/// War medal rules (<c>data/medals.json</c>): what the six medals do at each level. The kinds of
/// effects are code; their numbers, thresholds and the draft bonus table are data. The built-in values
/// reproduce the original rules exactly.
/// </summary>
public static class Medals
{
    /// <summary>Medals needed for the next level: base + perLevel * current level (original 2 + level).</summary>
    public static int upgradeCostBase = 2;
    public static int upgradeCostPerLevel = 1;
    /// <summary>Commerce medal: +percent of taxes and industry per level.</summary>
    public static int commerceIncomePercentPerLevel = 10;
    /// <summary>Air force medal: air strike attack bonus from a level on.</summary>
    public static int airStrikeAttackBonus = 1;
    public static int airStrikeAttackBonusFromLevel = 1;
    /// <summary>Air force medal: air strike radius (map units), larger from a level on.</summary>
    public static float airStrikeRadius = 300f;
    public static float airStrikeRadiusUpgraded = 400f;
    public static int airStrikeRadiusFromLevel = 2;
    /// <summary>Air force medal: air force cards have no cool-down from a level on.</summary>
    public static int airCardsNoCooldownFromLevel = 3;
    /// <summary>Bonuses for drafted units, applied in this order.</summary>
    public static List<DraftBonusDef> draftBonuses = BuiltInDraftBonuses();

    static Medals() => LoadBuiltIn();

    public static void LoadBuiltIn()
    {
        upgradeCostBase = 2;
        upgradeCostPerLevel = 1;
        commerceIncomePercentPerLevel = 10;
        airStrikeAttackBonus = 1;
        airStrikeAttackBonusFromLevel = 1;
        airStrikeRadius = 300f;
        airStrikeRadiusUpgraded = 400f;
        airStrikeRadiusFromLevel = 2;
        airCardsNoCooldownFromLevel = 3;
        draftBonuses = BuiltInDraftBonuses();
    }

    private static DraftBonusDef Bonus(WARMEDAL_ID medal, int level, CARD_TYPE? type, int[] cards, int upgrades, params ARMY_CARD[] add) =>
        new() { medal = medal, minLevel = level, cardType = type, cards = cards?.ToList(), upgrades = upgrades, addCards = add.ToList() };

    /// <summary>The original nested medal checks of CCountry::UseCard, as an ordered table.</summary>
    public static List<DraftBonusDef> BuiltInDraftBonuses()
    {
        int I(CARD_ID id) => (int)id;
        return new List<DraftBonusDef>
        {
            // navy: land units come with a transport ship; destroyers and cruisers, then battleships and carriers, one level up
            Bonus(WARMEDAL_ID.NAVY_MEDAL, 1, CARD_TYPE.ARMY_TYPE_CARD, null, 0, ARMY_CARD.CARRIER),
            Bonus(WARMEDAL_ID.NAVY_MEDAL, 2, null, new[] { I(CARD_ID.DESTROYER_CARD), I(CARD_ID.CRUISER_CARD) }, 1),
            Bonus(WARMEDAL_ID.NAVY_MEDAL, 3, null, new[] { I(CARD_ID.BATTLE_SHIP_CARD), I(CARD_ID.AIRCRAFT_CARRIER_CARD) }, 1),
            // infantry: one level, two levels, then assault and defence tactics
            Bonus(WARMEDAL_ID.INFANTRY_MEDAL, 1, null, new[] { I(CARD_ID.INFANTRY_CARD) }, 1),
            Bonus(WARMEDAL_ID.INFANTRY_MEDAL, 2, null, new[] { I(CARD_ID.INFANTRY_CARD) }, 1),
            Bonus(WARMEDAL_ID.INFANTRY_MEDAL, 3, null, new[] { I(CARD_ID.INFANTRY_CARD) }, 0, ARMY_CARD.ASSAULT, ARMY_CARD.DEFENCE),
            // artillery: artillery, then rockets one level up, then defence tactics for both
            Bonus(WARMEDAL_ID.ARTILLERY_MEDAL, 1, null, new[] { I(CARD_ID.ARTILLERY_CARD) }, 1),
            Bonus(WARMEDAL_ID.ARTILLERY_MEDAL, 2, null, new[] { I(CARD_ID.ROCKET_CARD) }, 1),
            Bonus(WARMEDAL_ID.ARTILLERY_MEDAL, 3, null, new[] { I(CARD_ID.ARTILLERY_CARD), I(CARD_ID.ROCKET_CARD) }, 0, ARMY_CARD.DEFENCE),
            // armour: armoured cars, then tanks, then heavy tanks one level up
            Bonus(WARMEDAL_ID.ARMOUR_MEDAL, 1, null, new[] { I(CARD_ID.ARMOUR_CARD) }, 1),
            Bonus(WARMEDAL_ID.ARMOUR_MEDAL, 2, null, new[] { I(CARD_ID.TANK_CARD) }, 1),
            Bonus(WARMEDAL_ID.ARMOUR_MEDAL, 3, null, new[] { I(CARD_ID.HEAVY_TANK_CARD) }, 1),
        };
    }

    /// <summary>Medals needed to raise a war medal from <paramref name="level"/>.</summary>
    public static int UpgradeCost(int level) => upgradeCostPerLevel * level + upgradeCostBase;

    /// <summary>Applies the draft bonuses of a country's medals to a unit drafted with a card.</summary>
    public static void ApplyDraftBonuses(CCountry country, CardDef cardDef, CArmy army)
    {
        foreach (var bonus in draftBonuses)
        {
            if (country.GetWarMedalLevel(bonus.medal) < bonus.minLevel) continue;
            if (bonus.cardType.HasValue && cardDef.type != bonus.cardType.Value) continue;
            if (bonus.cards != null && !bonus.cards.Contains((int)cardDef.id)) continue;
            for (int i = 0; i < bonus.upgrades; i++) army.Upgrade();
            foreach (var card in bonus.addCards) army.AddCard(card);
        }
    }

    // ------------------------------------------------------------------------------------
    // JSON
    // ------------------------------------------------------------------------------------

    private static readonly string[] MedalNames = { "infantry", "airforce", "artillery", "armour", "navy", "commerce" };
    private static readonly string[] CardTypeNames = { "army", "navy", "airforce", "development", "strategy" };
    private static readonly string[] TacticNames = { "assault", "defence", "carrier", "commander" };

    /// <summary>Loads data/medals.json over the built-in rules; false when the package has none.</summary>
    public static bool LoadJson()
    {
        var root = JsonData.LoadMerged(DataPaths.Medals);
        LoadBuiltIn();
        if (root == null) return false;
        var cost = root.Obj("upgradeCost");
        upgradeCostBase = cost.Int("base", upgradeCostBase);
        upgradeCostPerLevel = cost.Int("perLevel", upgradeCostPerLevel);
        commerceIncomePercentPerLevel = root.Obj("commerce").Int("incomePercentPerLevel", commerceIncomePercentPerLevel);
        var air = root.Obj("airforce");
        airStrikeAttackBonus = air.Int("attackBonus", airStrikeAttackBonus);
        airStrikeAttackBonusFromLevel = air.Int("attackBonusFromLevel", airStrikeAttackBonusFromLevel);
        airStrikeRadius = air.Float("radius", airStrikeRadius);
        airStrikeRadiusUpgraded = air.Float("radiusUpgraded", airStrikeRadiusUpgraded);
        airStrikeRadiusFromLevel = air.Int("radiusUpgradedFromLevel", airStrikeRadiusFromLevel);
        airCardsNoCooldownFromLevel = air.Int("noCooldownFromLevel", airCardsNoCooldownFromLevel);
        var bonuses = root.Arr("draftBonuses");
        if (bonuses != null)
        {
            draftBonuses = new List<DraftBonusDef>();
            foreach (var b in bonuses.Objects())
            {
                int medal = Array.IndexOf(MedalNames, b.Str("medal") ?? "");
                if (medal < 0) { Log("medals.json: unknown medal " + b.Str("medal")); continue; }
                var def = new DraftBonusDef { medal = (WARMEDAL_ID)medal, minLevel = b.Int("level", 1), upgrades = b.Int("upgrades", 0) };
                int type = Array.IndexOf(CardTypeNames, b.Str("cardType") ?? "");
                if (type >= 0) def.cardType = (CARD_TYPE)type;
                if (b.Arr("cards") is JsonArray cards)
                    def.cards = cards.Select(c => TryParseInt(c?.Text(), out int id) ? id : -1).Where(id => id >= 0).ToList();
                if (b.Arr("addTactics") is JsonArray tactics)
                    foreach (var t in tactics)
                    {
                        int tactic = Array.IndexOf(TacticNames, t?.Text() ?? "");
                        if (tactic >= 0) def.addCards.Add((ARMY_CARD)tactic);
                    }
                draftBonuses.Add(def);
            }
        }
        return true;
    }

    public static JsonObject BuiltInJson()
    {
        LoadBuiltIn();
        var bonuses = new JsonArray();
        foreach (var b in draftBonuses)
        {
            var o = new JsonObject { ["medal"] = MedalNames[(int)b.medal], ["level"] = b.minLevel };
            if (b.cardType.HasValue) o["cardType"] = CardTypeNames[(int)b.cardType.Value];
            if (b.cards != null) o["cards"] = new JsonArray(b.cards.Select(c => (JsonNode)c).ToArray());
            if (b.upgrades != 0) o["upgrades"] = b.upgrades;
            if (b.addCards.Count > 0) o["addTactics"] = new JsonArray(b.addCards.Select(c => (JsonNode)TacticNames[(int)c]).ToArray());
            bonuses.Add(o);
        }
        return new JsonObject
        {
            ["format"] = 1,
            ["about"] = "War medal rules (the values reproduce the original game). draftBonuses: a unit drafted with a card gets 'upgrades' level-ups and 'addTactics' when the owner's medal is at 'level' or above; 'cards' are card ids (data/cards.json), 'cardType' a card type. Field reference: docs/DATA.md#medals.",
            ["upgradeCost"] = new JsonObject { ["base"] = upgradeCostBase, ["perLevel"] = upgradeCostPerLevel },
            ["commerce"] = new JsonObject { ["incomePercentPerLevel"] = commerceIncomePercentPerLevel },
            ["airforce"] = new JsonObject
            {
                ["attackBonus"] = airStrikeAttackBonus,
                ["attackBonusFromLevel"] = airStrikeAttackBonusFromLevel,
                ["radius"] = airStrikeRadius,
                ["radiusUpgraded"] = airStrikeRadiusUpgraded,
                ["radiusUpgradedFromLevel"] = airStrikeRadiusFromLevel,
                ["noCooldownFromLevel"] = airCardsNoCooldownFromLevel,
            },
            ["draftBonuses"] = bonuses,
        };
    }

    public static object Snapshot() => new
    {
        upgradeCostBase, upgradeCostPerLevel, commerceIncomePercentPerLevel, airStrikeAttackBonus, airStrikeAttackBonusFromLevel,
        airStrikeRadius, airStrikeRadiusUpgraded, airStrikeRadiusFromLevel, airCardsNoCooldownFromLevel,
        bonuses = draftBonuses.ToList(),
    };
}
