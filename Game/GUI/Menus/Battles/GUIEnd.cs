namespace WC2;

/// <summary>Campaign finished screen (gui/menus/battles/GUIEnd.cpp).</summary>
public sealed class GUIEnd : GUIElement
{
    // same initial value as GUIBattleIntro but kept separate so it can be tuned on its own
    private const float kBattleEndTextScale = 0.7f;

    public ecTexture textureVictoryBg;
    public ecImage imageVictroyBg;
    public ecTexture textureVictory;
    public ecImage imageVictroy;
    public GUIButton buttonBack;
    public ecUniFont font4 = new ecUniFont();
    public ecText textVictory = new ecText();

    /// <summary>Creates the screen; the resources are created by Init.</summary>
    public GUIEnd() { }

    /// <summary>Releases the pictures and textures owned by the screen.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;

        imageVictroyBg = null;
        if (textureVictoryBg != null)
        {
            ecGraphics.Instance().FreeTexture(textureVictoryBg);
            textureVictoryBg = null;
        }

        imageVictroy = null;
        if (textureVictory != null)
        {
            ecGraphics.Instance().FreeTexture(textureVictory);
            textureVictory = null;
        }

        font4.Release();

        base.Dispose();
    }

    /// <summary>Initializes the faction background, the victory title, the back button and the text.</summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;

        int seriesId = g_GameManager.campaignSeriesId;
        string[] seriesNames = { "axis", "allies", "wto", "nato" };
        string seriesName = seriesNames[seriesId];
        ecGraphics graphics = ecGraphics.Instance();

        string backgroundName;
        float backgroundWidth = rect.w;
        if (Layout.Tablet)
        {
            backgroundName = Sprintf("victorybg_%s_iPad.webp", seriesName);
        }
        else if (Layout.Width480)
        {
            backgroundName = Sprintf("victorybg_%s.webp", seriesName);
        }
        else if (Layout.WidthUpTo568)
        {
            backgroundName = Sprintf("victorybg_%s-568h.webp", seriesName);
            backgroundWidth = 568f;
        }
        else
        {
            backgroundName = Sprintf("victorybg_%s-640h.webp", seriesName);
            backgroundWidth = 640f;
        }

        textureVictoryBg = graphics.LoadWEBPTexture(backgroundName);
        imageVictroyBg = new ecImage(textureVictoryBg, 0f, 0f, backgroundWidth, rect.h);

        ecTextureRect victoryRect;
        if (Layout.Tablet)
        {
            victoryRect = new ecTextureRect(6f, 6f, 596f, 132f, 298f, 66f);
            textureVictory = graphics.LoadTexture("victory@2x.png");
        }
        else
        {
            victoryRect = new ecTextureRect(3f, 3f, 298f, 66f, 149f, 33f);
            textureVictory = graphics.LoadTexture("victory.png");
        }
        imageVictroy = new ecImage(textureVictory, victoryRect);

        buttonBack = new GUIButton();
        GUIRect buttonRect;
        if (Layout.Tablet)
            buttonRect = new GUIRect(0f, 730f, 40f, 38f);
        else
            buttonRect = new GUIRect(0f, 290f, 32f, 30f);
        buttonBack.Init("buttontext_back.png", "buttontext_back_press.png", buttonRect, null);
        buttonBack.SetBackground("buttonboard_gray.png");
        AddChild(buttonBack, true);

        string language = g_LocalizableStrings.GetString("language");
        bool highDefinitionFont = false;
        string fontName;
        if (Layout.Tablet)
        {
            fontName = Sprintf("font4_%s_hd.fnt", language);
        }
        else if (g_contenscalefactor == 2.0f)
        {
            fontName = Sprintf("font4_%s_hd.fnt", language);
            highDefinitionFont = true;
        }
        else
        {
            fontName = Sprintf("font4_%s.fnt", language);
        }
        font4.Init(fontName, highDefinitionFont);
        textVictory.Init(g_FontKt);

        string[] victoryTextKeys =
        {
            "victory axis",
            "victory allies",
            "victory wto",
            "victory nato"
        };
        textVictory.SetText(g_StringTable.GetString(victoryTextKeys[seriesId]));
    }

    /// <summary>Draws the background, the title picture and the faction victory text.</summary>
    public override void OnRender()
    {
        ecGraphics graphics = ecGraphics.Instance();
        float screenWidth = graphics.screenW;

        if (screenWidth == 534f)
            imageVictroyBg.Render(-17f, 0f, 568f, 320f);
        else if (screenWidth == 512f)
            imageVictroyBg.Render(-28f, 0f, 568f, 320f);
        else
            imageVictroyBg.Render(0f, 0f);

        if (Layout.Tablet)
        {
            imageVictroy.Render(512f, 96f);
            ecTextDrawScaled(textVictory, 73f, 187f, 0, kBattleEndTextScale);
        }
        else
        {
            float xBase = (screenWidth - 480f) * 0.5f;
            imageVictroy.Render(xBase + 240f, 30f);
            ecTextDrawScaled(textVictory, xBase + 30f, 70f, 0, kBattleEndTextScale);
        }
    }

    /// <summary>Touch and back key handling of the victory screen.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.TOUCH)
        {
            // original engine order: the back button sees the touch first, then the screen swallows it
            base.OnEvent(evt);
            return true;
        }

        if (evt.type == Event.BACK_PRESSED &&
            (flags & SHOWN) != 0)
        {
            if (base.OnEvent(evt))
                return true;
            OnEvent(Event.Button(buttonBack));
            return true;
        }

        return base.OnEvent(evt);
    }
}
