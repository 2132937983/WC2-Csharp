namespace WC2;

/// <summary>
/// In-game state (CGameState.h). The life cycle lives in CGameStateLifeCycle.cs, the event
/// handling in CGameStateEvents.cs (both under Game/GUI/InGame).
/// </summary>
public sealed partial class CGameState : CBaseState
{
    /// <summary>Retired diplomacy button of the original game (extern GUIButton* GameButtonDI).</summary>
    public static GUIButton GameButtonDI;

    public float[] firstTouchStartPos = new float[2];
    public bool firstTouch;
    public bool secondTouch;
    public float[][] touchPos = { new float[2], new float[2] };
    public int firstTouchIndex;
    public int secondTouchIndex;
    public readonly CTouchInertia touchInertia = new();
    public GUIButton gameButtonRound;
    public GUIButton gameButtonCard;
    public GUIButton gameButtonPause;
    public GUIButton gameButtonCardRemove;
    public bool buyingCard;
    public GUIGold guiGold;
    public GUITax guiTax;
    public GUISmallCard guiSmallCard;
    public GUIAttackBox guiAttackBox;
    public GUIPauseBox guiPauseBox;
    public GUIBuyCard guiBuyCard;
    public GUIOptions guiOptions;
    public GUISave guiSave;
    public GUIBattle guiBattle;
    public GUIResult guiResult;
    public GUIDefeated guiDefeated;
    public GUIAIProgress guiaiProgress;
    public GUISelArmy guiSelArmy;
    public GUIBegin guiBegin;
    public GUIBattleIntro guiBattleIntro;
    public GUIVictory guiVictory;
    public GUITutorails guiTutorails;
    public GUIDialogue guiDialogue;
    public GUIBank guiBank;
    public GUIActionInfo guiActionInfo;
    public GUIWarning guiWarning;
    public GUIEnd guiEnd;
    public int motionGameButtonRound;
    public int motionGameButtonCard;
    public int motionGameButtonPause;
    public int motionGuiGold;
    public bool gameNotInterrupted;
    public bool shouldCameraAutoFixPos;
    public bool idleTimerEnabled;
    public float idleTimer;

    /// <summary>Creates the in-game state and initializes the touch inertia.</summary>
    public CGameState()
    {
        stateId = GAME;
        exist = true;
    }
}
