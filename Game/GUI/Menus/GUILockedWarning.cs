namespace WC2;

/// <summary>"Campaign locked" hint of the main menu (gui/menus/GUILockedWarning.cpp).</summary>
public sealed class GUILockedWarning : GUIElement
{
    public ecTexture textureBoardLocked;
    public ecImage imageBoardLocked;
    public ecUniFont font4 = new ecUniFont();
    public ecText textLockedWarning = new ecText();
    public bool touched;

    /// <summary>Posts the close event (OTHER/17) to the menu state.</summary>
    private static void PostCloseEvent(GUILockedWarning warning)
    {
        var closeEvent = Event.Gui(Event.Info.GUIInfo.OTHER, warning, 17);
        warning.PostBaseEvent(closeEvent);
    }

    /// <summary>GUIElement::OnEvent of the original (bubbles the GUI event to the parent).</summary>
    private void PostBaseEvent(Event evt) => base.OnEvent(evt);

    /// <summary>Creates the hint; the resources are created by Init.</summary>
    public GUILockedWarning() { }

    /// <summary>Releases the font, the picture and the texture owned by the hint.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;

        font4.Release();

        imageBoardLocked = null;

        if (textureBoardLocked != null)
        {
            ecGraphics.Instance().FreeTexture(textureBoardLocked);
            textureBoardLocked = null;
        }

        base.Dispose();
    }

    /// <summary>Initializes the locked board and its text resources.</summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;

        ecGraphics graphics = ecGraphics.Instance();
        bool wideLayout = Layout.Tablet;

        ecTextureRect textureRect;
        if (wideLayout)
        {
            textureRect = new ecTextureRect(0f, 0f, 454f, 262f, 4f, 4f);
            textureBoardLocked = graphics.LoadTexture("board_locked@2x.png");
        }
        else
        {
            textureRect = new ecTextureRect(0f, 0f, 227f, 131f, 2f, 2f);
            textureBoardLocked = graphics.LoadTexture("board_locked.png");
        }
        imageBoardLocked = new ecImage(textureBoardLocked, textureRect);
        touched = false;

        string language = g_LocalizableStrings.GetString("language");
        bool useHdFont = wideLayout || g_contenscalefactor == 2.0f;
        string fontName = Sprintf(useHdFont ? "font4_%s_hd.fnt" : "font4_%s.fnt", language);
        font4.Init(fontName, !wideLayout && useHdFont);
        textLockedWarning.Init(font4);
        textLockedWarning.SetText(g_StringTable.GetString("locked warning"));
    }

    /// <summary>Closes on tap and on the back key.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.TOUCH)
        {
            if (evt.info.touch.type == Event.Info.TouchInfo.BEGIN)
            {
                touched = true;
            }
            else if (evt.info.touch.type == Event.Info.TouchInfo.END)
            {
                if (touched)
                    PostCloseEvent(this);
                touched = false;
            }
            return true;
        }

        if (evt.type == Event.BACK_PRESSED &&
            (flags & SHOWN) != 0)
        {
            PostCloseEvent(this);
            return true;
        }

        return base.OnEvent(evt);
    }

    /// <summary>Draws the half transparent mask, the locked board and the hint text.</summary>
    public override void OnRender()
    {
        GetAbsRect(out GUIRect absRect);

        ecGraphics.Instance().Fade(0.5f);
        imageBoardLocked.Render(absRect.x, absRect.y);

        bool wideLayout = Layout.Tablet;
        float textOffsetX = wideLayout ? 32f : 16f;
        float textOffsetY = wideLayout ? 120f : 60f;
        textLockedWarning.DrawText(
                absRect.x + textOffsetX,
                absRect.y + textOffsetY,
                0);
    }
}
