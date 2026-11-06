namespace WC2;

/// <summary>The player's persistent commander profile (CCommander.cpp + CampaignSize.cpp parts).</summary>
public sealed partial class CCommander
{
    public bool canSave;
    public int level;
    public int medal;
    public int boughtMedal;
    public int[] warMedal = new int[6];
    public int[] campaignProgress = new int[SERIES_CNT];
    public int[][] campaignStar = NewStars();

    private static int[][] NewStars()
    {
        var a = new int[SERIES_CNT][];
        for (int i = 0; i < SERIES_CNT; i++) a[i] = new int[SERIES_MAX];
        return a;
    }

    // medals needed for the next commander level
    private static int[] g_CommanderUpgradeMedalCosts = BuiltInUpgradeMedalCosts();

    public static int[] BuiltInUpgradeMedalCosts() => new[]
    {
        5, 8, 12, 19, 30, 48, 75,
        120, 190, 300, 480, 760, 1200, 2000
    };

    // commander abilities per level: {attack, defence, turnRecovery, instantRecovery, maxHpPercentage}, coolDown;
    // data/commanders.json "levels" replaces the built-in table
    private static CommanderAbility[] commanderAbilities = BuiltInLevels();

    public static IReadOnlyList<CommanderAbility> LevelTable => commanderAbilities;
    public static IReadOnlyList<int> UpgradeMedalCosts => g_CommanderUpgradeMedalCosts;

    /// <summary>Replaces the commander level table (MAX_COMMANDER_LEVEL + 1 levels; missing entries keep the built-in values).</summary>
    public static void SetLevelTable(IList<CommanderAbility> levels, IList<int> medalsToNext)
    {
        var table = BuiltInLevels();
        var costs = BuiltInUpgradeMedalCosts();
        for (int i = 0; i < table.Length && levels != null && i < levels.Count; i++) if (levels[i] != null) table[i] = levels[i];
        for (int i = 0; i < costs.Length && medalsToNext != null && i < medalsToNext.Count; i++) costs[i] = medalsToNext[i];
        commanderAbilities = table;
        g_CommanderUpgradeMedalCosts = costs;
    }

    public static CommanderAbility[] BuiltInLevels() => new[]
    {
        Ability(0, 0, 0, 10, 5, 4),
        Ability(0, 0, 0, 15, 10, 4),
        Ability(0, 0, 0, 20, 15, 4),
        Ability(1, 0, 0, 25, 20, 4),
        Ability(1, 0, 0, 30, 25, 4),
        Ability(1, 0, 1, 35, 30, 3),
        Ability(1, 1, 2, 40, 35, 3),
        Ability(1, 1, 3, 45, 40, 3),
        Ability(1, 1, 4, 50, 45, 3),
        Ability(2, 1, 5, 55, 50, 3),
        Ability(2, 1, 6, 60, 55, 2),
        Ability(2, 1, 7, 65, 60, 2),
        Ability(3, 1, 8, 70, 65, 2),
        Ability(3, 1, 9, 75, 70, 2),
        Ability(3, 2, 10, 80, 75, 2),
    };

    private static CommanderAbility Ability(int attack, int defence, int turnRecovery, int instantRecovery, int maxHpPercentage, int coolDown)
        => new CommanderAbility
        {
            armyAbility = new ArmyAbility(attack, defence, turnRecovery, instantRecovery, maxHpPercentage),
            coolDown = coolDown,
        };

    /// <summary>Commander ability of a level.</summary>
    public static CommanderAbility GetCommanderAbility(int level)
    {
        if (level >= MAX_COMMANDER_LEVEL) level = MAX_COMMANDER_LEVEL;
        if (level < 0) level = 0;
        return commanderAbilities[level];
    }

    /// <summary>Default commander data and progress.</summary>
    public CCommander()
    {
        canSave = false;
        level = 0;
        medal = 50;
        boughtMedal = 0;

        for (int medalIndex = 0; medalIndex < 6; ++medalIndex)
        {
            warMedal[medalIndex] = 0;
        }

        for (int seriesId = 0; seriesId < SERIES_CNT; ++seriesId)
        {
            campaignProgress[seriesId] = 0;
            for (int levelId = 0; levelId < SERIES_MAX; ++levelId)
            {
                campaignStar[seriesId][levelId] = 0;
            }
        }
    }

    private const uint SAVE_MAGIC = 1161909081U;
    private const uint SAVE_VERSION = 1U;
    private const int SAVE_WORD_COUNT = 95;
    private const int WAR_MEDAL_OFFSET = 5;
    private const int CAMPAIGN_PROGRESS_OFFSET = 11;
    private const int CAMPAIGN_STAR_OFFSET = 15;

    /// <summary>Loads the commander level, medals and campaign progress from commander.sav.</summary>
    public void Load()
    {
        canSave = true;

        var saveFile = new ecFile();
        string savePath = GetDocumentPath("commander.sav");
        if (!saveFile.Open(savePath, "rb")) return;

        var raw = new byte[SAVE_WORD_COUNT * 4];
        bool readSucceeded = saveFile.Read(raw, 0, raw.Length);
        var saveData = new uint[SAVE_WORD_COUNT];
        if (readSucceeded)
        {
            for (int i = 0; i < SAVE_WORD_COUNT; i++) saveData[i] = BitConverter.ToUInt32(raw, i * 4);
        }
        if (readSucceeded &&
            saveData[0] == SAVE_MAGIC &&
            saveData[1] == SAVE_VERSION)
        {
            boughtMedal = (int)saveData[2];
            medal = (int)saveData[3];
            level = (int)saveData[4];

            for (int medalIndex = 0; medalIndex < 6; ++medalIndex)
            {
                warMedal[medalIndex] = (int)saveData[WAR_MEDAL_OFFSET + medalIndex];
            }

            for (int seriesIndex = 0; seriesIndex < SERIES_CNT; ++seriesIndex)
            {
                campaignProgress[seriesIndex] = (int)saveData[CAMPAIGN_PROGRESS_OFFSET + seriesIndex];

                for (int battleIndex = 0; battleIndex < SERIES_MAX; ++battleIndex)
                {
                    int saveIndex = CAMPAIGN_STAR_OFFSET + seriesIndex * SERIES_MAX + battleIndex;
                    campaignStar[seriesIndex][battleIndex] = (int)saveData[saveIndex];
                }
            }
        }

        saveFile.Close();
    }

    // Stop the game from saving commander data on game closing, which is suspected to cause data loss
    // Now it will save whenever a change in medal number is detected
    // please refer to ecGameUpdate in this project
    public void Save()
    {
    }

    /// <summary>Writes the complete commander and campaign data to commander.sav (extended format).</summary>
    public void NewSave()
    {
        if (!canSave) return;

        var saveData = new uint[SAVE_WORD_COUNT];
        saveData[0] = SAVE_MAGIC;
        saveData[1] = SAVE_VERSION;
        saveData[2] = (uint)boughtMedal;
        saveData[3] = (uint)medal;
        saveData[4] = (uint)level;

        for (int medalIndex = 0; medalIndex < 6; ++medalIndex)
        {
            saveData[WAR_MEDAL_OFFSET + medalIndex] = (uint)warMedal[medalIndex];
        }

        for (int seriesIndex = 0; seriesIndex < SERIES_CNT; ++seriesIndex)
        {
            saveData[CAMPAIGN_PROGRESS_OFFSET + seriesIndex] = (uint)campaignProgress[seriesIndex];

            for (int battleIndex = 0; battleIndex < SERIES_MAX; ++battleIndex)
            {
                int saveIndex = CAMPAIGN_STAR_OFFSET + seriesIndex * SERIES_MAX + battleIndex;
                saveData[saveIndex] = (uint)campaignStar[seriesIndex][battleIndex];
            }
        }

        var saveFile = new ecFile();
        string savePath = GetDocumentPath("commander.sav");
        if (saveFile.Open(savePath, "wb"))
        {
            for (int i = 0; i < SAVE_WORD_COUNT; i++) saveFile.WriteUInt32(saveData[i]);
            saveFile.Close();
        }
    }

    /// <summary>Medals needed for the next level; 0 when invalid or at the max level.</summary>
    public int GetUpgradeMedal()
    {
        if (level < 0 || IsMaxLevel()) return 0;
        return g_CommanderUpgradeMedalCosts[level];
    }

    /// <summary>Whether the medals suffice for the next commander level.</summary>
    public bool CheckUpgarde()
    {
        if (level < 0 || IsMaxLevel()) return false;
        // enough medals
        return medal >= GetUpgradeMedal();
    }

    /// <summary>Upgrades the commander.</summary>
    public void Upgrade()
    {
        // only when possible
        if (CheckUpgarde())
        {
            medal -= GetUpgradeMedal();
            level++;
        }
    }

    /// <summary>Whether the commander is at the max level.</summary>
    public bool IsMaxLevel() => level >= MAX_COMMANDER_LEVEL;

    /// <summary>Medal purchase.</summary>
    public void BuyMedal(int medalIn)
    {
        boughtMedal += medalIn;
        medal += medalIn;
        CCSoundBox.GetInstance().PlaySE(AudioDefs.Ui("buy"));
    }

    /// <summary>Level of a war medal.</summary>
    public int GetWarMedalLevel(WARMEDAL_ID warMedalId) => warMedal[(int)warMedalId];

    /// <summary>Medals needed to upgrade a war medal.</summary>
    public int GetNeedUpgradeMedal(WARMEDAL_ID warMedalId)
    {
        int curWarMedalLevel = warMedal[(int)warMedalId];
        if (curWarMedalLevel >= MAX_WAR_MEDAL_LEVEL) return 0;
        return Medals.UpgradeCost(curWarMedalLevel);   // original 1 * (level + 2)
    }

    /// <summary>Whether a war medal can be upgraded.</summary>
    public bool CheckUpgradeWarMedal(WARMEDAL_ID warMedalId)
    {
        if (warMedal[(int)warMedalId] >= MAX_WAR_MEDAL_LEVEL) return false;
        return medal >= GetNeedUpgradeMedal(warMedalId);
    }

    /// <summary>Upgrades a war medal.</summary>
    public void UpgradeWarMedal(WARMEDAL_ID warMedalId)
    {
        if (CheckUpgradeWarMedal(warMedalId))
        {
            medal -= GetNeedUpgradeMedal(warMedalId);
            warMedal[(int)warMedalId]++;
        }
    }

    /// <summary>Sets how many battles of a series were played.</summary>
    public void SetNumPlayedBatttles(int campaignSeriesId, int num)
    {
        campaignProgress[campaignSeriesId] = num;
    }

    public void SetBattlePlayed(int seriesId, int levelId)
    {
        if (levelId >= GetNumPlayedBatttles(seriesId))
        {
            SetNumPlayedBatttles(seriesId, levelId + 1);
        }
    }

    /// <summary>Copies the level and the six war medal levels into data.</summary>
    public void GetCommanderData(CommanderData data)
    {
        data.level = level;
        for (int medalIndex = 0; medalIndex < 6; ++medalIndex)
        {
            data.warMedal[medalIndex] = warMedal[medalIndex];
        }
    }

    // ---- CampaignSize.cpp ------------------------------------------------------------------

    public int GetNumBattleStars(int SeriesID, int LevelID)
    {
        if (SeriesID < 0 || LevelID < 0 || SeriesID >= SERIES_CNT || LevelID >= SERIES_MAX) return 0;
        return campaignStar[SeriesID][LevelID];
    }

    public void SetNumBattleStars(int SeriesID, int LevelID, int star)
    {
        if (SeriesID >= 0 && LevelID >= 0 && SeriesID < SERIES_CNT && LevelID < SERIES_MAX)
            campaignStar[SeriesID][LevelID] = star;
    }

    /// <summary>The remake unlocks every battle: always returns the series size.</summary>
    public int GetNumPlayedBatttles(int CampaignSeriesID) => SERIES_MAX;
}

/// <summary>CCountry methods defined in CCommander.cpp.</summary>
public sealed partial class CCountry
{
    /// <summary>Commander level of the country.</summary>
    public int GetCommanderLevel()
    {
        if (!ai)
        {
            if (g_GameManager.gameMode != CGameManager.MULTIPLAYER)
            {
                // player country outside multiplayer: the (single) player commander level
                return g_Commander.level;
            }
            CPlayer pPlayer = player;
            if (pPlayer != null)
            {
                CommanderData commanderData = pPlayer.commanderData;
                if (commanderData != null) return commanderData.level;
            }
        }
        CommanderDef pCommanderDef = commanderDef;
        if (pCommanderDef != null) return pCommanderDef.rank;
        // without a commander 8 is used, roughly the median
        return 8;
    }

    /// <summary>Whether the commander card can be used: the commander is dead and the cool-down is over.</summary>
    public bool CanUseCommander() => !commanderAlive && commanderTurn == 0;
}
