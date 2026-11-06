namespace WC2;

/// <summary>Battle selection screen of a campaign chapter (gui/menus/battles/GUISelBattle.cpp).</summary>
public sealed class GUISelBattle : GUIElement
{
    /// <summary>Flag / arrow picture placed on the minimap (GUISelBattle::ImageInfo of the original).</summary>
    public sealed class ImageInfo
    {
        public ecImage image;
        public float x;
        public float y;
        public float rot;
        public float scale;
    }

    public GUIButton buttonBack;
    public GUIButton buttonOk;
    public GUIButton buttonIntro;
    public GUIBattleList guiBattleList;
    public GUIMedal guiMedal;
    public GUIImage imageBoard;
    public GUIBattleIntro guiBattleIntro;
    public ecTexture[] textureMinimap = new ecTexture[8];
    public ecImage[] imageMinimap = new ecImage[8];
    public ecImage imageLogo;
    public ecImage imageFlagShadow;
    public List<ImageInfo> flag = new();
    public List<ImageInfo> arrow = new();
    public float centerX;
    public float centerY;
    public float w;
    public float h;
    public float ageX;
    public float ageY;
    public ecUniFont fontNum6 = new ecUniFont();
    public ecText textYear = new ecText();
    public int gameMode;
    public int campaignSeriesId;
    public string textureResNameBattleName = "";
    public int level;
    public float flagFadeAlpha;
    public float flagAlpha;
    public float xMinimap;
    public float yMinimap;
    public float xSpeed;
    public float ySpeed;
    public int status;
    public float xBase;

    /// <summary>
    /// The original only clears the resource/child pointers and builds flag, arrow, fontNum6
    /// and textYear; the scalar fields are set by Init and the interaction flow.
    /// </summary>
    public GUISelBattle() { }

    /// <summary>
    /// Releases the dynamic pictures, textures and resource tables of the screen. The level
    /// buttons and the other GUI children are released by the child tree.
    /// </summary>
    public override void Dispose()
    {
        if (IsDisposed) return;

        ReleaseImageList();

        for (int index = 0; index < 8; ++index)
        {
            imageMinimap[index] = null;
            if (textureMinimap[index] != null)
            {
                ecGraphics.Instance().FreeTexture(textureMinimap[index]);
                textureMinimap[index] = null;
            }
        }

        imageLogo = null;
        imageFlagShadow = null;

        fontNum6.Release();

        bool useHdResources = ecGraphics.Instance().widthMode == 3 || g_contenscalefactor == 2.0f;
        s_TextureRes.UnloadRes(useHdResources ? "selbattle_hd.xml" : "selbattle.xml");
        s_TextureRes.UnloadRes(textureResNameBattleName);

        base.Dispose();
    }

    /// <summary>Draws the minimap, the flags/arrows of the selected battle, the year and the faction logo.</summary>
    public override void OnRender()
    {
        ecGraphics.Instance().SetViewPoint(xMinimap, yMinimap, 1f);

        // the minimap on the left
        bool wideLayout = ecGraphics.Instance().widthMode == 3;
        float minimapSize = wideLayout ? 1000f : 500f;
        float mapWidth = wideLayout ? 3430f : 1715f;

        float imageX = 0f;
        float imageY = 0f;
        foreach (ecImage minimap in imageMinimap)
        {
            if (minimap != null)
            {
                minimap.Render(imageX, imageY);
            }

            imageX += minimapSize;
            if (imageX >= mapWidth)
            {
                imageY += minimapSize;
                imageX = 0f;
            }
        }
        // flags and arrows on the minimap
        foreach (ImageInfo flagInfo in flag)
        {
            imageFlagShadow.SetAlpha(flagAlpha, -1);
            flagInfo.image.SetAlpha(flagAlpha, -1);
            imageFlagShadow.RenderEx(flagInfo.x, flagInfo.y, flagInfo.rot, flagInfo.scale, 0f);
            flagInfo.image.RenderEx(flagInfo.x, flagInfo.y, flagInfo.rot, flagInfo.scale, 0f);
        }

        foreach (ImageInfo arrowInfo in arrow)
        {
            arrowInfo.image.SetAlpha(flagFadeAlpha, -1);
            arrowInfo.image.RenderEx(arrowInfo.x, arrowInfo.y, arrowInfo.rot, arrowInfo.scale, 0f);
        }

        textYear.SetAlpha(flagAlpha);
        textYear.DrawText(ageX, ageY, MIDDLE_ALIGNED);

        ecGraphics.Instance().SetViewPoint(0f, 0f, 1f);

        if (imageLogo == null)
        {
            return;
        }

        float logoX = 345f;
        if (!wideLayout)
        {
            logoX = (ecGraphics.Instance().screenW - 480f) * 0.5f + 170f;
        }
        // the faction logo on top
        imageLogo.Render(logoX, 0f);
    }

    /// <summary>
    /// Advances the fade / map move state machine of a battle switch.
    /// 0: idle; 1: fade out the current marks; 2: move the map; 3: fade in the flags; 4: fade in the arrows.
    /// The original does not call GUIElement::OnUpdate here.
    /// </summary>
    public override void OnUpdate(float deltaTimeS)
    {
        if (status == 1)
        {
            // fade out the flags and arrows at 2.5 alpha per second, then load the new level
            // flagAlpha follows flagFadeAlpha during this phase
            float nextFadeAlpha = flagFadeAlpha - deltaTimeS * 2.5f;
            if (nextFadeAlpha > 0f)
            {
                flagFadeAlpha = nextFadeAlpha;
            }
            else
            {
                flagFadeAlpha = 0f;
                SelBattle(campaignSeriesId, level);

                // SelBattle updates centerX/centerY. Snap when very close, otherwise move the
                // camera at a fixed speed along the normalized direction.
                float deltaX = centerX - xMinimap;
                float deltaY = centerY - yMinimap;
                float distanceSquared = deltaX * deltaX + deltaY * deltaY;
                if (distanceSquared <= 2f)
                {
                    xMinimap = centerX;
                    yMinimap = centerY;
                }
                else
                {
                    float distance = MathF.Sqrt(distanceSquared);
                    // HD map coordinates are doubled, so is the speed
                    float moveSpeed = ecGraphics.Instance().widthMode == 3 ? 2000f : 1000f;
                    xSpeed = deltaX * moveSpeed / distance;
                    ySpeed = deltaY * moveSpeed / distance;
                }
                status = 2;
            }

            flagAlpha = flagFadeAlpha;
            return;
        }

        if (status == 2)
        {
            // both axes must arrive before the next phase; checked before this frame's move on purpose
            float targetX = centerX;
            float targetY = centerY;
            if (xMinimap == targetX && yMinimap == targetY)
            {
                status = 3;
                return;
            }

            float nextX = xMinimap + xSpeed * deltaTimeS;
            float nextY = yMinimap + ySpeed * deltaTimeS;
            xMinimap = nextX;
            yMinimap = nextY;

            // each axis is clamped on its own when it crosses the target along its speed direction
            if ((xSpeed <= 0f && nextX <= targetX) ||
                (xSpeed > 0f && nextX >= targetX))
            {
                xMinimap = targetX;
            }

            if ((ySpeed <= 0f && nextY <= targetY) ||
                (ySpeed > 0f && nextY >= targetY))
            {
                yMinimap = targetY;
            }
            return;
        }

        if (status == 3)
        {
            // fade in the flags at 2.0 alpha per second
            float nextAlpha = flagAlpha + deltaTimeS * 2f;
            if (nextAlpha < 1f)
            {
                flagAlpha = nextAlpha;
                return;
            }

            flagAlpha = 1f;
            status = 4;
            return;
        }

        if (status == 4)
        {
            // then fade in the arrows at 1.5 alpha per second
            float nextFadeAlpha = flagFadeAlpha + deltaTimeS * 1.5f;
            if (nextFadeAlpha < 1f)
            {
                flagFadeAlpha = nextFadeAlpha;
                return;
            }

            flagFadeAlpha = 1f;
            status = 0;
        }
    }

    /// <summary>Handles the level list selection, the panel buttons, the battle intro and the back key.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.TOUCH)
        {
            // original engine order: the children see the touch first, then the screen swallows it
            base.OnEvent(evt);
            return true;
        }

        if (evt.type == Event.BACK_PRESSED)
        {
            // children (e.g. the battle intro) first
            if (base.OnEvent(evt))
                return true;
            // back
            if ((flags & SHOWN) != 0)
            {
                // the original re-enters OnEvent with a BUTTON event so the back button takes the normal path
                OnEvent(Event.Button(buttonBack));
                return true;
            }
            return false;
        }
        else if (evt.type == Event.GUI)
        {
            // button events
            if (evt.info.gui.type == Event.Info.GUIInfo.BUTTON)
            {
                GUIElement button = evt.info.gui.ptr;
                if (button == buttonOk)
                {
                    // start a new game
                    g_GameManager.NewGame((int)CGameManager.CAMPAIGN, -1, campaignSeriesId, guiBattleList.selectedLevel);
                }
                else if (button == buttonIntro)
                {
                    // point the battle intro at the selected battle
                    if (guiBattleIntro != null)
                    {
                        guiBattleIntro.SetBattle(campaignSeriesId, guiBattleList.selectedLevel);
                        guiBattleIntro.Show();
                    }
                    else
                    {
                        // lazily create the battle intro
                        guiBattleIntro = new GUIBattleIntro();

                        GUIRect introRect;
                        if (ecGraphics.Instance().widthMode == 3)
                        {
                            introRect = new GUIRect(32f, 32f, 960f, 640f);
                        }
                        else
                        {
                            introRect = new GUIRect((ecGraphics.Instance().screenW - 480f) * 0.5f, 0f, 480f, 320f);
                        }
                        guiBattleIntro.Init(introRect, campaignSeriesId, guiBattleList.selectedLevel);
                        AddChild(guiBattleIntro, false);
                    }
                }
            }
            else if (evt.info.gui.type == Event.Info.GUIInfo.OTHER)
            {
                if (evt.info.gui.info == 7)
                {
                    int selectedLevel = guiBattleList.selectedLevel;
                    if (level >= 0)
                    {
                        if (status != 1)
                        {
                            status = 1;
                            flagFadeAlpha = 1f;
                            flagAlpha = 1f;
                        }
                    }
                    else
                    {
                        // first selection of a level: shown immediately
                        SelBattle(campaignSeriesId, selectedLevel);
                        xMinimap = centerX;
                        yMinimap = centerY;
                    }
                    level = selectedLevel;
                }
                else if (evt.info.gui.info == 10 && guiBattleIntro != null)
                {
                    // linked with the battle intro event: hide it
                    guiBattleIntro.Hide();
                }
            }
        }

        return base.OnEvent(evt);
    }

    /// <summary>
    /// Initializes the resources, the layout and the children of the battle selection screen.
    /// The wide layout uses the 1024x768 design and HD resources; the others use the 480x320
    /// design and switch to HD resources at content scale 2. A non zero gameMode creates no
    /// intro button.
    /// </summary>
    /// <param name="rectIn">area inside the parent</param>
    /// <param name="seriesIn">campaign series to show</param>
    /// <param name="gameModeIn">0 single player campaign, 1 local multiplayer, 2 online multiplayer</param>
    public void Init(in GUIRect rectIn, int seriesIn, int gameModeIn)
    {
        rect = rectIn;
        gameMode = gameModeIn;
        campaignSeriesId = seriesIn;
        centerX = 0f;
        centerY = 0f;
        xBase = 0f;
        status = 0;
        flagFadeAlpha = 1f;
        flagAlpha = 1f;
        level = -1;
        xMinimap = centerX;
        yMinimap = centerY;

        bool wideLayout = ecGraphics.Instance().widthMode == 3;
        h = wideLayout ? 1500f : 750f;
        w = wideLayout ? 3430f : 1715f;

        bool useHdResources = wideLayout || g_contenscalefactor == 2.0f;
        string language = g_LocalizableStrings.GetString("language");
        if (wideLayout)
        {
            s_TextureRes.LoadRes("selbattle_hd.xml", false);
            textureResNameBattleName = Sprintf("battlename_%s_hd.xml", language);
        }
        else if (useHdResources)
        {
            s_TextureRes.LoadRes("selbattle_hd.xml", true);
            textureResNameBattleName = Sprintf("battlename_%s_hd.xml", language);
        }
        else
        {
            s_TextureRes.LoadRes("selbattle.xml", false);
            textureResNameBattleName = Sprintf("battlename_%s.xml", language);
        }
        s_TextureRes.LoadRes(textureResNameBattleName, !wideLayout && useHdResources);

        string logoName = null;
        // the four faction logos
        switch (seriesIn)
        {
            case 0: logoName = "logo_axis.png"; break;
            case 1: logoName = "logo_allies.png"; break;
            case 2: logoName = "logo_wto.png"; break;
            case 3: logoName = "logo_nato.png"; break;
            default: break;
        }
        if (logoName != null)
        {
            imageLogo = new ecImage(s_TextureRes.GetImage(logoName));
        }

        if (wideLayout)
        {
            fontNum6.Init("num6_hd.fnt", false);
        }
        else if (useHdResources)
        {
            fontNum6.Init("num6_hd.fnt", true);
        }
        else
        {
            fontNum6.Init("num6.fnt", false);
        }
        textYear.Init(fontNum6);

        for (int index = 0; index < 8; ++index)
        {
            string minimapName;
            if (wideLayout)
            {
                minimapName = Sprintf("minimap%d_%d@2x.pkm", 1, index + 1);
                textureMinimap[index] = ecGraphics.Instance().LoadETCTexture(minimapName);
                imageMinimap[index] = new ecImage(textureMinimap[index], 12f, 12f, 1000f, 1000f);
            }
            else
            {
                minimapName = Sprintf("minimap%d_%d.pkm", 1, index + 1);
                textureMinimap[index] = ecGraphics.Instance().LoadETCTexture(minimapName);
                imageMinimap[index] = new ecImage(textureMinimap[index], 6f, 6f, 500f, 500f);
            }
        }

        imageFlagShadow = new ecImage(s_TextureRes.GetImage("sflag_shadow.png"));
        guiBattleIntro = null;

        GUIRect controlRect;
        if (wideLayout)
        {
            buttonBack = new GUIButton();
            controlRect = new GUIRect(0f, 730f, 40f, 38f);
            buttonBack.Init("buttontext_back.png", "buttontext_back_press.png", controlRect, null);
            buttonBack.SetBackground("buttonboard_gray.png");
            AddChild(buttonBack, true);

            buttonOk = new GUIButton();
            controlRect = new GUIRect(984f, 730f, 40f, 38f);
            buttonOk.Init("buttontext_ok.png", "buttontext_ok_press.png", controlRect, null);
            buttonOk.SetBackground("buttonboard_green.png");
            AddChild(buttonOk, true);

            if (gameMode == 0)
            {
                // intro button [i]
                buttonIntro = new GUIButton();
                controlRect = new GUIRect(10f, 0f, 62f, 52f);
                buttonIntro.Init("buttontext_intro.png", "buttontext_intro_press.png", controlRect, null);
                buttonIntro.SetBackground("button_intro.png");
                AddChild(buttonIntro, true);
            }

            guiMedal = new GUIMedal();
            controlRect = new GUIRect(55f, 718f, 146f, 50f);
            guiMedal.Init(controlRect);
            AddChild(guiMedal, true);

            // the level list child
            guiBattleList = new GUIBattleList();
            controlRect = new GUIRect(766f, 20f, 268f, 728f);
            guiBattleList.Init(controlRect, campaignSeriesId);
            AddChild(guiBattleList, true);

            var boardTextureRect = new ecTextureRect(0f, 0f, 288f, 768f, 0f, 0f);
            var boardRect = new GUIRect(rect.w - 288f, 0f, 288f, 768f);
            imageBoard = GUIManager.Instance().AddImage("board_selbattle_iPad.webp", boardTextureRect, boardRect, this, false);
        }
        else
        {
            xBase = ecGraphics.Instance().screenW - 480f;

            buttonBack = new GUIButton();
            controlRect = new GUIRect(0f, 290f, 32f, 30f);
            buttonBack.Init("buttontext_back.png", "buttontext_back_press.png", controlRect, null);
            buttonBack.SetBackground("buttonboard_gray.png");
            AddChild(buttonBack, true);

            buttonOk = new GUIButton();
            controlRect = new GUIRect(xBase + 448f, 290f, 32f, 30f);
            buttonOk.Init("buttontext_ok.png", "buttontext_ok_press.png", controlRect, null);
            buttonOk.SetBackground("buttonboard_green.png");
            AddChild(buttonOk, true);

            if (gameMode == 0)
            {
                buttonIntro = new GUIButton();
                controlRect = new GUIRect(5f, 0f, 31f, 26f);
                buttonIntro.Init("buttontext_intro.png", "buttontext_intro_press.png", controlRect, null);
                buttonIntro.SetBackground("button_intro.png");
                AddChild(buttonIntro, true);
            }

            guiMedal = new GUIMedal();
            controlRect = new GUIRect(36f, 297f, 70f, 23f);
            guiMedal.Init(controlRect);
            AddChild(guiMedal, true);

            guiBattleList = new GUIBattleList();
            controlRect = new GUIRect(xBase + 351f, 10f, 134f, 300f);
            guiBattleList.Init(controlRect, campaignSeriesId);
            AddChild(guiBattleList, true);

            var boardTextureRect = new ecTextureRect(0f, 0f, 144f, 320f, 0f, 0f);
            var boardRect = new GUIRect(rect.w - 144f, 0f, 144f, 320f);
            imageBoard = GUIManager.Instance().AddImage("board_selbattle.webp", boardTextureRect, boardRect, this, false);
        }
        // the board is appended, so it stays behind the list

        if (campaignSeriesId != 4 &&
            g_Commander.GetNumPlayedBatttles(campaignSeriesId) < GetNumBattles(campaignSeriesId))
        {
            guiBattleList.SelectLastUnlocked();
        }
        else
        {
            guiBattleList.SetSelect(0);
        }
    }


    /// <summary>Creates the flag/arrow pictures of the given level and computes the initial map viewport.</summary>
    public void LoadImageList(int seriesIn, int levelIn)
    {
        string battleKey = GetBattleKeyName(seriesIn, levelIn);

        BattleDef battleDef = CObjectDef.Instance().GetBattleDef(battleKey);
        if (battleDef == null)
        {
            return;
        }

        foreach (FlagInfo flagDef in battleDef.flag)
        {
            string imageName = "sflag_" + flagDef.name + ".png";

            var imageInfo = new ImageInfo();
            imageInfo.image = new ecImage(s_TextureRes.GetImage(imageName));
            imageInfo.x = flagDef.x;
            imageInfo.y = flagDef.y;
            imageInfo.rot = flagDef.rot;
            imageInfo.scale = flagDef.scale;
            if (ecGraphics.Instance().widthMode == 3)
            {
                imageInfo.x += imageInfo.x;
                imageInfo.y += imageInfo.y;
            }
            flag.Add(imageInfo);
        }

        foreach (FlagInfo arrowDef in battleDef.arrow)
        {
            string imageName = "maparrow_" + arrowDef.name + ".png";

            var imageInfo = new ImageInfo();
            imageInfo.image = new ecImage(s_TextureRes.GetImage(imageName));
            imageInfo.x = arrowDef.x;
            imageInfo.y = arrowDef.y;
            imageInfo.rot = arrowDef.rot;
            imageInfo.scale = arrowDef.scale;
            if (ecGraphics.Instance().widthMode == 3)
            {
                imageInfo.x += imageInfo.x;
                imageInfo.y += imageInfo.y;
            }
            arrow.Add(imageInfo);
        }

        textYear.SetText(battleDef.age);

        ecGraphics graphics = ecGraphics.Instance();
        if (graphics.widthMode == 3)
        {
            centerX = battleDef.centerX + battleDef.centerX - graphics.screenW * 0.5f;
            centerY = battleDef.centerY + battleDef.centerY - graphics.screenH * 0.5f;
            ageX = battleDef.ageX + battleDef.ageX;
            ageY = battleDef.ageY + battleDef.ageY;
        }
        else
        {
            centerX = battleDef.centerX - graphics.screenW * 0.5f;
            centerY = battleDef.centerY - graphics.screenH * 0.5f;
            ageX = battleDef.ageX;
            ageY = battleDef.ageY;
        }

        if (centerX < 0f)
        {
            centerX = 0f;
        }
        if (centerX > w - graphics.screenW)
        {
            centerX = w - graphics.screenW;
        }
        if (centerY < 0f)
        {
            centerY = 0f;
        }
        if (centerY > h - graphics.screenH)
        {
            centerY = h - graphics.screenH;
        }
    }

    /// <summary>Selects a battle: releases the old flags/arrows and loads the new ones.</summary>
    public void SelBattle(int series, int levelIn)
    {
        // release the flags and arrows of the previously selected level
        ReleaseImageList();
        // load the new flags, arrows etc.
        LoadImageList(series, levelIn);
    }

    /// <summary>
    /// Releases the flag and arrow pictures created for the current level. The minimap, the
    /// logo, the flag shadow and the resource tables are released by Dispose.
    /// </summary>
    public void ReleaseImageList()
    {
        foreach (ImageInfo flagInfo in flag)
        {
            flagInfo.image = null;
        }
        flag.Clear();

        foreach (ImageInfo arrowInfo in arrow)
        {
            arrowInfo.image = null;
        }
        arrow.Clear();
    }
}
