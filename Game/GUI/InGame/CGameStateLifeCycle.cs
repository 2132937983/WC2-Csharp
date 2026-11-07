namespace WC2;

/// <summary>CGameStateLifeCycle.cpp: enter/exit, update, render and the resident HUD creation.</summary>
public sealed partial class CGameState
{
    /// <summary>
    /// Creates the resident GUI, registers the main button motions and initializes the
    /// input / flow flags. Resident objects are released in OnExit; short lived overlays
    /// (options, save, result, bank ...) are created on demand and SafeFreeChild'ed by their
    /// event handlers.
    /// </summary>
    public override void OnEnter()
    {
        GUIManager.Instance().currentState = this;
        GUIMotionManager.Instance().currentState = this;
        var graphics = ecGraphics.Instance();
        var tmp_rect = new GUIRect();
        tmp_rect.w = 32.0f;
        tmp_rect.h = 30.0f;
        if (Layout.Tablet)
        {
            tmp_rect.w = 40.0f;
            tmp_rect.h = 38.0f;
        }
        tmp_rect.x = graphics.screenW;
        tmp_rect.y = graphics.screenH;
        // The three main buttons start off screen; the first fade in (or the closing of the
        // battle intro) moves them to their final position through motionGameButton*.
        gameButtonRound = GUIManager.Instance().AddButton("gamebutton_round_normal.png", "gamebutton_round_press.png", tmp_rect, null, null);
        gameButtonRound.SetBackground("buttonboard_green.png");
        tmp_rect.x = 0.0f - tmp_rect.w;
        tmp_rect.y = graphics.screenH;
        gameButtonCard = GUIManager.Instance().AddButton("gamebutton_card_normal.png", "gamebutton_card_press.png", tmp_rect, null, null);
        gameButtonCard.SetBackground("buttonboard_blue.png");
        gameButtonCard.soundBtn = false;
        tmp_rect.y = 0.0f - tmp_rect.h;
        tmp_rect.x = graphics.screenW;
        gameButtonPause = GUIManager.Instance().AddButton("gamebutton_pause_normal.png", "gamebutton_pause_press.png", tmp_rect, null, null);
        gameButtonPause.SetBackground("buttonboard_red.png");
        tmp_rect.y = graphics.screenH - tmp_rect.h;
        tmp_rect.x = 0.0f;
        gameButtonCardRemove = GUIManager.Instance().AddButton("gamebutton_cardremove_normal.png", "gamebutton_cardremove_press.png", tmp_rect, null, null);
        gameButtonCardRemove.SetBackground("buttonboard_blue.png");
        gameButtonCardRemove.Hide();
        // Top / corner HUD: gold, area info, pending card and attack confirmation box.
        guiGold = new GUIGold();
        tmp_rect.y = -50.0f;
        tmp_rect.x = 0.0f;
        tmp_rect.w = 104.0f;
        tmp_rect.h = 38.0f;
        guiGold.Init(tmp_rect);
        GUIManager.Instance().AddChild(guiGold, true);
        guiTax = new GUITax();
        tmp_rect.w = 104.0f;
        tmp_rect.x = 0.0f;
        tmp_rect.y = 0.0f;
        tmp_rect.h = 38.0f;
        guiTax.Init(tmp_rect);
        guiTax.Hide();
        GUIManager.Instance().AddChild(guiTax, true);
        guiSmallCard = new GUISmallCard();
        tmp_rect.x = 0.0f;
        tmp_rect.y = 0.0f;
        tmp_rect.w = graphics.screenW;
        tmp_rect.h = graphics.screenH;
        guiSmallCard.Init(tmp_rect);
        guiSmallCard.Hide();
        GUIManager.Instance().AddChild(guiSmallCard, true);
        guiAttackBox = new GUIAttackBox();
        tmp_rect.x = 389.0f;
        tmp_rect.w = 258.0f;
        tmp_rect.y = 306.0f;
        tmp_rect.h = 186.0f;
        guiAttackBox.Init(tmp_rect);
        guiAttackBox.Hide();
        GUIManager.Instance().AddChild(guiAttackBox, false);
        guiAttackBox.Center();
        // The following three are resident overlays: hidden by default, only re-ordered and shown when needed.
        guiPauseBox = new GUIPauseBox();
        tmp_rect.x = 394.0f;
        tmp_rect.y = 213.0f;
        tmp_rect.w = 166.0f;
        tmp_rect.h = 262.0f;
        guiPauseBox.Init(tmp_rect);
        guiPauseBox.Hide();
        GUIManager.Instance().AddChild(guiPauseBox, true);
        guiPauseBox.Center();

        // On the wide layout the buy card panel docks at the bottom; otherwise it uses the full screen.
        guiBuyCard = new GUIBuyCard();
        if (Layout.Tablet)
        {
            tmp_rect.w = graphics.screenW;
            tmp_rect.h = graphics.screenH - 468.0f;
            tmp_rect.x = 0.0f;
            tmp_rect.y = 468.0f;
        }
        else
        {
            tmp_rect.w = graphics.screenW;
            tmp_rect.h = graphics.screenH;
            tmp_rect.x = 0.0f;
            tmp_rect.y = 0.0f;
        }
        guiBuyCard.Init(tmp_rect);
        guiBuyCard.Hide();
        GUIManager.Instance().AddChild(guiBuyCard, true);
        // The AI progress bar is only shown while a non player country acts; multiplayer has no local AI progress.
        guiaiProgress = new GUIAIProgress();
        if (Layout.Tablet)
        {
            tmp_rect.x = 272.0f;
            tmp_rect.y = 433.0f;
        }
        else
        {
            tmp_rect.y = 0.0f;
            tmp_rect.x = (graphics.screenW - 480.0f) * 0.5f;
        }
        tmp_rect.w = 480.0f;
        tmp_rect.h = 320.0f;
        guiaiProgress.Init(tmp_rect);
        GUIManager.Instance().AddChild(guiaiProgress, true);
        if (g_GameManager.GetCurCountry() != null && !g_GameManager.GetCurCountry().ai)
            guiaiProgress.Hide();
        else
            UpdateAIProgress();
        if (g_GameManager.gameMode == CGameManager.MULTIPLAYER)
            guiaiProgress.Hide();
        // The stacked army side bar is used by SelectArea/TouchEnd to switch the target unit or to pick a card target.
        guiSelArmy = new GUISelArmy();
        tmp_rect.h = 240.0f;
        tmp_rect.x = graphics.screenW - 60.0f;
        tmp_rect.w = 60.0f;
        tmp_rect.y = (graphics.screenH - 240.0f) * 0.5f;
        guiSelArmy.Init(tmp_rect);
        guiSelArmy.Hide();
        GUIManager.Instance().AddChild(guiSelArmy, true);
        guiBegin = new GUIBegin();
        tmp_rect.x = 0.0f;
        tmp_rect.w = 184.0f;
        tmp_rect.h = 258.0f;
        tmp_rect.y = 0.0f;
        guiBegin.Init(tmp_rect);
        guiBegin.Hide();
        GUIManager.Instance().AddChild(guiBegin, false);
        guiBegin.Center();
        // The following pages are created on demand; the explicit null is what OnEvent uses to tell their life time.
        guiOptions = null;
        guiSave = null;
        guiResult = null;
        guiBattleIntro = null;
        guiVictory = null;
        guiBank = null;
        guiEnd = null;
        guiWarning = null;

        // The action info only serves the traditional multiplayer mode; single player keeps it null.
        if (g_GameManager.gameMode == CGameManager.MULTIPLAYER)
        {
            guiActionInfo = new GUIActionInfo();
            if (Layout.Tablet)
            {
                tmp_rect.y = 0.0f;
                tmp_rect.x = 232.0f;
                tmp_rect.w = 560.0f;
                tmp_rect.h = 28.0f;
            }
            else
            {
                tmp_rect.w = 280.0f;
                tmp_rect.h = 14.0f;
                tmp_rect.y = 0.0f;
                tmp_rect.x = ((graphics.screenW - 480.0f) * 0.5f) + 100.0f;
            }
            guiActionInfo.Init(tmp_rect);
            GUIManager.Instance().AddChild(guiActionInfo, true);
            guiActionInfo.Hide();
        }
        else
        {
            guiActionInfo = null;
        }
        // Defeat hint and battle box are resident; victory/end pages are created by events.
        guiDefeated = new GUIDefeated();
        tmp_rect.w = 186.0f;
        tmp_rect.h = 190.0f;
        tmp_rect.x = 0.0f;
        tmp_rect.y = 0.0f;
        guiDefeated.Init(tmp_rect);
        guiDefeated.Hide();
        GUIManager.Instance().AddChild(guiDefeated, true);
        guiDefeated.Center();

        guiBattle = new GUIBattle();
        tmp_rect.x = 0.0f;
        tmp_rect.h = 320.0f;
        if (Layout.Tablet)
        {
            tmp_rect.y = 224.0f;
            tmp_rect.w = 1024.0f;
        }
        else
        {
            tmp_rect.y = 0.0f;
            tmp_rect.w = 480.0f;
        }
        guiBattle.Init(tmp_rect);
        guiBattle.Hide();
        GUIManager.Instance().AddChild(guiBattle, true);

        // Tutorial and normal dialogue are exclusive: only one full screen guidance overlay at a time.
        if (g_GameManager.gameMode == CGameManager.TUTORIAL)
        {
            guiTutorails = new GUITutorails();
            tmp_rect.x = 0.0f;
            tmp_rect.y = 0.0f;
            tmp_rect.w = graphics.screenW;
            tmp_rect.h = graphics.screenH;
            guiTutorails.Init(tmp_rect);
            GUIManager.Instance().AddChild(guiTutorails, false);
            guiDialogue = null;
        }
        else
        {
            guiTutorails = null;
            guiDialogue = new GUIDialogue();
            tmp_rect.x = 0.0f;
            tmp_rect.y = 0.0f;
            tmp_rect.w = graphics.screenW;
            tmp_rect.h = graphics.screenH;
            guiDialogue.Init(tmp_rect);
            GUIManager.Instance().AddChild(guiDialogue, false);
            guiDialogue.Hide();
        }

        float tmp_w = 32.0f, tmp_h = 30.0f;
        if (Layout.Tablet)
        {
            tmp_w = 40.0f;
            tmp_h = 38.0f;
        }
        // Remember the motion handles; fadingInJobs/closeBattleIntro activate the entrance motions with them.
        motionGameButtonRound = GUIMotionManager.Instance().AddMotion(gameButtonRound, graphics.screenW - tmp_w, graphics.screenH - tmp_h, 3.0f, 0);
        motionGameButtonCard = GUIMotionManager.Instance().AddMotion(gameButtonCard, 0.0f, graphics.screenH - tmp_h, 3.0f, 0);
        motionGameButtonPause = GUIMotionManager.Instance().AddMotion(gameButtonPause, graphics.screenW - tmp_w, 0.0f, 3.0f, 0);

        motionGuiGold = GUIMotionManager.Instance().AddMotion(guiGold, 0.0f, 0.0f, 3.0f, 0);
        // Pick one of the four battle tracks at random each time the level is entered.
        // (data/json/audio/audio.json "battle": the original four tracks)
        int random = GetRand() % Math.Max(1, AudioDefs.MusicCount("battle"));
        string tmp_char = AudioDefs.Music("battle", random) ?? Sprintf("battle%d.mp3", random + 1);
        CCSoundBox.GetInstance().LoadMusic(tmp_char, null);
        CCSoundBox.GetInstance().PlayMusic(true);
        GUIManager.Instance().FadeIn(1);

        idleTimer = 0.0f;
        firstTouch = false;
        secondTouch = false;
        firstTouchIndex = 0;
        secondTouchIndex = 0;
        buyingCard = false;
        gameNotInterrupted = false;
        shouldCameraAutoFixPos = false;
        idleTimerEnabled = false;
        // A new game starts its first turn once the UI exists and writes the auto save right away;
        // the load path must not overwrite that auto save.
        if (g_GameManager.isNewGame)
        {
            if (g_GameManager.gameMode != CGameManager.MULTIPLAYER && g_GameManager.gameMode != CGameManager.TUTORIAL)
            {
                g_GameManager.TurnBegin();
                g_GameManager.SaveGame(g_GameManager.gameMode == CGameManager.CONQUEST ? "conquest6.sav" : "game6.sav");
            }
        }
    }

    /// <summary>
    /// Leaves the level. When the save box is in "load" mode, result is set first so the auto
    /// save of the exit flow does not overwrite the source save. Then (what the original binary
    /// did) the auto save is written, every resident GUI, the music and the scene are released.
    /// </summary>
    public override void OnExit()
    {
        if (guiSave != null && guiSave.load)
            g_GameManager.result = true; // block the auto save of the exit flow
        if (guiSave != null)
            guiSave.load = false;

        // original exit flow: auto save unless the battle already has a result
        if (!g_GameManager.result)
            g_GameManager.SaveGame(g_GameManager.gameMode == CGameManager.CONQUEST ? "conquest6.sav" : "game6.sav");

        GUIMotionManager.Instance().ClearMotion();
        GUIManager.Instance().FreeAllChild();
        CCSoundBox.GetInstance().UnloadMusic();
        ReleaseGame();

        gameButtonRound = null;
        gameButtonCard = null;
        gameButtonPause = null;
        gameButtonCardRemove = null;
        guiGold = null;
        guiTax = null;
        guiSmallCard = null;
        guiAttackBox = null;
        guiPauseBox = null;
        guiBuyCard = null;
        guiOptions = null;
        guiSave = null;
        guiBattle = null;
        guiResult = null;
        guiDefeated = null;
        guiaiProgress = null;
        guiSelArmy = null;
        guiBegin = null;
        guiBattleIntro = null;
        guiVictory = null;
        guiTutorails = null;
        guiDialogue = null;
        guiBank = null;
        guiActionInfo = null;
        guiWarning = null;
        guiEnd = null;
    }

    /// <summary>
    /// Per frame update of the state, the animations and the game logic.
    /// SkipMode overrides the time step with a fixed value to push several AI actions per frame.
    /// </summary>
    public override void Update(float deltaTimeS)
    {
        float time = deltaTimeS;
        if (idleTimerEnabled)
        {
            idleTimer -= time;
            if (idleTimer <= 0.0f)
            {
                idleTimer = 0.0f;
                idleTimerEnabled = false;
            }
        }

        // After a card has been used the state must be refreshed: the card may not be usable again.
        if (buyingCard && g_GameManager.GetPlayerCountry().IsActionFinish())
        {
            if (guiBuyCard.CanBuySelCard())
            {
                // the card can still be used
                guiBuyCard.ResetCardTarget();
            }
            else
            {
                // the card cannot be used again, put it away
                guiBuyCard.ReleaseTarget();
                gameButtonCardRemove.Hide();
                gameButtonCard.Show();
                guiSmallCard.Hide();
            }
            buyingCard = false;
        }

        // SkipMode loops several times but pauses when an area changes owner so the player still sees it.
        do
        {
            // Stop the skip loop when any GUI that interrupts the flow is shown
            // (pause menu, options, save, warning, dialogue).
            if ((guiPauseBox.flags & SHOWN) != 0
                || (guiOptions != null && (guiOptions.flags & SHOWN) != 0)
                || (guiSave != null && (guiSave.flags & SHOWN) != 0)
                || (guiWarning != null && (guiWarning.flags & SHOWN) != 0)
                || (guiDialogue != null && (guiDialogue.flags & SHOWN) != 0))
                break;

            // the player is dragging the map
            if (g_GameManager.IsManipulate())
            {
                // update the inertia
                touchInertia.Update(time);
                // get the inertia speed
                if (touchInertia.GetSpeed(out float speedX, out float speedY))
                {
                    // move the scene with the inertia
                    if (g_Scene.Move(-speedX * time, -speedY * time))
                        // hitting the border stops the inertia
                        touchInertia.Stop();
                }
                else if (shouldCameraAutoFixPos)
                {
                    // restore the camera auto fix once the inertia ended
                    g_Scene.camera.SetAutoFixPos(true);
                    shouldCameraAutoFixPos = false;
                }
            }
            else
            {
                // SkipMode fixes the time step to 1, which pushes the game logic very fast
                if (g_GameManager.SkipMode)
                    time = 1.0f;
                // normal speed up mode
                else if (g_GameSettings.speed > 2)
                    time *= (g_GameSettings.speed == 3) ? 1.5f : 2.0f;
            }

            // update the effects
            using (PerfStats.Measure("game.effects")) ecEffectManager.Instance().Update(time);

            // update the scene
            using (PerfStats.Measure("game.scene")) g_Scene.Update(time);

            // update the floating texts
            using (PerfStats.Measure("game.fightText")) g_FightTextMgr.Update(time);

            // Stop the skip loop when the game got interrupted (defeat window, result screen ...).
            if ((guiDefeated != null && (guiDefeated.flags & SHOWN) != 0) || !gameNotInterrupted || g_GameManager.result)
                break;

            // During the bombing animation only the presentation is updated so new country actions
            // do not interleave with the explosion.
            if (!g_Scene.IsBombing())
                using (PerfStats.Measure("game.manager")) g_GameManager.GameUpdate(time);
            // With SkipMode on, keep running GameUpdate until an area change shows up (DisplayAreaChange):
            // only the game logic runs, nothing is rendered in between.
        } while (g_GameManager.SkipMode && !g_GameManager.DisplayAreaChange);
        g_GameManager.DisplayAreaChange = false;
    }

    /// <summary>
    /// Dispatches the in-game events coming from the GUIManager. Map touches go through the
    /// CBaseState touch callbacks (TouchEnd ...), not through here.
    /// Always returns false; handled events rely on the early returns to stop the other GUI branches.
    /// </summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type != Event.GUI) return false;

        var gui = evt.info.gui;

        switch (gui.type)
        {
            case Event.Info.GUIInfo.BUTTON:
            {
                // The order is the hit priority: a pointer belongs to a single overlay, stop once matched.
                // main in-game buttons
                if (mainBattleSceneButtonCheck(evt)) return false;

                // pause box
                if (pauseBoxCheck(evt)) return false;

                // buy card panel
                if (guiBuyCardCheck(evt)) return false;

                // options: only OK / Cancel are handled here
                if (guiOptionsOkCancelCheck(evt)) return false;

                // save panel and in-game load panel (specific to the C version)
                if (guiSaveActionCheck(evt)) return false;

                // attack box
                if (atkBoxActionCheck(evt)) return false;

                // country defeated panel
                if (guiDefeatedCheck(evt)) return false;

                // result screen
                if (guiResultCheck(evt)) return false;

                // turn begin screen
                if (guiBeginCheck(evt)) return false;

                // bank
                if (guiBankCheck(evt)) return false;

                // back button of the end screen
                if (guiEnd != null && ReferenceEquals(guiEnd.buttonBack, gui.ptr))
                    GUIManager.Instance().FadeOut(9, null);

                // warning
                if (guiWarningCheck(evt)) return false;

                break;
            }

            case Event.Info.GUIInfo.FADE_OUT:
            {
                // after a complete fade out
                fadingOutJobs(evt);
                break;
            }

            case Event.Info.GUIInfo.FADE_IN:
            {
                // after the fade in
                fadingInJobs(evt);
                break;
            }

            case Event.Info.GUIInfo.WARNING:
            {
                // The original GUI uses WARNING(1) to resume the flow, e.g. unblocking after the battle animation.
                if (gui.info == 1)
                    gameNotInterrupted = true;
                break;
            }

            case Event.Info.GUIInfo.OTHER:
            {
                // OTHER.info is a flow number agreed with each GUI, converted here into CGameState operations.
                switch (gui.info)
                {
                    case 3:
                        // after defeat or victory
                        afterFailureOrVictory();
                        break;

                    case 4:
                        // card usage (the original had multiplayer code here that was not rewritten)
                        setPlayerUseCardAction();
                        break;

                    case 10:
                        // close the battle intro
                        closeBattleIntro();
                        break;

                    // (13 / 14 opened and closed the in-app store of the original: removed, nothing is sold)

                    case 16:
                        // dialogue and victory animation
                        setDialogueAndVictoryAnimation();
                        break;

                    default:
                        break;
                }
                break;
            }

            default:
                break;
        }

        return false;
    }

    /// <summary>
    /// Dispatches the fixed main buttons (round, pause, card, cancel card).
    /// Returns true when the event belongs to a main button so the caller stops dispatching.
    /// </summary>
    public bool mainBattleSceneButtonCheck(Event evt)
    {
        if (mainGameButtonRoundCheck(evt))
            // end turn / speed up
            return true;
        if (mainGameButtonPauseCheck(evt))
            // pause
            return true;
        if (mainGameButtonCardCheck(evt))
            // use card
            return true;
        // stop using the card
        return mainGameButtonCardRemoveCheck(evt);
    }

    /// <summary>
    /// Renders the map, the global effects and the fight texts. The tablet layout always renders
    /// the map; the other layouts skip it while the buy card panel is visible, but the effects and
    /// the fight texts are still drawn. A wrapping map draws all three once per visible copy.
    /// </summary>
    public override void Render()
    {

        g_Scene.ForEachWrapCopy(renderLayers ??= () =>
        {
            if (Layout.Tablet || (guiBuyCard.flags & SHOWN) == 0)
                g_Scene.RenderView();
            ecEffectManager.Instance().Render();
            g_FightTextMgr.Render();
        });
    }

    private Action renderLayers;

    /// <summary>
    /// Saves the level when the game goes to the background. Nothing is written once a battle
    /// result exists; conquest and the other modes use `conquest6.sav` / `game6.sav`.
    /// </summary>
    public override void EnterBackground()
    {
        if (g_GameManager.result) return;

        string saveFileName = g_GameManager.gameMode == CGameManager.CONQUEST ? "conquest6.sav" : "game6.sav";
        g_GameManager.SaveGame(saveFileName);
    }

    /// <summary>
    /// Loads the resources of the battle and initializes the fight system and the touch inertia.
    /// The card atlas is chosen by the content scale factor (normal or HD).
    /// </summary>
    public void InitGame()
    {
        g_SoundRes.Load();
        g_FightTextMgr.Init();
        g_GameRes.Load();

        bool useHdCardTexture = g_contenscalefactor == 2.0f;
        GUIManager.Instance().LoadTextureRes(useHdCardTexture ? "cardtex_hd.xml" : "cardtex.xml", useHdCardTexture);

        g_GameManager.InitBattle();
        touchInertia.Init();
    }

    /// <summary>Releases the scene, the card atlas and the battle resources of this game.</summary>
    public void ReleaseGame()
    {
        g_Scene.Release();

        string cardTextureName = g_contenscalefactor == 2.0f ? "cardtex_hd.xml" : "cardtex.xml";
        GUIManager.Instance().UnloadTextureRes(cardTextureName);

        g_GameRes.Release();
        g_FightTextMgr.Release();
        g_SoundRes.Unload();
    }

    /// <summary>Refreshes the AI progress panel with the current AI country and its progress. Unchanged for player countries.</summary>
    public void UpdateAIProgress()
    {
        CCountry currentCountry = g_GameManager.GetCurCountry();
        if (currentCountry == null || !currentCountry.ai) return;

        guiaiProgress.SetCurCountryName(currentCountry.FlagKey);
        guiaiProgress.progressCount = CActionAI.Instance().aiProgressPercentage;
    }

    /// <summary>Enables the idle countdown with the given duration (seconds).</summary>
    public void EnableIdleTimer(float duration)
    {
        idleTimer = duration;
        idleTimerEnabled = true;
    }

    /// <summary>Hides the AI progress, restores the player turn buttons and starts a ten second idle timer.</summary>
    public void HideAIProgress()
    {
        guiaiProgress.Hide();
        gameButtonRound.Show();
        gameButtonCard.Show();
        EnableIdleTimer(10.0f);
    }

    /// <summary>Creates and centers a warning window of the given type. Only one warning at a time.</summary>
    public void ShowWarning(int warningType)
    {
        if (guiWarning != null) return;

        guiWarning = new GUIWarning();
        var warningRect = new GUIRect(0.0f, 0.0f, 214.0f, 140.0f);
        guiWarning.Init(warningRect, warningType);
        GUIManager.Instance().AddChild(guiWarning, false);
        guiWarning.Center();
    }

    /// <summary>
    /// Refreshes the "who is acting" hint of the traditional multiplayer mode: hidden for the
    /// local player, fixed text for a remote AI, the device name for a remote player.
    /// </summary>
    public void UpdateActionInfo()
    {
        if (g_GameManager.gameMode != CGameManager.MULTIPLAYER) return;

        CCountry currentCountry = g_GameManager.GetCurCountry();
        if (currentCountry.IsLocalPlayer())
        {
            guiActionInfo.Hide();
            return;
        }

        guiActionInfo.Show();
        if (currentCountry.ai)
        {
            guiActionInfo.SetInfo("AI in action.");
            return;
        }

        if (currentCountry.player != null)
        {
            string actionInfo = currentCountry.player.deviceName + " in action.";
            guiActionInfo.SetInfo(actionInfo);
        }
    }

    /// <summary>
    /// Resolves a battle instantly (FightLogics.cpp): first attack, apply, optional second attack.
    /// The battle animation (GUIBattle) is out of scope, so the parameters are unused.
    /// </summary>
    public void StartBattale(int startAreaId, int targetAreaId, int GUIBattlex288, bool animation)
    {
        var fight = new CFight();

        fight.FirstAttack(startAreaId, targetAreaId);
        fight.ApplyResult();
        if (fight.startArmySecondAttack || fight.targetArmySecondAttack)
        {
            fight.SecondAttack();
            fight.ApplyResult();
        }
    }
}
