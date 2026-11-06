namespace WC2;

/// <summary>Conquest description board of the conquest selection (gui/menus/conquests/GUIConquestIntro.cpp).</summary>
public sealed class GUIConquestIntro : GUIElement
{
    public GUIButtonEx buttonOk;
    public ecTexture textureBoard;
    public ecImage imageBoard;
    public ecUniFont fontNum7 = new ecUniFont();
    public ecText textAge = new ecText();
    public ecText textConquestIntro = new ecText();
    public ecText textConquestName = new ecText();
    public int conquestId;

    private static void ReadConquestText(int id, out string age, out string intro, out string name)
    {
        // the runtime conquest index is 0-based, the string table ids start at 1
        int stringId = id + 1;
        string key = Sprintf("conquest name %d", stringId);
        name = GetLocalized(key, null, key);
        key = Sprintf("conquest age %d", stringId);
        age = GetLocalized(key, "conquest age default", "conquest");
        key = Sprintf("conquest intro %d", stringId);
        intro = GetLocalized(key, "conquest intro default", "");
    }

    public GUIConquestIntro()
    {
        conquestId = 0;
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        // fontNum7 is kept for the class layout; the age text uses the global g_Num7
        fontNum7.Release();
        imageBoard = null;
        if (textureBoard != null)
        {
            ecGraphics.Instance().FreeTexture(textureBoard);
            textureBoard = null;
        }
        base.Dispose();
    }

    /// <summary>Initializes the board, the ok button and the texts.</summary>
    public void Init(in GUIRect rectIn, int id)
    {
        rect = rectIn;
        bool wide = Layout.Tablet;
        textureBoard = ecGraphics.Instance().LoadWEBPTexture(wide ? "board_intro@2x.webp" : "board_intro.webp");
        imageBoard = new ecImage(textureBoard, 0f, 0f, wide ? 960f : 480f, wide ? 640f : 320f);
        buttonOk = new GUIButtonEx();
        buttonOk.Init("green_normal.png", "green_normal.png",
                      wide ? new GUIRect(420f, 580f, 64f, 32f)
                           : new GUIRect(210f, 283f, 64f, 32f), null);
        buttonOk.SetImageText("buttontext_ok.png", "buttontext_ok_press.png");
        buttonOk.imagePos[0] = wide ? 11f : 13f;
        buttonOk.imagePos[1] = wide ? -5f : 0f;
        AddChild(buttonOk, true);

        // fontNum7 is kept for the class layout; the age text uses the global number font
        textAge.Init(g_Num7);
        textConquestIntro.Init(g_FontKt);
        textConquestName.Init(g_FontKt);
        textConquestIntro.SetColor(0xFF33313E);
        textConquestName.SetColor(0xFF33313E);
        SetConquest(id);
    }

    /// <summary>Sets the described conquest.</summary>
    public void SetConquest(int id)
    {
        conquestId = id;
        ReadConquestText(id, out string age, out string intro, out string name);
        textAge.SetText(age);
        textConquestIntro.SetText(intro);
        textConquestName.SetText(name);
    }

    /// <summary>Renders the conquest description.</summary>
    public override void OnRender()
    {
        GetAbsRect(out GUIRect absoluteRect);
        bool wide = Layout.Tablet;
        ecGraphics.Instance().Fade(0.5f);
        imageBoard.Render(absoluteRect.x, absoluteRect.y);
        float scale = g_introTextScale;
        if (wide)
        {
            textConquestName.DrawText(absoluteRect.x + 480f, absoluteRect.y + 76f, 2);
            textAge.DrawText(absoluteRect.x + 480f, absoluteRect.y + 130f, 2);
            ecTextDrawScaled(textConquestIntro, absoluteRect.x + 120f, absoluteRect.y + 176f, 0, scale);
        }
        else
        {
            textConquestName.DrawText(absoluteRect.x + 240f, absoluteRect.y + 38f, 2);
            textAge.DrawText(absoluteRect.x + 240f, absoluteRect.y + 65f, 2);
            ecTextDrawScaled(textConquestIntro, absoluteRect.x + 60f, absoluteRect.y + 88f, 0, scale);
        }
    }

    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.TOUCH)
        {
            // original engine order: the ok button sees the touch first, then the board swallows it
            base.OnEvent(evt);
            return true;
        }
        if (evt.type == Event.BACK_PRESSED && (flags & SHOWN) != 0)
        {
            if (base.OnEvent(evt)) return true;
            Hide();
            return true;
        }
        if (evt.type == Event.GUI &&
            evt.info.gui.type == Event.Info.GUIInfo.BUTTON &&
            evt.info.gui.ptr == buttonOk)
        {
            Hide();
        }
        return base.OnEvent(evt);
    }
}
