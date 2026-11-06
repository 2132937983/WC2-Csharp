namespace WC2;

/// <summary>Air strike damage setup (AirStrikes.cpp).</summary>
public sealed partial class CFight
{
    /// <summary>
    /// Air strike attack, regardless of whether a unit or a card started it.
    /// </summary>
    /// <param name="attacker">attacking country</param>
    /// <param name="TargetAreaId">target area</param>
    /// <param name="type">payload type (1 air strike, 2 bomber, 3 nuclear)</param>
    public void AirStrikesAttack(CCountry attacker, int TargetAreaId, int type)
    {
        CArea target_area = g_Scene.GetArea(TargetAreaId);
        targetAreaId = TargetAreaId;
        startAreaId = -1;
        topologicalDistance = -1;
        int minAttack, maxAttack;
        ArmyDef tmpArmyDef;

        // types 1 and 2 are air strike and bombing, each with its own values
        if (type == 1 || type == 2)
        {
            if (type == 1)
            {
                tmpArmyDef = CObjectDef.Instance().GetArmyDef((int)AIRSTRIKE_ARMY, attacker.name);
            }
            else
            {
                tmpArmyDef = CObjectDef.Instance().GetArmyDef((int)BOMBER_ARMY, attacker.name);
            }
            if (tmpArmyDef != null)
            {
                minAttack = tmpArmyDef.minAttack;
                maxAttack = tmpArmyDef.maxAttack;
            }
            else
            {
                minAttack = 0;
                maxAttack = 0;
            }
        }
        else
        {
            minAttack = 0;
            maxAttack = 0;
        }
        // the air force medal adds base damage
        if (attacker.GetWarMedalLevel(AIR_FORCE_MEDAL) > 0)
        {
            maxAttack++;
            minAttack++;
        }
        startArmyReceiveDamage = 0;
        airStrikeAttack = GetRand() % (maxAttack - minAttack + 1) + minAttack;
        // air strike / bombing damage is always a multiple of 5
        targetArmyReceiveDamage = 5 * airStrikeAttack;
        if (type == 3)
        {
            // a nuclear bomb does not "damage" units, it removes them
            targetArmyReceiveDamage = 0;
        }
        else
        {
            if (target_area.installationType == ANTIAIRCRAFT)
            {
                // anti-aircraft guns reduce the damage by 70%
                targetArmyReceiveDamage = 3 * airStrikeAttack / 2;
            }
            else if (target_area.installationType == RADAR)
            {
                // a radar is a bit weaker: 50%
                targetArmyReceiveDamage /= 2;
            }
        }
        // air strikes never start a second attack
        if (type < (int)COMBAT_AIRSTRIKE_PAYLOAD_AIRSTRIKE ||
            type > (int)COMBAT_AIRSTRIKE_PAYLOAD_NUCLEAR)
        {
            type = (int)COMBAT_AIRSTRIKE_PAYLOAD_AIRSTRIKE;
        }
        airStrikeType = type;
        airStrikePayload = type;
        specialAttackFromUnit = false;
        // Card air strikes do not cost the starting unit movement; -1 doubles as the "clear movement"
        // sentinel so a reused field never mistakes 0 for a free air strike.
        airStrikeAttackCost = -1;
        secondAttackIndex = 0;
        startArmySecondAttack = false;
        targetArmySecondAttack = false;
        canCounter = false;
        airStrikeCountry = attacker;
    }

    /// <summary>
    /// Air strike started by a unit (only the aircraft carrier in the original).
    /// </summary>
    /// <param name="StartAreaId">area of the striking unit</param>
    /// <param name="TargetAreaId">target area</param>
    public void AirStrikesAttack(int StartAreaId, int TargetAreaId)
    {
        AirStrikesAttack(StartAreaId, TargetAreaId, (int)COMBAT_AIRSTRIKE_PAYLOAD_AIRSTRIKE);
    }

    /// <summary>
    /// Air strike started by a map unit.
    /// </summary>
    /// <param name="StartAreaId">area of the striking unit</param>
    /// <param name="TargetAreaId">target area</param>
    /// <param name="payloadType">air strike payload type</param>
    public void AirStrikesAttack(int StartAreaId, int TargetAreaId, int payloadType)
    {
        CArea startArea = g_Scene.GetArea(StartAreaId);
        if (startArea == null || startArea.country == null)
        {
            return;
        }
        AirStrikesAttack(startArea.country, TargetAreaId, payloadType);
        startAreaId = StartAreaId;
        // air strikes never counter, so the topological distance of a normal fight is not computed
        topologicalDistance = -1;
        specialAttackFromUnit = true;
        airStrikePayload = airStrikeType;
        CArmy startArmy = startArea.GetArmy(0);
        int configuredAttackCost =
            startArmy == null || startArmy.basicAbilities == null
                ? 1
                : startArmy.basicAbilities.combat.attack.attackCost;
        // runtime fallback so that an external 0 never produces a free air strike
        airStrikeAttackCost = configuredAttackCost <= 0 ? -1 : configuredAttackCost;
    }
}
