namespace WC2;

/// <summary>Turn begin popup of the player (GUIBegin.cpp): round income, tech, medals, OK and bank entry.</summary>
public sealed class GUIBegin : GUIElement
{
    public GUIButtonEx buttonOk;
    public GUIButton buttonBank;
    public GUITech guiTech;
    public ecTexture x40;
    public ecImage imageRoundStartBoard;
    public ecImage[][] imageMedal = { new ecImage[3], new ecImage[3], new ecImage[3], new ecImage[3], new ecImage[3], new ecImage[3] };
    public readonly ecText textRound = new();
    public readonly ecText textTaxes = new();
    public readonly ecText textIndustry = new();
    public ecImage xE4;

    public GUIBegin() { }

    /// <summary>Releases the images and the texture held directly; the buttons and the tech widget belong to the child tree.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageRoundStartBoard = null;

        if (x40 != null)
        {
            ecGraphics.Instance().FreeTexture(x40);
            x40 = null;
        }

        foreach (var medalGroup in imageMedal)
            for (int j = 0; j < medalGroup.Length; j++) medalGroup[j] = null;

        xE4 = null;
        base.Dispose();
    }

    /// <summary>
    /// Initializes the popup: income, tech info, medals, OK and the bank entry.
    /// The medal pictures are preloaded as "category x three levels"; missing ones stay null.
    /// </summary>
    public void Init(in GUIRect rect_in)
    {
        string[] commander_medal_list = { "soldiers", "airforce", "cannon", "panzer", "navy", "honor" };
        rect.x = rect_in.x;
        rect.y = rect_in.y;
        rect.w = rect_in.w;
        rect.h = rect_in.h;
        imageRoundStartBoard = new ecImage(s_TextureRes.GetImage("roundstart_board.png"));

        // the resources are named soldiers/airforce/... combined with the levels 1..3
        for (int i = 0; i < 6; i++)
        {
            for (int j = 1; j <= 3; j++)
            {
                string tmp = Sprintf("medal_%s_%d.png", commander_medal_list[i], j);
                if (s_TextureRes.GetImage(tmp) != null)
                    imageMedal[i][j - 1] = new ecImage(s_TextureRes.GetImage(tmp));
            }
        }
        // the tech widget belongs to this popup and lives with its child tree
        guiTech = new GUITech();
        var tmp_rect = new GUIRect();
        tmp_rect.y = 56.0f;
        tmp_rect.w = 80.0f;
        tmp_rect.h = 30.0f;
        tmp_rect.x = (rect.w * 0.5f) - 40.0f;
        guiTech.Init(tmp_rect);
        AddChild(guiTech, false);
        guiTech.showBoard = false;
        textRound.Init(g_Num4);
        textRound.SetColor(0xFF161A8Fu);
        textTaxes.Init(g_Num4);
        textTaxes.SetColor(0xFF07303Du);
        textIndustry.Init(g_Num4);
        textIndustry.SetColor(0xFF07303Du);
        // the OK button is an extended button; the wide layout needs its own text picture offset
        buttonOk = new GUIButtonEx();
        tmp_rect.x = 114.0f;
        tmp_rect.y = 225.0f;
        tmp_rect.w = 64.0f;
        tmp_rect.h = 32.0f;
        buttonOk.Init("green_normal.png", "green_normal.png", tmp_rect, null);
        buttonOk.SetImageText("buttontext_ok.png", "buttontext_ok_press.png");
        if (ecGraphics.Instance().widthMode == 3)
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
        tmp_rect.x = 7.0f;
        tmp_rect.y = 225.0f;
        tmp_rect.w = 64.0f;
        tmp_rect.h = 32.0f;
        buttonBank = GUIManager.Instance().AddButton("buttontext_bank.png", "buttontext_bank_press.png", tmp_rect, this, null);
        buttonBank.SetBackground("blue_normal.png");
    }

    /// <summary>
    /// Draws the popup and the war medals equipped by the current country. The half transparent
    /// full screen fade is drawn before the popup so the map goes to the background.
    /// </summary>
    public override void OnRender()
    {
        GetAbsRect(out var tmp_rect);
        ecGraphics.Instance().Fade(0.5f);
        imageRoundStartBoard.Render(tmp_rect.x, tmp_rect.y);
        CCountry cur_country = g_GameManager.GetCurCountry();

        if (cur_country != null)
        {
            float x_tmp = tmp_rect.x + 30.0f;
            float y_tmp = tmp_rect.y + 168.0f;
            // one icon per medal category at the current level; half size on the wide layout
            for (int i = (int)INFANTRY_MEDAL; i < 6; i++)
            {
                int j = cur_country.GetWarMedalLevel((WARMEDAL_ID)i);
                if (j > 0 && j <= 3 && imageMedal[i][j - 1] != null)
                {
                    if (ecGraphics.Instance().widthMode == 3)
                        imageMedal[i][j - 1].RenderEx(x_tmp, y_tmp, 0.0f, 0.5f, 0.0f);
                    else
                        imageMedal[i][j - 1].Render(x_tmp, y_tmp);
                }
                x_tmp += 24.0f;
            }
        }
        textRound.DrawText(tmp_rect.x + 85.0f, tmp_rect.y + 33.0f, 2);
        textTaxes.DrawText(tmp_rect.x + 74.0f, tmp_rect.y + 96.0f, 0);
        textIndustry.DrawText(tmp_rect.x + 74.0f, tmp_rect.y + 123.0f, 0);
        if (xE4 != null)
            xE4.Render(tmp_rect.x + 117.0f, tmp_rect.y - 10.0f);
    }

    /// <summary>Refreshes the round, income, industry and the bank button state from the current turn and country.</summary>
    public void ResetData()
    {
        string text;
        if (g_GameManager.gameMode == CGameManager.CAMPAIGN)
            text = Sprintf("ROUND %d/%d", g_GameManager.currentTurnNumMinusOne + 1, g_GameManager.victoryTurn);
        else
            text = Sprintf("ROUND %d", g_GameManager.currentTurnNumMinusOne + 1);
        textRound.SetText(text);

        CCountry currentCountry = g_GameManager.GetCurCountry();
        if (currentCountry == null) return;

        text = Sprintf("%d", currentCountry.GetTaxes());
        textTaxes.SetText(text);
        text = Sprintf("%d", currentCountry.GetIndustrys());
        textIndustry.SetText(text);

        if (g_GameManager.gameMode == CGameManager.MULTIPLAYER)
            xE4 = null;

        if (currentCountry.borrowedLoan)
            buttonBank.Hide();
        else
            buttonBank.Show();
    }

    /// <summary>OK, touch and back key handling of the popup.</summary>
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

        if (evt.type == Event.GUI &&
            evt.info.gui.type == Event.Info.GUIInfo.BUTTON &&
            ReferenceEquals(evt.info.gui.ptr, buttonOk))
        {
            Hide();
        }

        return base.OnEvent(evt);
    }
}
