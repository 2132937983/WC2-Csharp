namespace WC2;

/// <summary>Turn end (CGameManager part of GameTurnEnd.cpp).</summary>
public sealed partial class CGameManager
{
    /// <summary>
    /// Ends the turn at the CGameManager level and starts the next country's turn.
    /// </summary>
    public void EndTurn()
    {
        TurnEnd();
        int countryCount = GetNumCountries();
        // walk the country list to the first country that is not defeated
        for (int i = 0; i < countryCount; i++)
        {
            currentCountryIndex++;
            if (currentCountryIndex >= countryCount)
            {
                currentCountryIndex = 0;
                currentTurnNumMinusOne++;
                // out of time, and not multiplayer
                if (currentTurnNumMinusOne >= victoryTurn && gameMode != MULTIPLAYER)
                {
                    setGameGivenResult(false);
                    CGameState pCGameState = CStateManager.Instance().GetStatePtr(GAME) as CGameState;
                    // defeat: show the defeat dialogue
                    pCGameState.ShowDialogue("commander failure 2", "Guider", false);
                    return;
                }
            }
            if (!(GetCurCountry().defeated))
            {
                break;
            }
        }

        if (GetCurCountry().IsLocalPlayer())
        {
            CGameState new_country_state = CStateManager.Instance().GetStatePtr(GAME) as CGameState;
            new_country_state.HideAIProgress();
        }
        TurnBegin();
    }

    /// <summary>Turn end: mostly the current country's TurnEnd.</summary>
    public void TurnEnd()
    {
        CCountry country = GetCurCountry();
        if (country != null)
        {
            country.TurnEnd();
        }
    }
}
