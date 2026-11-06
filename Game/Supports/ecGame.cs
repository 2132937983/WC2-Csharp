namespace WC2;

/// <summary>
/// Native entry points of the original library (ecGameInit / ecGameUpdate / ecGameRender /
/// touch callbacks) and the global font initialization from ecGame.cpp.
/// </summary>
public static class ecGame
{
    private static bool s_initialized;
    private static bool s_waiting;
    private static bool s_paused;

    /// <summary>Documents folder chosen on the command line (<c>--docs</c>); tests use a fresh one per run.</summary>
    public static string DocumentsOverride;

    /// <summary>Extra mod folder chosen on the command line (<c>--mods</c>).</summary>
    public static string ModsFolderOverride;
    /// <summary><c>--no-mods</c>: start with the base package only.</summary>
    public static bool ModsDisabled;

    /// <summary>
    /// Desktop start-up: chooses the documents folder, mounts the asset package (or a flat legacy
    /// asset folder) and the mods, and picks the language.
    /// </summary>
    public static void ConfigurePaths(string assetsOverride, string langOverride)
    {
        string docs = !string.IsNullOrEmpty(DocumentsOverride)
            ? DocumentsOverride
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WC2CSharp");
        SetDocumentPath(docs);

        string root = assetsOverride;
        string repository = FindRepository(AppContext.BaseDirectory);
        if (string.IsNullOrEmpty(root))
        {
            // the generated package next to the program first, then the asset tree of the repository
            // (runs without a package build: no generated formats), then a flat folder of originals
            string baseDir = AppContext.BaseDirectory;
            var candidates = new List<string>
            {
                Path.Combine(baseDir, "pack"),
                Path.Combine(Directory.GetCurrentDirectory(), "pack"),
                Path.Combine(baseDir, "assets"),
                Path.Combine(Directory.GetCurrentDirectory(), "assets"),
            };
            if (repository != null) candidates.Add(Path.Combine(repository, "assets"));
            root = candidates.FirstOrDefault(IsAssetFolder) ?? candidates[0];
        }
        root = Path.GetFullPath(root);

        // mod folders, lowest priority first in the list order does not matter (mod.json priority decides)
        var mods = new List<string>();
        if (!string.IsNullOrEmpty(ModsFolderOverride)) mods.Add(ModsFolderOverride);
        mods.Add(Path.Combine(DocumentFilePath, "mods"));
        mods.Add(Path.Combine(AppContext.BaseDirectory, "mods"));
        if (repository != null && string.IsNullOrEmpty(ModsFolderOverride)) mods.Add(Path.Combine(repository, "mods"));

        // a zip archive: a packed package, or an APK whose package sits under assets/pack
        if (File.Exists(root) && (root.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) || root.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
        {
            var probe = new ZipAssetSource(root);
            bool apk = probe.ListFiles().Any(f => f.Equals("assets/pack/" + AssetSystem.IndexFileName, StringComparison.OrdinalIgnoreCase));
            probe.Dispose();
            var zip = new ZipAssetSource(root, apk ? "assets/pack" : "");
            var zipAliases = AssetSystem.ReadAliases(zip.TryOpen);
            ConfigureAssets(zip, zipAliases, zip.Name, mods.ToArray(), langOverride);
            return;
        }

        var aliases = AssetSystem.ReadAliases(name =>
        {
            string file = Path.Combine(root, name);
            return File.Exists(file) ? File.OpenRead(file) : null;
        });
        ConfigureAssets(new DirectoryAssetSource(root), aliases, root, mods.ToArray(), langOverride);
    }

    /// <summary>A package (generated index), an asset tree (legacy-names.json) or a flat folder of originals.</summary>
    private static bool IsAssetFolder(string dir) =>
        Directory.Exists(dir) && (File.Exists(Path.Combine(dir, AssetSystem.IndexFileName))
                                  || File.Exists(Path.Combine(dir, AssetSystem.LegacyNamesFileName))
                                  || File.Exists(Path.Combine(dir, "carddef.xml")));

    /// <summary>The repository root (the folder with WC2.slnx) above the program, or null outside a checkout.</summary>
    public static string RepositoryRoot => FindRepository(AppContext.BaseDirectory);

    /// <summary>The repository root (the folder with WC2.slnx) above the program, when it runs from a build there.</summary>
    private static string FindRepository(string start)
    {
        for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "WC2.slnx"))) return dir.FullName;
        return null;
    }

    /// <summary>
    /// Mounts the base asset source (a generated package with an index, or a flat legacy folder when
    /// <paramref name="aliases"/> is null) and the mods, then picks the language folder. Used by the
    /// desktop start-up above and by the Android activity (APK assets).
    /// </summary>
    public static void ConfigureAssets(IAssetSource baseSource, Dictionary<string, string> aliases, string description, string[] modFolders, string langOverride)
    {
        AssetsRoot = description ?? "";
        ecStringTable.JsonTwinResolver = DataPaths.TextJson;
        JsonData.SchemaResolver = DataPaths.SchemaFor;   // every data file is checked against its schema when it loads
        JsonData.ResetSchemas();
        AssetSystem.Reset();
        AssetSystem.Mount(new AssetLayer { Id = "base", Name = "Base game", Source = baseSource, Aliases = aliases });
        if (!ModsDisabled) ModManager.MountMods(modFolders ?? Array.Empty<string>());
        Log("Assets: " + AssetsRoot + (aliases != null ? " (package)" : " (legacy folder)") + ", " + ModManager.Loaded.Count + " mod(s)");
        // definitions needed before the game initialises (layout choice, sounds, country art)
        LayoutProfiles.LoadJson();
        AudioDefs.LoadJson();
        CountryDefs.LoadJson();

        string lang = langOverride;
        if (string.IsNullOrEmpty(lang))
        {
            string culture = System.Globalization.CultureInfo.CurrentUICulture.Name.ToLowerInvariant();
            lang = culture.StartsWith("zh-tw") || culture.StartsWith("zh-hant") || culture.StartsWith("zh-hk") ? "zh_TW"
                 : culture.StartsWith("zh") ? "zh_CN"
                 : culture.StartsWith("ja") ? "ja"
                 : culture.StartsWith("ko") ? "kr"
                 : culture.StartsWith("ru") ? "ru"
                 : "English";
        }
        // language folders are "<lang>.lproj" in the original layout (a language folder <code>/ in the tree)
        string lproj = lang.EndsWith(".lproj") ? lang : lang + ".lproj";
        if (!AssetSystem.Exists(lproj + "/Localizable.strings")) lproj = "English.lproj";
        SetLangDir(lproj);
        Log("Documents: " + DocumentFilePath + "  Language: " + LangDir);
    }

    private static void InitGlobalFonts()
    {
        g_Font1.Init("font1.fnt", false);

        ecGraphics graphics = ecGraphics.Instance();
        bool tablet = graphics.widthMode == 3;
        bool hd = g_contenscalefactor == 2.0f;
        string language = g_LocalizableStrings.GetString("language");

        if (tablet)
        {
            g_Font2.Init(Sprintf("font2_%s_hd.fnt", language), false);
            g_Font3.Init(Sprintf("font3_%s_hd.fnt", language), false);
            if (hd)
            {
                g_Font6.Init(Sprintf("font6_%s_hd.fnt", language), true);
                g_Num3.Init("num3_hd.fnt", true);
                g_Num5.Init("num5_hd.fnt", true);
            }
            else
            {
                g_Font6.Init(Sprintf("font6_%s.fnt", language), false);
                g_Num3.Init("num3.fnt", false);
                g_Num5.Init("num5.fnt", false);
            }
            g_Num4.Init("num4.fnt", false);
            g_Num4b.Init("num4_hd.fnt", false);
            g_Font7.Init(Sprintf("font7_%s_hd.fnt", language), false);
            g_FontKt.Init(Sprintf("fontkt_%s_hd.fnt", language), false);
            g_FontHt.Init(Sprintf("fontht_%s_hd.fnt", language), false);
            g_Num7.Init("num7_hd.fnt", false);
            g_Num1.Init("num1_hd.fnt", false);
            EnsureFallbackFonts(language, false);
            return;
        }

        if (!hd)
        {
            g_Font2.Init(Sprintf("font2_%s.fnt", language), false);
            g_Font3.Init(Sprintf("font3_%s.fnt", language), false);
            g_Font6.Init(Sprintf("font6_%s.fnt", language), false);
            g_Font7.Init(Sprintf("font7_%s.fnt", language), false);
            g_FontKt.Init(Sprintf("fontkt_%s.fnt", language), false);
            g_FontHt.Init(Sprintf("fontht_%s.fnt", language), false);
            g_Num7.Init("num7.fnt", false);
            g_Num1.Init("num1.fnt", false);
            g_Num3.Init("num3.fnt", false);
            g_Num4.Init("num4.fnt", false);
            g_Num5.Init("num5.fnt", false);
            g_Num8.Init("num8.fnt", false);
            EnsureFallbackFonts(language, false);
            return;
        }

        g_Font2.Init(Sprintf("font2_%s_hd.fnt", language), true);
        g_Font3.Init(Sprintf("font3_%s_hd.fnt", language), true);
        g_Font6.Init(Sprintf("font6_%s_hd.fnt", language), true);
        g_Font7.Init(Sprintf("font7_%s_hd.fnt", language), true);
        g_FontKt.Init(Sprintf("fontkt_%s_hd.fnt", language), true);
        g_FontHt.Init(Sprintf("fontht_%s_hd.fnt", language), true);
        g_Num7.Init("num7_hd.fnt", true);
        g_Num1.Init("num1_hd.fnt", true);
        g_Num3.Init("num3_hd.fnt", true);
        g_Num4.Init("num4_hd.fnt", true);
        g_Num5.Init("num5_hd.fnt", true);
        g_Num8.Init("num8_hd.fnt", true);
        EnsureFallbackFonts(language, true);
    }

    /// <summary>
    /// The Hei/Kai fonts only ship for Chinese; for other languages fall back to the standard
    /// fonts so text is never invisible. Missing number fonts fall back to the closest size.
    /// </summary>
    private static void EnsureFallbackFonts(string language, bool hd)
    {
        if (!g_FontHt.IsLoaded)
        {
            if (ecFile.AssetExists(hd ? "fontht_cn_hd.fnt" : "fontht_cn.fnt") && language != "cn")
                g_FontHt.Init(hd ? "fontht_cn_hd.fnt" : "fontht_cn.fnt", hd);
            if (!g_FontHt.IsLoaded) g_FontHt.Init(Sprintf(hd ? "font2_%s_hd.fnt" : "font2_%s.fnt", language), hd);
        }
        if (!g_FontKt.IsLoaded)
        {
            if (ecFile.AssetExists(hd ? "fontkt_cn_hd.fnt" : "fontkt_cn.fnt") && language != "cn")
                g_FontKt.Init(hd ? "fontkt_cn_hd.fnt" : "fontkt_cn.fnt", hd);
            if (!g_FontKt.IsLoaded) g_FontKt.Init(Sprintf(hd ? "font3_%s_hd.fnt" : "font3_%s.fnt", language), hd);
        }
        if (!g_Num8.IsLoaded) g_Num8.Init(hd ? "num7_hd.fnt" : "num7.fnt", hd);
        if (!g_Num4b.IsLoaded) g_Num4b.Init(hd ? "num4_hd.fnt" : "num4.fnt", hd);
        if (!g_Font1.IsLoaded) g_Font1.Init(Sprintf(hd ? "font2_%s_hd.fnt" : "font2_%s.fnt", language), hd);
    }

    /// <summary>Port of ecGameInit: creates the renderer, GUI, states, tables, definitions and fonts.</summary>
    public static void ecGameInit(int layoutWidth, int layoutHeight, int orientation, int screenWidth, int screenHeight)
    {
        SetAIRandSeed(lrand48());
        SetRandSeed(lrand48());

        ecGraphics graphics = ecGraphics.Instance();
        graphics.Init(layoutWidth, layoutHeight, orientation, screenWidth, screenHeight);

        GUIManager guiManager = GUIManager.Instance();
        var screenRect = new GUIRect(0f, 0f, graphics.screenW, graphics.screenH);
        guiManager.Init(screenRect);
        GUIButton.PlayClickSound = () => CCSoundBox.GetInstance().PlaySE(AudioDefs.Ui("click"));

        CStateManager stateManager = CStateManager.Instance();
        stateManager.Init();
        stateManager.RegisterState(new CLogoState());
        stateManager.RegisterState(new CMenuState());
        stateManager.RegisterState(new CMatchState());
        stateManager.RegisterState(new CLoadState());
        stateManager.RegisterState(new CGameState());
        stateManager.RegisterState(new CEditState());
        stateManager.SetCurState(EState.LOGO);

        g_LocalizableStrings.Load("Localizable.strings");
        string stringTableName = graphics.widthMode == 3 ? "stringtable iPad" : "stringtable";
        string stringTable = g_LocalizableStrings.GetString(stringTableName);
        g_StringTable.Load(stringTable);
        LoadGlobalGuiAtlases(graphics, guiManager);

        Guard("CGameSettings.LoadSettings", () => g_GameSettings.LoadSettings());
        CCSoundBox sound = CCSoundBox.GetInstance();
        sound.SetMusicVolume(g_GameSettings.musicVolume);
        sound.SetSEVolume(g_GameSettings.seVolume);

        Guard("CObjectDef.Init", () => CObjectDef.Instance().Init());
        Guard("CCommander.Load", () => g_Commander.Load());

        foreach (var file in AudioDefs.preload) sound.LoadSE(file);
        InitGlobalFonts();
        s_initialized = true;
    }

    /// <summary>
    /// The original GUIManager loaded the shared UI atlases (common buttons, boards, sliders and
    /// the localized button texts) during initialization; every panel expects them in
    /// GUIElement.s_TextureRes.
    /// </summary>
    private static void LoadGlobalGuiAtlases(ecGraphics graphics, GUIManager guiManager)
    {
        string language = g_LocalizableStrings.GetString("language");
        bool hd = g_contenscalefactor == 2.0f;
        if (graphics.widthMode == 3)
        {
            // original CLogoState::OnEnter, tablet branch (atlas name, hd flag)
            guiManager.LoadTextureRes("ui_iPad.xml", false);
            guiManager.LoadTextureRes(hd ? "ui1_hd.xml" : "ui1.xml", hd);
            guiManager.LoadTextureRes("ui2_hd.xml", false);
            if (hd && !guiManager.LoadTextureResChecked(Sprintf("text_%s_iPad_hd.xml", language), true))
                guiManager.LoadTextureRes("text_en_iPad_hd.xml", true);
            if (!guiManager.LoadTextureResChecked(Sprintf("text_%s_iPad.xml", language), false))
                guiManager.LoadTextureRes("text_en_iPad.xml", false);
            guiManager.LoadTextureRes("image_newgame_hd.xml", false);
        }
        else
        {
            guiManager.LoadTextureRes(hd ? "ui1_hd.xml" : "ui1.xml", hd);
            guiManager.LoadTextureRes(hd ? "ui2_hd.xml" : "ui2.xml", hd);
            if (!guiManager.LoadTextureResChecked(Sprintf(hd ? "text_%s_hd.xml" : "text_%s.xml", language), hd))
                guiManager.LoadTextureRes(hd ? "text_en_hd.xml" : "text_en.xml", hd);
            guiManager.LoadTextureRes(hd ? "image_newgame_hd.xml" : "image_newgame.xml", hd);
        }
    }

    /// <summary>Runs a start-up step and logs instead of crashing when a data file is broken.</summary>
    private static void Guard(string what, Action action)
    {
        try { action(); }
        catch (Exception ex) { LogError(what + " failed: " + ex.Message); }
    }

    /// <summary>
    /// Java_com_easytech_wc2_ecRenderer_nativeInit: the logical layout follows the screen aspect
    /// ratio in four phone buckets (640/568/534/480 x 320) and the 1024x768 tablet layout below 1.40625.
    /// </summary>
    /// <summary>
    /// Picks the screen layout (data/json/layout/profiles.json; built in: the original nativeInit buckets
    /// 640/568/534/480 x 320 by aspect ratio, 1024x768 below 1.40625). Call after the assets are mounted.
    /// </summary>
    public static void SelectLayout(int screenW, int screenH, out int logicalW, out int logicalH)
    {
        var profile = LayoutProfiles.Select(screenW, screenH);
        logicalW = profile.width;
        logicalH = profile.height;
        Log($"Layout: {profile} for a {screenW}x{screenH} screen");
    }

    /// <summary>Per frame update (port of the library's ecGameUpdate plus the commander auto save hook).</summary>
    public static void ecGameUpdate(float deltaTimeS)
    {
        if (!s_initialized || s_waiting || s_paused) return;
        CommanderAutoSave();
        CStateManager.Instance().Update(deltaTimeS);
        GUIManager.Instance().Update(deltaTimeS);
        GUIMotionManager.Instance().ProcMotion(deltaTimeS);
        CCSoundBox.GetInstance().UpdateSound();
    }

    private static int s_lastMedal = int.MinValue;

    /// <summary>func_antiFileCheating.cpp: persist the commander whenever the medal count changes.</summary>
    private static void CommanderAutoSave()
    {
        if (s_lastMedal == g_Commander.medal) return;
        if (s_lastMedal != int.MinValue)
        {
            try { g_Commander.NewSave(); } catch (Exception ex) { Log("Commander auto save failed: " + ex.Message); }
        }
        s_lastMedal = g_Commander.medal;
    }

    public static void ecGameRender()
    {
        if (!s_initialized) return;
        var graphics = ecGraphics.Instance();
        graphics.RenderBegin();
        graphics.SetViewPoint(0f, 0f, 1f);
        CStateManager.Instance().Render();
        graphics.SetViewPoint(0f, 0f, 1f);
        GUIManager.Instance().Render();
        if (s_waiting || s_paused) graphics.Fade(0.5f); // original: darkened while paused or waiting
        graphics.RenderEnd();
    }

    /// <summary>Touches that began outside the logical screen; their moves and ends are dropped too.</summary>
    private static readonly HashSet<int> s_rejectedTouches = new();

    /// <summary>
    /// A finger down in the letterbox area (outside the logical screen) is ignored with its whole
    /// gesture: the original never received such touches, and controls it hid off the screen are
    /// there (see <see cref="GUIElement.CheckInRect(float, float)"/>).
    /// </summary>
    public static void ecTouchBegin(float x, float y, int index)
    {
        if (!s_initialized) return;
        if (!GUIElement.IsOnScreen(x, y)) { s_rejectedTouches.Add(index); return; }
        s_rejectedTouches.Remove(index);
        ecMultipleTouch.Instance().TouchBegan(index, x, y);
        var evt = Event.Touch(Event.Info.TouchInfo.BEGIN, x, y, index);
        if (!GUIManager.Instance().PostEvent(evt))
            CStateManager.Instance().TouchBegin(x, y, index);
    }

    public static void ecTouchMove(float x, float y, int index)
    {
        if (!s_initialized) return;
        if (s_rejectedTouches.Contains(index)) return;
        ecMultipleTouch.Instance().TouchMoved(index, x, y);
        var evt = Event.Touch(Event.Info.TouchInfo.MOVE, x, y, index);
        if (!GUIManager.Instance().PostEvent(evt))
            CStateManager.Instance().TouchMove(x, y, index);
    }

    public static void ecTouchEnd(float x, float y, int index)
    {
        if (!s_initialized) return;
        if (s_rejectedTouches.Remove(index)) return;
        var evt = Event.Touch(Event.Info.TouchInfo.END, x, y, index);
        bool consumed = GUIManager.Instance().PostEvent(evt);
        if (!consumed) CStateManager.Instance().TouchEnd(x, y, index);
        ecMultipleTouch.Instance().TouchEnded(index, x, y);
    }

    /// <summary>Returns true when the back key was handled by the game (original ecBackPressed).</summary>
    public static bool ecBackPressed()
    {
        if (!s_initialized) return false;
        if (GUIManager.Instance().PostEvent(Event.Back())) return true;
        return CStateManager.Instance().BackPressed();
    }

    /// <summary>
    /// Java_com_easytech_wc2_Wc2Activity_CallNativeExit: the Android back key; when nothing handled
    /// it, the menu state's exit box is shown whatever the current state is.
    /// </summary>
    public static void CallNativeExit()
    {
        if (!s_initialized) return;
        if (ecBackPressed()) return;
        if (CStateManager.Instance().GetStatePtr(EState.MENU) is CMenuState menu) menu.ShowExit();
    }

    public static void ecKeyDown(int key)
    {
        if (!s_initialized) return;
        CStateManager.Instance().KeyDown(key);
    }

    public static void ecScrollWheel(float x, float delta, float y)
    {
        if (!s_initialized) return;
        CStateManager.Instance().ScrollWheel(x, delta, y);
    }

    // original: two flags, both stop ecGameUpdate and darken the frame (byte_21EB1A pause, byte_21EB19 waiting)
    public static void ecGamePause() { s_paused = true; CCSoundBox.GetInstance().PauseMusic(); }
    public static void ecGameResume() { s_paused = false; CCSoundBox.GetInstance().ResumeMusic(); }
    public static void ecGameWaiting(bool setWaiting) => s_waiting = setWaiting;

    /// <summary>original ecGameDidEnterBackground: commander save, then every state's EnterBackground.</summary>
    public static void ecGameDidEnterBackground()
    {
        if (!s_initialized) return;
        try { g_Commander.Save(); } catch (Exception ex) { Log("Commander save failed: " + ex.Message); }
        CStateManager.Instance().EnterBackground();
    }

    /// <summary>original ecGameWillEnterForeground: re-apply the saved volumes, resume music, EnterForeground.</summary>
    public static void ecGameWillEnterForeground()
    {
        if (!s_initialized) return;
        var soundBox = CCSoundBox.GetInstance();
        soundBox.SetMusicVolume(g_GameSettings.musicVolume);
        soundBox.SetSEVolume(g_GameSettings.seVolume);
        soundBox.ResumeMusic();
        CStateManager.Instance().EnterForeground();
    }

    private static bool s_inBackground;

    /// <summary>
    /// Set by the Android activity: it drives nativePause/nativeResume from the life cycle, so the
    /// host's window-focus polling must stay out of the way (a desktop window may start unfocused
    /// and must not freeze the game).
    /// </summary>
    public static bool HostHandlesLifecycle;

    /// <summary>Java_com_easytech_wc2_Wc2Activity_nativePause (idempotent: focus loss and onPause may both report it).</summary>
    public static void nativePause()
    {
        if (!s_initialized || s_inBackground) return;
        s_inBackground = true;
        ecGameDidEnterBackground();
        ecGamePause();
    }

    /// <summary>Java_com_easytech_wc2_Wc2Activity_nativeResume.</summary>
    public static void nativeResume()
    {
        if (!s_initialized || !s_inBackground) return;
        s_inBackground = false;
        ecGameWillEnterForeground();
        ecGameResume();
    }

    public static void ecLoadSettings() => g_GameSettings.LoadSettings();

    /// <summary>Called by the host when the window closes.</summary>
    public static void ecGameShutdownHook()
    {
        if (!s_initialized) return;
        try { g_GameSettings.SaveSettings(); } catch { }
        try { g_Commander.Save(); } catch { }
        try { CCSoundBox.GetInstance().Destroy(); } catch { }
        s_initialized = false;
    }
}
