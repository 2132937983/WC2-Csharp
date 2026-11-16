namespace WC2;

public enum CardEffectOperation
{
    Set,
    Adjust,
    Clear,
}

public enum AreaStructureSlot
{
    Construction,
    Installation,
}

public enum ConstructionInitialLevel
{
    AreaDefault,
}

public enum UnitBuffKind
{
    Assault,
    Defence,
    Carrier,
    Commander,
}

public enum UnitBuffTarget
{
    SelectedArmy,
    FrontArmy,
}

public enum ArmyEffectTarget
{
    SelectedArmy,
    FrontArmy,
    AllArmies,
}

public abstract class CardEffectDef
{
    public string type = "";
    public readonly List<CardConditionDef> conditions = new();
}

public abstract class CardConditionDef
{
    public string type = "";
}

public enum CardConditionGroupMode
{
    All,
    Any,
    Not,
}

public sealed class CardConditionGroupDef : CardConditionDef
{
    public CardConditionGroupMode mode;
    public readonly List<CardConditionDef> children = new();
}

public sealed class TargetAreaArmiesConditionDef : CardConditionDef
{
    public bool matchAll;
    public readonly List<CardConditionDef> children = new();
}

public enum NumericConditionOperator
{
    LessThan,
    LessThanOrEqual,
    Equal,
    GreaterThanOrEqual,
    GreaterThan,
}

public sealed class TargetArmyDomainConditionDef : CardConditionDef
{
    public bool mustBeNavy;
}

public sealed class TargetArmyBuffConditionDef : CardConditionDef
{
    public UnitBuffKind buff;
    public bool mustHave;
}

public sealed class TargetArmyLevelConditionDef : CardConditionDef
{
    public NumericConditionOperator operation;
    public int value;
    public bool valueIsMaximum;
}

public sealed class TargetArmyTypeConditionDef : CardConditionDef
{
    public int armyTypeId;
    public bool mustEqual;
}

public sealed class TargetArmyUnitTypeConditionDef : CardConditionDef
{
    public byte unitType;
    public bool mustEqual;
}

public enum AreaLevelKind { City, Industry, Airport, Port }

public sealed class TargetAreaLevelConditionDef : CardConditionDef
{
    public AreaLevelKind kind;
    public NumericConditionOperator operation;
    public int value;
}

public sealed class TargetArmyHealthConditionDef : CardConditionDef
{
    public NumericConditionOperator operation;
    public short value;
    public bool valueIsPercent;
    public bool valueIsMaximum;
}

public sealed class SourceCountryBuffCountConditionDef : CardConditionDef
{
    public UnitBuffKind buff;
    public int lessThan;
}

public sealed class AreaStructureEffectDef : CardEffectDef
{
    public AreaStructureSlot slot;
    public CONSTRUCTION_TYPE structure;
    public INSTALLATION_TYPE installation;
    public CardEffectOperation operation;
    public int levelDelta;
    public bool toMaximum;
    public ConstructionInitialLevel initialLevel = ConstructionInitialLevel.AreaDefault;
}

public sealed class UnitBuffEffectDef : CardEffectDef
{
    public UnitBuffKind buff;
    public UnitBuffTarget target;
}

public sealed class ArmyHealthEffectDef : CardEffectDef
{
    // Bit 15 selects percent-of-max-HP; bits 0..14 are a signed 15-bit amount.
    public ushort packedAmount;
    public ArmyEffectTarget target;

    public bool IsPercent => (packedAmount & 0x8000) != 0;
    public int Amount
    {
        get
        {
            int value = packedAmount & 0x7fff;
            return (value & 0x4000) != 0 ? value - 0x8000 : value;
        }
    }
}

public sealed class ArmyLevelEffectDef : CardEffectDef
{
    public const sbyte ToMinimum = sbyte.MinValue;
    public const sbyte ToMaximum = sbyte.MaxValue;
    public sbyte levels;
    public ArmyEffectTarget target;
}

public sealed class ResearchEffectDef : CardEffectDef
{
    public byte turns;
    public byte maxLevel;
}

public sealed class DraftArmyEffectDef : CardEffectDef
{
    public int armyTypeId;
}

public sealed class StrategicStrikeEffectDef : CardEffectDef
{
    public COMBAT_AIRSTRIKE_PAYLOAD payload;
}

public sealed class AirborneDeployEffectDef : CardEffectDef
{
    public int armyTypeId;
    public bool capture;
}

public sealed class CardPresentationDef
{
    public string sound = "";
}

public sealed class CardEffectContext
{
    public CCountry sourceCountry;
    public CardDef card;
    public CArea targetArea;
    public int targetAreaId;
    public int targetArmyIndex;
}

public readonly struct CardEffectResult
{
    public bool Success { get; }
    public string ErrorCode { get; }

    private CardEffectResult(bool success, string errorCode)
    {
        Success = success;
        ErrorCode = errorCode ?? "";
    }

    public static CardEffectResult Ok() => new(true, "");
    public static CardEffectResult Fail(string errorCode) => new(false, errorCode);
}

public interface ICardEffectHandler
{
    bool CanApply(CardEffectContext context, CardEffectDef effect, out string errorCode);
    CardEffectResult Apply(CardEffectContext context, CardEffectDef effect);
}

public static class CardEffectRegistry
{
    private static readonly Dictionary<string, ICardEffectHandler> s_handlers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["area-structure"] = new AreaStructureEffectHandler(),
            ["unit-buff"] = new UnitBuffEffectHandler(),
            ["army-health"] = new ArmyHealthEffectHandler(),
            ["army-level"] = new ArmyLevelEffectHandler(),
            ["research"] = new ResearchEffectHandler(),
            ["draft-army"] = new DraftArmyEffectHandler(),
            ["strategic-strike"] = new StrategicStrikeEffectHandler(),
            ["airborne-deploy"] = new AirborneDeployEffectHandler(),
        };

    public static bool TryGet(string type, out ICardEffectHandler handler) =>
        s_handlers.TryGetValue(type ?? "", out handler);
}

public static class AirCardTargetRules
{
    public static bool ValidateCommon(CardEffectContext context, out CArea area, out string errorCode)
    {
        area = context?.targetArea;
        errorCode = "";
        if (context?.sourceCountry == null) { errorCode = "missing-source-country"; return false; }
        if (area == null) { errorCode = "missing-target-area"; return false; }
        if (!area.info.enable) { errorCode = "target-disabled"; return false; }
        if (context.sourceCountry.GetNumAirport() <= 0)
        { errorCode = "source-has-no-airport"; return false; }
        float distance = context.sourceCountry.GetMinDstToAirport(context.targetAreaId);
        if (distance <= 0 || distance >= context.sourceCountry.AirstrikeRadius())
        { errorCode = "target-out-of-air-range"; return false; }
        return true;
    }
}

public sealed class StrategicStrikeEffectHandler : ICardEffectHandler
{
    public bool CanApply(CardEffectContext context, CardEffectDef effect, out string errorCode)
    {
        if (effect is not StrategicStrikeEffectDef)
        { errorCode = "invalid-effect-definition"; return false; }
        if (!AirCardTargetRules.ValidateCommon(context, out CArea area, out errorCode)) return false;
        if (area.country == context.sourceCountry) { errorCode = "target-is-friendly"; return false; }
        if (area.armyCount <= 0) { errorCode = "target-has-no-army"; return false; }
        return true;
    }

    public CardEffectResult Apply(CardEffectContext context, CardEffectDef effect)
    {
        if (!CanApply(context, effect, out string errorCode)) return CardEffectResult.Fail(errorCode);
        var fight = new CFight();
        fight.AirStrikesAttack(context.sourceCountry, context.targetAreaId,
            (int)((StrategicStrikeEffectDef)effect).payload);
        fight.ApplyResult();
        return CardEffectResult.Ok();
    }
}

public sealed class AirborneDeployEffectHandler : ICardEffectHandler
{
    public bool CanApply(CardEffectContext context, CardEffectDef effect, out string errorCode)
    {
        if (effect is not AirborneDeployEffectDef deploy)
        { errorCode = "invalid-effect-definition"; return false; }
        if (!AirCardTargetRules.ValidateCommon(context, out CArea area, out errorCode)) return false;
        if (area.info.sea) { errorCode = "target-wrong-terrain"; return false; }
        if (CObjectDef.Instance().GetArmyDef(deploy.armyTypeId, context.sourceCountry.name) == null)
        { errorCode = "unknown-army-type"; return false; }
        if (!deploy.capture && area.country != context.sourceCountry)
        { errorCode = "airborne-capture-disabled"; return false; }
        if (area.country == context.sourceCountry)
        {
            if (area.armyCount >= AREA_RENDER_ARMY_LIMIT)
            { errorCode = "target-army-capacity"; return false; }
        }
        else if (area.armyCount > 0)
        { errorCode = "target-enemy-army-present"; return false; }
        return true;
    }

    public CardEffectResult Apply(CardEffectContext context, CardEffectDef effect)
    {
        if (!CanApply(context, effect, out string errorCode)) return CardEffectResult.Fail(errorCode);
        var deploy = (AirborneDeployEffectDef)effect;
        CArea area = context.targetArea;
        CCountry previousOwner = area.country;
        if (deploy.capture && previousOwner != context.sourceCountry)
        {
            previousOwner?.RemoveArea(context.targetAreaId);
            context.sourceCountry.AddArea(context.targetAreaId);
            area.country = context.sourceCountry;
            if (previousOwner != null && previousOwner.IsConquested())
                previousOwner.BeConquestedBy(context.sourceCountry);
        }
        return area.DraftArmy(deploy.armyTypeId) != null
            ? CardEffectResult.Ok() : CardEffectResult.Fail("draft-failed");
    }
}

public sealed class DraftArmyEffectHandler : ICardEffectHandler
{
    public bool CanApply(CardEffectContext context, CardEffectDef effect, out string errorCode)
    {
        errorCode = "";
        if (effect is not DraftArmyEffectDef draft)
        { errorCode = "invalid-effect-definition"; return false; }
        if (context?.sourceCountry == null)
        { errorCode = "missing-source-country"; return false; }
        CArea area = context?.targetArea;
        if (area == null) { errorCode = "missing-target-area"; return false; }
        if (!area.info.enable) { errorCode = "target-disabled"; return false; }
        if (area.country != context.sourceCountry)
        { errorCode = "target-wrong-owner"; return false; }
        if (area.armyCount >= AREA_RENDER_ARMY_LIMIT)
        { errorCode = "target-army-capacity"; return false; }
        ArmyDef armyDef = CObjectDef.Instance().GetArmyDef(draft.armyTypeId, context.sourceCountry.name);
        if (armyDef == null) { errorCode = "unknown-army-type"; return false; }
        bool navyCard = context.card.type == NAVY_TYPE_CARD;
        if (navyCard != armyDef.IsNavy())
        { errorCode = "card-army-domain-mismatch"; return false; }
        if (navyCard)
        {
            if (!area.info.sea || area.GetPortLevel() <= 0)
            { errorCode = "target-not-sea-port"; return false; }
            if (area.armyCount >= AREA_NAVY_LIMIT)
            { errorCode = "target-navy-capacity"; return false; }
        }
        else if (area.info.sea)
        { errorCode = "target-wrong-terrain"; return false; }
        return true;
    }

    public CardEffectResult Apply(CardEffectContext context, CardEffectDef effect)
    {
        if (!CanApply(context, effect, out string errorCode)) return CardEffectResult.Fail(errorCode);
        return context.targetArea.DraftArmy(((DraftArmyEffectDef)effect).armyTypeId) != null
            ? CardEffectResult.Ok() : CardEffectResult.Fail("draft-failed");
    }
}

public static class CardEffectExecutor
{
    public static bool CanApply(CardEffectContext context, out string errorCode)
    {
        errorCode = "";
        if (context?.card?.effects == null || context.card.effects.Count == 0)
        {
            errorCode = "missing-effect";
            return false;
        }

        foreach (CardEffectDef effect in context.card.effects)
        {
            if (!CardConditionEvaluator.CanApply(context, effect, out errorCode)) return false;
            if (!CardEffectRegistry.TryGet(effect.type, out ICardEffectHandler handler))
            {
                errorCode = "unknown-effect";
                return false;
            }
            if (!handler.CanApply(context, effect, out errorCode)) return false;
        }
        return true;
    }

    public static CardEffectResult Apply(CardEffectContext context)
    {
        if (!CanApply(context, out string errorCode)) return CardEffectResult.Fail(errorCode);

        foreach (CardEffectDef effect in context.card.effects)
        {
            CardEffectRegistry.TryGet(effect.type, out ICardEffectHandler handler);
            CardEffectResult result = handler.Apply(context, effect);
            if (!result.Success) return result;
        }
        return CardEffectResult.Ok();
    }
}

public static class CardConditionEvaluator
{
    public static bool CanApply(CardEffectContext context, CardEffectDef effect, out string errorCode)
    {
        errorCode = "";
        foreach (CardConditionDef condition in effect.conditions)
        {
            if (!Evaluate(condition, context, effect, null, out errorCode)) return false;
        }
        return true;
    }

    public static bool CanMeetSourceConditions(CCountry country, CardDef card)
    {
        if (country == null || card == null) return false;
        foreach (CardEffectDef effect in card.effects)
        foreach (CardConditionDef condition in effect.conditions)
        {
            if (EvaluateWithoutTarget(condition, country) == PartialConditionResult.False)
                return false;
        }
        return true;
    }

    private enum PartialConditionResult { False, True, Unknown }

    private static bool Evaluate(
        CardConditionDef condition, CardEffectContext context, CardEffectDef effect,
        CArmy armyOverride, out string errorCode)
    {
        errorCode = "";
        CArmy army = armyOverride ?? GetTargetArmy(context, effect);
        switch (condition)
        {
            case CardConditionGroupDef group:
                return EvaluateGroup(group, context, effect, armyOverride, out errorCode);
            case TargetAreaArmiesConditionDef armies:
                return EvaluateAreaArmies(armies, context, effect, out errorCode);
            case TargetArmyDomainConditionDef domain:
                if (army == null) { errorCode = "missing-target-army"; return false; }
                if (army.IsNavy() != domain.mustBeNavy)
                { errorCode = "target-army-domain-mismatch"; return false; }
                return true;
            case TargetArmyBuffConditionDef armyBuff:
                if (army == null) { errorCode = "missing-target-army"; return false; }
                if (army.HasCard(ToArmyCard(armyBuff.buff)) != armyBuff.mustHave)
                { errorCode = "target-army-buff-mismatch"; return false; }
                return true;
            case TargetArmyLevelConditionDef level:
                if (army == null) { errorCode = "missing-target-army"; return false; }
                int expectedLevel = level.valueIsMaximum ? MAX_ARMY_LEVEL : level.value;
                if (!CompareNumber(army.level, level.operation, expectedLevel))
                { errorCode = "target-army-level-limit"; return false; }
                return true;
            case TargetArmyTypeConditionDef armyType:
                if (army?.basicAbilities == null) { errorCode = "missing-target-army"; return false; }
                if ((army.basicAbilities.id == armyType.armyTypeId) != armyType.mustEqual)
                { errorCode = "target-army-type-mismatch"; return false; }
                return true;
            case TargetArmyUnitTypeConditionDef unitType:
                if (army?.basicAbilities == null) { errorCode = "missing-target-army"; return false; }
                if ((army.basicAbilities.unitType == unitType.unitType) != unitType.mustEqual)
                { errorCode = "target-army-unit-type-mismatch"; return false; }
                return true;
            case TargetAreaLevelConditionDef areaLevel:
                if (context?.targetArea == null) { errorCode = "missing-target-area"; return false; }
                int actualLevel = GetAreaLevel(context.targetArea, areaLevel.kind);
                if (!CompareNumber(actualLevel, areaLevel.operation, areaLevel.value))
                { errorCode = "target-area-level-mismatch"; return false; }
                return true;
            case TargetArmyHealthConditionDef health:
                if (army == null) { errorCode = "missing-target-army"; return false; }
                int healthTarget = health.valueIsMaximum ? army.GetMaxStrength() :
                    health.valueIsPercent ? health.value * army.GetMaxStrength() / 100 : health.value;
                if (!CompareNumber(army.hp, health.operation, healthTarget))
                { errorCode = "target-army-health-mismatch"; return false; }
                return true;
            case SourceCountryBuffCountConditionDef count:
                if (context.sourceCountry == null)
                { errorCode = "missing-source-country"; return false; }
                if (CountCountryBuff(context.sourceCountry, count.buff) >= count.lessThan)
                { errorCode = "source-buff-count-limit"; return false; }
                return true;
            default:
                errorCode = "unknown-condition";
                return false;
        }
    }

    private static bool EvaluateGroup(
        CardConditionGroupDef group, CardEffectContext context, CardEffectDef effect,
        CArmy armyOverride, out string errorCode)
    {
        errorCode = "";
        if (group.mode == CardConditionGroupMode.Not)
        {
            bool result = Evaluate(group.children[0], context, effect, armyOverride, out _);
            if (!result) return true;
            errorCode = "condition-not-failed";
            return false;
        }
        if (group.mode == CardConditionGroupMode.All)
        {
            foreach (CardConditionDef child in group.children)
                if (!Evaluate(child, context, effect, armyOverride, out errorCode)) return false;
            return true;
        }

        foreach (CardConditionDef child in group.children)
            if (Evaluate(child, context, effect, armyOverride, out _)) return true;
        errorCode = "condition-any-failed";
        return false;
    }

    private static bool EvaluateAreaArmies(
        TargetAreaArmiesConditionDef armies, CardEffectContext context, CardEffectDef effect,
        out string errorCode)
    {
        errorCode = "";
        CArea area = context?.targetArea;
        if (area == null) { errorCode = "missing-target-area"; return false; }
        if (area.armyCount <= 0) { errorCode = "missing-target-army"; return false; }

        for (int index = 0; index < area.armyCount; ++index)
        {
            CArmy army = area.GetArmy(index);
            bool matches = true;
            foreach (CardConditionDef child in armies.children)
            {
                if (!Evaluate(child, context, effect, army, out _))
                { matches = false; break; }
            }
            if (!armies.matchAll && matches) return true;
            if (armies.matchAll && !matches)
            { errorCode = "target-area-army-condition-failed"; return false; }
        }
        if (armies.matchAll) return true;
        errorCode = "target-area-no-matching-army";
        return false;
    }

    private static bool CompareNumber(int actual, NumericConditionOperator operation, int expected) =>
        operation switch
        {
            NumericConditionOperator.LessThan => actual < expected,
            NumericConditionOperator.LessThanOrEqual => actual <= expected,
            NumericConditionOperator.Equal => actual == expected,
            NumericConditionOperator.GreaterThanOrEqual => actual >= expected,
            NumericConditionOperator.GreaterThan => actual > expected,
            _ => false,
        };

    private static int GetAreaLevel(CArea area, AreaLevelKind kind) => kind switch
    {
        AreaLevelKind.City => area.GetCityLevel(),
        AreaLevelKind.Industry => area.GetIndustryLevel(),
        AreaLevelKind.Airport => area.GetAirportLevel(),
        AreaLevelKind.Port => area.GetPortLevel(),
        _ => 0,
    };

    private static PartialConditionResult EvaluateWithoutTarget(
        CardConditionDef condition, CCountry country)
    {
        if (condition is SourceCountryBuffCountConditionDef count)
            return CountCountryBuff(country, count.buff) < count.lessThan
                ? PartialConditionResult.True : PartialConditionResult.False;
        if (condition is not CardConditionGroupDef group)
            return PartialConditionResult.Unknown;

        if (group.mode == CardConditionGroupMode.Not)
        {
            PartialConditionResult child = EvaluateWithoutTarget(group.children[0], country);
            return child == PartialConditionResult.True ? PartialConditionResult.False :
                child == PartialConditionResult.False ? PartialConditionResult.True :
                PartialConditionResult.Unknown;
        }

        bool sawUnknown = false;
        foreach (CardConditionDef child in group.children)
        {
            PartialConditionResult result = EvaluateWithoutTarget(child, country);
            if (group.mode == CardConditionGroupMode.All && result == PartialConditionResult.False)
                return PartialConditionResult.False;
            if (group.mode == CardConditionGroupMode.Any && result == PartialConditionResult.True)
                return PartialConditionResult.True;
            if (result == PartialConditionResult.Unknown) sawUnknown = true;
        }
        if (sawUnknown) return PartialConditionResult.Unknown;
        return group.mode == CardConditionGroupMode.All
            ? PartialConditionResult.True : PartialConditionResult.False;
    }

    public static int CountCountryBuff(CCountry country, UnitBuffKind buff)
    {
        int count = 0;
        ARMY_CARD armyCard = ToArmyCard(buff);
        foreach (int areaId in country.areaIdList)
        {
            CArea area = g_Scene.GetArea(areaId);
            if (area == null) continue;
            for (int armyIndex = 0; armyIndex < area.armyCount; ++armyIndex)
                if (area.GetArmy(armyIndex)?.HasCard(armyCard) == true) ++count;
        }
        return count;
    }

    private static CArmy GetTargetArmy(CardEffectContext context, CardEffectDef effect)
    {
        if (context?.targetArea == null) return null;
        int armyIndex = effect switch
        {
            UnitBuffEffectDef unitBuff when unitBuff.target == UnitBuffTarget.FrontArmy => 0,
            ArmyHealthEffectDef health when health.target == ArmyEffectTarget.FrontArmy => 0,
            ArmyLevelEffectDef level when level.target == ArmyEffectTarget.FrontArmy => 0,
            _ => context.targetArmyIndex,
        };
        return context.targetArea.GetArmy(armyIndex);
    }

    internal static ARMY_CARD ToArmyCard(UnitBuffKind buff) => buff switch
    {
        UnitBuffKind.Assault => ASSAULT,
        UnitBuffKind.Defence => DEFENCE,
        UnitBuffKind.Carrier => CARRIER,
        UnitBuffKind.Commander => COMMANDER,
        _ => throw new ArgumentOutOfRangeException(nameof(buff)),
    };
}

public sealed class UnitBuffEffectHandler : ICardEffectHandler
{
    public bool CanApply(CardEffectContext context, CardEffectDef effect, out string errorCode)
    {
        errorCode = "";
        if (effect is not UnitBuffEffectDef buffEffect)
        {
            errorCode = "invalid-effect-definition";
            return false;
        }
        if (context.targetArea == null)
        {
            errorCode = "missing-target-area";
            return false;
        }
        if (!context.targetArea.info.enable)
        {
            errorCode = "target-disabled";
            return false;
        }
        if (context.targetArea.country != context.sourceCountry)
        {
            errorCode = "target-wrong-owner";
            return false;
        }

        int armyIndex = buffEffect.target == UnitBuffTarget.FrontArmy ? 0 : context.targetArmyIndex;
        CArmy army = context.targetArea.GetArmy(armyIndex);
        if (army == null)
        {
            errorCode = "missing-target-army";
            return false;
        }
        if (buffEffect.buff == UnitBuffKind.Commander && context.sourceCountry.commanderTurn > 0)
        {
            errorCode = "commander-cooling-down";
            return false;
        }
        return true;
    }

    public CardEffectResult Apply(CardEffectContext context, CardEffectDef effect)
    {
        if (!CanApply(context, effect, out string errorCode)) return CardEffectResult.Fail(errorCode);

        var buffEffect = (UnitBuffEffectDef)effect;
        int armyIndex = buffEffect.target == UnitBuffTarget.FrontArmy ? 0 : context.targetArmyIndex;
        CArmy army = context.targetArea.GetArmy(armyIndex);
        army.AddCard(CardConditionEvaluator.ToArmyCard(buffEffect.buff));

        if (buffEffect.buff == UnitBuffKind.Commander)
        {
            CCountry country = context.sourceCountry;
            country.commanderAlive = true;
            CommanderAbility ability = GetCommanderAbility(country.GetCommanderLevel());
            army.AddStrength(ability.armyAbility.instantRecovery);
            army.ResetMaxStrength(false);

            if (country.ai && g_GameManager.gameMode != CGameManager.MULTIPLAYER)
            {
                string commanderName = country.GetCommanderName();
                if (commanderName != null)
                {
                    string dialogue = Sprintf("commander return %ld", (long)(GetRand() % 5 + 1));
                    var gameState = (CGameState)CStateManager.Instance().GetStatePtr(GAME);
                    gameState.ShowDialogue(dialogue, commanderName, false);
                }
            }
        }
        return CardEffectResult.Ok();
    }

}

public sealed class ArmyHealthEffectHandler : ICardEffectHandler
{
    public bool CanApply(CardEffectContext context, CardEffectDef effect, out string errorCode)
    {
        errorCode = "";
        if (effect is not ArmyHealthEffectDef health)
        { errorCode = "invalid-effect-definition"; return false; }
        if (!TryValidateArmyTarget(context, out CArea area, out errorCode)) return false;

        foreach (int index in GetArmyIndices(area, context.targetArmyIndex, health.target))
        {
            CArmy army = area.GetArmy(index);
            if (army == null) continue;
            if (health.Amount > 0 && army.hp < army.GetMaxStrength()) return true;
            if (health.Amount < 0 && army.hp > 0) return true;
        }
        errorCode = health.Amount > 0 ? "target-army-full-health" : "target-army-no-health";
        return false;
    }

    public CardEffectResult Apply(CardEffectContext context, CardEffectDef effect)
    {
        if (!CanApply(context, effect, out string errorCode)) return CardEffectResult.Fail(errorCode);
        var health = (ArmyHealthEffectDef)effect;
        CArea area = context.targetArea;
        List<int> indices = GetArmyIndices(area, context.targetArmyIndex, health.target).ToList();
        // Removing a dead unit shifts the stack, so damage is applied from the back.
        if (health.Amount < 0) indices.Reverse();
        foreach (int index in indices)
        {
            CArmy army = area.GetArmy(index);
            if (army == null) continue;
            int amount = health.IsPercent
                ? health.Amount * army.GetMaxStrength() / 100
                : health.Amount;
            if (amount >= 0) army.AddStrength(amount);
            else area.LostArmyStrength(index, -amount);
        }
        return CardEffectResult.Ok();
    }

    internal static bool TryValidateArmyTarget(
        CardEffectContext context, out CArea area, out string errorCode)
    {
        area = context?.targetArea;
        errorCode = "";
        if (area == null) { errorCode = "missing-target-area"; return false; }
        if (!area.info.enable) { errorCode = "target-disabled"; return false; }
        if (area.country != context.sourceCountry)
        { errorCode = "target-wrong-owner"; return false; }
        if (area.armyCount <= 0) { errorCode = "missing-target-army"; return false; }
        return true;
    }

    internal static IEnumerable<int> GetArmyIndices(
        CArea area, int selectedIndex, ArmyEffectTarget target)
    {
        if (target == ArmyEffectTarget.AllArmies)
        {
            for (int index = 0; index < area.armyCount; ++index) yield return index;
            yield break;
        }
        yield return target == ArmyEffectTarget.FrontArmy ? 0 : selectedIndex;
    }
}

public sealed class ArmyLevelEffectHandler : ICardEffectHandler
{
    public bool CanApply(CardEffectContext context, CardEffectDef effect, out string errorCode)
    {
        errorCode = "";
        if (effect is not ArmyLevelEffectDef levelEffect)
        { errorCode = "invalid-effect-definition"; return false; }
        if (!ArmyHealthEffectHandler.TryValidateArmyTarget(context, out CArea area, out errorCode))
            return false;

        foreach (int index in ArmyHealthEffectHandler.GetArmyIndices(
                     area, context.targetArmyIndex, levelEffect.target))
        {
            CArmy army = area.GetArmy(index);
            if (army == null) continue;
            if (levelEffect.levels > 0 && army.level < MAX_ARMY_LEVEL) return true;
            if (levelEffect.levels < 0 && army.level > 0) return true;
        }
        errorCode = levelEffect.levels > 0 ? "target-army-max-level" : "target-army-min-level";
        return false;
    }

    public CardEffectResult Apply(CardEffectContext context, CardEffectDef effect)
    {
        if (!CanApply(context, effect, out string errorCode)) return CardEffectResult.Fail(errorCode);
        var levelEffect = (ArmyLevelEffectDef)effect;
        CArea area = context.targetArea;
        foreach (int index in ArmyHealthEffectHandler.GetArmyIndices(
                     area, context.targetArmyIndex, levelEffect.target))
            area.GetArmy(index)?.AdjustLevel(levelEffect.levels);
        return CardEffectResult.Ok();
    }
}

public sealed class ResearchEffectHandler : ICardEffectHandler
{
    public bool CanApply(CardEffectContext context, CardEffectDef effect, out string errorCode)
    {
        errorCode = "";
        if (effect is not ResearchEffectDef research)
        { errorCode = "invalid-effect-definition"; return false; }
        if (context?.sourceCountry == null)
        { errorCode = "missing-source-country"; return false; }
        if (context.sourceCountry.techTurn > 0)
        { errorCode = "research-in-progress"; return false; }
        if (context.sourceCountry.tech >= research.maxLevel)
        { errorCode = "research-max-level"; return false; }
        return true;
    }

    public CardEffectResult Apply(CardEffectContext context, CardEffectDef effect)
    {
        if (!CanApply(context, effect, out string errorCode)) return CardEffectResult.Fail(errorCode);
        var research = (ResearchEffectDef)effect;
        if (research.turns == 0) context.sourceCountry.tech++;
        else context.sourceCountry.techTurn = research.turns;
        return CardEffectResult.Ok();
    }
}

public sealed class AreaStructureEffectHandler : ICardEffectHandler
{
    public bool CanApply(CardEffectContext context, CardEffectDef effect, out string errorCode)
    {
        errorCode = "";
        if (effect is not AreaStructureEffectDef structureEffect)
        {
            errorCode = "invalid-effect-definition";
            return false;
        }
        if (context.targetArea == null)
        {
            errorCode = "missing-target-area";
            return false;
        }

        CArea area = context.targetArea;
        if (!area.info.enable)
        {
            errorCode = "target-disabled";
            return false;
        }
        if (area.country != context.sourceCountry)
        {
            errorCode = "target-wrong-owner";
            return false;
        }
        if (area.info.sea)
        {
            errorCode = "target-wrong-terrain";
            return false;
        }

        if (structureEffect.slot == AreaStructureSlot.Installation)
            return CanApplyInstallation(area, structureEffect, out errorCode);

        if (structureEffect.operation == CardEffectOperation.Clear)
        {
            if (area.info.constructionType == NO_CONSTRUCTION)
            {
                errorCode = "no-construction-to-destroy";
                return false;
            }
            return true;
        }

        if (area.info.constructionType != NO_CONSTRUCTION &&
            area.info.constructionType != structureEffect.structure)
        {
            errorCode = "construction-type-mismatch";
            return false;
        }

        int maximum = GetMaximumLevel(structureEffect.structure);
        if (maximum == 0)
        {
            errorCode = "invalid-construction-type";
            return false;
        }
        if (structureEffect.toMaximum)
        {
            if (area.info.constructionType == structureEffect.structure &&
                area.info.constructionLevel >= maximum)
            {
                errorCode = "construction-max-level";
                return false;
            }
            return true;
        }
        if (structureEffect.levelDelta > 0)
        {
            if (area.info.constructionType == structureEffect.structure &&
                area.info.constructionLevel >= maximum)
            {
                errorCode = "construction-max-level";
                return false;
            }
            return true;
        }
        if (structureEffect.levelDelta < 0)
        {
            if (area.info.constructionType != structureEffect.structure || area.info.constructionLevel <= 0)
            {
                errorCode = "construction-min-level";
                return false;
            }
            return true;
        }

        errorCode = "invalid-construction-adjustment";
        return false;
    }

    public CardEffectResult Apply(CardEffectContext context, CardEffectDef effect)
    {
        if (!CanApply(context, effect, out string errorCode)) return CardEffectResult.Fail(errorCode);

        var structureEffect = (AreaStructureEffectDef)effect;
        CArea area = context.targetArea;
        if (structureEffect.slot == AreaStructureSlot.Installation)
        {
            area.installationType = structureEffect.operation == CardEffectOperation.Clear
                ? NO_INSTALLATION
                : structureEffect.installation;
            return CardEffectResult.Ok();
        }
        if (structureEffect.operation == CardEffectOperation.Clear)
        {
            area.DestroyConstruction();
            return CardEffectResult.Ok();
        }

        int maximum = GetMaximumLevel(structureEffect.structure);
        if (area.info.constructionType == NO_CONSTRUCTION)
        {
            area.Construct((int)structureEffect.structure);
            if (structureEffect.toMaximum)
            {
                area.info.constructionLevel = maximum;
                return CardEffectResult.Ok();
            }

            int remainingIncrease = structureEffect.levelDelta - 1;
            if (remainingIncrease > 0)
                area.info.constructionLevel = Math.Min(maximum, area.info.constructionLevel + remainingIncrease);
            return CardEffectResult.Ok();
        }

        if (structureEffect.toMaximum)
        {
            area.info.constructionLevel = maximum;
        }
        else
        {
            area.info.constructionLevel = Math.Clamp(
                area.info.constructionLevel + structureEffect.levelDelta, 0, maximum);
            if (area.info.constructionLevel == 0) area.DestroyConstruction();
        }
        return CardEffectResult.Ok();
    }

    private static bool CanApplyInstallation(
        CArea area, AreaStructureEffectDef effect, out string errorCode)
    {
        errorCode = "";
        if (effect.operation == CardEffectOperation.Set)
        {
            if (effect.installation == NO_INSTALLATION)
            {
                errorCode = "invalid-installation-type";
                return false;
            }
            if (area.installationType != NO_INSTALLATION)
            {
                errorCode = "installation-already-exists";
                return false;
            }
            return true;
        }
        if (effect.operation == CardEffectOperation.Clear)
        {
            if (area.installationType == NO_INSTALLATION)
            {
                errorCode = "no-installation-to-destroy";
                return false;
            }
            return true;
        }

        errorCode = "invalid-installation-operation";
        return false;
    }

    private static int GetMaximumLevel(CONSTRUCTION_TYPE structure) => structure switch
    {
        CONSTRUCTION_TYPE.CITY => MAX_CITY_LEVEL,
        CONSTRUCTION_TYPE.INDUSTRY => MAX_INDUSTRY_LEVEL,
        CONSTRUCTION_TYPE.AIRPORT => MAX_AIRPORT_LEVEL,
        _ => 0,
    };
}
