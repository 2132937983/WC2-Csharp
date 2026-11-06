namespace WC2;

/// <summary>Campaign battle intro panel shown after entering a level (GUIBattleIntro.cpp).</summary>
public sealed class GUIBattleIntro : GUIElement
{
    // Size of the intro condition texts; 1.0f is the original size.
    private const float kBattleIntroConditionTextScale = 0.7f;

    public GUIButtonEx buttonOk;
    public ecTexture textureBoard;
    public ecImage imageBoard;
    public readonly ecUniFont font4 = new();
    public readonly ecText textBattleIntro = new();
    public readonly ecText textBattleName = new();
    public readonly ecUniFont fontNum7 = new();
    public readonly ecText textAge = new();
    public readonly ecText textVictory = new();
    public readonly ecText textGreatVictory = new();
    public readonly ecText textVictoryDays = new();
    public readonly ecText textGreatVictoryDays = new();
    public int series;
    public int level;
    public bool x15C;

    public GUIBattleIntro() { }

    /// <summary>Releases the font caches and the board; buttonOk belongs to the child tree.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        if (font4.IsLoaded) font4.Release();
        if (fontNum7.IsLoaded) fontNum7.Release();

        imageBoard = null;
        if (textureBoard != null)
        {
            ecGraphics.Instance().FreeTexture(textureBoard);
            textureBoard = null;
        }
        base.Dispose();
    }

    /// <summary>
    /// Initializes the layout, the board, the OK button and the texts. The wide layout uses the
    /// 960 x 640 HD board, the others 480 x 320. The level texts come from SetBattle.
    /// </summary>
    public void Init(in GUIRect rectIn, int seriesIn, int levelIn)
    {
        rect = rectIn;

        bool wideLayout = ecGraphics.Instance().widthMode == 3;
        if (wideLayout)
        {
            textureBoard = ecGraphics.Instance().LoadWEBPTexture("board_intro@2x.webp");
            imageBoard = new ecImage(textureBoard, 0.0f, 0.0f, 960.0f, 640.0f);
        }
        else
        {
            textureBoard = ecGraphics.Instance().LoadWEBPTexture("board_intro.webp");
            imageBoard = new ecImage(textureBoard, 0.0f, 0.0f, 480.0f, 320.0f);
        }

        buttonOk = new GUIButtonEx();
        GUIRect buttonRect = wideLayout ? new GUIRect(420.0f, 580.0f, 64.0f, 32.0f) : new GUIRect(210.0f, 283.0f, 64.0f, 32.0f);
        buttonOk.Init("green_normal.png", "green_normal.png", buttonRect, null);
        buttonOk.SetImageText("buttontext_ok.png", "buttontext_ok_press.png");
        if (wideLayout)
        {
            buttonOk.imagePos[0] = 11.0f;
            buttonOk.imagePos[1] = -5.0f;
        }
        else
        {
            buttonOk.imagePos[0] = 13.0f;
            buttonOk.imagePos[1] = 0.0f;
        }
        AddChild(buttonOk, true);

        // the victory condition label is a child image created by the GUIManager
        GUIRect conditionRect = wideLayout ? new GUIRect(480.0f, 384.0f, 1.0f, 1.0f) : new GUIRect(240.0f, 192.0f, 1.0f, 1.0f);
        GUIManager.Instance().AddImage("text_conditon.png", conditionRect, this);

        string language = g_LocalizableStrings.GetString("language");
        if (wideLayout)
        {
            // the wide layout uses the HD font file but loads it with the normal texture flag, like the original
            font4.Init(Sprintf("font4_%s_hd.fnt", language), false);
        }
        else if (g_contenscalefactor == 2.0f)
        {
            font4.Init(Sprintf("font4_%s_hd.fnt", language), true);
        }
        else
        {
            font4.Init(Sprintf("font4_%s.fnt", language), false);
        }

        textAge.Init(g_Num7);
        textBattleIntro.Init(g_FontKt);
        textBattleIntro.SetColor(0xFF33313Eu);

        textBattleIntro.fontSize[0] = 0.0f;
        textBattleIntro.fontSize[1] = wideLayout ? -6.0f : -3.0f;
        textBattleName.Init(g_FontKt);
        textBattleName.SetColor(0xFF33313Eu);

        textVictory.Init(g_FontHt);
        textVictory.SetColor(0xFF33313Eu);
        textVictory.SetText(g_StringTable.GetString("victory"));

        textGreatVictory.Init(g_FontHt);
        textGreatVictory.SetColor(0xFF33313Eu);
        textGreatVictory.SetText(g_StringTable.GetString("great victory"));

        textVictoryDays.Init(g_FontHt);
        textVictoryDays.SetColor(0xFF33313Eu);
        textGreatVictoryDays.Init(g_FontHt);
        textGreatVictoryDays.SetColor(0xFF33313Eu);

        SetBattle(seriesIn, levelIn);
        x15C = false;
    }

    /// <summary>Draws the dim, the board and the battle objective texts.</summary>
    public override void OnRender()
    {
        GetAbsRect(out var absRect);

        ecGraphics.Instance().Fade(0.5f);
        imageBoard.Render(absRect.x, absRect.y);

        if (ecGraphics.Instance().widthMode == 3)
        {
            textBattleName.DrawText(absRect.x + 480.0f, absRect.y + 76.0f, 2);
            ecTextDrawScaled(textBattleIntro, absRect.x + 120.0f, absRect.y + 176.0f, 0, g_introTextScale);
            textAge.DrawText(absRect.x + 480.0f, absRect.y + 130.0f, 2);
            ecTextDrawScaled(textVictory, absRect.x + 366.0f, absRect.y + 410.0f, 0, kBattleIntroConditionTextScale);
            ecTextDrawScaled(textGreatVictory, absRect.x + 366.0f, absRect.y + 470.0f, 0, kBattleIntroConditionTextScale);
            ecTextDrawScaled(textVictoryDays, absRect.x + 556.0f, absRect.y + 410.0f, 0, kBattleIntroConditionTextScale);
            ecTextDrawScaled(textGreatVictoryDays, absRect.x + 556.0f, absRect.y + 470.0f, 0, kBattleIntroConditionTextScale);
            return;
        }

        textBattleName.DrawText(absRect.x + 240.0f, absRect.y + 38.0f, 2);
        ecTextDrawScaled(textBattleIntro, absRect.x + 60.0f, absRect.y + 88.0f, 0, g_introTextScale);
        textAge.DrawText(absRect.x + 240.0f, absRect.y + 65.0f, 2);
        ecTextDrawScaled(textVictory, absRect.x + 183.0f, absRect.y + 205.0f, 0, kBattleIntroConditionTextScale);
        ecTextDrawScaled(textGreatVictory, absRect.x + 183.0f, absRect.y + 235.0f, 0, kBattleIntroConditionTextScale);
        ecTextDrawScaled(textVictoryDays, absRect.x + 278.0f, absRect.y + 205.0f, 0, kBattleIntroConditionTextScale);
        ecTextDrawScaled(textGreatVictoryDays, absRect.x + 278.0f, absRect.y + 235.0f, 0, kBattleIntroConditionTextScale);
    }

    /// <summary>
    /// OK first sends OTHER/10 up the GUI chain so the game state removes the intro, then the
    /// original BUTTON event continues. Touches are always consumed. The back key while shown
    /// synthesizes a BUTTON event for buttonOk so both paths share the close notification.
    /// </summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.TOUCH)
        {
            // BEGIN, MOVE and END never reach the map while the panel is shown
            base.OnEvent(evt);
            return true;
        }

        if (evt.type == Event.GUI &&
            evt.info.gui.type == Event.Info.GUIInfo.BUTTON &&
            ReferenceEquals(evt.info.gui.ptr, buttonOk))
        {
            // only the own OK button triggers the close notification of the game state
            base.OnEvent(Event.Gui(Event.Info.GUIInfo.OTHER, this, 10));

            // the original button event still goes through the base tree, its result is returned
            return base.OnEvent(evt);
        }

        if (evt.type == Event.BACK_PRESSED && (flags & SHOWN) != 0)
        {
            OnEvent(Event.Button(buttonOk));
            return true;
        }

        return base.OnEvent(evt);
    }

    /// <summary>
    /// Refreshes all texts from the series and the level. Series 0..3 map to the Axis, Allies,
    /// WTO and NATO keys; the age and the victory turns come from the BattleDef.
    /// </summary>
    public void SetBattle(int seriesIn, int levelIn)
    {
        series = seriesIn;
        level = levelIn;

        string introKeyFormat = null;
        string nameKeyFormat = null;
        switch (seriesIn)
        {
            case 0:
                introKeyFormat = "axis battle intro %d";
                nameKeyFormat = "axis battle name %d";
                break;
            case 1:
                introKeyFormat = "allies battle intro %d";
                nameKeyFormat = "allies battle name %d";
                break;
            case 2:
                introKeyFormat = "wto battle intro %d";
                nameKeyFormat = "wto battle name %d";
                break;
            case 3:
                introKeyFormat = "nato battle intro %d";
                nameKeyFormat = "nato battle name %d";
                break;
            default:
                break;
        }

        if (introKeyFormat != null)
        {
            // the text resources number the levels from 1, the runtime level from 0
            string localizedKey = Sprintf(introKeyFormat, levelIn + 1);
            textBattleIntro.SetText(g_StringTable.GetString(localizedKey));

            localizedKey = Sprintf(nameKeyFormat, levelIn + 1);
            textBattleName.SetText(g_StringTable.GetString(localizedKey));
        }

        GetBattleKeyName(seriesIn, levelIn, out string battleKey);
        BattleDef battleDef = CObjectDef.Instance().GetBattleDef(battleKey);
        if (battleDef == null) return;

        textAge.SetText(battleDef.age);

        string victoryDaysPrefix = g_StringTable.GetString("victory days1");
        string victoryDaysSuffix = g_StringTable.GetString("victory days2");
        string victoryDays;
        if (victoryDaysPrefix != null)
        {
            victoryDays = Sprintf("%s %d %s", victoryDaysPrefix, battleDef.victory, victoryDaysSuffix);
            textVictoryDays.SetText(victoryDays);

            victoryDays = Sprintf("%s %d %s", victoryDaysPrefix, battleDef.greatVictory, victoryDaysSuffix);
            textGreatVictoryDays.SetText(victoryDays);
        }
        else
        {
            victoryDays = Sprintf("%d%s", battleDef.victory, victoryDaysSuffix);
            textVictoryDays.SetText(victoryDays);

            victoryDays = Sprintf("%d%s", battleDef.greatVictory, victoryDaysSuffix);
            textGreatVictoryDays.SetText(victoryDays);
        }
    }
}
