namespace WC2;

/// <summary>
/// Main menu state (port of gui/menus/CMenuState.cpp and CMenuStateBridge.cpp).
/// The panels are children of the GUIManager; this state only creates them on demand and
/// routes the GUI events between them.
/// </summary>
public sealed class CMenuState : CBaseState
{
    public GUIMainMenu guiMainMenu;
    public GUIOptions guiOptions;
    public GUISelBattle guiSelBattle;      // campaign chapter / battle selection panel
    public GUISelCountry guiSelCountry;    // conquest country selection panel
    public GUISave guiSave;
    public GUICommander guiCommander;
    public GUIIap guiIap;
    public GUIWarning guiWarning;
    public GUILockedWarning guiLockedWarning;
    public GUIService guiService;
    public GUIElement x38;
    public GUIElement x3C;
    public GUIExitWarning guiExitWarning;
    public GUINewGame guiNewGame;
    public GUIHostList guiHostList;
    public GUIMatchSetup guiMatchSetup;
    public int selectedCampaignSeries;
    public int selectedConquest;

    /// <summary>Rectangle covering the current display area.</summary>
    private static GUIRect GetMenuScreenRect()
    {
        return new GUIRect(0f, 0f, ecGraphics.Instance().screenW, ecGraphics.Instance().screenH);
    }

    /// <summary>Creates the loading overlay and starts a fade out with the given event number.</summary>
    private static void FadeOutMenuWithLoading(int fadeInfo)
    {
        var loading = new GUILoading();
        loading.Init(GetMenuScreenRect());
        GUIManager.Instance().FadeOut(fadeInfo, loading);
    }

    /// <summary>
    /// The original used the second byte of g_GameManager+0x31 as the "multiplayer start" flag;
    /// that byte lives inside randomRewardMedal.
    /// </summary>
    private static bool IsMultiplayerStartPending()
    {
        return ((uint)g_GameManager.randomRewardMedal & 0x0000FF00u) != 0;
    }

    /// <summary>
    /// The original read the StartMultiplayGame level id from g_GameManager+0x50, which is
    /// areasEnable[16..19] interpreted as a little endian int.
    /// </summary>
    private static int GetMultiplayerStartLevelId()
    {
        string areasEnable = g_GameManager.areasEnable;
        if (areasEnable == null || areasEnable.Length < 20) return 0;
        return (areasEnable[16] & 0xFF) | ((areasEnable[17] & 0xFF) << 8)
             | ((areasEnable[18] & 0xFF) << 16) | ((areasEnable[19] & 0xFF) << 24);
    }

    /// <summary>
    /// The original only marks the state type and the exist flag; the panel references are
    /// created on demand by OnEnter/OnEvent.
    /// </summary>
    public CMenuState()
    {
        stateId = EState.MENU;
        exist = true;
    }

    /// <summary>
    /// Enters the main menu state, creates the main menu and registers the GUI event receiver.
    /// When the previous battle set selectNextBattle the main menu is skipped and the campaign
    /// series is opened directly.
    /// </summary>
    public override void OnEnter()
    {
        // register ourselves as the current state
        GUIManager.Instance().currentState = this;
        GUIMotionManager.Instance().currentState = this;

        CCSoundBox.GetInstance().LoadMusic(AudioDefs.Music("menu") ?? "battle1.mp3", null);
        CCSoundBox.GetInstance().PlayMusic(true);

        guiMainMenu = new GUIMainMenu();
        var screenRect = new GUIRect(0f, 0f, ecGraphics.Instance().screenW, ecGraphics.Instance().screenH);
        guiMainMenu.Init(screenRect);
        GUIManager.Instance().AddChild(guiMainMenu, true);

        // every other panel is created on demand by the GUI events
        guiOptions = null;
        guiSelBattle = null;
        guiSelCountry = null;
        guiMatchSetup = null;
        guiHostList = null;
        guiSave = null;
        guiCommander = null;
        guiIap = null;
        guiWarning = null;
        guiLockedWarning = null;
        guiService = null;
        x38 = null;
        x3C = null;
        guiExitWarning = null;
        guiNewGame = null;

        if (g_GameManager.selectNextBattle)
        {
            guiMainMenu.Hide();
            // jump directly to the campaign chapter
            guiSelBattle = new GUISelBattle();

            screenRect = new GUIRect(0f, 0f, ecGraphics.Instance().screenW, ecGraphics.Instance().screenH);
            guiSelBattle.Init(screenRect, g_GameManager.campaignSeriesId, 0);
            GUIManager.Instance().AddChild(guiSelBattle, true);
            GUIManager.Instance().FadeIn(100);
            g_GameManager.selectNextBattle = false;
        }

        // the selectNextBattle branch already faded in once; the original still calls it again unconditionally
        GUIManager.Instance().FadeIn(100);
    }

    /// <summary>
    /// Leaves the menu state: drops the menu music, the pending motions and the whole menu GUI
    /// tree (the GUIManager owns every panel created here).
    /// </summary>
    public override void OnExit()
    {
        // the menu music must not leak into the following game state
        CCSoundBox.GetInstance().UnloadMusic();

        // drop unfinished GUI motions so they do not reference freed GUI nodes
        GUIMotionManager.Instance().ClearMotion();

        // release the whole menu GUI tree managed by the GUIManager
        GUIManager.Instance().FreeAllChild();
    }

    /// <summary>The menu state has no per frame logic of its own.</summary>
    public override void Update(float deltaTimeS) { }

    /// <summary>The menu state has no rendering of its own; the GUIManager draws the panels.</summary>
    public override void Render() { }

    public override void TouchBegin(float x, float y, int index) { }

    public override void TouchMove(float x, float y, int index) { }

    public override void TouchEnd(float x, float y, int index) { }

    /// <summary>Peer connected while the host list is shown.</summary>
    public void PeerConnected()
    {
        if (guiHostList != null && (guiHostList.flags & GUIElement.SHOWN) != 0)
        {
            GUIManager.Instance().FadeOut(19, null);
        }
    }

    /// <summary>Creates the multiplayer game and switches to the load state.</summary>
    public void StartMultiplayGame(int levelId)
    {
        g_GameManager.NewGame((int)CGameManager.MULTIPLAYER, -1, 0, levelId);
        GUIManager.Instance().FadeOut(14, null);
    }

    /// <summary>Toggles the exit confirmation box.</summary>
    public void ShowExit()
    {
        GUIManager guiManager = GUIManager.Instance();
        if (guiExitWarning != null)
        {
            guiManager.SafeFreeChild(guiExitWarning);
            guiExitWarning = null;
            return;
        }

        guiExitWarning = new GUIExitWarning();
        var warningRect = new GUIRect();
        if (ecGraphics.Instance().widthMode == 3)
        {
            warningRect.w = 444f;
            warningRect.h = 252f;
        }
        else
        {
            warningRect.w = 222f;
            warningRect.h = 126f;
        }
        guiExitWarning.Init(warningRect);
        guiManager.AddChild(guiExitWarning, false);
        guiExitWarning.Center();
    }

    /// <summary>Creates and centers the menu warning box.</summary>
    public void ShowWarning(int warningType)
    {
        if (guiWarning != null)
            return;

        guiWarning = new GUIWarning();
        var warningRect = new GUIRect(0f, 0f, 214f, 140f);
        guiWarning.Init(warningRect, warningType);
        GUIManager.Instance().AddChild(guiWarning, false);
        guiWarning.Center();
    }

    /// <summary>
    /// Handles the GUI events of the main menu and its sub panels. The original always returns
    /// false; the widget events themselves are handled by the GUIManager dispatch chain.
    /// </summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type != Event.GUI)
        {
            return false;
        }

        Event.Info.GUIInfo.GUI_TYPE eventType = evt.info.gui.type;
        int eventInfo = evt.info.gui.info;
        GUIElement clickedElement = evt.info.gui.ptr;

        if (eventType == Event.Info.GUIInfo.FADE_IN)
        {
            // after the initial fade in start the main menu slide and show the ad when allowed
            if (eventInfo == 100)
            {
                guiMainMenu.lastPressed = 1;
                if (g_GameSettings.eq0 == 0)
                {
                    guiMainMenu.ShowAD();
                }
            }
            return false;
        }

        if (eventType == Event.Info.GUIInfo.OTHER)
        {
            switch (eventInfo)
            {
                case 13: // open the in-app purchase panel
                {
                    guiIap = new GUIIap();
                    guiIap.Init(GetMenuScreenRect());
                    GUIManager.Instance().AddChild(guiIap, false);
                    break;
                }

                case 14: // close the in-app purchase panel
                    if (guiIap != null)
                    {
                        GUIManager.Instance().SafeFreeChild(guiIap);
                        guiIap = null;
                    }
                    break;

                case 17: // close the campaign locked hint
                    if (guiLockedWarning != null)
                    {
                        GUIManager.Instance().SafeFreeChild(guiLockedWarning);
                        guiLockedWarning = null;
                    }
                    break;

                case 26: // close the exit confirmation box
                    if (guiExitWarning != null)
                    {
                        GUIManager.Instance().SafeFreeChild(guiExitWarning);
                        guiExitWarning = null;
                    }
                    break;

                case 20: // open the service panel; only one instance at a time
                {
                    if (guiService != null)
                    {
                        break;
                    }

                    guiService = new GUIService();
                    var serviceRect = new GUIRect();
                    if (ecGraphics.Instance().widthMode == 3)
                    {
                        serviceRect.x = 278f;
                        serviceRect.y = 234f;
                        serviceRect.w = 468f;
                        serviceRect.h = 300f;
                    }
                    else
                    {
                        serviceRect.x = (ecGraphics.Instance().screenW - 234f) * 0.5f;
                        serviceRect.y = 85f;
                        serviceRect.w = 234f;
                        serviceRect.h = 150f;
                    }
                    guiService.Init(serviceRect);
                    GUIManager.Instance().AddChild(guiService, false);
                    break;
                }

                case 21: // close the service panel
                    if (guiService != null)
                    {
                        GUIManager.Instance().SafeFreeChild(guiService);
                        guiService = null;
                    }
                    break;

                case 22: // reserved events of the original
                case 24:
                case 25:
                    break;

                case 23: // close the unnamed temporary panel
                    if (x38 != null)
                    {
                        GUIManager.Instance().SafeFreeChild(x38);
                        x38 = null;
                    }
                    break;

                case 27: // open the "more games / new game" promotion panel
                {
                    var methodInfo = new MethodInfo();
                    if (getStaticMethodInfo(methodInfo, "com/easytech/promotion/ecPromotion", "SetNewTipVisible", "(ZZZ)V"))
                    {
                        // Java call (SetNewTipVisible(true, false, false)) is not available on desktop
                    }

                    guiMainMenu.RefreshNewTip();
                    if (guiNewGame != null)
                    {
                        break;
                    }

                    guiNewGame = new GUINewGame();
                    var newGameRect = new GUIRect();
                    if (ecGraphics.Instance().widthMode == 3)
                    {
                        newGameRect.x = 114f;
                        newGameRect.y = 122f;
                        newGameRect.w = 796f;
                        newGameRect.h = 524f;
                    }
                    else
                    {
                        newGameRect.x = (ecGraphics.Instance().screenW - 398f) * 0.5f;
                        newGameRect.y = 29f;
                        newGameRect.w = 398f;
                        newGameRect.h = 262f;
                    }
                    guiNewGame.Init(newGameRect);
                    GUIManager.Instance().AddChild(guiNewGame, false);
                    break;
                }

                case 28: // close the promotion panel and refresh the red dot
                    guiMainMenu.RefreshNewTip();
                    if (guiNewGame != null)
                    {
                        GUIManager.Instance().SafeFreeChild(guiNewGame);
                        guiNewGame = null;
                    }
                    break;

                default:
                    break;
            }
            return false;
        }

        if (eventType == Event.Info.GUIInfo.BUTTON)
        {
            GUIMainMenu menu = guiMainMenu;

            // while the main menu is not inside a sub page the campaign/conquest buttons start a loading fade out
            if (menu.lastPressed == 0)
            {
                for (int seriesId = 0; seriesId < 4; ++seriesId)
                {
                    // check whether one of the four campaign chapters was pressed
                    if (!ReferenceEquals(menu.buttonCampaigns[seriesId], clickedElement))
                    {
                        continue;
                    }

                    // NATO/WTO are unlocked by finishing the allies/axis campaign
                    // if the chapter is locked show the warning instead
                    bool unlocked = seriesId <= 1
                            || g_Commander.GetNumPlayedBatttles(0) >= GetNumBattles(0)
                            || g_Commander.GetNumPlayedBatttles(1) >= GetNumBattles(1);
                    if (unlocked)
                    {
                        FadeOutMenuWithLoading(3);
                        selectedCampaignSeries = seriesId;
                    }
                    else
                    {
                        guiLockedWarning = new GUILockedWarning();
                        var warningRect = new GUIRect();
                        warningRect.x = 0f;
                        warningRect.y = 0f;
                        if (ecGraphics.Instance().widthMode == 3)
                        {
                            warningRect.w = 444f;
                            warningRect.h = 252f;
                        }
                        else
                        {
                            warningRect.w = 222f;
                            warningRect.h = 126f;
                        }
                        guiLockedWarning.Init(warningRect);
                        GUIManager.Instance().AddChild(guiLockedWarning, false);
                        guiLockedWarning.Center();
                    }
                    break;
                }

                // conquest selection
                if (menu.conquestList != null)
                {
                    if (menu.conquestList.HandlePageButton(clickedElement))
                    {
                        return false;
                    }
                    int conquestIndex = menu.conquestList.GetConquestId(clickedElement);
                    if (conquestIndex >= 0)
                    {
                        FadeOutMenuWithLoading(4);
                        // GetConquestKeyName maps the index to the actual conquest_N resource
                        selectedConquest = conquestIndex;
                    }
                }

                // the other global buttons
                if (menu.buttonGlobal == clickedElement)
                {
                    FadeOutMenuWithLoading(6);
                    return false;
                }
                if (menu.buttonHost == clickedElement)
                {
                    FadeOutMenuWithLoading(7);
                    return false;
                }
                if (menu.buttonJoin == clickedElement)
                {
                    GUIManager.Instance().FadeOut(17, null);
                    g_PlayerManager.SetupSession(false);
                    return false;
                }
                if (menu.buttonTutorial == clickedElement)
                {
                    g_GameManager.NewGame(5, menu.lastPressed, 0, 0);
                    GUIManager.Instance().FadeOut(14, null);
                    return false;
                }
                if (menu.buttonCommander == clickedElement)
                {
                    GUIManager.Instance().FadeOut(11, null);
                    return false;
                }
                if (menu.buttonLoadCampaign == clickedElement)
                {
                    GUIManager.Instance().FadeOut(2, null);
                    return false;
                }
                if (menu.buttonLoadConquest == clickedElement)
                {
                    GUIManager.Instance().FadeOut(21, null);
                    return false;
                }
            }

            // the following checks belong to the currently opened sub panels
            if (guiSave != null)
            {
                if (guiSave.buttonBack == clickedElement)
                {
                    GUIManager.Instance().FadeOut(9, null);
                    return false;
                }
                if (guiSave.buttonOk == clickedElement)
                {
                    GUIManager.Instance().FadeOut(14, null);
                    return false;
                }
            }

            if (menu.lastPressed == 0 && menu.buttonOptions == clickedElement)
            {
                GUIManager.Instance().FadeOut(12, null);
                return false;
            }

            if (guiOptions != null
                    && (guiOptions.buttonBack == clickedElement
                            || guiOptions.buttonOk == clickedElement))
            {
                GUIManager.Instance().FadeOut(9, null);
                return false;
            }

            // enter a battle of a campaign chapter
            GUISelBattle battleSelection = guiSelBattle;
            if (battleSelection != null)
            {
                if (battleSelection.buttonBack == clickedElement)
                {
                    GUIManager.Instance().FadeOut(9, null);
                    return false;
                }
                if (battleSelection.buttonOk == clickedElement)
                {
                    if (battleSelection.gameMode == 0)
                    {
                        GUIManager.Instance().FadeOut(14, null);
                    }
                    else if (battleSelection.gameMode == 1)
                    {
                        g_PlayerManager.InitCorps(battleSelection.guiBattleList.selectedLevel);
                        g_PlayerManager.StartMatch();
                    }
                    else if (battleSelection.gameMode == 2)
                    {
                        GUIManager.Instance().FadeOut(18, null);
                    }
                    return false;
                }
            }

            if (guiSelCountry != null)
            {
                if (guiSelCountry.buttonBack == clickedElement)
                {
                    GUIManager.Instance().FadeOut(9, null);
                    return false;
                }
                if (guiSelCountry.buttonOk == clickedElement)
                {
                    GUIManager.Instance().FadeOut(14, null);
                    return false;
                }
            }
            // match setup
            if (guiMatchSetup != null)
            {
                if (guiMatchSetup.buttonOk == clickedElement)
                {
                    if (IsMultiplayerStartPending())
                    {
                        g_PlayerManager.SendStartLoadGame();
                        StartMultiplayGame(GetMultiplayerStartLevelId());
                    }
                    return false;
                }
                if (guiMatchSetup.buttonBack == clickedElement)
                {
                    if (battleSelection == null)
                    {
                        if (guiHostList == null)
                        {
                            return false;
                        }
                        GUIManager.Instance().FadeOut(17, null);
                        g_PlayerManager.DestroySession();
                        g_PlayerManager.SetupSession(false);
                    }
                    else if (battleSelection.gameMode == 1)
                    {
                        GUIManager.Instance().FadeOut(6, null);
                        g_PlayerManager.StopMatch();
                    }
                    else if (battleSelection.gameMode == 2)
                    {
                        GUIManager.Instance().FadeOut(7, null);
                        g_PlayerManager.DestroySession();
                    }
                    return false;
                }
            }

            if (guiHostList != null && guiHostList.buttonBack == clickedElement)
            {
                GUIManager.Instance().FadeOut(9, null);
                g_PlayerManager.DestroySession();
                return false;
            }

            if (guiCommander != null && guiCommander.buttonBack == clickedElement)
            {
                GUIManager.Instance().FadeOut(9, null);
                return false;
            }

            if (guiWarning != null && guiWarning.buttonOk == clickedElement)
            {
                if (guiWarning.warningType != 6)
                {
                    GUIManager.Instance().FadeOut(9, null);
                }
                GUIManager.Instance().SafeFreeChild(guiWarning);
                guiWarning = null;
            }
            return false;
        }

        if (eventType != Event.Info.GUIInfo.FADE_OUT)
        {
            return false;
        }

        // handle the events caused by a finished FADE_OUT
        switch (eventInfo)
        {
            case 2:  // load a campaign save
            case 21: // load a conquest save
            {
                guiMainMenu.Hide();
                guiSave = new GUISave();
                guiSave.Init(GetMenuScreenRect(), eventInfo == 2 ? 1 : 2, true);
                GUIManager.Instance().AddChild(guiSave, true);
                GUIManager.Instance().FadeIn(-1);
                return false;
            }

            case 12: // options
                guiMainMenu.Hide();
                guiOptions = new GUIOptions();
                guiOptions.Init(GetMenuScreenRect());
                GUIManager.Instance().AddChild(guiOptions, true);
                GUIManager.Instance().FadeIn(-1);
                return false;

            case 11: // commander
            {
                guiMainMenu.Hide();
                guiCommander = new GUICommander();
                var commanderRect = new GUIRect(0f, 0f, 480f, 320f);
                guiCommander.Init(commanderRect);
                GUIManager.Instance().AddChild(guiCommander, true);
                GUIManager.Instance().FadeIn(-1);
                return false;
            }

            case 9: // back to the main menu from any sub panel
                guiMainMenu.Show();
                if (guiOptions != null)
                {
                    GUIManager.Instance().FreeChild(guiOptions);
                    guiOptions = null;
                }
                else if (guiSave != null)
                {
                    GUIManager.Instance().FreeChild(guiSave);
                    guiSave = null;
                }
                else if (guiSelBattle != null)
                {
                    GUIManager.Instance().FreeChild(guiSelBattle);
                    guiSelBattle = null;
                }
                else if (guiSelCountry != null)
                {
                    GUIManager.Instance().FreeChild(guiSelCountry);
                    guiSelCountry = null;
                }
                else if (guiHostList != null)
                {
                    GUIManager.Instance().FreeChild(guiHostList);
                    guiHostList = null;
                }
                else if (guiCommander != null)
                {
                    GUIManager.Instance().FreeChild(guiCommander);
                    guiCommander = null;
                }
                if (guiMatchSetup != null)
                {
                    GUIManager.Instance().FreeChild(guiMatchSetup);
                    guiMatchSetup = null;
                }
                GUIManager.Instance().FadeIn(-1);
                return false;

            case 3: // open the single player battle selection
                guiMainMenu.Hide();
                guiSelBattle = new GUISelBattle();
                guiSelBattle.Init(GetMenuScreenRect(), selectedCampaignSeries, 0);
                GUIManager.Instance().AddChild(guiSelBattle, true);
                GUIManager.Instance().FadeIn(-1);
                return false;

            case 4: // open the conquest country selection
                guiMainMenu.Hide();
                guiSelCountry = new GUISelCountry();
                guiSelCountry.Init(GetMenuScreenRect(), selectedConquest);
                GUIManager.Instance().AddChild(guiSelCountry, true);
                GUIManager.Instance().FadeIn(-1);
                return false;

            case 6: // create the local multiplayer battle list
                guiMainMenu.Hide();
                guiSelBattle = new GUISelBattle();
                guiSelBattle.Init(GetMenuScreenRect(), 4, 1);
                GUIManager.Instance().AddChild(guiSelBattle, true);
                GUIManager.Instance().FadeIn(-1);
                return false;

            case 7: // create or restore the online battle list
                if (guiMatchSetup != null)
                {
                    GUIManager.Instance().FreeChild(guiMatchSetup);
                    guiMatchSetup = null;
                }
                guiMainMenu.Hide();
                if (guiSelBattle == null)
                {
                    guiSelBattle = new GUISelBattle();
                    guiSelBattle.Init(GetMenuScreenRect(), 4, 2);
                    GUIManager.Instance().AddChild(guiSelBattle, true);
                }
                else
                {
                    guiSelBattle.Show();
                }
                GUIManager.Instance().FadeIn(-1);
                return false;

            case 17: // create or restore the online host list
                if (guiMatchSetup != null)
                {
                    GUIManager.Instance().FreeChild(guiMatchSetup);
                    guiMatchSetup = null;
                }
                guiMainMenu.Hide();
                if (guiHostList == null)
                {
                    guiHostList = new GUIHostList();
                    guiHostList.Init(GetMenuScreenRect());
                    GUIManager.Instance().AddChild(guiHostList, true);
                }
                else
                {
                    guiHostList.ResetConnectState();
                    guiHostList.Show();
                }
                GUIManager.Instance().FadeIn(-1);
                return false;

            case 18: // local multiplayer: level chosen, go to the corps setup
                g_PlayerManager.InitCorps(guiSelBattle.guiBattleList.selectedLevel);
                g_PlayerManager.SetupSession(true);
                guiSelBattle.Hide();
                guiMatchSetup = new GUIMatchSetup();
                guiMatchSetup.Init(GetMenuScreenRect());
                GUIManager.Instance().AddChild(guiMatchSetup, true);
                GUIManager.Instance().FadeIn(-1);
                return false;

            case 19: // joined a room, go to the corps setup
                guiHostList.Hide();
                guiMatchSetup = new GUIMatchSetup();
                guiMatchSetup.Init(GetMenuScreenRect());
                GUIManager.Instance().AddChild(guiMatchSetup, true);
                GUIManager.Instance().FadeIn(-1);
                return false;

            case 14: // start the single player game; the load state takes over
                CStateManager.Instance().SetCurState(LOAD);
                return false;

            default:
                return false;
        }
    }
}
