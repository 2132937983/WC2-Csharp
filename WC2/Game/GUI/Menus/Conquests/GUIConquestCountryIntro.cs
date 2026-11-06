namespace WC2;

/// <summary>Country description board of the conquest selection (gui/menus/conquests/GUIConquestCountryIntro.cpp).</summary>
public sealed class GUIConquestCountryIntro : GUIElement
{
    public GUIButtonEx buttonOk;
    public ecTexture textureBoard;
    public ecImage imageBoard;
    public ecText textConquestCountryIntro = new ecText();
    public ecText textConquestCountryName = new ecText();
    public ecText textVictory = new ecText();
    public string countryId = "";

    public GUIConquestCountryIntro() { }

    public override void Dispose()
    {
        if (IsDisposed) return;
        imageBoard = null;
        if (textureBoard != null)
        {
            ecGraphics.Instance().FreeTexture(textureBoard);
            textureBoard = null;
        }
        base.Dispose();
    }

    public void Init(in GUIRect rectIn, string countryName, string id, int conquestId)
    {
        rect = rectIn;
        bool wide = ecGraphics.Instance().widthMode == 3;
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

        textConquestCountryIntro.Init(g_FontKt);
        textConquestCountryName.Init(g_FontKt);
        textVictory.Init(g_FontHt);
        textConquestCountryIntro.SetColor(0xFF33313E);
        textConquestCountryName.SetColor(0xFF33313E);
        textVictory.SetColor(0xFF33313E);
        SetCountry(countryName, id, conquestId);
    }

    /// <summary>
    /// Sets the described country.
    /// </summary>
    /// <param name="countryName">faction display name of the original; kept for compatibility, not used for the unique name fallback</param>
    /// <param name="id">country id</param>
    /// <param name="conquestId">conquest index</param>
    public void SetCountry(string countryName, string id, int conquestId)
    {
        countryId = id ?? "";
        // name is the faction display name shared by several countries; without a string table entry the unique id is used
        string safeName = countryId;
        int stringId = conquestId + 1;
        string key = Sprintf("conquest country name %d %s", stringId, countryId);
        string fallbackKey = Sprintf("country name %s", countryId);
        safeName = GetLocalized(key, fallbackKey, safeName);
        key = Sprintf("conquest country intro %d %s", stringId, countryId);
        fallbackKey = Sprintf("country intro %s", countryId);
        string genericIntro = GetLocalized(fallbackKey, "country intro default", "");
        string intro = GetLocalized(key, null, genericIntro);
        key = Sprintf("conquest country victory %d %s", stringId, countryId);
        fallbackKey = Sprintf("country victory %s", countryId);
        string genericVictory = GetLocalized(fallbackKey, "country victory default", "");
        string victory = GetLocalized(key, null, genericVictory);
        textConquestCountryIntro.SetText(intro);
        textConquestCountryName.SetText(safeName);
        textVictory.SetText(victory);
    }

    public override void OnRender()
    {
        GetAbsRect(out GUIRect absoluteRect);
        bool wide = ecGraphics.Instance().widthMode == 3;
        ecGraphics.Instance().Fade(0.5f);
        imageBoard.Render(absoluteRect.x, absoluteRect.y);
        float scale = g_introTextScale;
        if (wide)
        {
            textConquestCountryName.DrawText(absoluteRect.x + 480f, absoluteRect.y + 76f, 2);
            ecTextDrawScaled(textConquestCountryIntro, absoluteRect.x + 120f, absoluteRect.y + 176f, 0, scale);
            ecTextDrawScaled(textVictory, absoluteRect.x + 366f, absoluteRect.y + 410f, 0, scale);
        }
        else
        {
            textConquestCountryName.DrawText(absoluteRect.x + 240f, absoluteRect.y + 38f, 2);
            ecTextDrawScaled(textConquestCountryIntro, absoluteRect.x + 60f, absoluteRect.y + 88f, 0, scale);
            ecTextDrawScaled(textVictory, absoluteRect.x + 183f, absoluteRect.y + 205f, 0, scale);
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
