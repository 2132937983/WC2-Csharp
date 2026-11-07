namespace WC2;

/// <summary>Hint shown when any country of the level is defeated (GUIDefeated.cpp).</summary>
public sealed class GUIDefeated : GUIElement
{
    public GUIButtonEx buttonOk;
    public ecTexture x38;
    public ecImage imageCountryDefeat;
    public readonly ecTextureRes textureResFlag = new();
    public ecImage imageFlag;
    public ecImage imageGeneral;
    public CCountry defeatedCountry;
    public readonly ecText textDefeated = new();
    public float autoCloseTimer;
    public float x9C;
    public float xA0;
    public int xA4;

    public GUIDefeated() { }

    /// <summary>Hides the hint and releases the resources loaded for the last display.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        HideDefeated();
        base.Dispose();
    }

    /// <summary>Initializes the animation state, the OK button and the hint text.</summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;
        autoCloseTimer = -1.0f;
        x9C = 1.0f;
        xA0 = 1.0f;
        xA4 = 0;

        buttonOk = new GUIButtonEx();
        var buttonRect = new GUIRect(61.0f, 140.0f, 64.0f, 32.0f);
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

        textDefeated.Init(g_FontKt);
        textDefeated.SetText(g_StringTable.GetString("defeated"));
        textDefeated.SetColor(0xFF000000u);
        Hide();
    }

    /// <summary>
    /// Prepares and shows the popup for the country that was just defeated. The flag comes from
    /// a separate atlas; an AI shows its commander photo, falling back to the common photo.
    /// </summary>
    public void ShowDefeated(CCountry country_in)
    {
        imageCountryDefeat = new ecImage(s_TextureRes.GetImage("country_defeat.png"));
        // same flag selection rule as CGameRes so the popup matches the map flags
        if (Layout.Tablet || g_contenscalefactor != 2.0f)
            textureResFlag.LoadRes("flag.xml", false);
        else
            textureResFlag.LoadRes("flag_hd.xml", true);
        string tmp_char = Sprintf("flag_%s.png", country_in.name);
        // without a dedicated flag the reference stays null and the render skips it
        // a mod's flag picture, the flag table, a new country's picture (CountryArt)
        if (CountryArt.BoxFlag(textureResFlag, tmp_char) is ecImageAttr flagAttr)
            imageFlag = new ecImage(flagAttr);
        else
            imageFlag = null;
        defeatedCountry = country_in;
        if (!country_in.ai)
        {
            imageGeneral = new ecImage(s_TextureRes.GetImage("general_player.png"));
        }
        else
        {
            string commanderName = country_in.GetCommanderName();
            if (!string.IsNullOrEmpty(commanderName))
            {
                GeneralPhoto tmp_general_photo = CObjectDef.Instance().GetGeneralPhoto(commanderName);
                if (tmp_general_photo != null)
                {
                    // the GeneralPhoto file name is not the png name: strip the original extension and append .png
                    string tmp_str = tmp_general_photo.fileName;
                    if (tmp_str.Length >= 5) tmp_str = tmp_str.Substring(0, tmp_str.Length - 5);
                    tmp_str += ".png";
                    imageGeneral = new ecImage(s_TextureRes.GetImage(tmp_str));
                }
                else
                {
                    imageGeneral = new ecImage(s_TextureRes.GetImage("general_common.png"));
                }
            }
        }
        Show();
        // these fields drive the auto close / entrance animation timers of the original GUIDefeated
        autoCloseTimer = 0.0f;
        x9C = 3.0f;
        xA0 = 0.0f;
        xA4 = 1;
        g_SoundRes.PlayCharSE(POP_WAV);
    }

    /// <summary>Auto close timer of the multiplayer mode.</summary>
    public override void OnUpdate(float deltaTimeS)
    {
        if (g_GameManager.gameMode != CGameManager.MULTIPLAYER) return;
        if (autoCloseTimer < 0.0f || (flags & SHOWN) == 0) return;

        float nextTimer = autoCloseTimer + deltaTimeS;
        if (nextTimer < 5.0f)
        {
            autoCloseTimer = nextTimer;
            return;
        }

        autoCloseTimer = -1.0f;
        base.OnEvent(Event.Button(buttonOk));
    }

    /// <summary>Releases the resources of the last display and hides the widget.</summary>
    public void HideDefeated()
    {
        imageCountryDefeat = null;

        if (x38 != null)
        {
            ecGraphics.Instance().FreeTexture(x38);
            x38 = null;
        }

        imageFlag = null;
        imageGeneral = null;

        textureResFlag.Release();
        Hide();
    }

    /// <summary>Touch and back key handling of the hint.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.TOUCH)
        {
            base.OnEvent(evt);
            return true;
        }

        if (evt.type == Event.BACK_PRESSED && (flags & SHOWN) != 0)
        {
            OnEvent(Event.Button(buttonOk));
            return true;
        }

        return base.OnEvent(evt);
    }

    /// <summary>Draws the board, the flag, the general photo and the commander medal.</summary>
    public override void OnRender()
    {
        GetAbsRect(out var absoluteRect);

        if (imageCountryDefeat != null)
            imageCountryDefeat.Render(absoluteRect.x, absoluteRect.y);
        if (imageFlag != null)
            imageFlag.Render(absoluteRect.x + 19.0f, absoluteRect.y + 55.0f);

        if (imageGeneral != null)
        {
            imageGeneral.Render(absoluteRect.x + 76.0f, absoluteRect.y + 90.0f);
            if (defeatedCountry != null)
            {
                float medalX = absoluteRect.x + 144.0f;
                float medalY = absoluteRect.y + 102.0f;
                if (defeatedCountry.ai)
                    g_GameRes.RenderAICommanderMedal(1, medalX, medalY, defeatedCountry.name, defeatedCountry.alliance);
                else
                    g_GameRes.RenderCommanderMedal(1, medalX, medalY, defeatedCountry.GetCommanderLevel());
            }
        }

        textDefeated.DrawText(absoluteRect.x + absoluteRect.w * 0.5f, absoluteRect.y + 99.0f, 2);
    }
}
