namespace WC2;

/// <summary>
/// Binary layout of the save game structures (SaveHeader.h, SaveCountryInfo.h, SaveAreaInfo.h,
/// SaveArmyInfo.h). Fields are written in the C++ declaration order, little endian, int/float = 4
/// bytes, bool = 1 byte, char[n] = n bytes, with the natural 4 byte struct padding reproduced.
/// </summary>
public static class SaveIO
{
    /// <summary>sizeof(SaveHeader) including the trailing padding after isPause.</summary>
    public const int SAVE_HEADER_SIZE = 196;
    /// <summary>Size of the original SaveHeader (without randSeed / isPause); read by GetSaveHeader. Do not change!</summary>
    public const int SAVE_HEADER_LEGACY_SIZE = 0xBC;
    public const int SAVE_COUNTRY_INFO_SIZE = 1300;
    public const int SAVE_ARMY_INFO_SIZE = 44;
    public const int SAVE_AREA_INFO_SIZE = 200;

    // ---- small helpers -------------------------------------------------------------------

    private static void WriteIntArray(ecFile f, int[] values, int count)
    {
        for (int i = 0; i < count; i++)
        {
            f.WriteInt32(values != null && i < values.Length ? values[i] : 0);
        }
    }

    private static void ReadIntArray(ecFile f, int[] values, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int v = f.ReadInt32();
            if (values != null && i < values.Length) values[i] = v;
        }
    }

    private static bool ReadPadding(ecFile f, int count)
    {
        return count <= 0 || f.ReadBytes(count) != null;
    }

    // ---- SaveHeader ----------------------------------------------------------------------

    public static bool WriteSaveHeader(ecFile f, SaveHeader h)
    {
        f.WriteInt32(h.easy);
        f.WriteInt32(h.eq1);
        f.WriteInt32(h.gameMode);
        f.WriteInt32(h.eq0);
        f.WriteInt32(h.mapId);
        f.WriteFixedString(h.areasEnable, 32);
        for (int i = 0; i < 8; i++)
        {
            f.WriteFixedString(i < h.playerCountryName.Length ? h.playerCountryName[i] : "", 4);
        }
        f.WriteFixedString(h.battleFileName, 32);
        f.WriteFloat(h.xCamera);
        f.WriteFloat(h.yCamera);
        f.WriteFloat(h.scaleCamera);
        f.WriteInt32(h.currentCountryIndex);
        f.WriteInt32(h.currentDialogueIndex);
        f.WriteInt32(h.countryCount);
        f.WriteInt32(h.areaCount);
        f.WriteInt32(h.currentTurnNumMinusOne);
        f.WriteInt32(h.randomRewardMedal);
        f.WriteInt32(h.year);
        f.WriteInt32(h.month);
        f.WriteInt32(h.day);
        f.WriteInt32(h.hour);
        f.WriteInt32(h.min);
        f.WriteInt32(h.campaignSeriesId);
        f.WriteInt32(h.campaignLevelId);
        f.WriteInt32(h.victoryTurn);
        f.WriteInt32(h.greatVictoryTurn);
        f.WriteInt32(h.randSeed);
        f.WriteBool(h.isPause);
        return f.WritePadding(3);
    }

    /// <summary>
    /// Reads a SaveHeader. With <paramref name="legacyPrefixOnly"/> only the first 0xBC bytes
    /// (the original header without randSeed / isPause) are read, like GetSaveHeader does.
    /// </summary>
    public static bool ReadSaveHeader(ecFile f, SaveHeader h, bool legacyPrefixOnly = false)
    {
        long start = f.GetCurPos();
        h.easy = f.ReadInt32();
        h.eq1 = f.ReadInt32();
        h.gameMode = f.ReadInt32();
        h.eq0 = f.ReadInt32();
        h.mapId = f.ReadInt32();
        h.areasEnable = f.ReadFixedString(32);
        for (int i = 0; i < 8; i++)
        {
            string s = f.ReadFixedString(4);
            if (i < h.playerCountryName.Length) h.playerCountryName[i] = s;
        }
        h.battleFileName = f.ReadFixedString(32);
        h.xCamera = f.ReadFloat();
        h.yCamera = f.ReadFloat();
        h.scaleCamera = f.ReadFloat();
        h.currentCountryIndex = f.ReadInt32();
        h.currentDialogueIndex = f.ReadInt32();
        h.countryCount = f.ReadInt32();
        h.areaCount = f.ReadInt32();
        h.currentTurnNumMinusOne = f.ReadInt32();
        h.randomRewardMedal = f.ReadInt32();
        h.year = f.ReadInt32();
        h.month = f.ReadInt32();
        h.day = f.ReadInt32();
        h.hour = f.ReadInt32();
        h.min = f.ReadInt32();
        h.campaignSeriesId = f.ReadInt32();
        h.campaignLevelId = f.ReadInt32();
        h.victoryTurn = f.ReadInt32();
        h.greatVictoryTurn = f.ReadInt32();
        if (legacyPrefixOnly)
        {
            return f.GetCurPos() - start == SAVE_HEADER_LEGACY_SIZE;
        }
        h.randSeed = f.ReadInt32();
        h.isPause = f.ReadBool();
        bool ok = ReadPadding(f, 3);
        return ok && f.GetCurPos() - start == SAVE_HEADER_SIZE;
    }

    // ---- SaveCountryInfo -----------------------------------------------------------------

    public static bool WriteSaveCountryInfo(ecFile f, SaveCountryInfo c)
    {
        f.WriteInt32(c.money);
        f.WriteInt32(c.industry);
        f.WriteInt32(c.tech);
        f.WriteInt32(c.techTurn);
        f.WriteBool(c.ai);
        f.WritePadding(3);
        f.WriteInt32(c.alliance);
        f.WriteInt32(c.defeatType);
        WriteIntArray(f, c.oldCardRound, 28);
        f.WriteFixedString(c.id, 16);
        f.WriteFixedString(c.name, 16);
        f.WriteByte(c.r);
        f.WriteByte(c.g);
        f.WriteByte(c.b);
        f.WriteByte(c.a);
        f.WriteFloat(c.taxRate);
        WriteIntArray(f, c.destroyCount, 10);
        WriteIntArray(f, c.warMedal, 6);
        f.WriteFixedString(c.commanderName, 24);
        f.WriteInt32(c.commanderTurn);
        f.WriteBool(c.commanderAlive);
        f.WriteBool(c.borrowedLoan);
        f.WriteBool(c.defeated);
        f.WritePadding(1);
        WriteIntArray(f, c.cardRound, 128);
        WriteIntArray(f, c.cardTimes, 128);
        return true;
    }

    public static bool ReadSaveCountryInfo(ecFile f, SaveCountryInfo c)
    {
        long start = f.GetCurPos();
        c.money = f.ReadInt32();
        c.industry = f.ReadInt32();
        c.tech = f.ReadInt32();
        c.techTurn = f.ReadInt32();
        c.ai = f.ReadBool();
        ReadPadding(f, 3);
        c.alliance = f.ReadInt32();
        c.defeatType = f.ReadInt32();
        ReadIntArray(f, c.oldCardRound, 28);
        c.id = f.ReadFixedString(16);
        c.name = f.ReadFixedString(16);
        c.r = f.ReadByte();
        c.g = f.ReadByte();
        c.b = f.ReadByte();
        c.a = f.ReadByte();
        c.taxRate = f.ReadFloat();
        ReadIntArray(f, c.destroyCount, 10);
        ReadIntArray(f, c.warMedal, 6);
        c.commanderName = f.ReadFixedString(24);
        c.commanderTurn = f.ReadInt32();
        c.commanderAlive = f.ReadBool();
        c.borrowedLoan = f.ReadBool();
        c.defeated = f.ReadBool();
        ReadPadding(f, 1);
        ReadIntArray(f, c.cardRound, 128);
        ReadIntArray(f, c.cardTimes, 128);
        return f.GetCurPos() - start == SAVE_COUNTRY_INFO_SIZE;
    }

    // ---- SaveArmyInfo --------------------------------------------------------------------

    public static bool WriteSaveArmyInfo(ecFile f, SaveArmyInfo a)
    {
        a ??= new SaveArmyInfo();
        f.WriteInt32(a.id);
        f.WriteInt32(a.hp);
        f.WriteInt32(a.movement);
        f.WriteInt32(a.cards);
        f.WriteInt32(a.maxHp);
        f.WriteInt32(a.level);
        f.WriteInt32(a.exp);
        f.WriteInt32(a.morale);
        f.WriteInt32(a.moraleUpTurn);
        f.WriteFloat(a.direction);
        f.WriteBool(a.active);
        return f.WritePadding(3);
    }

    public static bool ReadSaveArmyInfo(ecFile f, SaveArmyInfo a)
    {
        long start = f.GetCurPos();
        a.id = f.ReadInt32();
        a.hp = f.ReadInt32();
        a.movement = f.ReadInt32();
        a.cards = f.ReadInt32();
        a.maxHp = f.ReadInt32();
        a.level = f.ReadInt32();
        a.exp = f.ReadInt32();
        a.morale = f.ReadInt32();
        a.moraleUpTurn = f.ReadInt32();
        a.direction = f.ReadFloat();
        a.active = f.ReadBool();
        ReadPadding(f, 3);
        return f.GetCurPos() - start == SAVE_ARMY_INFO_SIZE;
    }

    // ---- SaveAreaInfo --------------------------------------------------------------------

    public static bool WriteSaveAreaInfo(ecFile f, SaveAreaInfo area)
    {
        f.WriteInt32(area.id);
        f.WriteInt32(area.countryIndex);
        f.WriteInt32(area.armyCount);
        f.WriteInt32(area.constructionType);
        f.WriteInt32(area.constructionLevel);
        f.WriteInt32(area.installationType);
        for (int i = 0; i < 4; i++)
        {
            WriteSaveArmyInfo(f, area.army != null && i < area.army.Length ? area.army[i] : null);
        }
        return true;
    }

    public static bool ReadSaveAreaInfo(ecFile f, SaveAreaInfo area)
    {
        long start = f.GetCurPos();
        area.id = f.ReadInt32();
        area.countryIndex = f.ReadInt32();
        area.armyCount = f.ReadInt32();
        area.constructionType = f.ReadInt32();
        area.constructionLevel = f.ReadInt32();
        area.installationType = f.ReadInt32();
        for (int i = 0; i < 4; i++)
        {
            if (area.army[i] == null) area.army[i] = new SaveArmyInfo();
            ReadSaveArmyInfo(f, area.army[i]);
        }
        return f.GetCurPos() - start == SAVE_AREA_INFO_SIZE;
    }
}
