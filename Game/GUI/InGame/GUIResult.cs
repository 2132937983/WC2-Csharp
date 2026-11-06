namespace WC2;

/// <summary>Level result screen (GUIResult.cpp).</summary>
public sealed class GUIResult : GUIElement
{
    public int showRestart;
    public ecTexture textureCommonBg;
    public ecImage imageCommonBg;
    public ecTexture textureCommander;
    public ecImage imageCommander;
    public ecTexture textureBoardResult;
    public ecImage imageBoardResult;
    public ecTexture x50;
    public ecImage x54;
    public GUIButton buttonBack;
    public GUIButton buttonNext;
    public GUIButton buttonRestart;
    public GUIAdornMedal guiAdornMedal;
    public ecImage imageMedal;
    public ecImage imageRankStar;
    public readonly ecText textTurn = new();
    public readonly ecText textRandomRewardMedal = new();
    public readonly ecText textCampaignRewardMedal = new();
    public int numVictoryStars;
    public float xC8;
    public float xCC;
    public int xD0;
    public float xBase;

    public GUIResult() { }

    /// <summary>Releases the images and textures held directly.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        x54 = null;
        if (x50 != null)
        {
            ecGraphics.Instance().FreeTexture(x50);
            x50 = null;
        }

        imageCommander = null;
        if (textureCommander != null)
        {
            ecGraphics.Instance().FreeTexture(textureCommander);
            textureCommander = null;
        }

        imageBoardResult = null;
        if (textureBoardResult != null)
        {
            ecGraphics.Instance().FreeTexture(textureBoardResult);
            textureBoardResult = null;
        }

        imageCommonBg = null;
        if (textureCommonBg != null)
        {
            ecGraphics.Instance().FreeTexture(textureCommonBg);
            textureCommonBg = null;
        }

        imageMedal = null;
        imageRankStar = null;
        base.Dispose();
    }

    /// <summary>Initializes the background, the reward info, the rating and the navigation buttons.</summary>
    public void Init(in GUIRect rectIn, int showRestartIn)
    {
        rect = rectIn;
        xBase = 0.0f;
        showRestart = showRestartIn;

        ecGraphics graphics = ecGraphics.Instance();
        bool wideLayout = Layout.Tablet;
        if (wideLayout)
        {
            textureCommonBg = graphics.LoadTexture("commonbg_iPad.png");
            imageCommonBg = new ecImage(textureCommonBg, 0.0f, 0.0f, rect.w, rect.h);

            textureCommander = graphics.LoadTexture("commander@2x.png");
            imageCommander = new ecImage(textureCommander, 0.0f, 0.0f, 479.0f, 708.0f);

            textureBoardResult = graphics.LoadWEBPTexture("board_result@2x.webp");
            imageBoardResult = new ecImage(textureBoardResult, 0.0f, 0.0f, 712.0f, 544.0f);
        }
        else
        {
            xBase = graphics.screenW - 480.0f;

            string commonBackgroundName;
            if (Layout.Width480) commonBackgroundName = "commonbg.png";
            else if (Layout.WidthUpTo568) commonBackgroundName = "commonbg-568h.png";
            else commonBackgroundName = "commonbg-640h.png";
            textureCommonBg = graphics.LoadTexture(commonBackgroundName);
            imageCommonBg = new ecImage(textureCommonBg, 0.0f, 0.0f, rect.w, rect.h);

            textureCommander = graphics.LoadTexture("commander.png");
            float commanderX = g_contenscalefactor == 2.0f ? 25.5f : 0.0f;
            imageCommander = new ecImage(textureCommander, commanderX, 0.0f, 214.0f, 315.0f);

            textureBoardResult = graphics.LoadWEBPTexture("board_result.webp");
            float boardHeight = g_contenscalefactor == 2.0f ? 272.0f : 269.0f;
            imageBoardResult = new ecImage(textureBoardResult, 0.0f, 0.0f, 340.0f, boardHeight);
        }

        imageMedal = new ecImage(s_TextureRes.GetImage("medal.png"));
        imageRankStar = new ecImage(s_TextureRes.GetImage("rank_star.png"));
        numVictoryStars = g_GameManager.GetNumVictoryStars();

        GUIRect childRect;
        buttonBack = new GUIButton();
        childRect = wideLayout ? new GUIRect(0.0f, 730.0f, 40.0f, 38.0f) : new GUIRect(0.0f, 290.0f, 32.0f, 30.0f);
        buttonBack.Init("buttontext_back.png", "buttontext_back_press.png", childRect, null);
        buttonBack.SetBackground("buttonboard_gray.png");
        AddChild(buttonBack, true);

        buttonNext = new GUIButton();
        childRect = wideLayout ? new GUIRect(984.0f, 730.0f, 40.0f, 38.0f) : new GUIRect(xBase + 448.0f, 290.0f, 32.0f, 30.0f);
        buttonNext.Init("buttontext_next.png", "buttontext_next_press.png", childRect, null);
        buttonNext.SetBackground("buttonboard_green.png");
        AddChild(buttonNext, true);

        childRect = wideLayout ? new GUIRect(463.0f, 728.0f, 98.0f, 34.0f) : new GUIRect(xBase * 0.5f + 191.0f, 280.0f, 98.0f, 34.0f);
        buttonRestart = GUIManager.Instance().AddButton("buttontext_restart.png", "buttontext_restart_press.png", childRect, this, null);
        buttonRestart.SetBackground("longred_normal.png");

        GUIRect labelRect;
        if (wideLayout)
        {
            labelRect = new GUIRect(652.0f, 219.0f, 1.0f, 1.0f);
            GUIManager.Instance().AddImage("text_rating.png", labelRect, this);
            labelRect = new GUIRect(481.0f, 115.0f, 1.0f, 1.0f);
            GUIManager.Instance().AddImage("text_round.png", labelRect, this);
            if (g_GameManager.gameMode != CGameManager.MULTIPLAYER)
            {
                labelRect = new GUIRect(481.0f, 387.0f, 1.0f, 1.0f);
                GUIManager.Instance().AddImage("text_medals.png", labelRect, this);
            }
            labelRect = new GUIRect(464.0f, 272.0f, 1.0f, 1.0f);
            GUIManager.Instance().AddImage(numVictoryStars == 5 ? "rank_1.png" : "rank_2.png", labelRect, this);

            textTurn.Init(g_Num4b);
            textRandomRewardMedal.Init(g_Num4b);
            textCampaignRewardMedal.Init(g_Num4b);
        }
        else
        {
            labelRect = new GUIRect(xBase + 308.0f, 108.0f, 1.0f, 1.0f);
            GUIManager.Instance().AddImage("text_rating.png", labelRect, this);
            labelRect = new GUIRect(xBase + 226.0f, 55.0f, 1.0f, 1.0f);
            GUIManager.Instance().AddImage("text_round.png", labelRect, this);
            if (g_GameManager.gameMode != CGameManager.MULTIPLAYER)
            {
                labelRect = new GUIRect(xBase + 226.0f, 193.0f, 1.0f, 1.0f);
                GUIManager.Instance().AddImage("text_medals.png", labelRect, this);
            }
            labelRect = new GUIRect(xBase + 216.0f, 134.0f, 1.0f, 1.0f);
            GUIManager.Instance().AddImage(numVictoryStars == 5 ? "rank_1.png" : "rank_2.png", labelRect, this);

            textTurn.Init(g_Num4);
            textRandomRewardMedal.Init(g_Num4);
            textCampaignRewardMedal.Init(g_Num4);
        }

        textTurn.SetColor(0xFF000000u);
        textRandomRewardMedal.SetColor(0xFF000000u);
        textCampaignRewardMedal.SetColor(0xFF000000u);

        textTurn.SetText(Sprintf("%d", g_GameManager.currentTurnNumMinusOne + 1));
        textRandomRewardMedal.SetText(Sprintf("%d", g_GameManager.randomRewardMedal));
        textCampaignRewardMedal.SetText(Sprintf("%d", g_GameManager.campaignRewardMedal));

        guiAdornMedal = new GUIAdornMedal();
        guiAdornMedal.Init(rect);
        AddChild(guiAdornMedal, true);

        if (showRestartIn != 0)
        {
            buttonNext.Hide();
            if (g_GameManager.gameMode == CGameManager.MULTIPLAYER)
                buttonRestart.Hide();
        }
        else
        {
            buttonRestart.Hide();
            if (g_GameManager.gameMode != CGameManager.CAMPAIGN)
                buttonNext.Hide();
        }

        xC8 = 1.0f;
        xCC = 1.0f;
        xD0 = 0;
    }

    /// <summary>Touch and back key handling of the result screen.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.TOUCH)
        {
            base.OnEvent(evt);
            return true;
        }

        if (evt.type == Event.BACK_PRESSED && (flags & SHOWN) != 0)
        {
            OnEvent(Event.Button(buttonBack));
            return true;
        }

        return base.OnEvent(evt);
    }

    /// <summary>Draws the background, the rating stars, the turn count and the medal rewards.</summary>
    public override void OnRender()
    {
        imageCommonBg.Render(0.0f, 0.0f);

        bool wideLayout = Layout.Tablet;
        if (wideLayout)
        {
            imageBoardResult.Render(304.0f, 0.0f);
            imageCommander.Render(0.0f, 60.0f);
            textTurn.DrawText(754.0f, 105.0f, 0);

            float firstStarX = 652.0f - (numVictoryStars - 1) * 20.0f;
            for (int index = 0; index < numVictoryStars; ++index)
                imageRankStar.Render(firstStarX + index * 40.0f, 274.0f);

            if (g_GameManager.gameMode != CGameManager.MULTIPLAYER)
            {
                imageMedal.Render(818.0f, 252.0f);
                textCampaignRewardMedal.DrawText(862.0f, 254.0f, 0);
                imageMedal.Render(818.0f, 362.0f);
                textRandomRewardMedal.DrawText(862.0f, 364.0f, 0);
            }
        }
        else
        {
            imageBoardResult.Render(xBase + 140.0f, 0.0f);
            imageCommander.Render(0.0f, 5.0f);
            textTurn.DrawText(xBase + 360.0f, 50.0f, 0);

            float firstStarX = xBase + 308.0f - (numVictoryStars - 1) * 10.0f;
            for (int index = 0; index < numVictoryStars; ++index)
                imageRankStar.Render(firstStarX + index * 20.0f, 134.0f);

            if (g_GameManager.gameMode != CGameManager.MULTIPLAYER)
            {
                imageMedal.Render(xBase + 392.0f, 126.0f);
                textCampaignRewardMedal.DrawText(xBase + 412.0f, 128.0f, 0);
                imageMedal.Render(xBase + 392.0f, 180.0f);
                textRandomRewardMedal.DrawText(xBase + 412.0f, 182.0f, 0);
            }
        }
    }

    /// <summary>The result screen has no per frame logic.</summary>
    public override void OnUpdate(float deltaTimeS) { }
}
