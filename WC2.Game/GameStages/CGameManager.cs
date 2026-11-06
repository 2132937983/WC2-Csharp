namespace WC2;

/// <summary>
/// Match state and turn flow (CGameManager.h). Methods are split like the original sources:
/// CGameManagerBasics.cs, GameBattleStartEnd.cs, GameLoadSave.cs, GameSaveBattle2Xml.cs,
/// GameTurnBegin.cs, GameTurnEnd.cs and LoadBattleFromXml.cs.
/// </summary>
public sealed partial class CGameManager
{
    public enum GAME_MODE { CAMPAIGN = 1, CONQUEST = 2, MULTIPLAYER = 4, TUTORIAL = 5 }

    public const GAME_MODE CAMPAIGN = GAME_MODE.CAMPAIGN;
    public const GAME_MODE CONQUEST = GAME_MODE.CONQUEST;
    public const GAME_MODE MULTIPLAYER = GAME_MODE.MULTIPLAYER;
    public const GAME_MODE TUTORIAL = GAME_MODE.TUTORIAL;

    public List<CCountry> allCountry = new();
    public List<CCountry> belligerentCountry = new();
    public List<DialogueDef> dialogueList = new();
    public int currentCountryIndex;
    public int currentDialogueIndex;
    public int currentTurnNumMinusOne;
    public int randomRewardMedal;
    public GAME_MODE gameMode;
    public int x38;
    public int mapId;
    public string areasEnable = "";
    public string battleFileName = "";
    public string loadFileName = "";
    public string[] playerCountryName = { "", "", "", "", "", "", "", "" };
    public string conquestPlayerCountryId = "";
    public bool isNewGame;
    public bool localHost;
    public bool result;
    public bool resultWin;
    public bool selectNextBattle;
    public int campaignSeriesId;
    public int campaignLevelId;
    public int victoryTurn;
    public int greatVictoryTurn;
    public int campaignRewardMedal;
    public bool AIAction;
    public bool SkipMode;
    public bool DisplayAreaChange;
    public int randSeed;
    public FinishedEvent SFinishedEvent = new();
    public bool isPause;

    public CGameManager()
    {
        allCountry.Clear();
        belligerentCountry.Clear();
        dialogueList.Clear();
        campaignLevelId = -1;
        selectNextBattle = false;
    }

    /// <summary>DEPRECATED_EMPTY in the original.</summary>
    public void Init() { }

    /// <summary>DEPRECATED_EMPTY in the original.</summary>
    public void NextBattle() { }
}
