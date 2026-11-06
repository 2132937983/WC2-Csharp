namespace WC2;

/// <summary>
/// A unit stack on the map (port of CArmy.cpp). GetNumDices comes from FightAssist.cpp,
/// TurnBegin/TurnEnd from GameTurnBegin.cpp / GameTurnEnd.cpp and SaveArmy/LoadArmy from GameLoadSave.cpp.
/// </summary>
public sealed partial class CArmy
{
    public ArmyDef basicAbilities;
    public CCountry country;
    public int hp;
    public int maxHp;
    public int movement;
    public int cards;
    public int level;
    public int exp;
    public int morale;
    public int moraleUpTurn;
    public float direction;
    public bool active;

    private static readonly ArmyAbility[] abilities =
    {
        new ArmyAbility(0, 0, 0, 0, 0),
        new ArmyAbility(1, 0, 0, 10, 0),
        new ArmyAbility(1, 1, 0, 20, 0),
        new ArmyAbility(1, 1, 0, 30, 20),
        new ArmyAbility(1, 1, 2, 40, 20),
    };

    /// <summary>Per level ability table (attack, defence, turn recovery, instant recovery, max hp bonus %).</summary>
    public static ArmyAbility GetArmyAbility(int level)
    {
        if (level > MAX_ARMY_LEVEL) level = MAX_ARMY_LEVEL;
        if (level < 0) level = 0;
        return abilities[level];
    }

    public CArmy() { }

    /// <summary>Whether the unit carries the tactic card.</summary>
    public bool HasCard(ARMY_CARD CardID) => ((cards >> (int)CardID) & 1) != 0;
    public void AddCard(ARMY_CARD CardID) => cards |= 1 << (int)CardID;
    public void RemoveCard(ARMY_CARD CardID) => cards &= ~(1 << (int)CardID);

    public void Init(ArmyDef armyDef, CCountry country_in)
    {
        basicAbilities = armyDef;
        country = country_in;
        hp = armyDef.hp;
        maxHp = armyDef.hp;
        cards = 0;
        level = 0;
        movement = armyDef.movement;
        exp = 0;
        morale = (int)MORALE_NORMAL;
        moraleUpTurn = 0;
        direction = 1.0f;
        active = false;
        ResetMaxStrength(false);
    }

    /// <summary>Recomputes the max hp.</summary>
    /// <param name="keepHpValue">keep the current hp value</param>
    public void ResetMaxStrength(bool keepHpValue)
    {
        if (country == null) return;
        int tmpHpPercentage = GetArmyAbility(level).maxHpPercentage;
        if (HasCard(COMMANDER))
        {
            CommanderAbility commanderAbility = GetCommanderAbility(country.GetCommanderLevel());
            // use the commander bonus when it is larger
            if (tmpHpPercentage < commanderAbility.armyAbility.maxHpPercentage)
                tmpHpPercentage = commanderAbility.armyAbility.maxHpPercentage;
        }
        int tmpMaxHp = ((tmpHpPercentage + 100) * basicAbilities.hp) / 100;
        // when the hp is not kept it is rescaled
        if (!keepHpValue && maxHp > 0)
        {
            hp = (hp * tmpMaxHp) / maxHp;
        }

        maxHp = tmpMaxHp;
        if (hp > maxHp) hp = maxHp;
    }

    /// <summary>Heals the unit by HP (never above the max strength).</summary>
    public void AddStrength(int HP)
    {
        hp += HP;
        // not above full health
        if (hp > GetMaxStrength()) hp = GetMaxStrength();
    }

    /// <summary>High morale for two turns.</summary>
    public void Breakthrough()
    {
        // morale high
        morale = (int)MORALE_HIGH;
        // lasts 2 turns
        moraleUpTurn = 2;
    }

    public int GetMaxStrength() => maxHp;

    /// <summary>Whether the unit is at full health.</summary>
    public bool IsFullHealth() => hp == GetMaxStrength();

    public float CalcHpRate() => (float)hp / ((float)GetMaxStrength() * 1.0f);

    /// <summary>Applies damage.</summary>
    /// <param name="damage">damage taken</param>
    /// <returns>whether the unit ended at 0 hp</returns>
    public bool LostStrength(int damage)
    {
        if (hp - damage < 0) hp = 0;
        else hp = hp - damage;
        return hp <= 0;
    }

    public bool IsNavy()
    {
        // may change in the future
        return basicAbilities != null && basicAbilities.IsNavy();
    }

    /// <summary>Adds experience to the unit.</summary>
    public void AddExp(int Exp)
    {
        exp += Exp;
        // max level, nothing to gain
        if (level >= MAX_ARMY_LEVEL) return;

        int[] exp_needs = { 90, 150, 200, 300 };

        // the original penalised the navy, this port levels by hp percentage so no penalty is needed
        // the original values were about 100/150/200/250

        // not enough experience
        if (exp < (exp_needs[level] * maxHp / 100)) return;
        exp -= exp_needs[level];
        Upgrade();
        g_SoundRes.PlayCharSE(LV_UP_WAV);
    }

    public void Upgrade()
    {
        if (level != MAX_ARMY_LEVEL)
        {
            level += 1;
            AddStrength(GetArmyAbility(level).instantRecovery);
            ResetMaxStrength(false);
        }
    }

    /// <summary>Sets the morale.</summary>
    public void SetMorale(int Morale)
    {
        morale = Morale;
        if (morale != (int)MORALE_HIGH) moraleUpTurn = 0;
    }

    /// <summary>Poison damage: halves the hp (never below one so poison cannot kill).</summary>
    public void Poisoning()
    {
        if (hp > 1) hp >>= 1;
    }

    // ---- FightAssist.cpp -----------------------------------------------------------------

    /// <summary>Number of combat dice for the given hp.</summary>
    /// <param name="curHp">current hp</param>
    public int GetNumDices(int curHp)
    {
        int hereMaxHp = GetMaxStrength();
        if (hereMaxHp <= 0) hereMaxHp = 1; // guard against a division by zero
        int x = curHp * 100 / hereMaxHp;
        // original thresholds are inclusive: 1 die up to 5 %, 2 up to 15 %, 3 up to 25 %, 4 up to 50 %
        if (x > 50) return 5;
        if (x > 25) return 4;
        if (x > 15) return 3;
        return x > 5 ? 2 : 1;
    }

    /// <summary>Number of combat dice.</summary>
    public int GetNumDices() => GetNumDices(hp);

    /// <summary>
    /// Used by the second combat (e.g. land units countering ships with the fort guns).
    /// </summary>
    /// <param name="damage">damage the unit takes</param>
    /// <returns>number of dice</returns>
    public int GetNumDicesIfLostStrength(int damage)
    {
        // original: no dice left when the damage reaches the hp
        if (damage >= hp) return 0;
        return GetNumDices(hp - damage);
    }

    // ---- GameLoadSave.cpp ----------------------------------------------------------------

    /// <summary>Stores the unit into saveArmyInfo.</summary>
    public void SaveArmy(SaveArmyInfo saveArmyInfo)
    {
        saveArmyInfo.id = basicAbilities.id;
        saveArmyInfo.hp = hp;
        saveArmyInfo.maxHp = maxHp;
        saveArmyInfo.movement = movement;
        saveArmyInfo.cards = cards;
        saveArmyInfo.level = level;
        saveArmyInfo.exp = exp;
        saveArmyInfo.morale = morale;
        saveArmyInfo.moraleUpTurn = moraleUpTurn;
        saveArmyInfo.direction = direction;
        saveArmyInfo.active = active;
    }

    /// <summary>Restores the unit from saveArmyInfo.</summary>
    public void LoadArmy(SaveArmyInfo saveArmyInfo)
    {
        hp = saveArmyInfo.hp;
        maxHp = saveArmyInfo.maxHp;
        cards = saveArmyInfo.cards;
        level = saveArmyInfo.level;
        exp = saveArmyInfo.exp;
        morale = saveArmyInfo.morale;
        moraleUpTurn = saveArmyInfo.moraleUpTurn;
        direction = saveArmyInfo.direction;
        movement = saveArmyInfo.movement;
        // movement sanity check
        if (movement < 0) movement = 0;
        active = saveArmyInfo.active;
        // the original recomputes the max hp after loading (commander level may have changed)
        ResetMaxStrength(false);
    }

    // ---- GameTurnBegin.cpp / GameTurnEnd.cpp ---------------------------------------------

    /// <summary>Unit turn start.</summary>
    public void TurnBegin()
    {
        movement = basicAbilities.movement;
        active = true;
    }

    /// <summary>Unit turn end: updates the morale buff duration.</summary>
    public void TurnEnd()
    {
        movement = 0;
        active = false;
        if (moraleUpTurn > 0)
        {
            // morale timer
            moraleUpTurn -= 1;
            if (moraleUpTurn == 0) morale = 0;
        }
    }
}
