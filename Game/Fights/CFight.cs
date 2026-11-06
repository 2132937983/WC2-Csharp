namespace WC2;

/// <summary>
/// Combat resolution (CFight.h). The methods are split like the original sources:
/// FightLogics.cs (attack flow / result), FightAssist.cs (counter, buffs, dice) and AirStrikes.cs.
/// </summary>
public sealed partial class CFight
{
    public List<int> startArmyDices = new();
    public List<int> targetArmyDices = new();
    public CCountry airStrikeCountry;
    public int startArmyNumDicesLost;
    public int targetArmyNumDicesLost;
    public int startArmyDefence;
    public int startArmyExtraAttack;
    public int targetArmyDefence;
    public int targetArmyExtraAttack;
    public int x34;
    public int x38;
    public int startAreaId;
    public int targetAreaId;
    public int startArmyNumDices;
    public int targetArmyNumDices;
    public int startArmyNumDices1;
    public int targetArmyNumDices1;
    public int startArmyReceiveDamage;
    public int targetArmyReceiveDamage;
    public int startArmyReceiveDamageFirst;
    public int targetArmyReceiveDamageFirst;
    public int x64;
    public int x68;
    public int startArmyMaxAttack;
    public int startArmyMinAttack;
    public int targetArmyMaxAttack;
    public int targetArmyMinAttack;
    public int airStrikeAttack;
    public int airStrikeType;
    public bool canCounter;
    public int secondAttackIndex;
    public bool startArmySecondAttack;
    public bool targetArmySecondAttack;
    public int secondAttackSideIndex;
    public bool battleStarted;
    /// <summary>Shortest adjacency-graph distance from the start area to the target; -1 when there is no valid start (air strikes).</summary>
    public int topologicalDistance = -1;
    /// <summary>Whether this normal attack actually triggered a counter attack; a second attack does not count.</summary>
    public bool counterOccurred;
    /// <summary>Air strike payload type; 0 for a normal fight.</summary>
    public int airStrikePayload;
    /// <summary>Whether the special attack was started by a unit on the map instead of a card (air strikes for now).</summary>
    public bool specialAttackFromUnit;
    /// <summary>Movement cost of a unit special attack; &lt;= 0 clears the movement after the fight.</summary>
    public int airStrikeAttackCost = -1;

    public CFight()
    {
        targetArmyDices.Clear();
        startArmyDices.Clear();
        topologicalDistance = -1;
        counterOccurred = false;
        airStrikePayload = 0;
        specialAttackFromUnit = false;
        // -1 means "clear the movement"; keep the field away from 0 even when this is not an air strike.
        airStrikeAttackCost = -1;
    }
}
