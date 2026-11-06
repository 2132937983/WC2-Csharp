namespace WC2;

/// <summary>
/// The game level globals of the original code (g_Scene, g_GameManager, ...) plus the
/// unscoped enum members and constants, so ported code can use them without qualification.
/// </summary>
public static class GameGlobals
{
    // ---- global objects ------------------------------------------------------------------
    public static readonly CScene g_Scene = new();
    public static readonly CGameManager g_GameManager = new();
    public static readonly CCommander g_Commander = new();
    public static readonly CGameRes g_GameRes = new();
    public static readonly CGameSettings g_GameSettings = new();
    public static readonly CSoundRes g_SoundRes = new();
    public static readonly CFightTextMgr g_FightTextMgr = new();
    public static readonly CPlayerManager g_PlayerManager = new();
    public static readonly CNationInfo g_NationInfo = new();

    // ---- constants -----------------------------------------------------------------------
    public const int MAX_ARMY_LEVEL = 4;
    public const int MAX_CITY_LEVEL = 4;
    public const int MAX_INDUSTRY_LEVEL = 3;
    public const int MAX_AIRPORT_LEVEL = 1;
    public const int SERIES_MAX = 20;
    public const int SERIES_CNT = 4;
    public const int MAX_COMMANDER_LEVEL = 14;
    public const int MAX_WAR_MEDAL_LEVEL = 3;
    public const int AREA_TROOP_LIMIT = 4;
    public const int AREA_NAVY_LIMIT = 4;
    public const int AREA_RENDER_ARMY_LIMIT = 4;
    public const int NeutralID = 4;
    public const int TIXML_SUCCESS = TiXmlConst.TIXML_SUCCESS;
    public const int TIXML_NO_ATTRIBUTE = TiXmlConst.TIXML_NO_ATTRIBUTE;
    public const int TIXML_WRONG_TYPE = TiXmlConst.TIXML_WRONG_TYPE;
    public const int TIXML_ENCODING_UNKNOWN = TiXmlConst.TIXML_ENCODING_UNKNOWN;

    public const int LEFT_ALIGNED = TextAlign.LEFT_ALIGNED;
    public const int RIGHT_ALIGNED = TextAlign.RIGHT_ALIGNED;
    public const int MIDDLE_ALIGNED = TextAlign.MIDDLE_ALIGNED;

    // ---- EState --------------------------------------------------------------------------
    public const EState LOGO = EState.LOGO;
    public const EState MENU = EState.MENU;
    public const EState MATCH = EState.MATCH;
    public const EState LOAD = EState.LOAD;
    public const EState GAME = EState.GAME;
    public const EState EDIT = EState.EDIT;

    // ---- CARD_ID -------------------------------------------------------------------------
    public const CARD_ID INVALID_CARD = CARD_ID.INVALID_CARD;
    public const CARD_ID NO_CARD = CARD_ID.NO_CARD;
    public const CARD_ID INFANTRY_CARD = CARD_ID.INFANTRY_CARD;
    public const CARD_ID ARMOUR_CARD = CARD_ID.ARMOUR_CARD;
    public const CARD_ID ARTILLERY_CARD = CARD_ID.ARTILLERY_CARD;
    public const CARD_ID ROCKET_CARD = CARD_ID.ROCKET_CARD;
    public const CARD_ID TANK_CARD = CARD_ID.TANK_CARD;
    public const CARD_ID HEAVY_TANK_CARD = CARD_ID.HEAVY_TANK_CARD;
    public const CARD_ID DESTROYER_CARD = CARD_ID.DESTROYER_CARD;
    public const CARD_ID CRUISER_CARD = CARD_ID.CRUISER_CARD;
    public const CARD_ID BATTLE_SHIP_CARD = CARD_ID.BATTLE_SHIP_CARD;
    public const CARD_ID AIRCRAFT_CARRIER_CARD = CARD_ID.AIRCRAFT_CARRIER_CARD;
    public const CARD_ID AIR_STRIKE_CARD = CARD_ID.AIR_STRIKE_CARD;
    public const CARD_ID BOMBER_CARD = CARD_ID.BOMBER_CARD;
    public const CARD_ID AIRBORNE_FORCE_CARD = CARD_ID.AIRBORNE_FORCE_CARD;
    public const CARD_ID NUCLEAR_BOMB_CARD = CARD_ID.NUCLEAR_BOMB_CARD;
    public const CARD_ID CITY_CARD = CARD_ID.CITY_CARD;
    public const CARD_ID INDUSTRY_CARD = CARD_ID.INDUSTRY_CARD;
    public const CARD_ID AIRPORT_CARD = CARD_ID.AIRPORT_CARD;
    public const CARD_ID LAND_FORT_CARD = CARD_ID.LAND_FORT_CARD;
    public const CARD_ID ENTRENCHMENT_CARD = CARD_ID.ENTRENCHMENT_CARD;
    public const CARD_ID ANTIAIRCRAFT_CARD = CARD_ID.ANTIAIRCRAFT_CARD;
    public const CARD_ID RADAR_CARD = CARD_ID.RADAR_CARD;
    public const CARD_ID RESEARCH_CARD = CARD_ID.RESEARCH_CARD;
    public const CARD_ID CARRIER_CARD = CARD_ID.CARRIER_CARD;
    public const CARD_ID ASSAULT_ART_CARD = CARD_ID.ASSAULT_ART_CARD;
    public const CARD_ID DEFEND_ART_CARD = CARD_ID.DEFEND_ART_CARD;
    public const CARD_ID COMMANDER_CARD = CARD_ID.COMMANDER_CARD;
    public const CARD_ID SUPPLY_LINE_CARD = CARD_ID.SUPPLY_LINE_CARD;
    public const CARD_ID ACE_FORCES_CARD = CARD_ID.ACE_FORCES_CARD;
    public const CARD_ID ELITE_INFANTRY_CARD = CARD_ID.ELITE_INFANTRY_CARD;
    public const int VALID_CARD_COUNT = (int)CARD_ID.VALID_CARD_COUNT;

    // ---- WARMEDAL_ID ---------------------------------------------------------------------
    public const WARMEDAL_ID INFANTRY_MEDAL = WARMEDAL_ID.INFANTRY_MEDAL;
    public const WARMEDAL_ID AIR_FORCE_MEDAL = WARMEDAL_ID.AIR_FORCE_MEDAL;
    public const WARMEDAL_ID ARTILLERY_MEDAL = WARMEDAL_ID.ARTILLERY_MEDAL;
    public const WARMEDAL_ID ARMOUR_MEDAL = WARMEDAL_ID.ARMOUR_MEDAL;
    public const WARMEDAL_ID NAVY_MEDAL = WARMEDAL_ID.NAVY_MEDAL;
    public const WARMEDAL_ID COMMERCE_MEDAL = WARMEDAL_ID.COMMERCE_MEDAL;

    // ---- ARMY_TYPE -----------------------------------------------------------------------
    public const ARMY_TYPE INFANTRY = ARMY_TYPE.INFANTRY;
    public const ARMY_TYPE PANZER = ARMY_TYPE.PANZER;
    public const ARMY_TYPE ARTILLERY = ARMY_TYPE.ARTILLERY;
    public const ARMY_TYPE ROCKET = ARMY_TYPE.ROCKET;
    public const ARMY_TYPE TANK = ARMY_TYPE.TANK;
    public const ARMY_TYPE HEAVY_TANK = ARMY_TYPE.HEAVY_TANK;
    public const ARMY_TYPE DESTROYER = ARMY_TYPE.DESTROYER;
    public const ARMY_TYPE CRUISER = ARMY_TYPE.CRUISER;
    public const ARMY_TYPE BATTLESHIP = ARMY_TYPE.BATTLESHIP;
    public const ARMY_TYPE AIRCRAFT_CARRIER = ARMY_TYPE.AIRCRAFT_CARRIER;
    public const ARMY_TYPE CARRIER_ARMY = ARMY_TYPE.CARRIER_ARMY;
    public const ARMY_TYPE AIRSTRIKE_ARMY = ARMY_TYPE.AIRSTRIKE_ARMY;
    public const ARMY_TYPE BOMBER_ARMY = ARMY_TYPE.BOMBER_ARMY;
    public const ARMY_TYPE AIRBORNE_ARMY = ARMY_TYPE.AIRBORNE_ARMY;
    public const ARMY_TYPE NUCLEAR_BOMB_ARMY = ARMY_TYPE.NUCLEAR_BOMB_ARMY;
    public const ARMY_TYPE ELITE_INFANTRY = ARMY_TYPE.ELITE_INFANTRY;

    // ---- CARD_TYPE -----------------------------------------------------------------------
    public const CARD_TYPE ARMY_TYPE_CARD = CARD_TYPE.ARMY_TYPE_CARD;
    public const CARD_TYPE NAVY_TYPE_CARD = CARD_TYPE.NAVY_TYPE_CARD;
    public const CARD_TYPE AIR_FORCE_TYPE_CARD = CARD_TYPE.AIR_FORCE_TYPE_CARD;
    public const CARD_TYPE DEVELOPMENT_TYPE_CARD = CARD_TYPE.DEVELOPMENT_TYPE_CARD;
    public const CARD_TYPE STRATEGY_TYPE_CARD = CARD_TYPE.STRATEGY_TYPE_CARD;

    // ---- DEFEAT_TYPE ---------------------------------------------------------------------
    public const DEFEAT_TYPE LAND = DEFEAT_TYPE.LAND;
    public const DEFEAT_TYPE ARMY = DEFEAT_TYPE.ARMY;
    public const DEFEAT_TYPE CORE = DEFEAT_TYPE.CORE;

    // ---- INSTALLATION_TYPE ---------------------------------------------------------------
    public const INSTALLATION_TYPE NO_INSTALLATION = INSTALLATION_TYPE.NO_INSTALLATION;
    public const INSTALLATION_TYPE FORT = INSTALLATION_TYPE.FORT;
    public const INSTALLATION_TYPE ENTRENCHMENT = INSTALLATION_TYPE.ENTRENCHMENT;
    public const INSTALLATION_TYPE ANTIAIRCRAFT = INSTALLATION_TYPE.ANTIAIRCRAFT;
    public const INSTALLATION_TYPE RADAR = INSTALLATION_TYPE.RADAR;

    // ---- CONSTRUCTION_TYPE ---------------------------------------------------------------
    public const CONSTRUCTION_TYPE NO_CONSTRUCTION = CONSTRUCTION_TYPE.NO_CONSTRUCTION;
    public const CONSTRUCTION_TYPE CITY = CONSTRUCTION_TYPE.CITY;
    public const CONSTRUCTION_TYPE INDUSTRY = CONSTRUCTION_TYPE.INDUSTRY;
    public const CONSTRUCTION_TYPE AIRPORT = CONSTRUCTION_TYPE.AIRPORT;

    // ---- AREA_TYPE -----------------------------------------------------------------------
    public const AREA_TYPE NORMAL = AREA_TYPE.NORMAL;
    public const AREA_TYPE CAPITAL = AREA_TYPE.CAPITAL;
    public const AREA_TYPE PORT = AREA_TYPE.PORT;
    public const AREA_TYPE LARGE_CITY = AREA_TYPE.LARGE_CITY;
    public const AREA_TYPE NORMAL_CITY = AREA_TYPE.NORMAL_CITY;

    // ---- TERRAIN_TYPE --------------------------------------------------------------------
    public const TERRAIN_TYPE LAND_PLAIN = TERRAIN_TYPE.LAND_PLAIN;
    public const TERRAIN_TYPE WATER_SEA = TERRAIN_TYPE.WATER_SEA;

    // ---- ARMY_CARD -----------------------------------------------------------------------
    public const ARMY_CARD ASSAULT = ARMY_CARD.ASSAULT;
    public const ARMY_CARD DEFENCE = ARMY_CARD.DEFENCE;
    public const ARMY_CARD CARRIER = ARMY_CARD.CARRIER;
    public const ARMY_CARD COMMANDER = ARMY_CARD.COMMANDER;

    // ---- MORALE_SETS ---------------------------------------------------------------------
    public const MORALE_SETS MORALE_NORMAL = MORALE_SETS.MORALE_NORMAL;
    public const MORALE_SETS MORALE_UPSET = MORALE_SETS.MORALE_UPSET;
    public const MORALE_SETS MORALE_HIGH = MORALE_SETS.MORALE_HIGH;

    // ---- combat enums --------------------------------------------------------------------
    public const COMBAT_ATTACK_MODE COMBAT_ATTACK_DIRECT = COMBAT_ATTACK_MODE.COMBAT_ATTACK_DIRECT;
    public const COMBAT_ATTACK_MODE COMBAT_ATTACK_PROJECTILE = COMBAT_ATTACK_MODE.COMBAT_ATTACK_PROJECTILE;
    public const COMBAT_ATTACK_MODE COMBAT_ATTACK_AIRSTRIKE = COMBAT_ATTACK_MODE.COMBAT_ATTACK_AIRSTRIKE;
    public const COMBAT_AIRSTRIKE_PAYLOAD COMBAT_AIRSTRIKE_PAYLOAD_AIRSTRIKE = COMBAT_AIRSTRIKE_PAYLOAD.COMBAT_AIRSTRIKE_PAYLOAD_AIRSTRIKE;
    public const COMBAT_AIRSTRIKE_PAYLOAD COMBAT_AIRSTRIKE_PAYLOAD_BOMBER = COMBAT_AIRSTRIKE_PAYLOAD.COMBAT_AIRSTRIKE_PAYLOAD_BOMBER;
    public const COMBAT_AIRSTRIKE_PAYLOAD COMBAT_AIRSTRIKE_PAYLOAD_NUCLEAR = COMBAT_AIRSTRIKE_PAYLOAD.COMBAT_AIRSTRIKE_PAYLOAD_NUCLEAR;
    public const COMBAT_RANGE_SHAPE COMBAT_RANGE_GRAPH = COMBAT_RANGE_SHAPE.COMBAT_RANGE_GRAPH;
    public const COMBAT_RANGE_SHAPE COMBAT_RANGE_CIRCLE = COMBAT_RANGE_SHAPE.COMBAT_RANGE_CIRCLE;
    public const COMBAT_ENGAGEMENT_MODE COMBAT_ENGAGEMENT_CLOSE = COMBAT_ENGAGEMENT_MODE.COMBAT_ENGAGEMENT_CLOSE;
    public const COMBAT_ENGAGEMENT_MODE COMBAT_ENGAGEMENT_PROJECTILE = COMBAT_ENGAGEMENT_MODE.COMBAT_ENGAGEMENT_PROJECTILE;
    public const uint ARMY_COMBAT_TRAIT_NONE = 0;
    public const uint ARMY_COMBAT_TRAIT_CROSS_ENTRENCHMENT = 1u << 0;
    public const uint ARMY_COMBAT_TRAIT_SECONDARY_ATTACK = 1u << 1;
    public const uint ARMY_COMBAT_TRAIT_RECV_FORT_SECOND_COUNTER = 1u << 2;
    public const uint ARMY_COMBAT_TRAIT_CANNOT_OCCUPY = 1u << 3;
    public const byte COMBAT_DAMAGE_ATTACK = 1;
    public const byte COMBAT_DAMAGE_COUNTER = 2;
    public const byte ARMY_UNIT_TYPE_UNSPECIFIED = 0;
    public const ushort DAMAGE_RATE_INFINITY = 0xFFFF;

    // ---- alliance ------------------------------------------------------------------------
    public const ALLIANCE_TYPE ALLY = ALLIANCE_TYPE.ALLY;
    public const ALLIANCE_TYPE NOT_ALLY = ALLIANCE_TYPE.NOT_ALLY;
    public const ALLIANCE_TYPE ENEMY = ALLIANCE_TYPE.ENEMY;
    public const ALLIANCE_TYPE SELF = ALLIANCE_TYPE.SELF;
    public const SPECIAL_ALLIENCE_TYPE NEUTRAL = SPECIAL_ALLIENCE_TYPE.NEUTRAL;
    public const SPECIAL_ALLIENCE_TYPE NO_BELONGING = SPECIAL_ALLIENCE_TYPE.NO_BELONGING;

    // ---- bomber --------------------------------------------------------------------------
    public const BomberActionType BOMBER_NO_ACTION = BomberActionType.BOMBER_NO_ACTION;
    public const BomberActionType BOMBER_AIRSTRIKE = BomberActionType.BOMBER_AIRSTRIKE;
    public const BomberActionType BOMBER_BOMB = BomberActionType.BOMBER_BOMB;
    public const BomberActionType BOMBER_NUCLEAR_BOMB = BomberActionType.BOMBER_NUCLEAR_BOMB;

    // ---- sound ---------------------------------------------------------------------------
    public const SND_EFFECT DRAFT_WAV = SND_EFFECT.DRAFT_WAV;
    public const SND_EFFECT MOVE_WAV = SND_EFFECT.MOVE_WAV;
    public const SND_EFFECT OCCUPY_WAV = SND_EFFECT.OCCUPY_WAV;
    public const SND_EFFECT CANNON_WAV = SND_EFFECT.CANNON_WAV;
    public const SND_EFFECT ROCKET_WAV = SND_EFFECT.ROCKET_WAV;
    public const SND_EFFECT FIRE_WAV = SND_EFFECT.FIRE_WAV;
    public const SND_EFFECT EXP_WAV = SND_EFFECT.EXP_WAV;
    public const SND_EFFECT STRIKE_WAV = SND_EFFECT.STRIKE_WAV;
    public const SND_EFFECT SELECT_WAV = SND_EFFECT.SELECT_WAV;
    public const SND_EFFECT CANCEL_WAV = SND_EFFECT.CANCEL_WAV;
    public const SND_EFFECT SHIP_WAV = SND_EFFECT.SHIP_WAV;
    public const SND_EFFECT CELEBRATE_WAV = SND_EFFECT.CELEBRATE_WAV;
    public const SND_EFFECT AFTER_WAR_WAV = SND_EFFECT.AFTER_WAR_WAV;
    public const SND_EFFECT BUFF_WAV = SND_EFFECT.BUFF_WAV;
    public const SND_EFFECT BUILD_WAV = SND_EFFECT.BUILD_WAV;
    public const SND_EFFECT CARD_INTERFACE_WAV = SND_EFFECT.CARD_INTERFACE_WAV;
    public const SND_EFFECT GAS_WAV = SND_EFFECT.GAS_WAV;
    public const SND_EFFECT LV_UP_WAV = SND_EFFECT.LV_UP_WAV;
    public const SND_EFFECT MACHINE_GUN_WAV = SND_EFFECT.MACHINE_GUN_WAV;
    public const SND_EFFECT NAVAL_GUN_WAV = SND_EFFECT.NAVAL_GUN_WAV;
    public const SND_EFFECT POP_WAV = SND_EFFECT.POP_WAV;
    public const SND_EFFECT SUPPLY_WAV = SND_EFFECT.SUPPLY_WAV;

    // ---- free functions from functions.h implemented by the game layer ------------------
    public static ArmyAbility GetArmyAbility(int level) => CArmy.GetArmyAbility(level);
    public static CommanderAbility GetCommanderAbility(int level) => CCommander.GetCommanderAbility(level);
    public static int GetNumBattles(int campaignId) => CampaignSize.GetNumBattles(campaignId);
    public static void GetBattleKeyName(int series, int level, out string result) => result = CampaignSize.GetBattleKeyName(series, level);
    public static string GetBattleKeyName(int series, int level) => CampaignSize.GetBattleKeyName(series, level);
    public static string GetConquestKeyName(int id) => CampaignSize.GetConquestKeyName(id);
    public static string GetBattleFileName(int gameMode, int unused, int campaignId, int level) => CampaignSize.GetBattleFileName(gameMode, unused, campaignId, level);
    public static int GetNumMultiPlayerBattles(int unused) => CampaignSize.GetNumMultiPlayerBattles(unused);
    public static bool HasUnitMotion(string countryName, int armyTypeId) => false;
    public static void GameStart() => g_GameManager.isPause = false;
    public static void GamePause() => g_GameManager.isPause = true;
}
