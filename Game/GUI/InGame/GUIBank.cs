namespace WC2;

/// <summary>Bank (medal exchange) page opened from the turn begin hint (GUIBank.cpp).</summary>
public sealed class GUIBank : GUIElement
{
    private const float kBankTipTextScale = 0.8f;

    public ecTexture textureBankBg;
    public ecImage imageBankBg;
    public GUIButton buttonBack;
    public GUIButton buttonIndustrial10;
    public GUIButton buttonIndustrial50;
    public GUIButton buttonMoney20;
    public GUIButton buttonMoney100;
    public GUIMedal guiMedal;
    public GUIGold guiGold;
    public readonly ecText textBankTip = new();
    public int x74;
    public int x78;
    public float xBase;

    public GUIBank() { }

    /// <summary>Releases the background image and texture; buttons and HUD belong to the child tree.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageBankBg = null;
        if (textureBankBg != null)
        {
            ecGraphics.Instance().FreeTexture(textureBankBg);
            textureBankBg = null;
        }
        base.Dispose();
    }

    /// <summary>The bank has no per frame logic.</summary>
    public override void OnUpdate(float deltaTimeS) { }

    /// <summary>Draws the background and the exchange tip.</summary>
    public override void OnRender()
    {
        float screenWidth = ecGraphics.Instance().screenW;
        if (screenWidth == 534.0f)
            imageBankBg.Render(-17.0f, 0.0f, 568.0f, 320.0f);
        else if (screenWidth == 512.0f)
            imageBankBg.Render(-28.0f, 0.0f, 568.0f, 320.0f);
        else
            imageBankBg.Render(0.0f, 0.0f);

        if (Layout.Tablet)
            ecTextDrawScaled(textBankTip, 512.0f, 430.0f, 2, kBankTipTextScale);
        else
            ecTextDrawScaled(textBankTip, xBase + 240.0f, 215.0f, 2, kBankTipTextScale);
    }

    /// <summary>Back and the four medal exchange buttons.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.TOUCH)
        {
            base.OnEvent(evt);
            return true;
        }

        if (evt.type == Event.BACK_PRESSED)
        {
            if ((flags & SHOWN) != 0)
            {
                OnEvent(Event.Button(buttonBack));
                return true;
            }
            return base.OnEvent(evt);
        }

        if (evt.type == Event.GUI && evt.info.gui.type == Event.Info.GUIInfo.BUTTON)
        {
            CCountry country;
            int medalCost = 0;
            int industryGain = 0;
            int moneyGain = 0;

            if (ReferenceEquals(evt.info.gui.ptr, buttonIndustrial10) && g_Commander.medal > 0)
            {
                medalCost = 1;
                industryGain = 6;
            }
            else if (ReferenceEquals(evt.info.gui.ptr, buttonIndustrial50) && g_Commander.medal > 4)
            {
                medalCost = 5;
                industryGain = 30;
            }
            else if (ReferenceEquals(evt.info.gui.ptr, buttonMoney20) && g_Commander.medal > 0)
            {
                medalCost = 1;
                moneyGain = 12;
            }
            else if (ReferenceEquals(evt.info.gui.ptr, buttonMoney100) && g_Commander.medal > 4)
            {
                medalCost = 5;
                moneyGain = 60;
            }

            if (medalCost != 0)
            {
                country = g_GameManager.GetCurCountry();
                country.industry += industryGain;
                country.money += moneyGain;
                x74 += industryGain;
                x78 += moneyGain;
                g_Commander.medal -= medalCost;
                country.borrowedLoan = true;
                CCSoundBox.GetInstance().PlaySE(AudioDefs.Ui("buy"));
            }
        }

        return base.OnEvent(evt);
    }

    /// <summary>Initializes the background, the HUD, the exchange buttons and the tip text.</summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;

        ecGraphics graphics = ecGraphics.Instance();
        float backgroundWidth = rect.w;
        string backgroundName;
        if (Layout.Tablet)
        {
            backgroundName = "bankbg_iPad.webp";
        }
        else if (Layout.Width480)
        {
            backgroundName = "bankbg.webp";
        }
        else if (Layout.WidthUpTo568)
        {
            backgroundName = "bankbg-568h.webp";
            backgroundWidth = 568.0f;
        }
        else
        {
            backgroundName = "bankbg-640h.webp";
            backgroundWidth = 640.0f;
        }

        textureBankBg = graphics.LoadWEBPTexture(backgroundName);
        imageBankBg = new ecImage(textureBankBg, 0.0f, 0.0f, backgroundWidth, rect.h);

        xBase = 0.0f;
        GUIRect childRect;
        if (Layout.Tablet)
        {
            buttonBack = new GUIButton();
            childRect = new GUIRect(0.0f, 730.0f, 40.0f, 38.0f);
            buttonBack.Init("buttontext_back.png", "buttontext_back_press.png", childRect, null);
            buttonBack.SetBackground("buttonboard_gray.png");
            AddChild(buttonBack, true);

            guiMedal = new GUIMedal();
            childRect = new GUIRect(55.0f, 718.0f, 146.0f, 50.0f);
            guiMedal.Init(childRect);
            AddChild(guiMedal, true);

            guiGold = new GUIGold();
            childRect = new GUIRect(0.0f, 0.0f, 104.0f, 38.0f);
            guiGold.Init(childRect);
            AddChild(guiGold, false);

            childRect = new GUIRect(512.0f, 160.0f, 1.0f, 1.0f);
            GUIManager.Instance().AddImage("text_bank.png", childRect, this);

            childRect = new GUIRect(233.0f, 318.0f, 196.0f, 62.0f);
            buttonIndustrial10 = GUIManager.Instance().AddButton("button_Industrial10.png", "button_Industrial10.png", childRect, this, null);
            buttonIndustrial10.greyEnable = true;

            childRect = new GUIRect(578.0f, 318.0f, 196.0f, 62.0f);
            buttonIndustrial50 = GUIManager.Instance().AddButton("button_Industrial50.png", "button_Industrial50.png", childRect, this, null);
            buttonIndustrial50.greyEnable = true;

            childRect = new GUIRect(233.0f, 218.0f, 196.0f, 62.0f);
            buttonMoney20 = GUIManager.Instance().AddButton("button_money20.png", "button_money20.png", childRect, this, null);
            buttonMoney20.greyEnable = true;

            childRect = new GUIRect(578.0f, 218.0f, 196.0f, 62.0f);
        }
        else
        {
            xBase = (graphics.screenW - 480.0f) * 0.5f;

            buttonBack = new GUIButton();
            childRect = new GUIRect(0.0f, 290.0f, 32.0f, 30.0f);
            buttonBack.Init("buttontext_back.png", "buttontext_back_press.png", childRect, null);
            buttonBack.SetBackground("buttonboard_gray.png");
            AddChild(buttonBack, true);

            guiMedal = new GUIMedal();
            childRect = new GUIRect(36.0f, 297.0f, 70.0f, 23.0f);
            guiMedal.Init(childRect);
            AddChild(guiMedal, true);

            guiGold = new GUIGold();
            childRect = new GUIRect(0.0f, 0.0f, 104.0f, 38.0f);
            guiGold.Init(childRect);
            AddChild(guiGold, false);

            childRect = new GUIRect(xBase + 240.0f, 80.0f, 1.0f, 1.0f);
            GUIManager.Instance().AddImage("text_bank.png", childRect, this);

            childRect = new GUIRect(xBase + 102.0f, 159.0f, 98.0f, 31.0f);
            buttonIndustrial10 = GUIManager.Instance().AddButton("button_Industrial10.png", "button_Industrial10.png", childRect, this, null);
            buttonIndustrial10.greyEnable = true;

            childRect = new GUIRect(xBase + 278.0f, 159.0f, 98.0f, 31.0f);
            buttonIndustrial50 = GUIManager.Instance().AddButton("button_Industrial50.png", "button_Industrial50.png", childRect, this, null);
            buttonIndustrial50.greyEnable = true;

            childRect = new GUIRect(xBase + 102.0f, 109.0f, 98.0f, 31.0f);
            buttonMoney20 = GUIManager.Instance().AddButton("button_money20.png", "button_money20.png", childRect, this, null);
            buttonMoney20.greyEnable = true;

            childRect = new GUIRect(xBase + 278.0f, 109.0f, 98.0f, 31.0f);
        }

        buttonMoney100 = GUIManager.Instance().AddButton("button_money100.png", "button_money100.png", childRect, this, null);
        buttonMoney100.greyEnable = true;

        textBankTip.Init(g_FontHt);
        textBankTip.SetText(g_StringTable.GetString("bank tip"));
        x74 = 0;
        x78 = 0;
    }
}
