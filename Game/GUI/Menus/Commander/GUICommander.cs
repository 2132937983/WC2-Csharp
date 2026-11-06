namespace WC2;

// this file holds the main body of the headquarters (commander) screen

/// <summary>Commander screen: rank, war medals and upgrades (gui/menus/commander/GUICommander.cpp).</summary>
public sealed class GUICommander : GUIElement
{
    private const float kCommanderNameTextScale = 0.9f;
    private const float kCommanderIntroTextScale = 0.65f;

    public GUIButton buttonBack;
    public GUIUpgradeButton guiUpgradeButton;
    public GUIMedal guiMedal;
    public GUICommanderMedal guiCommanderMedal;
    public GUIWarMedal[] guiWarMedal = new GUIWarMedal[6];
    public GUIAdornMedal guiAdornMedal;
    public ecTexture textureCommonBg;
    public ecImage imageCommonBg;
    public ecTexture textureCommander;
    public ecImage imageCommander;
    public ecTexture textureWoodenBox;
    public ecImage imageWoodenBox;
    public ecTexture textureBoardCommanderIntro;
    public ecImage imageBoardCommanderIntro;
    public ecText textName = new ecText();
    public ecText textIntro = new ecText();
    public ecEffect effectUpgrade;
    public int warMedal;
    public float xBase;
    public float xBase0_625;

    // ecEffectResManager wrappers (effect atlas and definitions live in the effect manager).
    private static void LoadEffectTextureRes(string name) => ecEffectResManager.Instance().LoadTextureRes(name);
    private static void LoadEffectRes(string name) => ecEffectResManager.Instance().LoadEffectRes(name);
    private static void ReleaseAllEffectRes() => ecEffectResManager.Instance().ReleaseAllEffectRes();
    private static void ReleaseEffectTextureRes() => ecEffectResManager.Instance().ReleaseTextureRes();

    /// <summary>Creates the screen with the name/intro texts and the resource references cleared.</summary>
    public GUICommander() { }

    /// <summary>Releases the pictures, textures and the upgrade effect; clears the shared effect resources.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;

        imageCommonBg = null;
        if (textureCommonBg != null)
        {
            ecGraphics.Instance().FreeTexture(textureCommonBg);
            textureCommonBg = null;
        }

        imageCommander = null;
        if (textureCommander != null)
        {
            ecGraphics.Instance().FreeTexture(textureCommander);
            textureCommander = null;
        }

        imageWoodenBox = null;
        if (textureWoodenBox != null)
        {
            ecGraphics.Instance().FreeTexture(textureWoodenBox);
            textureWoodenBox = null;
        }

        imageBoardCommanderIntro = null;
        if (textureBoardCommanderIntro != null)
        {
            ecGraphics.Instance().FreeTexture(textureBoardCommanderIntro);
            textureBoardCommanderIntro = null;
        }

        effectUpgrade = null;

        ReleaseAllEffectRes();
        ReleaseEffectTextureRes();

        base.Dispose();
    }

    /// <summary>Draws the background, the commander portrait, the intro texts and the upgrade effect.</summary>
    public override void OnRender()
    {
        imageCommonBg.Render(0f, 0f);

        if (Layout.Tablet)
        {
            imageWoodenBox.Render(396f, 390f);
            imageBoardCommanderIntro.Render(304f, 0f);
            imageCommander.Render(0f, 60f);

            if (warMedal >= 0)
            {
                ecTextDrawScaled(textName, 674f, 4f, 2, kCommanderNameTextScale);
                ecTextDrawScaled(textIntro, 432f, 80f, 0, kCommanderIntroTextScale);
            }
        }
        else
        {
            imageWoodenBox.Render(xBase0_625 + 171f, 158f);
            imageBoardCommanderIntro.Render(xBase + 120f, 0f);
            imageCommander.Render(0f, 5f);

            if (warMedal >= 0)
            {
                ecTextDrawScaled(textName, xBase + 305f, 2f, 2, kCommanderNameTextScale);
                ecTextDrawScaled(textIntro, xBase + 182f, 40f, 0, kCommanderIntroTextScale);
            }
        }

        if (effectUpgrade != null)
        {
            effectUpgrade.Render();
        }
    }

    /// <summary>Refreshes the description, the upgrade cost and the medal levels of the current selection.</summary>
    public void SetCommanderInfo()
    {
        string stringKey;

        if (warMedal == 6)
        {
            stringKey = Sprintf("rank %d", g_Commander.level + 1);
            textName.SetText(g_StringTable.GetString(stringKey));

            stringKey = Sprintf("rank %d intro", g_Commander.level + 1);
            textIntro.SetText(g_StringTable.GetString(stringKey));

            if (g_Commander.IsMaxLevel())
            {
                guiUpgradeButton.Hide();
            }
            else
            {
                guiUpgradeButton.Show();
                guiUpgradeButton.SetNeedMedal(g_Commander.GetUpgradeMedal());
            }
        }
        else if (warMedal >= 0)
        {
            var medalId = (WARMEDAL_ID)warMedal;
            int medalLevel = g_Commander.GetWarMedalLevel(medalId);
            if (medalLevel < 1)
            {
                medalLevel = 1;
            }

            stringKey = Sprintf("war medal %d level %d", warMedal + 1, medalLevel);
            textName.SetText(g_StringTable.GetString(stringKey));

            stringKey = Sprintf("war medal %d level %d intro", warMedal + 1, medalLevel);
            textIntro.SetText(g_StringTable.GetString(stringKey));

            if (medalLevel > 2)
            {
                guiUpgradeButton.Hide();
            }
            else
            {
                guiUpgradeButton.Show();
                guiUpgradeButton.SetNeedMedal(g_Commander.GetNeedUpgradeMedal(medalId));
            }
        }

        guiCommanderMedal.level = g_Commander.level;
        for (int medalIndex = 0; medalIndex < 6; ++medalIndex)
        {
            var medalId = (WARMEDAL_ID)medalIndex;
            guiWarMedal[medalIndex].SetLevel(g_Commander.GetWarMedalLevel(medalId));
        }
        guiAdornMedal.Refresh();
    }

    /// <summary>Updates the upgrade effect and releases it once it finished.</summary>
    public override void OnUpdate(float deltaTimeS)
    {
        if (effectUpgrade == null)
        {
            return;
        }

        effectUpgrade.Update(deltaTimeS);
        if (!effectUpgrade.IsLive())
        {
            effectUpgrade = null;
        }
    }

    /// <summary>Handles touches, the back key and the buttons; performs the rank / war medal upgrades.</summary>
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
            if (base.OnEvent(evt))
                return true;
            if ((flags & SHOWN) != 0)
            {
                OnEvent(Event.Button(buttonBack));
                return true;
            }
            return false;
        }
        else if (evt.type == Event.GUI &&
                 evt.info.gui.type == Event.Info.GUIInfo.BUTTON)
        {
            // one of the six war medals?
            for (int medalIndex = 0; medalIndex < 6; ++medalIndex)
            {
                if (evt.info.gui.ptr == guiWarMedal[medalIndex])
                {
                    SelectedMedal(medalIndex);
                    break;
                }
            }
            // the commander medal?
            if (evt.info.gui.ptr == guiCommanderMedal)
            {
                SelectedMedal(6);
            }
            else if (evt.info.gui.ptr == guiUpgradeButton)
            {
                // upgrade button
                if (warMedal == 6)
                {
                    // upgrading the commander
                    if (g_Commander.CheckUpgarde())
                    {
                        CCSoundBox.GetInstance().PlaySE(AudioDefs.Ui("commanderLevelUp"));
                        g_Commander.Upgrade();
                        SetCommanderInfo();
                        g_Commander.Save();

                        effectUpgrade = null;

                        bool wideLayout = Layout.Tablet;
                        string effectName = wideLayout
                                            ? "effect_upgrade_iPad.xml"
                                            : "effect_upgrade.xml";
                        effectUpgrade = ecEffectManager.Instance().CreateEffect(effectName);
                        effectUpgrade.FireAt(
                                wideLayout ? 554f : 246f,
                                wideLayout ? 560f : 244f,
                                0f);

                        if (g_Commander.IsMaxLevel())
                        {
                            guiUpgradeButton.Hide();
                        }
                        else
                        {
                            guiUpgradeButton.SetNeedMedal(g_Commander.GetUpgradeMedal());
                        }
                    }
                }
                else
                {
                    var medalId = (WARMEDAL_ID)warMedal;
                    if (g_Commander.CheckUpgradeWarMedal(medalId))
                    {
                        // upgrading a war medal
                        CCSoundBox.GetInstance().PlaySE(AudioDefs.Ui("commanderLevelUp"));
                        g_Commander.UpgradeWarMedal(medalId);
                        SetCommanderInfo();
                        g_Commander.Save();

                        effectUpgrade = null;

                        string effectName = Layout.Tablet
                                            ? "effect_upgrade_iPad.xml"
                                            : "effect_upgrade.xml";
                        effectUpgrade = ecEffectManager.Instance().CreateEffect(effectName);

                        GUIRect medalRect = guiWarMedal[warMedal].rect;
                        effectUpgrade.FireAt(
                                medalRect.x + medalRect.w * 0.5f,
                                medalRect.y + medalRect.h * 0.5f,
                                0f);
                    }
                }
            }
        }

        return base.OnEvent(evt);
    }

    /// <summary>Switches the selected rank / war medal and refreshes its description and upgrade info.</summary>
    /// <param name="index">0..5 war medal, 6 the commander rank</param>
    public void SelectedMedal(int index)
    {
        warMedal = index;

        guiCommanderMedal.selected = false;
        for (int medalIndex = 0; medalIndex < 6; ++medalIndex)
        {
            guiWarMedal[medalIndex].selected = false;
        }

        if (index == 6)
        {
            guiCommanderMedal.SetArrowColor(0xFF00FF00);
            guiCommanderMedal.selected = true;
        }
        else
        {
            guiWarMedal[index].SetArrowColor(0xFF00FF00);
            guiWarMedal[index].selected = true;
        }

        SetCommanderInfo();
    }

    /// <summary>Initializes the resources, widgets, texts and the wide / normal layout.</summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;

        LoadEffectTextureRes("eff.xml");

        ecGraphics graphics = ecGraphics.Instance();
        bool wideLayout = Layout.Tablet;
        LoadEffectRes(wideLayout ? "effect_upgrade_iPad.xml" : "effect_upgrade.xml");

        xBase = 0f;
        xBase0_625 = 0f;

        GUIRect controlRect;
        if (wideLayout)
        {
            textureCommonBg = graphics.LoadTexture("commonbg_iPad.png");
            imageCommonBg = new ecImage(textureCommonBg, 0f, 0f, graphics.screenW, graphics.screenH);

            textureCommander = graphics.LoadTexture("commander@2x.png");
            imageCommander = new ecImage(textureCommander, 0f, 0f, 479f, 708f);

            textureWoodenBox = graphics.LoadTexture("commander_woodenbox@2x.png");
            imageWoodenBox = new ecImage(textureWoodenBox, 0f, 0f, 674f, 400f);

            textureBoardCommanderIntro = graphics.LoadWEBPTexture("board_commander_intro@2x.webp");
            imageBoardCommanderIntro = new ecImage(textureBoardCommanderIntro, 0f, 0f, 720f, 340f);

            buttonBack = new GUIButton();
            controlRect = new GUIRect(0f, 730f, 40f, 38f);
            buttonBack.Init("buttontext_back.png", "buttontext_back_press.png", controlRect, null);
            buttonBack.SetBackground("buttonboard_gray.png");
            AddChild(buttonBack, true);

            guiMedal = new GUIMedal();
            controlRect = new GUIRect(55f, 718f, 146f, 50f);
            guiMedal.Init(controlRect);
            AddChild(guiMedal, true);

            guiUpgradeButton = new GUIUpgradeButton();
            controlRect = new GUIRect(857f, 212f, 128f, 64f);
            guiUpgradeButton.Init("blue_big.png", "blue_big.png", controlRect, null);
            AddChild(guiUpgradeButton, true);

            guiCommanderMedal = new GUICommanderMedal();
            controlRect = new GUIRect(504f, 454f, 100f, 160f);
            guiCommanderMedal.Init(controlRect, g_Commander.level);
            AddChild(guiCommanderMedal, true);

            for (int medalIndex = 0; medalIndex < 6; ++medalIndex)
            {
                int row = medalIndex / 3;
                int column = medalIndex % 3;
                controlRect = new GUIRect(
                        column * 104f + 648f + row * 52f,
                        row * 100f + 430f,
                        62f,
                        96f);

                guiWarMedal[medalIndex] = new GUIWarMedal();
                guiWarMedal[medalIndex].Init(controlRect, (WARMEDAL_ID)medalIndex, 0);
                AddChild(guiWarMedal[medalIndex], true);
            }
        }
        else
        {
            xBase = graphics.screenW - 480f;
            xBase0_625 = xBase * 0.625f;

            string backgroundName;
            if (Layout.Width480)
            {
                backgroundName = "commonbg.png";
            }
            else if (Layout.WidthUpTo568)
            {
                backgroundName = "commonbg-568h.png";
            }
            else
            {
                backgroundName = "commonbg-640h.png";
            }

            textureCommonBg = graphics.LoadTexture(backgroundName);
            imageCommonBg = new ecImage(textureCommonBg, 0f, 0f, graphics.screenW, graphics.screenH);

            textureCommander = graphics.LoadTexture("commander.png");
            imageCommander = new ecImage(
                    textureCommander,
                    g_contenscalefactor == 2.0f ? 25.5f : 0f,
                    0f,
                    214f,
                    315f);

            textureWoodenBox = graphics.LoadTexture("commander_woodenbox.png");
            imageWoodenBox = new ecImage(textureWoodenBox, 0f, 0f, 337f, 200f);

            textureBoardCommanderIntro = graphics.LoadWEBPTexture("board_commander_intro.webp");
            imageBoardCommanderIntro = new ecImage(textureBoardCommanderIntro, 0f, 0f, 360f, 170f);

            buttonBack = new GUIButton();
            controlRect = new GUIRect(0f, 290f, 32f, 30f);
            buttonBack.Init("buttontext_back.png", "buttontext_back_press.png", controlRect, null);
            buttonBack.SetBackground("buttonboard_gray.png");
            AddChild(buttonBack, true);

            guiMedal = new GUIMedal();
            controlRect = new GUIRect(36f, 297f, 70f, 23f);
            guiMedal.Init(controlRect);
            AddChild(guiMedal, true);

            guiUpgradeButton = new GUIUpgradeButton();
            controlRect = new GUIRect(xBase + 405f, 103f, 64f, 32f);
            guiUpgradeButton.Init("blue_normal.png", "blue_normal.png", controlRect, null);
            AddChild(guiUpgradeButton, true);

            guiCommanderMedal = new GUICommanderMedal();
            controlRect = new GUIRect(xBase0_625 + 220f, 192f, 50f, 80f);
            guiCommanderMedal.Init(controlRect, g_Commander.level);
            AddChild(guiCommanderMedal, true);

            for (int medalIndex = 0; medalIndex < 6; ++medalIndex)
            {
                int row = medalIndex / 3;
                int column = medalIndex % 3;
                controlRect = new GUIRect(
                        xBase0_625 + 292f + column * 52f + row * 26f,
                        row * 50f + 178f,
                        31f,
                        48f);

                guiWarMedal[medalIndex] = new GUIWarMedal();
                guiWarMedal[medalIndex].Init(controlRect, (WARMEDAL_ID)medalIndex, 0);
                AddChild(guiWarMedal[medalIndex], true);
            }
        }

        guiAdornMedal = new GUIAdornMedal();
        guiAdornMedal.Init(rect);
        AddChild(guiAdornMedal, true);

        guiUpgradeButton.SetNeedMedal(g_Commander.GetUpgradeMedal());
        guiUpgradeButton.Hide();

        textName.Init(g_FontHt);
        textName.SetColor(0xFF07313E);
        textName.fontSize[0] = 1f;
        textName.fontSize[1] = 0f;

        textIntro.Init(g_FontHt);
        textIntro.SetColor(0xFF07313E);

        warMedal = -1;
        guiCommanderMedal.selected = true;
        for (int medalIndex = 0; medalIndex < 6; ++medalIndex)
        {
            guiWarMedal[medalIndex].selected = true;
        }

        SetCommanderInfo();
    }
}
