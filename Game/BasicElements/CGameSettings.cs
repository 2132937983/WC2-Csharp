namespace WC2;

/// <summary>Persistent options (CGameSettings.cpp), stored as settings.cfg in GetDocumentPath.</summary>
public sealed class CGameSettings
{
    public int musicVolume;
    public int seVolume;
    public int speed;
    public bool battleAnimation;
    public int eq0;
    public bool fullScreen;
    public bool useNewConquestLoading;
    public bool aiHosting;

    // SettingsFile layout, ten little endian ints:
    // magic, version, musicVolume, seVolume, speed, battleAnimation, eq0, fullScreen,
    // useNewConquestLoading, aiHosting (the last two are absent in old 32 byte files)
    private const int SETTINGS_MAGIC = 1161909081;
    private const int SETTINGS_VERSION = 1;
    private const int LEGACY_SIZE = 32;

    public CGameSettings()
    {
        musicVolume = 50;
        seVolume = 50;
        speed = 2;
        battleAnimation = true;
        eq0 = 0;
        fullScreen = true;
        useNewConquestLoading = true;
        aiHosting = true;
    }

    public void LoadSettings()
    {
        var file = new ecFile();
        string path = GetDocumentPath("settings.cfg");
        if (!file.Open(path, "rb")) return;

        var legacy = new byte[LEGACY_SIZE];
        if (!file.Read(legacy, 0, legacy.Length))
        {
            file.Close();
            return;
        }
        int savedMagic = BitConverter.ToInt32(legacy, 0);
        int savedVersion = BitConverter.ToInt32(legacy, 4);
        int savedMusicVolume = BitConverter.ToInt32(legacy, 8);
        int savedSeVolume = BitConverter.ToInt32(legacy, 12);
        int savedSpeed = BitConverter.ToInt32(legacy, 16);
        int savedBattleAnimation = BitConverter.ToInt32(legacy, 20);
        int savedEq0 = BitConverter.ToInt32(legacy, 24);
        int savedFullScreen = BitConverter.ToInt32(legacy, 28);

        // The extension fields are absent in old 32-byte settings files.
        var ext = new byte[4];
        int savedUseNewConquestLoading;
        if (file.Read(ext, 0, 4)) savedUseNewConquestLoading = BitConverter.ToInt32(ext, 0);
        else savedUseNewConquestLoading = useNewConquestLoading ? 1 : 0;
        int savedAiHosting;
        if (file.Read(ext, 0, 4)) savedAiHosting = BitConverter.ToInt32(ext, 0);
        else savedAiHosting = aiHosting ? 1 : 0;
        file.Close();

        if (savedMagic != SETTINGS_MAGIC || savedVersion != SETTINGS_VERSION) return;

        musicVolume = savedMusicVolume;
        if (musicVolume < 0) musicVolume = 0;
        if (musicVolume > 100) musicVolume = 100;

        seVolume = savedSeVolume;
        if (seVolume < 0) seVolume = 0;
        if (seVolume > 100) seVolume = 100;

        speed = savedSpeed;
        if (speed < 0) speed = 0;
        // GUILevelSel uses the five steps 0..4; the camera speed table has five entries too
        if (speed > 4) speed = 4;

        battleAnimation = savedBattleAnimation != 0;
        eq0 = savedEq0;
        fullScreen = savedFullScreen != 0;
        useNewConquestLoading = savedUseNewConquestLoading != 0;
        aiHosting = savedAiHosting != 0;
    }

    public void SaveSettings()
    {
        var file = new ecFile();
        string path = GetDocumentPath("settings.cfg");
        if (!file.Open(path, "wb")) return;
        file.WriteInt32(SETTINGS_MAGIC);
        file.WriteInt32(SETTINGS_VERSION);
        file.WriteInt32(musicVolume);
        file.WriteInt32(seVolume);
        file.WriteInt32(speed);
        file.WriteInt32(battleAnimation ? 1 : 0);
        file.WriteInt32(eq0);
        file.WriteInt32(fullScreen ? 1 : 0);
        file.WriteInt32(useNewConquestLoading ? 1 : 0);
        file.WriteInt32(aiHosting ? 1 : 0);
        file.Close();
    }
}
