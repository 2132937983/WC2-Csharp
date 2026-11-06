namespace WC2;

/// <summary>Turn start (CGameManager part of GameTurnBegin.cpp).</summary>
public sealed partial class CGameManager
{
    /// <summary>
    /// Turn begin; player and AI turns are handled differently.
    /// </summary>
    public void TurnBegin()
    {
        CCountry pCountry = GetCurCountry();

        if (pCountry == null)
        {
            return;
        }
        // the country exists: run its turn begin
        pCountry.TurnBegin();
        AIAction = false;
        CGameState game = CStateManager.Instance().GetStatePtr(GAME) as CGameState;
        if (pCountry.ai)
        {
            // show the AI progress bar
            game.guiaiProgress.Show();
        }
        else
        {
            // player turn: hide the AI progress bar
            game.HideAIProgress();
            if (gameMode != CGameManager.TUTORIAL)
            {
                game.PlayerCountryBegin();
            }
            SkipMode = false;
            // focus the camera on the highest value area
            int areaId = pCountry.GetHighestValueArea();
            if (areaId >= 0)
            {
                var action = new CountryAction(CountryAction.CAMERA_ACTION, NO_CARD, 0, areaId, 0);
                pCountry.Action(action);
            }
        }
    }
}
