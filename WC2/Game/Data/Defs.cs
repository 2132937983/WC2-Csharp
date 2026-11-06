namespace WC2;

/// <summary>Static per-area data from the map definition.</summary>
public sealed class AreaInfo
{
    public AREA_TYPE areaType;
    public int tax;
    public int[] armyPos = new int[2];
    public int[] constructionPos = new int[2];
    public int[] installationPos = new int[2];
    public bool enable;
    public bool sea;
    public CONSTRUCTION_TYPE constructionType;
    public int constructionLevel;

    public AreaInfo() { }

    public AreaInfo(AREA_TYPE type, int taxIn, int[] armyPosIn = null)
    {
        areaType = type;
        tax = taxIn;
        if (armyPosIn != null) { armyPos[0] = armyPosIn[0]; armyPos[1] = armyPosIn[1]; }
    }

    public AreaInfo(AREA_TYPE type, int taxIn, int armyX, int armyY) : this(type, taxIn, new[] { armyX, armyY }) { }

    public AreaInfo Clone()
    {
        return new AreaInfo
        {
            areaType = areaType,
            tax = tax,
            armyPos = (int[])armyPos.Clone(),
            constructionPos = (int[])constructionPos.Clone(),
            installationPos = (int[])installationPos.Clone(),
            enable = enable,
            sea = sea,
            constructionType = constructionType,
            constructionLevel = constructionLevel,
        };
    }

    public void CopyFrom(AreaInfo other)
    {
        if (other == null) return;
        areaType = other.areaType; tax = other.tax;
        Array.Copy(other.armyPos, armyPos, 2);
        Array.Copy(other.constructionPos, constructionPos, 2);
        Array.Copy(other.installationPos, installationPos, 2);
        enable = other.enable; sea = other.sea;
        constructionType = other.constructionType; constructionLevel = other.constructionLevel;
    }
}

public sealed class ArmyAbility
{
    public int attack;
    public int defence;
    public int turnRecovery;
    public int instantRecovery;
    public int maxHpPercentage;

    public ArmyAbility() { }
    public ArmyAbility(int attack, int defence, int turnRecovery, int instantRecovery, int maxHpPercentage)
    {
        this.attack = attack; this.defence = defence; this.turnRecovery = turnRecovery;
        this.instantRecovery = instantRecovery; this.maxHpPercentage = maxHpPercentage;
    }
}

public sealed class CommanderAbility
{
    public ArmyAbility armyAbility = new();
    public int coolDown;
}

public sealed class CNationInfo
{
    public int money;
    public int industry;
    public int tech;
    public bool[] canUseCard = new bool[(int)CARD_ID.VALID_CARD_COUNT + 10];
}

public sealed class CommanderData
{
    public int level;
    public int[] warMedal = new int[6];
}

public sealed class CommanderDef
{
    public string name = "";
    public string country = "";
    public int rank;
    public int[] medal = new int[6];
}

public sealed class FlagInfo
{
    public string name = "";
    public float x;
    public float y;
    public float rot;
    public float scale = 1f;
}

public sealed class BattleDef
{
    public string name = "";
    public string age = "";
    public float centerX;
    public float centerY;
    public float ageX;
    public float ageY;
    public int victory;
    public int greatVictory;
    public List<FlagInfo> flag = new();
    public List<FlagInfo> arrow = new();
}

public sealed class ConquestDef
{
    public string name = "";
    public float centerX;
    public float centerY;
    public List<FlagInfo> flag = new();
}

public sealed class DialogueDef
{
    public string commander = "";
    public int index;
    public int atRound;
    public bool left;
}

public sealed class GeneralPhoto
{
    public string fileName = "";
    public float w;
    public float h;
    public float refx;
}

public sealed class UnitMotion
{
    public string name = "";
    public float at;
    public int index;
    public float fireX;
    public float fireY;
}

public sealed class UnitMotions
{
    public string res = "";
    public float dir;
    public string fireEffect = "";
    public List<UnitMotion> standByMotions = new();
    public List<UnitMotion> attackMotions = new();
    public List<UnitMotion> destroyedMotions = new();
}

public sealed class UnitPositions
{
    public float[] x = new float[5];
    public float[] y = new float[5];
    public float[] scale = new float[5];
}

public sealed class Belligerent
{
    public string id = "";
    public string name = "";
    public string commander = "";
    public int alliance;
}

/// <summary>Binary save game header (GameLoadSave.cpp). Field order matches the original struct.</summary>
public sealed class SaveHeader
{
    public int easy;
    public int eq1;
    public int gameMode;
    public int eq0;
    public int mapId;
    public string areasEnable = "";              // char[32]
    public string[] playerCountryName = NewNames(8); // char[8][4]
    public string battleFileName = "";           // char[32]
    public float xCamera;
    public float yCamera;
    public float scaleCamera;
    public int currentCountryIndex;
    public int currentDialogueIndex;
    public int countryCount;
    public int areaCount;
    public int currentTurnNumMinusOne;
    public int randomRewardMedal;
    public int year;
    public int month;
    public int day;
    public int hour;
    public int min;
    public int campaignSeriesId;
    public int campaignLevelId;
    public int victoryTurn;
    public int greatVictoryTurn;
    public int randSeed;
    public bool isPause;

    private static string[] NewNames(int n)
    {
        var a = new string[n];
        for (int i = 0; i < n; i++) a[i] = "";
        return a;
    }
}

public sealed class SaveArmyInfo
{
    public int id;
    public int hp;
    public int movement;
    public int cards;
    public int maxHp;
    public int level;
    public int exp;
    public int morale;
    public int moraleUpTurn;
    public float direction;
    public bool active;
}

public sealed class SaveAreaInfo
{
    public int id;
    public int countryIndex;
    public int armyCount;
    public int constructionType;
    public int constructionLevel;
    public int installationType;
    public SaveArmyInfo[] army = { new(), new(), new(), new() };
}

public sealed class SaveCountryInfo
{
    public int money;
    public int industry;
    public int tech;
    public int techTurn;
    public bool ai;
    public int alliance;
    public int defeatType;
    public int[] oldCardRound = new int[28];
    public string id = "";       // char[16]
    public string name = "";     // char[16]
    public byte r, g, b, a;
    public float taxRate;
    public int[] destroyCount = new int[10];
    public int[] warMedal = new int[6];
    public string commanderName = ""; // char[24]
    public int commanderTurn;
    public bool commanderAlive;
    public bool borrowedLoan;
    public bool defeated;
    public int[] cardRound = new int[128];
    public int[] cardTimes = new int[128];
}

/// <summary>AI search node.</summary>
public struct NODE
{
    public int actionToNextAreaTargetId;
    public int targetValue;
    public int actionToNextAreaArmyIndex;
    public bool targetIsPlayer;
}

public sealed class ActionNode
{
    public enum ACTION_NODE_TYPE
    {
        CARD_NODE = 0x10010,
        MOVE_NODE = 0x10020,
        ATTACK_NODE = 0x10030,
        MOVE_FRONT_NODE = 0x10050,
        INV_NODE = 0x10040,
        CLEAR_NODE = 0x10000,
    }

    public const ACTION_NODE_TYPE CARD_NODE = ACTION_NODE_TYPE.CARD_NODE;
    public const ACTION_NODE_TYPE MOVE_NODE = ACTION_NODE_TYPE.MOVE_NODE;
    public const ACTION_NODE_TYPE ATTACK_NODE = ACTION_NODE_TYPE.ATTACK_NODE;
    public const ACTION_NODE_TYPE MOVE_FRONT_NODE = ACTION_NODE_TYPE.MOVE_FRONT_NODE;
    public const ACTION_NODE_TYPE INV_NODE = ACTION_NODE_TYPE.INV_NODE;
    public const ACTION_NODE_TYPE CLEAR_NODE = ACTION_NODE_TYPE.CLEAR_NODE;

    public CARD_ID cardId;
    public int startAreaId;
    public int targetAreaId;
    public ACTION_NODE_TYPE actionType;
    public int x1888;
    public int armyIndex;
}

/// <summary>One queued action of a country (move, attack, card ...).</summary>
public sealed class CountryAction
{
    public enum ACTION_TYPE
    {
        NO_ACTION, MOVE_ACTION, INACTIVATE_ACTION, ATTACK_ACTION, MOVE_FRONT_ACTION, CARD_ACTION, CAMERA_ACTION,
    }

    public const ACTION_TYPE NO_ACTION = ACTION_TYPE.NO_ACTION;
    public const ACTION_TYPE MOVE_ACTION = ACTION_TYPE.MOVE_ACTION;
    public const ACTION_TYPE INACTIVATE_ACTION = ACTION_TYPE.INACTIVATE_ACTION;
    public const ACTION_TYPE ATTACK_ACTION = ACTION_TYPE.ATTACK_ACTION;
    public const ACTION_TYPE MOVE_FRONT_ACTION = ACTION_TYPE.MOVE_FRONT_ACTION;
    public const ACTION_TYPE CARD_ACTION = ACTION_TYPE.CARD_ACTION;
    public const ACTION_TYPE CAMERA_ACTION = ACTION_TYPE.CAMERA_ACTION;

    public ACTION_TYPE actionType;
    public CARD_ID cardId;
    public int startAreaId;
    public int targetAreaId;
    public int armyIndex;

    public CountryAction() { }

    public CountryAction(ACTION_TYPE type, CARD_ID card, int startArea, int targetArea, int armyIdx)
    {
        actionType = type; cardId = card; startAreaId = startArea; targetAreaId = targetArea; armyIndex = armyIdx;
    }

    public CountryAction Clone() => new CountryAction(actionType, cardId, startAreaId, targetAreaId, armyIndex);

    public void CopyFrom(CountryAction o)
    {
        if (o == null) return;
        actionType = o.actionType; cardId = o.cardId; startAreaId = o.startAreaId; targetAreaId = o.targetAreaId; armyIndex = o.armyIndex;
    }
}

public sealed class FinishedEvent
{
    public bool finished_event_choose_AI;
    public bool finished_event_difficulty_selection;
}

/// <summary>Layout of the original fixed card table.</summary>
public class DeprecatedCardDef
{
    public string name = "";
    public string image = "";
    public string intro = "";
    public CARD_ID id;
    public CARD_TYPE type;
    public int price;
    public int industry;
    public int round;
    public int tech;
}

/// <summary>Card definition from carddef.xml (per country).</summary>
public class CardDef : DeprecatedCardDef
{
    public int relatedArmyId;
    public int maxTimes;

    // The following helpers are currently unused (CardDefs.cpp keeps SetCardRound, GetCardMaxTimes,
    // CheckIfTimesFull and DealWithCardRoundAndTimes commented out; they are ported from that code).

    public int GetCardRound() => round;

    public void SetCardRound(CCountry country_in)
    {
        int round_here;
        if (id != COMMANDER_CARD)
        {
            if (CheckIfTimesFull(country_in)) round_here = GetCardRoundIfTimesFull();
            else round_here = GetCardRound();
            if (round_here > 0)
            {
                if (type == AIR_FORCE_TYPE_CARD)
                    country_in.cardRound[(int)id] = country_in.GetWarMedalLevel(AIR_FORCE_MEDAL) > 2 ? 0 : round_here;
                else
                    country_in.cardRound[(int)id] = round_here;
            }
        }
    }

    public int GetCardMaxTimes() => maxTimes;

    public bool CheckIfTimesFull(CCountry country_in)
    {
        if (maxTimes <= 0) return false;
        return country_in.cardTimes[(int)id] >= GetCardMaxTimes();
    }

    public int GetCardRoundIfTimesFull()
    {
        if (round <= 1) return 1;
        return round;
    }

    public void DealWithCardRoundAndTimes(CCountry country_in)
    {
        PlusTimesCount(country_in);
        SetCardRound(country_in);
    }

    public void PlusTimesCount(CCountry country_in) => country_in.cardTimes[(int)id]++;
}

public sealed class ToNewCardDef
{
    public Dictionary<int, CardDef> mapCarddef = new();
    /// <summary>[0] land troop card order, [1] navy troop card order.</summary>
    public List<CARD_ID>[] troopOrder = { new(), new() };
}

// ---- army definitions ----------------------------------------------------------------------

public sealed class ArmyCombatRange
{
    public COMBAT_RANGE_SHAPE shape;
    /// <summary>Fixed point: graph 1000 = one step, circle 1000 = map distance 100.</summary>
    public ushort minRange;
    public ushort maxRange;
    public bool maxInclusive;
    public bool radiusFromCountry;

    public ArmyCombatRange Clone() => (ArmyCombatRange)MemberwiseClone();
}

public sealed class ArmyCombatProfile
{
    public COMBAT_ATTACK_MODE mode;
    public ArmyCombatRange range = new();
    public COMBAT_ENGAGEMENT_MODE engagementMode;
    public COMBAT_AIRSTRIKE_PAYLOAD payloadType;
    /// <summary>Movement cost of a special attack; &lt;= 0 clears the movement.</summary>
    public int attackCost;

    public ArmyCombatProfile Clone()
    {
        var c = (ArmyCombatProfile)MemberwiseClone();
        c.range = range.Clone();
        return c;
    }
}

public sealed class ArmyCombatRules
{
    public ArmyCombatProfile attack = new();
    public ArmyCombatProfile counter = new();
    public bool hasExplicitAttack;
    public bool hasExplicitCounter;
    public uint traits;
    public int constructionLevelDegrade;
    public int constructionLevelDegradeChance;
    public int movementRestoreAfterKill;
    /// <summary>Sparse conditional damage table keyed by MakeDamageRuleKey(phase, targetType).</summary>
    public Dictionary<ushort, ushort> damageRates = new();
    public int aiNoCounterBonus = 300;
}

public sealed class ArmyDef
{
    public string name = "";
    public int id;
    public int hp;
    public int movement;
    public int minAttack;
    public int maxAttack;
    public ArmyCombatRules combat = new();
    public byte unitType;
    public uint weaponType;
    public byte armorType;
    public string imageName = "";
    public bool imageCountryIndependent;
    public bool hasImageCountryIndependent;
    public bool isNavy;
    public bool hasExplicitNavy;

    public bool IsNavy() => isNavy;
}

public sealed class ArmyDefList
{
    public Dictionary<int, ArmyDef> def = new();
}

/// <summary>Free functions of ArmyDef.h (implemented in ArmyDefCombatRules.cs).</summary>
public static partial class ArmyDefFunctions
{
    public static ushort MakeDamageRuleKey(byte phase, byte targetType) => (ushort)((phase << 8) | targetType);
    public static ushort MakeWeaponArmorRuleKey(byte weaponType, byte armorType) => (ushort)((weaponType << 8) | armorType);
}

// ---- battle scenario pre-parse ---------------------------------------------------------------

public sealed class BattleScenarioCountryData
{
    public TiXmlElement node;
    public string id = "";
    public string name = "";
    public string commander = "";
    public int ai = 1;
    public int alliance;
    public int defeated;
    public int r = 255, g = 255, b = 255, a = 255;
}

public sealed class BattleScenarioAreaData
{
    public TiXmlElement node;
    public int id = -1;
    public string country = "";
    public int construction;
    public int constructionLevel;
    public int installation;
    public List<string> armyTypes = new();
}

public sealed class BattleScenarioData
{
    public string fileName = "";
    public int mapId = 1;
    public string areasEnable = "";
    public List<BattleScenarioCountryData> countries = new();
    public List<BattleScenarioAreaData> areas = new();

    public void Clear()
    {
        fileName = "";
        mapId = 1;
        areasEnable = "";
        countries.Clear();
        areas.Clear();
    }
}

// ---- scene data ------------------------------------------------------------------------------

public sealed class AreaData
{
    public int[] AreaPos = new int[2];
    public int[] AreaSize = new int[2];
    public int[] ArmyPos = new int[2];
    public int[] ConstructPos = new int[2];
    public int[] InstallationPos = new int[2];
    public TERRAIN_TYPE Type;
}

public struct RenderAreaEntry
{
    public float sortY;
    public CArea area;

    public RenderAreaEntry(float sortY, CArea area) { this.sortY = sortY; this.area = area; }
}

public sealed class CombatSpatialIndex
{
    public List<List<int>> buckets = new();
    public int minX;
    public int minY;
    public int columns;
    public int rows;
    public List<uint> visitMarks = new();
    public uint visitToken;
}

public sealed class Corps
{
    public Belligerent belligerent = new();
    public CPlayer player;
}
