namespace WC2;

/// <summary>Exit confirmation box of the main menu (gui/menus/GUIExitWarning.cpp).</summary>
public sealed class GUIExitWarning : GUIElement
{
    public ecTexture textureBoardExit;
    public ecImage imageBoardExit;
    public ecUniFont font4 = new ecUniFont();
    public ecText textExitWarning = new ecText();
    public bool x80;
    public GUIButton buttonCancleExit;
    public GUIButton buttonConfirmExit;

    /// <summary>Creates the box; the resources are created by Init.</summary>
    public GUIExitWarning() { }

    /// <summary>Releases the font, the picture and the texture owned by the box.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;

        font4.Release();

        imageBoardExit = null;

        if (textureBoardExit != null)
        {
            ecGraphics.Instance().FreeTexture(textureBoardExit);
            textureBoardExit = null;
        }

        base.Dispose();
    }

    /// <summary>Initializes the board, the warning text and the two buttons.</summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;

        ecGraphics graphics = ecGraphics.Instance();
        bool wideLayout = Layout.Tablet;
        string language = g_LocalizableStrings.GetString("language");
        bool chinese = language == "cn";

        ecTextureRect textureRect;
        if (wideLayout)
        {
            textureRect = new ecTextureRect(0f, 0f, 454f, 262f, 4f, 4f);
            textureBoardExit = graphics.LoadTexture(chinese ? "board_exit_cn@2x.png" : "board_exit@2x.png");
        }
        else
        {
            textureRect = new ecTextureRect(0f, 0f, 227f, 131f, 2f, 2f);
            textureBoardExit = graphics.LoadTexture(chinese ? "board_exit_cn.png" : "board_exit.png");
        }
        imageBoardExit = new ecImage(textureBoardExit, textureRect);
        x80 = false;

        bool useHdFont = wideLayout || g_contenscalefactor == 2.0f;
        string fontName = Sprintf(useHdFont ? "font4_%s_hd.fnt" : "font4_%s.fnt", language);
        font4.Init(fontName, !wideLayout && useHdFont);
        textExitWarning.Init(font4);
        textExitWarning.SetText(g_StringTable.GetString("exit warning"));

        buttonCancleExit = new GUIButton();
        buttonConfirmExit = new GUIButton();

        GUIRect buttonRect;
        if (wideLayout)
        {
            buttonRect = new GUIRect(10f, 190f, 80f, 76f);
            buttonCancleExit.Init("button_cancleexit.png", "button_cancleexit.png", buttonRect, null);
            buttonRect = new GUIRect(350f, 190f, 80f, 76f);
        }
        else
        {
            buttonRect = new GUIRect(5f, 95f, 40f, 38f);
            buttonCancleExit.Init("button_cancleexit.png", "button_cancleexit.png", buttonRect, null);
            buttonRect = new GUIRect(175f, 95f, 40f, 38f);
        }
        buttonConfirmExit.Init("button_confirmexit.png", "button_confirmexit.png", buttonRect, null);

        AddChild(buttonCancleExit, true);
        AddChild(buttonConfirmExit, true);
    }

    /// <summary>Handles the cancel and confirm buttons.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.TOUCH)
        {
            // original engine order: the buttons see the touch first, then the box swallows it (modal)
            base.OnEvent(evt);
            return true;
        }

        if (evt.type == Event.GUI &&
            evt.info.gui.type == Event.Info.GUIInfo.BUTTON)
        {
            if (evt.info.gui.ptr == buttonCancleExit)
            {
                var closeEvent = Event.Gui(Event.Info.GUIInfo.OTHER, this, 26);
                base.OnEvent(closeEvent);
            }

            if (evt.info.gui.ptr == buttonConfirmExit)
                AppJavaExit();
        }

        return base.OnEvent(evt);
    }

    /// <summary>Draws the half transparent mask and the exit board.</summary>
    public override void OnRender()
    {
        GetAbsRect(out GUIRect absRect);
        ecGraphics.Instance().Fade(0.5f);
        imageBoardExit.Render(absRect.x, absRect.y);
    }
}
