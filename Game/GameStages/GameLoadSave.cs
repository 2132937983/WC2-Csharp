namespace WC2;

/// <summary>
/// Save game writing and loading (CGameManager part of GameLoadSave.cpp). The per object fill is
/// done by CCountry.SaveCountry / CArea.SaveAera and the Load counterparts; the binary layout is in SaveIO.
/// </summary>
public sealed partial class CGameManager
{
    /// <summary>
    /// Saves the game progress to the given file.
    /// </summary>
    /// <param name="FileName">save file name</param>
    public void SaveGame(string FileName)
    {
        var pEcFile = new ecFile();
        // only conquest and campaign are saved
        if ((gameMode == CAMPAIGN || gameMode == CONQUEST) &&
            pEcFile.Open(GetDocumentPath(FileName), "wb"))
        {
            var saveHeader = new SaveHeader();
            DateTime tm = DateTime.Now;
            saveHeader.easy = 0x45415359;
            saveHeader.eq1 = 1;
            // file_version
            saveHeader.gameMode = (int)gameMode;
            saveHeader.eq0 = x38;
            // the two members above are always 0
            saveHeader.mapId = mapId;
            // The file name deciding the area switches. This is why the "area enable state" is hard
            // to save unless it becomes a saved object itself.
            saveHeader.areasEnable = areasEnable;
            // player country names
            for (int i = 0; i != 8; i += 2)
            {
                saveHeader.playerCountryName[i] = playerCountryName[i];
            }
            saveHeader.battleFileName = battleFileName;
            saveHeader.year = tm.Year;
            saveHeader.xCamera = g_Scene.camera.centerPos[0];
            saveHeader.yCamera = g_Scene.camera.centerPos[1];
            saveHeader.scaleCamera = g_Scene.camera.scale;
            saveHeader.currentCountryIndex = currentCountryIndex;
            saveHeader.currentDialogueIndex = currentDialogueIndex;
            saveHeader.areaCount = 0;
            saveHeader.countryCount = allCountry.Count;
            saveHeader.currentTurnNumMinusOne = currentTurnNumMinusOne;
            saveHeader.randomRewardMedal = randomRewardMedal;
            saveHeader.month = tm.Month;
            saveHeader.day = tm.Day;
            saveHeader.hour = tm.Hour;
            saveHeader.min = tm.Minute;
            saveHeader.campaignSeriesId = campaignSeriesId;
            saveHeader.campaignLevelId = campaignLevelId;
            saveHeader.victoryTurn = victoryTurn;
            saveHeader.greatVictoryTurn = greatVictoryTurn;
            saveHeader.randSeed = randSeed;
            saveHeader.isPause = isPause;

            // country data
            var CountriesInfo = new SaveCountryInfo[saveHeader.countryCount];
            for (int CountryIndex = 0; CountryIndex < saveHeader.countryCount; CountryIndex++)
            {
                // the country saves itself
                CountriesInfo[CountryIndex] = new SaveCountryInfo();
                allCountry[CountryIndex].SaveCountry(CountriesInfo[CountryIndex]);
                saveHeader.areaCount += allCountry[CountryIndex].areaIdList.Count;
            }

            // all areas
            var AreasInfo = new SaveAreaInfo[saveHeader.areaCount];
            for (int CountryIndex = 0, AreaInfoIndex = 0;
                 CountryIndex < saveHeader.countryCount; CountryIndex++)
            {
                foreach (int AreaID in allCountry[CountryIndex].areaIdList)
                {
                    var areaInfo = new SaveAreaInfo();
                    areaInfo.countryIndex = CountryIndex;
                    g_Scene.GetArea(AreaID).SaveAera(areaInfo);
                    AreasInfo[AreaInfoIndex++] = areaInfo;
                }
            }
            SaveIO.WriteSaveHeader(pEcFile, saveHeader);
            for (int i = 0; i < saveHeader.countryCount; i++)
            {
                SaveIO.WriteSaveCountryInfo(pEcFile, CountriesInfo[i]);
            }
            for (int i = 0; i < saveHeader.areaCount; i++)
            {
                SaveIO.WriteSaveAreaInfo(pEcFile, AreasInfo[i]);
            }
            pEcFile.Close();
        }
        pEcFile.Close();
    }

    /// <summary>
    /// Restores the game from a save file.
    /// </summary>
    /// <param name="FileName">save file name</param>
    public void RealLoadGame(string FileName)
    {
        ClearBattle();
        ApplySceneOptions();
        g_Scene.Init(areasEnable, mapId);
        var pEcFile = new ecFile();
        string Path = GetDocumentPath(FileName);
        if (pEcFile.Open(Path, "rb"))
        {
            // game control data, countries and areas
            var Header = new SaveHeader();
            SaveIO.ReadSaveHeader(pEcFile, Header);
            var CountriesInfo = new SaveCountryInfo[Math.Max(0, Header.countryCount)];
            for (int i = 0; i < CountriesInfo.Length; i++)
            {
                CountriesInfo[i] = new SaveCountryInfo();
                SaveIO.ReadSaveCountryInfo(pEcFile, CountriesInfo[i]);
            }
            var AreasInfo = new SaveAreaInfo[Math.Max(0, Header.areaCount)];
            for (int i = 0; i < AreasInfo.Length; i++)
            {
                AreasInfo[i] = new SaveAreaInfo();
                SaveIO.ReadSaveAreaInfo(pEcFile, AreasInfo[i]);
            }
            pEcFile.Close();
            // restore the countries
            for (int i = 0; i < CountriesInfo.Length; i++)
            {
                var Country = new CCountry();
                Country.Init(CountriesInfo[i].id, CountriesInfo[i].name);
                Country.LoadCountry(CountriesInfo[i]);
                allCountry.Add(Country);
                // non neutral countries join the belligerent list
                if (Country.alliance != (int)NEUTRAL)
                {
                    belligerentCountry.Add(Country);
                }
            }

            // restore the areas
            for (int i = 0; i < AreasInfo.Length; i++)
            {
                CArea area = g_Scene.GetArea(AreasInfo[i].id);
                area.country = allCountry[AreasInfo[i].countryIndex];
                area.LoadArea(AreasInfo[i]);
                // give the area to its owner
                area.country.AddArea(area.id);
            }

            // restore the game control data
            currentCountryIndex = Header.currentCountryIndex;
            campaignSeriesId = Header.campaignSeriesId;
            campaignLevelId = Header.campaignLevelId;
            currentTurnNumMinusOne = Header.currentTurnNumMinusOne;
            randomRewardMedal = Header.randomRewardMedal;
            currentDialogueIndex = Header.currentDialogueIndex;
            victoryTurn = Header.victoryTurn;
            greatVictoryTurn = Header.greatVictoryTurn;
            randSeed = Header.randSeed;
            isPause = Header.isPause;
            g_Scene.camera.SetPos(Header.xCamera, Header.yCamera, false);
            g_Scene.camera.scale = Header.scaleCamera;

            // factions, relations and flags are scenario data: read them again from the saved game's scenario
            // (the announcements of the current round were already shown before the save)
            var scenario = new BattleScenarioData();
            if (!string.IsNullOrEmpty(Header.battleFileName) &&
                BattleScenario.LoadBattleScenarioData(Header.battleFileName, scenario))
            {
                Diplomacy.Set(scenario.diplomacy, currentTurnNumMinusOne + 1);
                // so are the scenario's own flags
                foreach (BattleScenarioCountryData data in scenario.countries)
                    if (FindCountry(data.id) is CCountry saved) saved.flag = data.flag;
            }
        }
        pEcFile.Close();
    }

    public bool GetSaveHeader(string fileName, SaveHeader saveHeader)
    {
        var tmp_file = new ecFile();
        bool ifOk = tmp_file.Open(GetDocumentPath(fileName), "rb");
        if (!ifOk)
        {
            return false;
        }
        // 0xBC bytes: the size of the original SaveHeader, do not change!
        ifOk = SaveIO.ReadSaveHeader(tmp_file, saveHeader, true);
        tmp_file.Close();
        return ifOk;
    }
}
