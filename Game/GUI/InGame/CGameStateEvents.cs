namespace WC2;

/// <summary>
/// CGameStateEvents.cpp: button responses of the in-game overlays, flow switches and the
/// action submission after a map tap. Creation, per frame update and the central event
/// dispatch live in CGameStateLifeCycle.cs.
/// </summary>
public sealed partial class CGameState
{
    /// <summary>
    /// Round button: ends the turn when the player may act; asks to skip the animation while the AI acts.
    /// With the battle animation on only AIAction is set (Update -> GameUpdate pushes the rest);
    /// without it EndTurn runs directly, the AI progress is shown and advanced once.
    /// </summary>
    public bool mainGameButtonRoundCheck(Event evt)
    {
        if (!ReferenceEquals(evt.info.gui.ptr, gameButtonRound)) return false;
        if (g_GameManager.IsManipulate())
        {
            if (guiBuyCard.cardCanTarget)
            {
                guiBuyCard.ReleaseTarget();
                gameButtonCardRemove.Hide();
                gameButtonCard.Show();
                guiSmallCard.Hide();
                buyingCard = false;
            }

            // Card targeting, the selection and the touch state must be undone before the turn ends,
            // so no input of the previous turn leaks into the AI turn.
            gameButtonCard.Hide();
            ResetTouchState();
            UnselectArea();
            g_Scene.camera.SetAutoFixPos(true);
            shouldCameraAutoFixPos = false;
            g_Scene.newRoundFlashing = false;
            if (g_GameSettings.aiHosting)
            {
                g_GameManager.AIAction = true;
                CActionAI.Instance().InitAI();
            }
            else
            {
                guiaiProgress.Show();
                g_GameManager.EndTurn();
                CActionAI.Instance().InitAI();
                UpdateAIProgress();
            }
        }
        else
        {
            if (g_GameManager.AIAction)
                g_GameManager.SkipMode = true;
        }
        return true;
    }

    /// <summary>Opens the buy card page; a pending target selection and its HUD are undone first.</summary>
    public bool mainGameButtonCardCheck(Event evt)
    {
        if (!ReferenceEquals(evt.info.gui.ptr, gameButtonCard)) return false;
        if (g_GameManager.IsManipulate())
        {
            ResetTouchState();
            if (guiBuyCard != null && guiBuyCard.cardCanTarget)
            {
                guiBuyCard.ReleaseTarget();
                gameButtonCardRemove.Hide();
                gameButtonCard.Show();
                guiSmallCard.Hide();
                buyingCard = false;
            }
            // The buy window is resident; opening it only resets the selection / target state.
            GUIManager.Instance().MoveToFront(guiBuyCard);
            guiBuyCard.ResetCardState();
            guiBuyCard.Show();
            g_SoundRes.PlayCharSE(CARD_INTERFACE_WAV);
        }
        return true;
    }

    /// <summary>Shows the pause layer and stops interpreting the current map gesture.</summary>
    public bool mainGameButtonPauseCheck(Event evt)
    {
        if (!ReferenceEquals(evt.info.gui.ptr, gameButtonPause)) return false;
        ResetTouchState();
        GUIManager.Instance().MoveToFront(guiPauseBox);
        guiPauseBox.Show();
        return true;
    }

    /// <summary>Cancels the card waiting for a target and restores the normal card button.</summary>
    public bool mainGameButtonCardRemoveCheck(Event evt)
    {
        if (!ReferenceEquals(evt.info.gui.ptr, gameButtonCardRemove)) return false;
        gameButtonCardRemove.Hide();
        gameButtonCard.Show();
        guiSmallCard.Hide();
        guiBuyCard.ReleaseTarget();
        return true;
    }

    /// <summary>
    /// Dispatches the pause box buttons to their handlers. The return value tells whether the
    /// event belongs to the pause box so OnEvent stops the other GUI handlers.
    /// </summary>
    public bool pauseBoxCheck(Event evt)
    {
        GUIPauseBox pauseBox = guiPauseBox;
        if (pauseBox == null) return false;
        if (pauseBoxOptionCheck(evt, pauseBox)) return true;
        if (pauseBoxRestartCheck(evt, pauseBox)) return true;
        if (pauseBoxSaveCheck(evt, pauseBox)) return true;
        return pauseBoxQuitCheck(evt, pauseBox);
    }

    /// <summary>Temporarily replaces the pause box with the options page; the pause box is shown again when it closes.</summary>
    public bool pauseBoxOptionCheck(Event evt, GUIPauseBox box_in)
    {
        if (!ReferenceEquals(evt.info.gui.ptr, box_in.buttonOption)) return false;
        box_in.Hide();
        guiOptions = new GUIOptions();
        var option_rect = new GUIRect(0.0f, 0.0f, ecGraphics.Instance().screenW, ecGraphics.Instance().screenH);
        guiOptions.Init(option_rect);
        GUIManager.Instance().AddChild(guiOptions, false);
        GUIManager.Instance().MoveToFront(guiOptions);
        guiOptions.Show();
        return true;
    }

    /// <summary>Exports the current battle XML before opening the binary save panel.</summary>
    public bool pauseBoxSaveCheck(Event evt, GUIPauseBox box_in)
    {
        if (!ReferenceEquals(evt.info.gui.ptr, box_in.buttonSave)) return false;
        g_GameManager.SaveBattleJson("game.json"); // the battle as a scenario JSON in the documents folder (the remake wrote game.xml)
        OpenSaveGUI(false);
        return true;
    }

    /// <summary>"Restart" actually enters the in-game load flow here.</summary>
    public bool pauseBoxRestartCheck(Event evt, GUIPauseBox box_in)
    {
        if (!ReferenceEquals(evt.info.gui.ptr, box_in.buttonRestart)) return false;
        OpenSaveGUI(true);
        return true;
    }

    /// <summary>Fade out number 9 is interpreted by fadingOutJobs as "back to the main menu".</summary>
    public bool pauseBoxQuitCheck(Event evt, GUIPauseBox box_in)
    {
        if (!ReferenceEquals(evt.info.gui.ptr, box_in.buttonQuit)) return false;
        GUIManager.Instance().FadeOut(9, null);
        return true;
    }

    /// <summary>
    /// Back and OK of the buy card page. OK does not use the card right away but enters the
    /// "pick a map target" state; the real CountryAction is submitted by the map touch or by
    /// the OTHER event of the army selection page.
    /// </summary>
    public bool guiBuyCardCheck(Event evt)
    {
        if (guiBuyCard == null) return false;

        if (ReferenceEquals(guiBuyCard.buttonBack, evt.info.gui.ptr))
        {
            guiBuyCard.Hide();
            return true;
        }

        if (ReferenceEquals(guiBuyCard.buttonOk, evt.info.gui.ptr))
        {
            if (guiBuyCard.GetSelCard() == null || !guiBuyCard.cardCanTarget) return true;
            gameButtonCardRemove.Show();
            gameButtonCard.Hide();
            guiSmallCard.SetCard(guiBuyCard.GetSelCard());
            guiSmallCard.Show();
            return true;
        }
        return false;
    }

    /// <summary>Destroys the temporary options page and restores the pause box below it.</summary>
    public bool guiOptionsOkCancelCheck(Event evt)
    {
        if (guiOptions == null) return false;
        if (ReferenceEquals(evt.info.gui.ptr, guiOptions.buttonOk) || ReferenceEquals(evt.info.gui.ptr, guiOptions.buttonBack))
        {
            GUIManager.Instance().SafeFreeChild(guiOptions);
            guiOptions = null;
            GUIManager.Instance().MoveToFront(guiPauseBox);
            guiPauseBox.Show();
            return true;
        }
        return false;
    }

    /// <summary>Forwards OK / cancel of the save / load box to the matching flow.</summary>
    public bool guiSaveActionCheck(Event evt)
    {
        if (guiSave == null) return false;
        if (ReferenceEquals(evt.info.gui.ptr, guiSave.buttonOk)) return GUISaveOKEvent();
        if (ReferenceEquals(evt.info.gui.ptr, guiSave.buttonBack)) return GUISaveCancelEvent();
        return false;
    }

    /// <summary>
    /// After the attack confirmation the start and target stored by the attack box become a
    /// CountryAction. The GUI only confirms; the battle and the damage are handled by the
    /// country action and the fight module.
    /// </summary>
    public bool atkBoxActionCheck(Event evt)
    {
        if (guiAttackBox == null) return false;
        if (ReferenceEquals(guiAttackBox.buttonOk, evt.info.gui.ptr))
        {
            guiAttackBox.Hide();
            var this_action = new CountryAction
            {
                actionType = CountryAction.ATTACK_ACTION,
                startAreaId = guiAttackBox.startAreaId,
                targetAreaId = guiAttackBox.targetAreaId,
                armyIndex = 0,
            };
            g_GameManager.GetPlayerCountry().Action(this_action);
            return true;
        }
        return false;
    }

    /// <summary>The overall result is checked only after the single country defeat hint closes, so consecutive defeats do not enter the result too early.</summary>
    public bool guiDefeatedCheck(Event evt)
    {
        if (guiDefeated == null) return false;
        if (ReferenceEquals(guiDefeated.buttonOk, evt.info.gui.ptr))
        {
            guiDefeated.HideDefeated();
            if (!g_GameManager.CheckAndSetResult()) return true; // is there a result (e.g. victory)?
            if (g_GameManager.gameMode != CGameManager.MULTIPLAYER)
            {
                spawnGUIVictory();
                return true;
            }
        }
        return false;
    }

    /// <summary>Builds the dialogue key from the result, the stars and the new medals and shows the guide dialogue before the result.</summary>
    public void spawnGUIVictory()
    {
        string key;
        if (g_GameManager.resultWin)
        {
            g_GameManager.BattleVictory();
            int stars = g_GameManager.GetNumVictoryStars();
            if (g_GameManager.campaignRewardMedal > 0 || stars <= 1)
                key = Sprintf("commander victory %d", 6 - stars);
            else
                key = Sprintf("commander victory %d no award", 6 - g_GameManager.GetNumVictoryStars());
        }
        else
        {
            key = "commander failure 1";
        }
        ShowDialogue(key, "Guider", false);
    }

    /// <summary>Final result page: back to the menu, next level / chapter end, or retry through the load state.</summary>
    public bool guiResultCheck(Event evt)
    {
        if (guiResult == null) return false;
        if (ReferenceEquals(guiResult.buttonBack, evt.info.gui.ptr))
        {
            GUIManager.Instance().FadeOut(9, null);
            return true;
        }
        if (ReferenceEquals(guiResult.buttonNext, evt.info.gui.ptr))
        {
            if (g_GameManager.gameMode == CGameManager.CAMPAIGN)
            {
                if (g_GameManager.IsLastBattle())
                {
                    GUIManager.Instance().FadeOut(20, null);
                }
                else
                {
                    g_GameManager.selectNextBattle = true;
                    GUIManager.Instance().FadeOut(9, null);
                }
            }
            else
            {
                GUIManager.Instance().FadeOut(9, null);
            }
            return true;
        }

        // after a defeat a restart is allowed
        if (ReferenceEquals(guiResult.buttonRestart, evt.info.gui.ptr))
        {
            g_GameManager.RetryGame();
            GUIManager.Instance().FadeOut(14, null);
            return true;
        }
        return false;
    }

    /// <summary>Turn begin hint of the player; the bank page is created on demand and released by guiBankCheck.</summary>
    public bool guiBeginCheck(Event evt)
    {
        if (guiBegin == null) return false;
        if (ReferenceEquals(guiBegin.buttonOk, evt.info.gui.ptr))
        {
            checkDialogueShown();
            g_Scene.newRoundFlashing = true;
            return true;
        }

        if (ReferenceEquals(guiBegin.buttonBank, evt.info.gui.ptr))
        {
            guiBank = new GUIBank();
            var tmp_rect_begin_bank = new GUIRect(0.0f, 0.0f, ecGraphics.Instance().screenW, ecGraphics.Instance().screenH);
            guiBank.Init(tmp_rect_begin_bank);
            GUIManager.Instance().AddChild(guiBank, false);
            return true;
        }
        return false;
    }

    /// <summary>After the bank closes the turn begin data is reset so gold / loan are recomputed.</summary>
    public bool guiBankCheck(Event evt)
    {
        if (guiBank == null) return false;
        if (ReferenceEquals(guiBank.buttonBack, evt.info.gui.ptr))
        {
            GUIManager.Instance().SafeFreeChild(guiBank);
            guiBank = null;
            guiBegin.ResetData();
            return true;
        }
        return false;
    }

    /// <summary>Warning cancel goes back to the pause box; OK leaves the level and stops the frame from advancing.</summary>
    public bool guiWarningCheck(Event evt)
    {
        if (guiWarning == null) return false;
        if (ReferenceEquals(guiWarning.buttonCancel, evt.info.gui.ptr))
        {
            GUIManager.Instance().SafeFreeChild(guiWarning);
            guiWarning = null;
            GUIManager.Instance().MoveToFront(guiPauseBox);
            guiPauseBox.Show();
            return true;
        }
        if (ReferenceEquals(guiWarning.buttonOk, evt.info.gui.ptr))
        {
            GUIManager.Instance().FadeOut(9, null);
            gameNotInterrupted = false;
            return true;
        }
        return false;
    }

    /// <summary>
    /// State switches by the fade out number. The numbers are agreed at the FadeOut call site;
    /// GUIManager does not infer them.
    /// </summary>
    public void fadingOutJobs(Event evt)
    {
        if (evt.info.gui.info == 9)
        {
            CStateManager.Instance().SetCurState(MENU);
        }
        else if (evt.info.gui.info == 14)
        {
            CStateManager.Instance().SetCurState(LOAD);
        }
        else if (evt.info.gui.info == 20)
        {
            GUIManager.Instance().SafeFreeChild(guiResult);
            guiResult = null;
            var pGuiEnd = new GUIEnd();
            guiEnd = pGuiEnd;
            var new_rect = new GUIRect(0.0f, 0.0f, ecGraphics.Instance().screenW, ecGraphics.Instance().screenH);
            pGuiEnd.Init(new_rect);
            GUIManager.Instance().AddChild(guiEnd, false);
            GUIManager.Instance().FadeIn(-1);
        }
    }

    /// <summary>Entering a level or a conquest.</summary>
    public void fadingInJobs(Event evt)
    {
        // Number 1 is the first fade in after entering the level: campaigns show the intro first,
        // the other modes start the main button motions right away.
        if (evt.info.gui.info == 1)
        {
            GUIRect rect_this;
            if (g_GameManager.gameMode == CGameManager.CAMPAIGN)
            {
                if (Layout.Tablet)
                    rect_this = new GUIRect(32.0f, 32.0f, 960.0f, 640.0f);
                else
                    rect_this = new GUIRect((ecGraphics.Instance().screenW - 480.0f) * 0.5f, 0.0f, 480.0f, 320.0f);
                guiBattleIntro = new GUIBattleIntro();
                guiBattleIntro.Init(rect_this, g_GameManager.campaignSeriesId, g_GameManager.campaignLevelId);
                GUIManager.Instance().AddChild(guiBattleIntro, false);
                // the battle intro is loaded here (a conquest intro could be done the same way)
            }
            else if (g_GameManager.gameMode == CGameManager.CONQUEST)
            {
                GUIMotionManager.Instance().ActiveMotion(motionGameButtonRound, 0);
                GUIMotionManager.Instance().ActiveMotion(motionGameButtonCard, 0);
                GUIMotionManager.Instance().ActiveMotion(motionGameButtonPause, 0);
                GUIMotionManager.Instance().ActiveMotion(motionGuiGold, 0);
            }
            else
            {
                GUIMotionManager.Instance().ActiveMotion(motionGameButtonRound, 0);
                GUIMotionManager.Instance().ActiveMotion(motionGameButtonCard, 0);
                GUIMotionManager.Instance().ActiveMotion(motionGameButtonPause, 0);
                GUIMotionManager.Instance().ActiveMotion(motionGuiGold, 0);
            }
            if (g_GameManager.gameMode == CGameManager.MULTIPLAYER)
            {
                // the original branch handles the traditional multiplayer session; not rewritten
            }
        }
    }

    /// <summary>
    /// After the victory / defeat animation: frees the animation layer and creates the final
    /// result page by result. Returns early when guiResult already exists so the same OTHER
    /// event does not create the page twice.
    /// </summary>
    public void afterFailureOrVictory()
    {
        GUIManager.Instance().SafeFreeChild(guiVictory);
        guiVictory = null;
        if (guiResult != null) return; // already set up

        bool result_other_3;
        if (g_GameManager.resultWin)
        {
            if (g_GameManager.gameMode == CGameManager.CAMPAIGN)
                ecShowReview();
            result_other_3 = false;
        }
        else
        {
            result_other_3 = true;
        }
        guiResult = new GUIResult();
        var tmp_rect_other_3 = new GUIRect(0.0f, 0.0f, ecGraphics.Instance().screenW, ecGraphics.Instance().screenH);
        guiResult.Init(tmp_rect_other_3, result_other_3 ? 1 : 0);
        GUIManager.Instance().AddChild(guiResult, false);
    }

    /// <summary>
    /// Converts the target unit chosen in the army selection page into a card action.
    /// Triggered by GUI OTHER(4), for cards that target a single unit of a stack.
    /// </summary>
    public void setPlayerUseCardAction()
    {
        var pCountryAction = new CountryAction
        {
            actionType = CountryAction.CARD_ACTION,
            cardId = guiBuyCard.GetSelCard().id,
            targetAreaId = guiSelArmy.areaId,
            armyIndex = guiSelArmy.targetArmyIndex,
        };
        g_GameManager.GetPlayerCountry().Action(pCountryAction);
    }

    /// <summary>Closes the battle intro and starts the four main HUD entrance motions it was hiding.</summary>
    public void closeBattleIntro()
    {
        if (guiBattleIntro == null) return;
        if (g_GameManager.gameMode == CGameManager.CAMPAIGN)
        {
            GUIMotionManager.Instance().ActiveMotion(motionGameButtonRound, 0);
            GUIMotionManager.Instance().ActiveMotion(motionGameButtonCard, 0);
            GUIMotionManager.Instance().ActiveMotion(motionGameButtonPause, 0);
            GUIMotionManager.Instance().ActiveMotion(motionGuiGold, 0);
            guiBattleIntro.Hide();

            // released here; keep it shown instead if the intro should be reused
            GUIManager.Instance().SafeFreeChild(guiBattleIntro);
            guiBattleIntro = null;
        }
    }

    /// <summary>
    /// Creates the one shot save / load overlay. With load the confirmation fades to the LOAD
    /// state; otherwise the overlay is just closed, GUISave does the saving itself.
    /// (A helper of the C version to share the save and the load flow.)
    /// </summary>
    public void OpenSaveGUI(bool load)
    {
        guiPauseBox.Hide();
        guiSave = new GUISave();
        var rect = new GUIRect(0.0f, 0.0f, ecGraphics.Instance().screenW, ecGraphics.Instance().screenH);
        guiSave.Init(rect, (int)g_GameManager.gameMode, load);
        GUIManager.Instance().AddChild(guiSave, true);
        GUIManager.Instance().MoveToFront(guiSave);
        guiSave.Show();
    }

    /// <summary>OK of the save box: loading fades to LOAD, saving only closes the temporary window.</summary>
    public bool GUISaveOKEvent()
    {
        if (guiSave.load)
        {
            GUIManager.Instance().FadeOut(14, null);
        }
        else
        {
            GUIManager.Instance().SafeFreeChild(guiSave);
            guiSave = null;
        }
        return true;
    }

    /// <summary>Cancel of the save box: releases the temporary window without touching the battle.</summary>
    public bool GUISaveCancelEvent()
    {
        GUIManager.Instance().SafeFreeChild(guiSave);
        guiSave = null;
        return true;
    }

    /// <summary>
    /// Shows one dialogue and advances the cursor when the current turn reached the trigger turn
    /// of the next dialogue. One per call, so the dialogues keep the order of the resource table.
    /// </summary>
    public void checkDialogueShown()
    {
        DialogueDef dialogue_cur = g_GameManager.GetCurDialogue();
        if (dialogue_cur != null && dialogue_cur.atRound <= g_GameManager.currentTurnNumMinusOne + 1)
        {
            g_GameManager.GetCurDialogueString(out string dialogue_string);
            ShowDialogue(dialogue_string, dialogue_cur.commander, dialogue_cur.left);
            g_GameManager.NextDialogue();
        }
    }

    /// <summary>
    /// Single finger drag and two finger pinch on the battle map. One finger moves the map and
    /// records the inertia track; two fingers zoom around the finger that did not move, keeping
    /// the scene point under that anchor fixed.
    /// </summary>
    public override void TouchMove(float x, float y, int index)
    {
        if (!g_GameManager.IsManipulate() || !firstTouch) return;

        if (!secondTouch)
        {
            if (index != firstTouchIndex) return;

            touchInertia.TouchMove(x, y, index);
            g_Scene.Move(touchPos[0][0] - x, touchPos[1][0] - y);
            touchPos[0][0] = x;
            touchPos[1][0] = y;
            return;
        }

        float oldDeltaX = touchPos[0][0] - touchPos[0][1];
        float oldDeltaY = touchPos[1][0] - touchPos[1][1];
        float oldDistance = MathF.Sqrt(oldDeltaX * oldDeltaX + oldDeltaY * oldDeltaY);

        if (index == firstTouchIndex)
        {
            float newDeltaX = x - touchPos[0][1];
            float newDeltaY = y - touchPos[1][1];
            float newDistance = MathF.Sqrt(newDeltaX * newDeltaX + newDeltaY * newDeltaY);

            if (oldDistance > 40.0f && newDistance > 40.0f)
            {
                float anchorX = touchPos[0][1];
                float anchorY = touchPos[1][1];
                g_Scene.ScreenToScene(ref anchorX, ref anchorY);
                var zoomResult = new PinchZoomResult();
                if (CalculatePinchZoom(g_Scene.camera.centerPos[0], g_Scene.camera.centerPos[1], g_Scene.camera.scale,
                                       anchorX, anchorY, oldDistance, newDistance, 0.0f, 0.0f, ref zoomResult))
                {
                    g_Scene.camera.SetPosAndScale(zoomResult.centerX, zoomResult.centerY, zoomResult.scale);
                }
            }

            touchPos[0][0] = x;
            touchPos[1][0] = y;
            return;
        }

        if (index == secondTouchIndex)
        {
            float newDeltaX = touchPos[0][0] - x;
            float newDeltaY = touchPos[1][0] - y;
            float newDistance = MathF.Sqrt(newDeltaX * newDeltaX + newDeltaY * newDeltaY);

            if (oldDistance > 40.0f && newDistance > 40.0f)
            {
                float anchorX = touchPos[0][0];
                float anchorY = touchPos[1][0];
                g_Scene.ScreenToScene(ref anchorX, ref anchorY);
                var zoomResult = new PinchZoomResult();
                if (CalculatePinchZoom(g_Scene.camera.centerPos[0], g_Scene.camera.centerPos[1], g_Scene.camera.scale,
                                       anchorX, anchorY, oldDistance, newDistance, 0.0f, 0.0f, ref zoomResult))
                {
                    g_Scene.camera.SetPosAndScale(zoomResult.centerX, zoomResult.centerY, zoomResult.scale);
                }
            }

            touchPos[0][1] = x;
            touchPos[1][1] = y;
        }
    }

    /// <summary>OTHER(16): in the normal flow shows the due dialogue; in the ending flow creates and plays the result animation.</summary>
    public void setDialogueAndVictoryAnimation()
    {
        if (!g_GameManager.result)
        {
            checkDialogueShown();
        }
        else
        {
            if (guiVictory == null)
            {
                guiVictory = new GUIVictory();
                var victory_other_16 = new GUIRect(0.0f, 0.0f, ecGraphics.Instance().screenW, ecGraphics.Instance().screenH);
                guiVictory.Init(victory_other_16, g_GameManager.resultWin);
                GUIManager.Instance().AddChild(guiVictory, false);
                guiVictory.Play();
            }
        }
    }

    /// <summary>
    /// Map touch end. Only the first finger with a move below the threshold counts as a tap;
    /// everything else is left to the inertia drag.
    /// </summary>
    public override void TouchEnd(float x, float y, int index)
    {
        // check the touching state
        int FirstTouchIndex = firstTouchIndex;
        if (index == secondTouchIndex)
        {
            secondTouch = false;
            secondTouchIndex = 0;
        }
        if (index == firstTouchIndex)
        {
            firstTouch = false;
            firstTouchIndex = 0;
        }
        if (g_GameManager.IsManipulate())
        {
            var action = new CountryAction(CountryAction.NO_ACTION, NO_CARD, 0, 0, 0);
            shouldCameraAutoFixPos = false;
            if (index != FirstTouchIndex) return;
            touchInertia.TouchEnd(x, y, index);
            if (MathF.Abs(firstTouchStartPos[0] - x) >= 15.0f) return;
            if (MathF.Abs(firstTouchStartPos[1] - y) >= 15.0f) return;
            g_Scene.newRoundFlashing = false;
            CArea TouchArea = g_Scene.ScreenToArea(x, y);
            if (TouchArea == null || !TouchArea.info.enable) return;
            // A card target has priority over the normal selection: after the submission wait for the
            // CountryAction to finish, then Update decides whether the card can be used again.
            CardDef Card = guiBuyCard.GetSelCard();
            if (Card != null && guiBuyCard.cardCanTarget && !buyingCard)
            {
                if (g_GameManager.GetPlayerCountry().CheckCardTargetArea(Card, TouchArea.id))
                {
                    action.actionType = CountryAction.CARD_ACTION;
                    action.cardId = Card.id;
                    action.targetAreaId = TouchArea.id;
                    action.armyIndex = 0;
                    g_GameManager.GetPlayerCountry().Action(action);
                    buyingCard = true;
                    guiTax.SetArea(TouchArea.id);
                }
                return;
            }
            // With an own active unit selected, the second tap tries to move or opens the attack box.
            CArea SelectedArea = g_Scene.GetSelectedArea();
            if (SelectedArea != null)
            {
                if (ReferenceEquals(SelectedArea, TouchArea))
                {
                    UnselectArea();
                    g_SoundRes.PlayCharSE(CANCEL_WAV);
                    return;
                }
                // manipulating army
                if (SelectedArea.IsActive() && ReferenceEquals(SelectedArea.country, g_GameManager.GetPlayerCountry()))
                {
                    if (SelectedArea.armyDrafting != null || SelectedArea.armyMovingIn != null || SelectedArea.armyMovingToFront != null)
                        return;
                    if (g_Scene.CheckMoveable(SelectedArea.id, TouchArea.id, 0))
                    {
                        action.actionType = CountryAction.MOVE_ACTION;
                        action.startAreaId = SelectedArea.id;
                        action.armyIndex = 0;
                        action.targetAreaId = TouchArea.id;
                        g_GameManager.GetPlayerCountry().Action(action);
                    }
                    else if (g_Scene.CheckAttackable(SelectedArea.id, TouchArea.id, 0))
                    {
                        guiAttackBox.SetAttack(SelectedArea.id, TouchArea.id);
                        guiAttackBox.Show();
                    }
                    UnselectArea();
                    return;
                }
                UnselectArea();
                g_SoundRes.PlayCharSE(CANCEL_WAV);
            }
            SelectArea(TouchArea.id);
        }
    }

    /// <summary>Clears the map selection and hides the tax and stacked army panels that depend on it.</summary>
    public void UnselectArea()
    {
        g_Scene.UnselectArea();
        guiTax.Hide();
        guiSelArmy.Hide();
    }

    /// <summary>Selects the area and shows its info; with more than one army the army selection side bar is shown.</summary>
    public void SelectArea(int Areaid)
    {
        g_Scene.SelectArea(Areaid);
        guiTax.SetArea(Areaid);
        guiTax.Show();
        // this allows seeing the stacks of other countries; change the condition to turn it off
        bool canSeeSelArmy = g_Scene.GetArea(Areaid).armyCount > 1;
        if (canSeeSelArmy)
        {
            guiSelArmy.SetArea(Areaid);
            guiSelArmy.useCard = false;
            guiSelArmy.Show();
        }
        g_SoundRes.PlayCharSE(SELECT_WAV);
    }

    /// <summary>Brings the defeat hint to the front and hands it the country that was just defeated.</summary>
    public void ShowDefeated(CCountry defeatedCountry)
    {
        GUIManager.Instance().MoveToFront(guiDefeated);
        guiDefeated.ShowDefeated(defeatedCountry);
    }

    /// <summary>Shows the turn begin hint of the local player; its OK checks the dialogues of the turn.</summary>
    public void PlayerCountryBegin()
    {
        guiBegin.ResetData();
        guiBegin.Show();
        g_SoundRes.PlayCharSE(POP_WAV);
    }

    /// <summary>Asks the dialogue layer to show the resource; the tutorial mode has no such layer, so null is allowed.</summary>
    public void ShowDialogue(string dialogueString, string commander, bool left)
    {
        if (guiDialogue != null)
            guiDialogue.ShowDlg(dialogueString, commander, left);
    }

    /// <summary>Compatibility handler of the retired diplomacy entry; only reports the button hit.</summary>
    public bool GUIDIButtonCheck(Event evt)
    {
        if (!ReferenceEquals(evt.info.gui.ptr, GameButtonDI)) return false;
        return true;
    }

    /// <summary>
    /// Mouse wheel zoom of the map. Only while the player may act, without a touch gesture and
    /// while the camera is not moving by itself. Zooms around the camera center; the minimum
    /// scale depends on the content scale factor.
    /// </summary>
    public override void ScrollWheel(float x, float delta, float y)
    {
        if (!g_GameManager.IsManipulate() || firstTouch || secondTouch || g_Scene.camera.isMoving)
            return;

        float scale = g_Scene.camera.scale + delta * 0.1f;
        float minimumScale = g_contenscalefactor == 2.0f ? 0.5f : 0.68f;
        if (scale > 1.0f) scale = 1.0f;
        else if (scale < minimumScale) scale = minimumScale;

        g_Scene.camera.SetPosAndScale(g_Scene.camera.centerPos[0], g_Scene.camera.centerPos[1], scale);
    }

    /// <summary>Clears the one / two finger touch state and re-initializes the inertia record.</summary>
    public void ResetTouchState()
    {
        firstTouch = false;
        firstTouchIndex = 0;
        secondTouch = false;
        secondTouchIndex = 0;
        touchInertia.Init();
    }

    /// <summary>
    /// Back key in the level. The tutorial leaves the level; a shown pause box is closed; when the
    /// battle screen is hidden the touch state is cleared and the pause box opened. Always handled.
    /// </summary>
    public override bool BackPressed()
    {
        if (g_GameManager.gameMode == CGameManager.TUTORIAL)
        {
            GUIManager.Instance().FadeOut(9, null);
            return true;
        }

        if ((guiPauseBox.flags & SHOWN) != 0)
        {
            guiPauseBox.Hide();
        }
        else if ((guiBattle.flags & SHOWN) == 0)
        {
            ResetTouchState();
            GUIManager.Instance().MoveToFront(guiPauseBox);
            guiPauseBox.Show();
        }

        return true;
    }

    /// <summary>
    /// Starts recording a map touch and switches to the two finger gesture when a second touch
    /// appears. When the multi touch manager only holds the current touch the previous state is
    /// cleared first. The first touch also starts the inertia track; any valid touch temporarily
    /// disables the camera auto fix.
    /// </summary>
    public override void TouchBegin(float x, float y, int index)
    {
        if (!g_GameManager.IsManipulate()) return;

        ecMultipleTouch multipleTouch = ecMultipleTouch.Instance();
        if (multipleTouch.touch.Count == 1)
            ResetTouchState();

        if (firstTouchIndex != 0)
        {
            if (secondTouchIndex == 0)
            {
                secondTouchIndex = index;
                secondTouch = true;
                touchPos[0][1] = x;
                touchPos[1][1] = y;
            }
        }
        else
        {
            firstTouchStartPos[0] = x;
            firstTouchStartPos[1] = y;
            firstTouch = true;
            firstTouchIndex = index;
            touchPos[0][0] = x;
            touchPos[1][0] = y;
            touchInertia.TouchBegin(x, y, index);
        }

        g_Scene.camera.SetAutoFixPos(false);
        shouldCameraAutoFixPos = false;
    }
}
