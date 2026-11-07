namespace WC2;

/// <summary>Basic queries and the per frame update of the match (CGameManagerBasics.cpp, InitCameraPos from Camera.cpp).</summary>
public sealed partial class CGameManager
{
    /// <summary>Releases the current battle data.</summary>
    public void Release()
    {
        ClearBattle();
    }

    /// <summary>
    /// GameUpdate at the CGameManager level.
    /// </summary>
    public void GameUpdate(float time)
    {
        // multiplayer branch, not important
        if (!localHost)
        {
            return;
        }
        // the game already has a result, stop updating
        if (result) return;
        // current country
        CCountry Country = GetCurCountry();
        if (Country == null)
        {
            return;
        }

        // update the country
        using (PerfStats.Measure("game.country")) Country.Update(time);
        // the country action is still running: no further update
        if (!Country.IsActionFinish()) return;

        // a newly defeated country shows the defeat window
        CCountry DefeatedCountry;
        using (PerfStats.Measure("game.defeated")) DefeatedCountry = GetNewDefeatedCountry();
        CGameState GameState = CStateManager.Instance().GetStatePtr(GAME) as CGameState;
        if (DefeatedCountry != null)
        {
            GameState.ShowDefeated(DefeatedCountry);
            return;
        }

        // country progress bar
        if (Country.ai)
        {
            using (PerfStats.Measure("game.aiProgress")) GameState.UpdateAIProgress();
            AIAction = true;
        }
        if (!AIAction)
            return;
        Next();
    }

    /// <summary>Immediate game step (also used by the skip logic).</summary>
    public void Next()
    {
        CCountry Country = GetCurCountry();
        if (Country == null)
        {
            return;
        }
        if (!AIAction)
        {
            // the player logic is handled elsewhere
            return;
        }
        // neutral countries are set defeated in InitBattle so their turn is skipped
        if (Country.areaIdList.Count == 0 || Country.defeated)
        {
            EndTurn();
            return;
        }

        // run the AI logic
        ActionNode actionNode;
        using (PerfStats.Measure("ai.decide")) actionNode = CActionAI.Instance().setCpuDriverbyId(currentCountryIndex, x38);
        // no AI action left: end the turn
        if (actionNode == null)
        {
            EndTurn();
            return;
        }
        // translate the node into a country action
        var Action = new CountryAction(CountryAction.NO_ACTION,
            actionNode.cardId,
            actionNode.startAreaId,
            actionNode.targetAreaId,
            actionNode.armyIndex);
        switch (actionNode.actionType)
        {
            default:
                return;
            case ActionNode.CARD_NODE:
                Action.actionType = CountryAction.CARD_ACTION;
                Action.armyIndex = 0;
                break;
            case ActionNode.MOVE_NODE:
                if (actionNode.startAreaId == actionNode.targetAreaId)
                    Action.actionType = CountryAction.INACTIVATE_ACTION;
                else
                    Action.actionType = CountryAction.MOVE_ACTION;
                break;
            case ActionNode.ATTACK_NODE:
                Action.actionType = CountryAction.ATTACK_ACTION;
                break;
            case ActionNode.MOVE_FRONT_NODE:
                Action.actionType = CountryAction.MOVE_FRONT_ACTION;
                break;
        }
        Country.Action(Action);
    }

    /// <summary>Sets the game result.</summary>
    /// <param name="ifWin">whether the player won</param>
    public void setGameGivenResult(bool ifWin)
    {
        result = true;
        resultWin = ifWin;
    }

    /// <summary>Clears the CGameManager data.</summary>
    public void ClearBattle()
    {
        allCountry.Clear();
        belligerentCountry.Clear();
        dialogueList.Clear();
        Diplomacy.Clear();
    }

    /// <summary>Current dialogue from currentDialogueIndex.</summary>
    public DialogueDef GetCurDialogue()
    {
        // out of range
        if (currentDialogueIndex < 0 ||
            currentDialogueIndex >= GetNumDialogues())
        {
            return null;
        }
        return GetDialogueByIndex(currentDialogueIndex);
    }

    public DialogueDef GetDialogueByIndex(int index)
    {
        if (index < 0 || index >= GetNumDialogues())
        {
            return null;
        }
        return dialogueList[index];
    }

    public int GetNumDialogues()
    {
        return dialogueList.Count;
    }

    /// <summary>
    /// String key of the current dialogue.
    /// </summary>
    /// <param name="curDialogueResult">receives the dialogue key</param>
    /// <returns>whether a current dialogue exists</returns>
    public bool GetCurDialogueString(out string curDialogueResult)
    {
        DialogueDef curDialogue = GetCurDialogue();
        if (curDialogue != null)
        {
            string[] chapters = { "axis", "allies", "wto", "nato" };
            campaignSeriesId = campaignSeriesId > 3 ? 3 : campaignSeriesId;
            curDialogueResult = Sprintf("%s battle %d %s %d", chapters[campaignSeriesId], campaignLevelId + 1, curDialogue.commander, curDialogue.index);
            return true;
        }
        curDialogueResult = "";
        return false;
    }

    /// <returns>the newly defeated country, or null</returns>
    public CCountry GetNewDefeatedCountry()
    {
        foreach (CCountry this_country in belligerentCountry)
        {
            if (!this_country.defeated)
            {
                // original: a country is reported defeated once it owns no area at all
                // (BeConquestedBy empties the list; IsConquested alone would also flag a land
                // country that merely lost its land while keeping sea areas)
                if (this_country.areaIdList.Count == 0)
                {
                    this_country.defeated = true;
                    return this_country;
                }
            }
        }
        return null;
    }

    /// <summary>Whether the player is currently in control.</summary>
    public bool IsManipulate()
    {
        CCountry cur_country = GetCurCountry();
        if (cur_country != null)
        {
            CPlayer player_here = cur_country.player;
            if (cur_country.ai || !cur_country.IsActionFinish() || (gameMode == MULTIPLAYER
                && ((player_here) == null || !player_here.local)) || result)
            {
                return false;
            }
            return localHost;
        }
        return localHost;
    }

    /// <summary>The country whose turn it is.</summary>
    public CCountry GetCurCountry()
    {
        if (currentCountryIndex < 0)
        {
            return null;
        }
        return GetCountryByIndex(currentCountryIndex);
    }

    public CCountry GetCountryByIndex(int index)
    {
        return allCountry[index];
    }

    public void SetPlayercountryname(int index, string name)
    {
        if (index <= 3)
        {
            playerCountryName[2 * index] = name ?? "";
        }
    }

    /// <summary>
    /// Country name of a player slot. Every logical name uses two consecutive 4 byte entries,
    /// hence the 2 * index.
    /// </summary>
    /// <param name="index">player slot, 0 to 3 in the original</param>
    /// <returns>the country name; null when the index is above 3</returns>
    public string GetPlayerCountryName(int index)
    {
        if (index > 3)
        {
            return null;
        }
        return playerCountryName[2 * index];
    }

    /// <summary>
    /// Player slot of a country name.
    /// </summary>
    /// <returns>the matching slot; -1 when not found</returns>
    public int GetPlayerNo(string countryName)
    {
        for (int playerNo = 0; playerNo < 4; ++playerNo)
        {
            string storedName = playerCountryName[2 * playerNo];
            if (!string.IsNullOrEmpty(storedName) && countryName == storedName)
            {
                return playerNo;
            }
        }
        return -1;
    }

    public void SetConquestPlayerCountryID(string idIn)
    {
        conquestPlayerCountryId = idIn ?? "";
    }

    /// <summary>Restart the game: sets isNewGame.</summary>
    public void RetryGame()
    {
        isNewGame = true;
    }

    /// <summary>Whether this is the last battle of the campaign chapter (decides the chapter CG).</summary>
    public bool IsLastBattle()
    {
        if (gameMode == CAMPAIGN)
        {
            return (campaignLevelId == GetNumBattles(campaignSeriesId) - 1);
        }
        else
        {
            return false;
        }
    }

    public int GetNumCountries()
    {
        return allCountry.Count;
    }

    /// <summary>Finds a country by id.</summary>
    public CCountry FindCountry(string idIn)
    {
        foreach (CCountry i in allCountry)
        {
            if (i.id == idIn)
            {
                return i;
            }
        }
        return null;
    }

    /// <summary>The conquest being played (conquests.json "conquest N"); null in the other modes.</summary>
    public ConquestDef CurrentConquestDef() =>
        gameMode == CONQUEST ? CObjectDef.Instance().GetConquestDef(GetConquestKeyName(campaignLevelId)) : null;

    /// <summary>
    /// Hands the conquest's map options (fogOfWar, leftRightWrap) to the scene; called before every
    /// g_Scene.Init (new game and loaded save alike). Other modes play without either.
    /// </summary>
    public void ApplySceneOptions()
    {
        ConquestDef conquest = CurrentConquestDef();
        g_Scene.wrapX = conquest?.leftRightWrap ?? false;
        g_Scene.fogOfWar = conquest?.fogOfWar ?? false;
    }

    public CCountry GetPlayerCountry()
    {
        for (int i = currentCountryIndex; ;)
        {
            if (!allCountry[i].ai)
            {
                return allCountry[i];
            }
            i++;
            if (i >= GetNumCountries())
            {
                i = 0;
            }
            if (i == currentCountryIndex)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Moves the first player country to the front of the country list. Only the first non-AI
    /// country is handled; nothing changes when there is no player country.
    /// </summary>
    public void MovePlayerCountryToFront()
    {
        for (int countryIt = 0; countryIt < allCountry.Count; ++countryIt)
        {
            CCountry country = allCountry[countryIt];
            if (!country.ai)
            {
                allCountry.RemoveAt(countryIt);
                allCountry.Insert(0, country);
                return;
            }
        }
    }

    public CCountry GetLocalPlayerCountry()
    {
        for (int i = currentCountryIndex; ;)
        {
            if (allCountry[i].IsLocalPlayer())
            {
                return allCountry[i];
            }
            i++;
            if (i >= GetNumCountries())
            {
                i = 0;
            }
            if (i == currentCountryIndex)
            {
                return null;
            }
        }
    }

    public void NextDialogue()
    {
        currentDialogueIndex++;
    }

    /// <summary>Camera on the highest value area of the current country (Camera.cpp).</summary>
    public void InitCameraPos()
    {
        CCountry this_country = GetCurCountry();
        if (this_country != null)
        {
            int area_here = this_country.GetHighestValueArea();
            if (area_here >= 0)
            {
                g_Scene.SetCameraToArea(area_here);
            }
        }
    }
}
