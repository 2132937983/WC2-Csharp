namespace WC2;

/// <summary>Warning popup (GUIWarning.cpp); mainly used by the multiplayer flows.</summary>
public sealed class GUIWarning : GUIElement
{
    public GUIButtonEx buttonOk;
    public GUIButton buttonCancel;
    public ecTexture textureWarningBoard;
    public ecImage imageWarningBoard;
    public readonly ecLabelText labelText1 = new();
    public readonly ecLabelText labelText2 = new();
    public int warningType;

    public GUIWarning() { }

    /// <summary>Releases the board image and texture.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageWarningBoard = null;
        if (textureWarningBoard != null)
        {
            ecGraphics.Instance().FreeTexture(textureWarningBoard);
            textureWarningBoard = null;
        }
        base.Dispose();
    }

    /// <summary>Initializes the board, the buttons and the localized title and body.</summary>
    public void Init(in GUIRect rectIn, int warning)
    {
        rect = rectIn;

        var boardTextureRect = new ecTextureRect(0.0f, 0.0f, 231.0f, 157.0f, 7.0f, 3.0f);
        textureWarningBoard = ecGraphics.Instance().LoadTexture("warning_board.png");
        imageWarningBoard = new ecImage(textureWarningBoard, boardTextureRect);

        buttonOk = new GUIButtonEx();
        var buttonRect = new GUIRect(76.0f, 98.0f, 64.0f, 32.0f);
        buttonOk.Init("green_normal.png", "green_normal.png", buttonRect, null);
        buttonOk.SetImageText("buttontext_ok.png", "buttontext_ok_press.png");
        if (Layout.Tablet)
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

        if (warning == 3)
        {
            buttonRect = new GUIRect(13.0f, 98.0f, 64.0f, 32.0f);
            buttonCancel = GUIManager.Instance().AddButton("buttontext_cancel.png", "buttontext_cancel_press.png", buttonRect, this, null);
            buttonCancel.SetBackground("gray_normal.png");
            buttonOk.SetPos(134.0f, 98.0f);
        }
        else
        {
            buttonCancel = null;
        }

        labelText1.Init("Arial", 20, 2, (int)rect.w, 24);
        labelText2.Init("Arial", 16, 2, (int)(rect.w - 12.0f), 70);
        labelText2.SetColor(0xFF33313Eu);

        labelText1.SetText(g_StringTable.GetString(Sprintf("warning title %d", warning)));
        labelText2.SetText(g_StringTable.GetString(Sprintf("warning text %d", warning)));
        warningType = warning;
    }

    /// <summary>Draws the dim, the board, the title and the body.</summary>
    public override void OnRender()
    {
        GetAbsRect(out var absoluteRect);

        ecGraphics.Instance().Fade(0.5f);
        imageWarningBoard.Render(absoluteRect.x, absoluteRect.y);
        labelText1.DrawText(absoluteRect.x, absoluteRect.y + 3.0f);
        labelText2.DrawText(absoluteRect.x + 6.0f, absoluteRect.y + 34.0f);
    }

    /// <summary>Button, touch and back key handling; back means cancel, or OK without a cancel button.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.TOUCH)
        {
            base.OnEvent(evt);
            return true;
        }

        if (evt.type == Event.BACK_PRESSED && (flags & SHOWN) != 0)
        {
            GUIElement targetButton = buttonCancel != null ? buttonCancel : buttonOk;
            OnEvent(Event.Button(targetButton));
            return true;
        }

        if (evt.type == Event.GUI &&
            evt.info.gui.type == Event.Info.GUIInfo.BUTTON &&
            ReferenceEquals(evt.info.gui.ptr, buttonOk))
        {
            Hide();
        }

        return base.OnEvent(evt);
    }

    /// <summary>The warning has no per frame logic.</summary>
    public override void OnUpdate(float deltaTimeS) { }
}
