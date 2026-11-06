namespace WC2;

/// <summary>Main menu panel: background, title and the sliding button pages (gui/menus/GUIMainMenu.cpp).</summary>
public sealed class GUIMainMenu : GUIElement
{
    public GUIImage guiMainButton;
    public GUIImage guiSelCampaigns;
    public GUIImage guiSelMultiplayer;
    public GUIImage guiSelLocal;
    public GUIImage guiSelConquest;
    public GUIButton[] buttonCampaigns = new GUIButton[4];
    public GUIButton buttonLoadCampaign;
    public GUIButton buttonBackCampaign;
    // keeps the original array layout; the conquest entries are managed by conquestList
    public GUIButton[] buttonConquests = new GUIButton[8];
    public GUIButton buttonLoadConquest;
    public GUIButton buttonBackConquest;
    public GUIButton buttonGlobal;
    public GUIButton buttonLocal;
    public GUIButton buttonBackMultiplayer;
    public GUIButton buttonHost;
    public GUIButton buttonJoin;
    public GUIButton buttonBackLocal;
    public GUIButton buttonCampaign;
    public GUIButton buttonConquest;
    public GUIButton buttonMultiPlayer;
    public GUIButton buttonOptions;
    public GUIButton buttonTutorial;
    public GUIButton buttonCommander;
    public GUIButton buttonQuitGame;
    public GUIButton buttonMoreGame;
    public ecImage xC0;
    public ecTexture textureMainBg;
    public ecImage imageMainBg;
    public ecTextureRes textureResTitle = new ecTextureRes();
    public ecImage imageTitle;
    public int lastPressed;
    public float xLeftOffset;
    public float xRightOffset;
    public float xCampaignLeftMoving;
    public float xCampaignRightMoving;
    public float xCampaignLeftBase;
    public float xCampaignRightBase;
    public float xConquestLeftMoving;
    public float xConquestRightMoving;
    public float xConquestLeftBase;
    public float xConquestRightBase;
    public float xCenter;
    public int x130;
    public int adShown;
    public GUIImage imageNewGameTip;
    public GUIButton buttonMail;
    // dynamic two column conquest list; the load and back buttons stay in the main menu
    public GUIConquestList conquestList;

    /// <summary>
    /// Counts the consecutive conquest scenarios loaded by CObjectDef. Conquest ids are
    /// 0-based internally, GetConquestKeyName only builds the resource key.
    /// </summary>
    private static int GetAvailableConquestCount()
    {
        const int maxConquests = 64;
        int count = 0;
        for (; count < maxConquests; ++count)
        {
            string key = GetConquestKeyName(count);
            if (CObjectDef.Instance().GetConquestDef(key) == null)
                break;
        }
        // keep the original eight entries when the definitions failed to load
        return count > 0 ? count : 8;
    }

    /// <summary>The original only builds the title resource table and clears its four resource pointers.</summary>
    public GUIMainMenu()
    {
        for (int i = 0; i < buttonConquests.Length; i++)
            buttonConquests[i] = null;
    }

    /// <summary>Releases the background, the title and the resource tables owned by the main menu.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;

        imageMainBg = null;
        if (textureMainBg != null)
        {
            ecGraphics.Instance().FreeTexture(textureMainBg);
            textureMainBg = null;
        }
        imageTitle = null;

        textureResTitle.Release();

        xC0 = null;

        bool useHdResources = Layout.Tablet || g_contenscalefactor == 2.0f;
        GUIManager guiManager = GUIManager.Instance();
        guiManager.UnloadTextureRes(useHdResources ? "mui_hd.xml" : "mui.xml");
        guiManager.UnloadTextureRes(useHdResources ? "mui2_hd.xml" : "mui2.xml");

        string language = g_LocalizableStrings.GetString("language");
        string localizedResourceName = Sprintf(useHdResources ? "mui_%s_hd.xml" : "mui_%s.xml", language);
        guiManager.UnloadTextureRes(localizedResourceName);

        base.Dispose();
    }

    /// <summary>Draws the background and the title (the coordinates switch between normal and HD layout).</summary>
    public override void OnRender()
    {
        imageMainBg.Render(0f, 0f);

        if (Layout.Tablet)
        {
            imageTitle.Render(512f, 324f);
        }
        else
        {
            imageTitle.Render(240f, 160f);
        }
    }

    /// <summary>Asks the Java promotion module whether the "new content" tip must be shown (never on desktop).</summary>
    public bool IsShowNewTip()
    {
        var methodInfo = new MethodInfo();
        if (!getStaticMethodInfo(methodInfo, "com/easytech/promotion/ecPromotion", "IsShowNewTip", "()Z"))
        {
            return false;
        }

        // Java call (IsShowNewTip) is not available on desktop
        return false;
    }

    /// <summary>Shows or hides the new content tip according to the promotion module.</summary>
    public void RefreshNewTip()
    {
        if (IsShowNewTip())
        {
            imageNewGameTip.Show();
        }
        else
        {
            imageNewGameTip.Hide();
        }
    }

    /// <summary>
    /// Initializes the resources, the page containers and every entry button. The pages are the
    /// main entry, campaign, conquest, online and local multiplayer containers; the last four are
    /// hidden after the initialization.
    /// </summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;
        xCenter = 0f;

        ecGraphics graphics = ecGraphics.Instance();
        GUIManager guiManager = GUIManager.Instance();
        bool wideLayout = Layout.Tablet;
        bool useHdResources = wideLayout || g_contenscalefactor == 2.0f;
        string language = g_LocalizableStrings.GetString("language");
        string localizedMenuResource;

        if (useHdResources)
        {
            string titleResource = language == "cn" ? "title_cn_hd.xml" : "title_hd.xml";
            textureResTitle.LoadRes(titleResource, !wideLayout);
            guiManager.LoadTextureRes("mui_hd.xml", !wideLayout);
            guiManager.LoadTextureRes("mui2_hd.xml", !wideLayout);
            localizedMenuResource = Sprintf("mui_%s_hd.xml", language);
            guiManager.LoadTextureRes(localizedMenuResource, !wideLayout);
        }
        else
        {
            textureResTitle.LoadRes("title.xml", false);
            guiManager.LoadTextureRes("mui.xml", false);
            guiManager.LoadTextureRes("mui2.xml", false);
            localizedMenuResource = Sprintf("mui_%s.xml", language);
            guiManager.LoadTextureRes(localizedMenuResource, false);
        }

        string backgroundName;
        if (wideLayout)
        {
            backgroundName = "mainbg_iPad.webp";
        }
        else if (Layout.Width480)
        {
            backgroundName = "mainbg.webp";
        }
        else if (Layout.WidthUpTo568)
        {
            backgroundName = "mainbg-568h.webp";
        }
        else
        {
            backgroundName = "mainbg-640h.webp";
        }
        textureMainBg = graphics.LoadWEBPTexture(backgroundName);
        imageMainBg = new ecImage(textureMainBg, 0f, 0f, graphics.screenW, graphics.screenH);
        imageTitle = new ecImage(textureResTitle.GetImage("title.png"));

        GUIRect itemRect;
        GUIButton addButton(GUIElement parent, string normalImage, string pressedImage, in GUIRect buttonRect)
        {
            return guiManager.AddButton(normalImage, pressedImage, buttonRect, parent, null);
        }
        static void configureTextButton(GUIButton button, string textImage)
        {
            button.SetTextImage(textImage);
            button.greyEnable = true;
        }
        GUIButton addBackButton(GUIElement parent, in GUIRect buttonRect)
        {
            var button = new GUIButton();
            button.Init("buttontext_back.png", "buttontext_back_press.png", buttonRect, null);
            button.SetBackground("buttonboard_gray.png");
            parent.AddChild(button, true);
            return button;
        }

        if (wideLayout)
        {
            itemRect = new GUIRect(rect.w, 0f, 214f, 768f);
            guiMainButton = guiManager.AddImage(null, itemRect, this);
            guiSelCampaigns = guiManager.AddImage(null, rect, this);
            guiSelMultiplayer = guiManager.AddImage(null, rect, this);
            guiSelLocal = guiManager.AddImage(null, rect, this);
            guiSelConquest = guiManager.AddImage(null, rect, this);

            itemRect = new GUIRect(0f, 283f, 214f, 66f);
            buttonCampaign = addButton(guiMainButton, "button_mainmenu.png", "button_mainmenu.png", itemRect);
            configureTextButton(buttonCampaign, "m_buttontext_campaign.png");
            itemRect.y = 363f;
            buttonConquest = addButton(guiMainButton, "button_mainmenu.png", "button_mainmenu.png", itemRect);
            configureTextButton(buttonConquest, "m_buttontext_conquest.png");
            buttonMultiPlayer = null;
            itemRect.y = 443f;
            buttonTutorial = addButton(guiMainButton, "button_mainmenu.png", "button_mainmenu.png", itemRect);
            configureTextButton(buttonTutorial, "m_buttontext_tutorials.png");
            itemRect.y = 523f;
            buttonCommander = addButton(guiMainButton, "button_mainmenu.png", "button_mainmenu.png", itemRect);
            configureTextButton(buttonCommander, "m_buttontext_commander.png");
            itemRect.y = 603f;
            buttonOptions = addButton(guiMainButton, "button_mainmenu.png", "button_mainmenu.png", itemRect);
            configureTextButton(buttonOptions, "m_buttontext_options.png");

            imageNewGameTip = new GUIImage();
            itemRect = new GUIRect(68f, 694f, 38f, 16f);
            imageNewGameTip.Init("new_game_tip.png", itemRect);
            AddChild(imageNewGameTip, true);
            itemRect = new GUIRect(114f, 702f, 110f, 66f);
            buttonMail = addButton(this, "button_mail.png", "button_mail.png", itemRect);
            buttonMail.greyEnable = true;
            itemRect.x = 0f;
            buttonMoreGame = addButton(this, "button_moregame.png", "button_moregame.png", itemRect);
            buttonMoreGame.greyEnable = true;

            GUIRect[] campaignRects =
            {
                new GUIRect(80f, 328f, 410f, 138f), new GUIRect(530f, 328f, 410f, 138f),
                new GUIRect(80f, 482f, 410f, 138f), new GUIRect(530f, 482f, 410f, 138f),
            };
            string[] campaignImages =
            {
                "button_axis.png", "button_allies.png", "button_wto.png", "button_nato.png",
            };
            string[] campaignTexts =
            {
                "m_buttontext_axis.png", "m_buttontext_allies.png", "m_buttontext_wto.png", "m_buttontext_nato.png",
            };
            for (int index = 0; index < 4; ++index)
            {
                buttonCampaigns[index] = addButton(
                        guiSelCampaigns, campaignImages[index], campaignImages[index], campaignRects[index]);
                configureTextButton(buttonCampaigns[index], campaignTexts[index]);
            }
            itemRect = new GUIRect(448f, 688f, 128f, 64f);
            buttonLoadCampaign = addButton(guiSelCampaigns, "m_buttontext_load.png", "m_buttontext_load_press.png", itemRect);
            buttonLoadCampaign.SetBackground("blue_big.png");
            buttonBackCampaign = addBackButton(guiSelCampaigns, new GUIRect(0f, 730f, 40f, 38f));

            var conquestListRect = new GUIRect(118f, 100f, 784f, 450f);
            conquestList = new GUIConquestList();
            conquestList.Init(conquestListRect, GetAvailableConquestCount(), true);
            guiSelConquest.AddChild(conquestList, true);
            itemRect = new GUIRect(448f, 688f, 128f, 64f);
            buttonLoadConquest = addButton(guiSelConquest, "m_buttontext_load.png", "m_buttontext_load_press.png", itemRect);
            buttonLoadConquest.SetBackground("blue_big.png");
            buttonBackConquest = addBackButton(guiSelConquest, new GUIRect(0f, 730f, 40f, 38f));

            itemRect = new GUIRect(307f, 328f, 410f, 138f);
            buttonGlobal = addButton(guiSelMultiplayer, "button_global.png", "button_global.png", itemRect);
            configureTextButton(buttonGlobal, "m_buttontext_global.png");
            itemRect.y = 482f;
            buttonLocal = addButton(guiSelMultiplayer, "button_local.png", "button_local.png", itemRect);
            configureTextButton(buttonLocal, "m_buttontext_local.png");
            buttonBackMultiplayer = addBackButton(guiSelMultiplayer, new GUIRect(0f, 730f, 40f, 38f));

            itemRect = new GUIRect(307f, 328f, 410f, 138f);
            buttonHost = addButton(guiSelLocal, "button_host.png", "button_host.png", itemRect);
            configureTextButton(buttonHost, "m_buttontext_host.png");
            itemRect.y = 482f;
            buttonJoin = addButton(guiSelLocal, "button_join.png", "button_join.png", itemRect);
            configureTextButton(buttonJoin, "m_buttontext_join.png");
            buttonBackLocal = addBackButton(guiSelLocal, new GUIRect(0f, 730f, 40f, 38f));

            itemRect = new GUIRect(0f, 683f, 214f, 66f);
        }
        else
        {
            xCenter = (graphics.screenW - 480f) * 0.5f;
            itemRect = new GUIRect(rect.w, 0f, 107f, 320f);
            guiMainButton = guiManager.AddImage(null, itemRect, this);
            guiSelCampaigns = guiManager.AddImage(null, rect, this);
            guiSelMultiplayer = guiManager.AddImage(null, rect, this);
            guiSelLocal = guiManager.AddImage(null, rect, this);
            guiSelConquest = guiManager.AddImage(null, rect, this);

            itemRect = new GUIRect(0f, 98f, 107f, 33f);
            buttonCampaign = addButton(guiMainButton, "button_mainmenu.png", "button_mainmenu.png", itemRect);
            configureTextButton(buttonCampaign, "m_buttontext_campaign.png");
            itemRect.y = 135f;
            buttonConquest = addButton(guiMainButton, "button_mainmenu.png", "button_mainmenu.png", itemRect);
            configureTextButton(buttonConquest, "m_buttontext_conquest.png");
            buttonMultiPlayer = null;
            itemRect.y = 172f;
            buttonTutorial = addButton(guiMainButton, "button_mainmenu.png", "button_mainmenu.png", itemRect);
            configureTextButton(buttonTutorial, "m_buttontext_tutorials.png");
            itemRect.y = 209f;
            buttonCommander = addButton(guiMainButton, "button_mainmenu.png", "button_mainmenu.png", itemRect);
            configureTextButton(buttonCommander, "m_buttontext_commander.png");
            itemRect.y = 246f;
            buttonOptions = addButton(guiMainButton, "button_mainmenu.png", "button_mainmenu.png", itemRect);
            configureTextButton(buttonOptions, "m_buttontext_options.png");

            imageNewGameTip = new GUIImage();
            itemRect = new GUIRect(34f, 283f, 19f, 8f);
            imageNewGameTip.Init("new_game_tip.png", itemRect);
            AddChild(imageNewGameTip, true);
            itemRect = new GUIRect(58f, 287f, 55f, 33f);
            buttonMail = addButton(this, "button_mail.png", "button_mail.png", itemRect);
            buttonMail.greyEnable = true;
            itemRect.x = 0f;
            buttonMoreGame = addButton(this, "button_moregame.png", "button_moregame.png", itemRect);
            buttonMoreGame.greyEnable = true;

            float x = xCenter;
            GUIRect[] campaignRects =
            {
                new GUIRect(x + 24f, 113f, 205f, 69f), new GUIRect(x + 249f, 113f, 205f, 69f),
                new GUIRect(x + 24f, 190f, 205f, 69f), new GUIRect(x + 249f, 190f, 205f, 69f),
            };
            string[] campaignImages =
            {
                "button_axis.png", "button_allies.png", "button_wto.png", "button_nato.png",
            };
            string[] campaignTexts =
            {
                "m_buttontext_axis.png", "m_buttontext_allies.png", "m_buttontext_wto.png", "m_buttontext_nato.png",
            };
            for (int index = 0; index < 4; ++index)
            {
                buttonCampaigns[index] = addButton(
                        guiSelCampaigns, campaignImages[index], campaignImages[index], campaignRects[index]);
                configureTextButton(buttonCampaigns[index], campaignTexts[index]);
            }
            itemRect = new GUIRect(x + 208f, 280f, 64f, 32f);
            buttonLoadCampaign = addButton(guiSelCampaigns, "m_buttontext_load.png", "m_buttontext_load_press.png", itemRect);
            buttonLoadCampaign.SetBackground("blue_normal.png");
            buttonBackCampaign = addBackButton(guiSelCampaigns, new GUIRect(0f, 290f, 32f, 30f));

            var conquestListRect = new GUIRect(x + 38f, 37f, 404f, 183f);
            conquestList = new GUIConquestList();
            conquestList.Init(conquestListRect, GetAvailableConquestCount(), false);
            guiSelConquest.AddChild(conquestList, true);
            itemRect = new GUIRect(x + 208f, 280f, 64f, 32f);
            buttonLoadConquest = addButton(guiSelConquest, "m_buttontext_load.png", "m_buttontext_load_press.png", itemRect);
            buttonLoadConquest.SetBackground("blue_normal.png");
            buttonBackConquest = addBackButton(guiSelConquest, new GUIRect(0f, 290f, 32f, 30f));

            itemRect = new GUIRect(x + 138f, 113f, 205f, 69f);
            buttonGlobal = addButton(guiSelMultiplayer, "button_global.png", "button_global.png", itemRect);
            configureTextButton(buttonGlobal, "m_buttontext_global.png");
            itemRect.y = 190f;
            buttonLocal = addButton(guiSelMultiplayer, "button_local.png", "button_local.png", itemRect);
            configureTextButton(buttonLocal, "m_buttontext_local.png");
            buttonBackMultiplayer = addBackButton(guiSelMultiplayer, new GUIRect(0f, 290f, 32f, 30f));

            itemRect = new GUIRect(x + 138f, 113f, 205f, 69f);
            buttonHost = addButton(guiSelLocal, "button_host.png", "button_host.png", itemRect);
            configureTextButton(buttonHost, "m_buttontext_host.png");
            itemRect.y = 190f;
            buttonJoin = addButton(guiSelLocal, "button_join.png", "button_join.png", itemRect);
            configureTextButton(buttonJoin, "m_buttontext_join.png");
            buttonBackLocal = addBackButton(guiSelLocal, new GUIRect(0f, 290f, 32f, 30f));

            itemRect = new GUIRect(0f, 283f, 107f, 33f);
        }

        // keep the compatibility references of the first eight entries; the buttons belong to GUIConquestList
        for (int index = 0; index < 8; ++index)
            buttonConquests[index] = conquestList.GetButton(index);

        buttonQuitGame = addButton(guiMainButton, "button_mainmenu.png", "button_mainmenu.png", itemRect);
        configureTextButton(buttonQuitGame, "m_buttontext_quitgame.png");

        if (g_Commander.GetNumPlayedBatttles(0) < GetNumBattles(0) &&
            g_Commander.GetNumPlayedBatttles(1) < GetNumBattles(1))
        {
            float lockX = wideLayout ? 300f : 150f;
            float lockY = wideLayout ? 70f : 35f;
            var lockRect = new GUIRect(lockX, lockY, 1f, 1f);
            guiManager.AddImage("mark_locked.png", lockRect, buttonCampaigns[2]);
            guiManager.AddImage("mark_locked.png", lockRect, buttonCampaigns[3]);
            buttonCampaigns[2].greyValue = 0.7f;
            buttonCampaigns[3].greyValue = 0.7f;
        }

        xCampaignRightMoving = rect.w;
        xCampaignLeftBase = buttonCampaigns[0].rect.x;
        xCampaignLeftMoving = -buttonCampaigns[0].rect.w;
        xCampaignRightBase = buttonCampaigns[1].rect.x;
        xConquestRightMoving = rect.w;
        xConquestLeftBase = conquestList.rect.x;
        xConquestLeftMoving = -conquestList.rect.w;
        xConquestRightBase = conquestList.rect.x;

        buttonCampaign.soundBtn = false;
        buttonBackCampaign.soundBtn = false;
        guiSelCampaigns.Hide();
        guiSelMultiplayer.Hide();
        guiSelLocal.Hide();
        guiSelConquest.Hide();
        lastPressed = 0;
        RefreshNewTip();
        MainMenuLoadedJNI();
    }

    public override void OnUpdate(float dt)
    {
        float speed = Layout.Tablet ? 800f : 400f;
        float slideSpeed = dt * speed;
        float itemSlideSpeed = slideSpeed * 2f;

        if (lastPressed == 1)
        {
            guiMainButton.rect.x -= slideSpeed;
            float targetX = rect.w - guiMainButton.rect.w;
            if (guiMainButton.rect.x <= targetX)
            {
                guiMainButton.rect.x = targetX;
                lastPressed = 0;
            }
        }

        int pressed = lastPressed;
        if (pressed == 2 || pressed == 6 || pressed == 7)
        {
            guiMainButton.rect.x += slideSpeed;
            if (guiMainButton.rect.x >= rect.w)
            {
                guiMainButton.rect.x = rect.w;

                if (pressed == 2)
                {
                    lastPressed = 3;
                    guiSelCampaigns.Show();

                    float left = xCampaignLeftMoving;
                    float right = xCampaignRightMoving;
                    xLeftOffset = xCampaignLeftBase - left;
                    xRightOffset = right - xCampaignRightBase;
                    buttonCampaigns[0].rect.x = left;
                    buttonCampaigns[2].rect.x = left;
                    buttonCampaigns[1].rect.x = right;
                    buttonCampaigns[3].rect.x = right;
                }
                else if (pressed == 7)
                {
                    lastPressed = 8;
                    guiSelConquest.Show();
                    conquestList.SetPos(xConquestRightMoving, conquestList.rect.y);
                }
                else
                {
                    guiSelMultiplayer.Show();
                    lastPressed = 0;
                }
            }
        }

        if (lastPressed == 3)
        {
            if (xLeftOffset > 0f)
            {
                xLeftOffset -= itemSlideSpeed;
                if (xLeftOffset < 0f)
                    xLeftOffset = 0f;
            }
            if (xRightOffset > 0f)
            {
                xRightOffset -= itemSlideSpeed;
                if (xRightOffset < 0f)
                    xRightOffset = 0f;
            }

            float leftX = xCampaignLeftBase - xLeftOffset;
            float rightX = xCampaignRightBase + xRightOffset;
            buttonCampaigns[0].rect.x = leftX;
            buttonCampaigns[2].rect.x = leftX;
            buttonCampaigns[1].rect.x = rightX;
            buttonCampaigns[3].rect.x = rightX;
            if (xLeftOffset == 0f && xRightOffset == 0f)
                lastPressed = 0;
        }

        if (lastPressed == 4)
        {
            float leftLimit = xCampaignLeftBase - xCampaignLeftMoving;
            if (xLeftOffset < leftLimit)
            {
                xLeftOffset += itemSlideSpeed;
                if (xLeftOffset > leftLimit)
                    xLeftOffset = leftLimit;
            }
            float rightLimit = xCampaignRightMoving - xCampaignRightBase;
            if (xRightOffset < rightLimit)
            {
                xRightOffset += itemSlideSpeed;
                if (xRightOffset > rightLimit)
                    xRightOffset = rightLimit;
            }

            float leftX = xCampaignLeftBase - xLeftOffset;
            float rightX = xCampaignRightBase + xRightOffset;
            buttonCampaigns[0].rect.x = leftX;
            buttonCampaigns[2].rect.x = leftX;
            buttonCampaigns[1].rect.x = rightX;
            buttonCampaigns[3].rect.x = rightX;
            if (xLeftOffset == leftLimit && xRightOffset == rightLimit)
            {
                lastPressed = 5;
                guiSelCampaigns.Hide();
                buttonMoreGame.Show();
                RefreshNewTip();
                buttonMail.Show();
            }
        }

        if (lastPressed == 5)
        {
            guiMainButton.rect.x -= slideSpeed;
            float targetX = rect.w - guiMainButton.rect.w;
            if (guiMainButton.rect.x <= targetX)
            {
                guiMainButton.rect.x = targetX;
                lastPressed = 0;
            }
        }

        if (lastPressed == 8)
        {
            float listX = conquestList.rect.x - itemSlideSpeed;
            if (listX < xConquestLeftBase)
                listX = xConquestLeftBase;
            conquestList.SetPos(listX, conquestList.rect.y);
            if (listX == xConquestLeftBase)
                lastPressed = 0;
        }

        if (lastPressed == 9)
        {
            float listX = conquestList.rect.x + itemSlideSpeed;
            if (listX > xConquestRightMoving)
                listX = xConquestRightMoving;
            conquestList.SetPos(listX, conquestList.rect.y);
            if (listX == xConquestRightMoving)
            {
                lastPressed = 5;
                guiSelConquest.Hide();
                buttonMoreGame.Show();
                buttonMail.Show();
            }
        }
    }

    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.TOUCH)
        {
            // original engine order: the children see the touch first, then the panel swallows it
            base.OnEvent(evt);
            return true;
        }

        if (evt.type == Event.BACK_PRESSED)
        {
            // children first (original engine order)
            if (base.OnEvent(evt))
                return true;
            if ((flags & SHOWN) == 0)
                return false;
            if (lastPressed != 0)
                return true;

            if ((guiSelCampaigns.flags & SHOWN) != 0)
            {
                OnEvent(Event.Button(buttonBackCampaign));
                return true;
            }
            if ((guiSelConquest.flags & SHOWN) != 0)
            {
                OnEvent(Event.Button(buttonBackConquest));
                return true;
            }
            return false;
        }

        if (evt.type != Event.GUI || evt.info.gui.type != Event.Info.GUIInfo.BUTTON || lastPressed != 0)
            return base.OnEvent(evt);

        GUIElement button = evt.info.gui.ptr;
        if (button == buttonMoreGame || button == buttonMail)
        {
            var otherEvent = Event.Gui(Event.Info.GUIInfo.OTHER, this, button == buttonMoreGame ? 27 : 20);
            base.OnEvent(otherEvent);
            return base.OnEvent(evt);
        }

        CCSoundBox soundBox = null;
        if (button == buttonCampaign)
        {
            buttonMoreGame.Hide();
            imageNewGameTip.Hide();
            buttonMail.Hide();
            lastPressed = 2;
            soundBox = CCSoundBox.GetInstance();
        }
        else if (button == buttonBackCampaign)
        {
            lastPressed = 4;
            soundBox = CCSoundBox.GetInstance();
        }
        else if (button == buttonConquest)
        {
            buttonMoreGame.Hide();
            imageNewGameTip.Hide();
            buttonMail.Hide();
            lastPressed = 7;
            soundBox = CCSoundBox.GetInstance();
        }
        else if (button == buttonBackConquest)
        {
            lastPressed = 9;
            soundBox = CCSoundBox.GetInstance();
        }
        else if (button == buttonMultiPlayer)
        {
            buttonMoreGame.Hide();
            imageNewGameTip.Hide();
            buttonMail.Hide();
            lastPressed = 6;
            soundBox = CCSoundBox.GetInstance();
        }
        else if (button == buttonBackMultiplayer)
        {
            buttonMoreGame.Show();
            RefreshNewTip();
            buttonMail.Show();
            guiSelMultiplayer.Hide();
            lastPressed = 5;
            soundBox = CCSoundBox.GetInstance();
        }
        else if (button == buttonLocal)
        {
            guiSelMultiplayer.Hide();
            guiSelLocal.Show();
        }
        else if (button == buttonBackLocal)
        {
            guiSelMultiplayer.Show();
            guiSelLocal.Hide();
        }
        else if (button == buttonQuitGame)
        {
            var menuState = (CMenuState)CStateManager.Instance().GetStatePtr(MENU);
            menuState.ShowExit();
        }

        if (soundBox != null)
            soundBox.PlaySE(AudioDefs.Ui("mainMenu"));
        return base.OnEvent(evt);
    }

    public void ShowAD()
    {
        if (adShown == 0) adShown = 1;
    }
}
