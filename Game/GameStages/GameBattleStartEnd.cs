namespace WC2;

/// <summary>Battle setup and result evaluation (GameBattleStartEnd.cpp).</summary>
public sealed partial class CGameManager
{
    /// <summary>
    /// Load game from the save menu: reads the header data first, RealLoadGame does the full load.
    /// </summary>
    /// <param name="fileName">save file name</param>
    public void LoadGame(string fileName)
    {
        var tmp_file = new ecFile();
        var ptr = new SaveHeader();
        if (tmp_file.Open(GetDocumentPath(fileName), "rb"))
        {
            SaveIO.ReadSaveHeader(tmp_file, ptr);
            tmp_file.Close();
            gameMode = (GAME_MODE)ptr.gameMode;
            x38 = ptr.eq0;
            mapId = ptr.mapId;
            areasEnable = ptr.areasEnable;
            for (int i = 0; i < 8; i += 2)
            {
                playerCountryName[i] = ptr.playerCountryName[i];
            }
            battleFileName = ptr.battleFileName;
            campaignSeriesId = ptr.campaignSeriesId;
            campaignLevelId = ptr.campaignLevelId;
            loadFileName = fileName;
        }
        isNewGame = false;
        tmp_file.Close();
    }

    /// <summary>
    /// Called after a level was chosen and before the game is initialised; presets the needed data.
    /// </summary>
    public void NewGame(int GameMode, int MapId, int CampaignSeriesId, int LevelId)
    {
        campaignSeriesId = CampaignSeriesId;
        campaignLevelId = LevelId;
        if (MapId >= 0)
            mapId = MapId + 1;
        x38 = 0;
        gameMode = (GAME_MODE)GameMode;
        victoryTurn = 100000;
        greatVictoryTurn = 10;
        string tmp_chars;
        if (GameMode == (int)CAMPAIGN)
        {
            GetBattleKeyName(CampaignSeriesId, LevelId, out tmp_chars);
            BattleDef battle_here = CObjectDef.Instance().GetBattleDef(tmp_chars);
            if (battle_here != null)
            {
                victoryTurn = battle_here.victory;
                greatVictoryTurn = battle_here.greatVictory;
            }
        }
        battleFileName = GetBattleFileName(GameMode, MapId, CampaignSeriesId, LevelId);
        for (int i = 0; i < 8; i++)
        {
            playerCountryName[i] = "";
        }
        isNewGame = true;
    }

    /// <summary>Initialises the level: new game branch and load branch.</summary>
    public void InitBattle()
    {
        if (isNewGame)
        {
            LoadBattle(battleFileName);
            currentDialogueIndex = 0;
            currentTurnNumMinusOne = 0;
            randomRewardMedal = 0;
            if (gameMode != MULTIPLAYER)
            {
                MovePlayerCountryToFront();
                CCountry PlayerCountry = GetPlayerCountry();
                if (PlayerCountry != null)
                {
                    SetPlayercountryname(0, PlayerCountry.name);
                }
                foreach (CCountry Country in allCountry)
                {
                    if (gameMode == CONQUEST)
                    {
                        if (PlayerCountry != null &&
                            Country.alliance == PlayerCountry.alliance)
                        {
                            Country.taxRate = 1.0f;
                        }
                    }

                    if (Country.alliance == 4)
                    {
                        Country.defeated = true;
                    }
                    else
                    {
                        belligerentCountry.Add(Country);
                    }
                }
            }
            else
            {
                foreach (CCountry Country in allCountry)
                {
                    Country.ai = true;
                }
                SetPlayerOfCountry();
            }
            InitCameraPos();
        }
        else
        {
            RealLoadGame(loadFileName);
        }
        result = false;
        campaignRewardMedal = 0;
        localHost = (gameMode != GAME_MODE.MULTIPLAYER);
        AIAction = false;
        SkipMode = false;
        DisplayAreaChange = false;
        g_Scene.AllAreasEncirclement();
        CActionAI.Instance().InitAI();
    }

    /// <summary>
    /// Private helper: the original g_PlayerManager.SetPlayerOfCountry() has no parameters, the
    /// CPlayerManager no-op stub only offers the (CCountry) overload (multiplayer is out of scope).
    /// </summary>
    private static void SetPlayerOfCountry() => g_PlayerManager.SetPlayerOfCountry(null);

    /// <summary>
    /// Battle result check.
    /// </summary>
    /// <returns>whether the game is over</returns>
    public bool CheckAndSetResult()
    {
        if (currentTurnNumMinusOne >= victoryTurn)
        {
            setGameGivenResult(false);
            return true;
        }
        // (seems unused: the timeout check lives in CGameManager::EndTurn)

        // the player's country was defeated
        CCountry player_country = GetPlayerCountry();
        if (player_country.IsConquested())
        {
            setGameGivenResult(false);
            return true;
        }
        // not every enemy country is destroyed yet
        foreach (CCountry country_here in allCountry)
        {
            if (!country_here.IsConquested())
            {
                // a country at lasting peace with the player (scenario relations) need not fall
                if (country_here.alliance != (int)NEUTRAL &&
                    country_here.alliance != player_country.alliance &&
                    !Diplomacy.AtLastingPeace(country_here, player_country))
                {
                    return false;
                }
            }
        }
        // all enemies destroyed: victory (more checks could be added here)
        setGameGivenResult(true);
        return true;
    }

    /// <summary>
    /// Number of stars for the finished level.
    /// </summary>
    public int GetNumVictoryStars()
    {
        // no win, no stars
        if (!resultWin)
            return 0;
        // great victory: 5 stars
        if (currentTurnNumMinusOne + 1 <= greatVictoryTurn)
            return 5;
        // right at the limit: 1 star
        if (currentTurnNumMinusOne + 1 >= victoryTurn)
        {
            return 1;
        }
        // proportional stars
        int res = 4 * (victoryTurn - (currentTurnNumMinusOne + 1)) /
                  (victoryTurn - greatVictoryTurn) + 1;
        if (res < 2)
            res = 2;
        return res;
    }

    /// <summary>
    /// Gives the medals for a victory. Reads the player's progress and tops up the medals when a
    /// higher star count was reached. The medal amounts can be tuned here.
    /// </summary>
    public void BattleVictory()
    {
        if (gameMode != CGameManager.CAMPAIGN || campaignLevelId >= 80)
            return;
        // stars of this victory
        int star = GetNumVictoryStars();
        // no stars: nothing to do
        if (star <= 0)
            return;
        g_Commander.SetBattlePlayed(campaignSeriesId, campaignLevelId);
        int[] MedalReward = { 0, 0, 5, 15, 25, 50 };
        int last_star = g_Commander.GetNumBattleStars(campaignSeriesId, campaignLevelId);
        // no medals when the old result is as good or better
        if (last_star >= star)
            return;
        // pay the difference and update the stars
        campaignRewardMedal = MedalReward[star] - MedalReward[last_star];
        g_Commander.medal += campaignRewardMedal;
        g_Commander.SetNumBattleStars(campaignSeriesId, campaignLevelId, star);
    }
}
