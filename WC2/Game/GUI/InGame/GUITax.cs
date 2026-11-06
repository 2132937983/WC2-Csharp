namespace WC2;

/// <summary>Economic info panel of the selected area (GUITax.cpp).</summary>
public sealed class GUITax : GUIElement
{
    public ecImage imageIncomeBoard;
    public ecImage imageIncomeBoard2;
    public ecImage imageCommander;
    public readonly ecText moneyText = new();
    public int money = -1;
    public readonly ecText industryText = new();
    public int industry = -1;
    public ecImage[] imageStamp = new ecImage[4];
    public int installationType;
    public CCountry country;

    public GUITax() { }

    /// <summary>Releases the images owned by the panel.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageIncomeBoard = null;
        imageIncomeBoard2 = null;
        for (int i = 0; i < imageStamp.Length; i++) imageStamp[i] = null;
        imageCommander = null;
        base.Dispose();
    }

    /// <summary>Updates the money income; unchanged values do not rebuild the text.</summary>
    public void SetMoney(int moneyIn)
    {
        if (money == moneyIn) return;

        money = moneyIn;
        moneyText.SetText(Sprintf("%d", moneyIn));
    }

    /// <summary>Updates the industry income; unchanged values do not rebuild the text.</summary>
    public void SetIndustry(int industryIn)
    {
        if (industry == industryIn) return;

        industry = industryIn;
        industryText.SetText(Sprintf("%d", industryIn));
    }

    /// <summary>Switches the panel to the area and refreshes income, installation and commander photo.</summary>
    public void SetArea(int areaId)
    {
        CArea area = g_Scene.GetArea(areaId);
        SetMoney(area.GetRealTax());
        SetIndustry(area.GetIndustry());
        installationType = (int)area.installationType;

        imageCommander = null;

        if (!area.HasArmyCard((int)ARMY_CARD.COMMANDER)) return;

        country = area.country;
        if (country.ai)
        {
            string commanderName = country.GetCommanderName();
            if (string.IsNullOrEmpty(commanderName)) return;

            GeneralPhoto generalPhoto = CObjectDef.Instance().GetGeneralPhoto(commanderName);
            if (generalPhoto != null)
            {
                string imageName = generalPhoto.fileName;
                if (imageName.Length >= 5)
                    imageName = imageName.Substring(0, imageName.Length - 5) + ".png";
                imageCommander = new ecImage(s_TextureRes.GetImage(imageName));
                return;
            }

            imageCommander = new ecImage(s_TextureRes.GetImage("general_common.png"));
            return;
        }

        imageCommander = new ecImage(s_TextureRes.GetImage("general_player.png"));
    }

    /// <summary>
    /// Renders the economic info of the selected area, the installation stamp and (if any) the
    /// commander info. Uses the absolute rectangle so it stays aligned when moved as a child.
    /// </summary>
    public override void OnRender()
    {
        GetAbsRect(out var Rect);
        if (imageCommander != null)
        {
            // with a commander photo the taller panel reserves the lower half for photo and medal
            imageIncomeBoard2.Render(Rect.x, Rect.y);
            imageCommander.Render(Rect.x, Rect.y + 108.0f);
            if (country != null && country.ai)
            {
                // an AI has no player commander level, the common country / alliance medal is used
                g_GameRes.RenderAICommanderMedal(1, Rect.x + 60.0f, Rect.y + 112.0f, country.name, country.alliance);
            }
            else
            {
                g_GameRes.RenderCommanderMedal(1, Rect.x + 60.0f, Rect.y + 112.0f, country.GetCommanderLevel());
            }
        }
        else
        {
            imageIncomeBoard.Render(Rect.x, Rect.y);
        }
        // installationType 0 means no installation; the other values map to the four stamp icons
        if (installationType > 0 && installationType <= 4)
            imageStamp[installationType - 1].Render(Rect.x + 55.0f, Rect.y + 28.0f);
        moneyText.DrawText(Rect.x + 50.0f, Rect.y + 25.0f, 1);
        industryText.DrawText(Rect.x + 50.0f, Rect.y + 45.0f, 1);
    }

    /// <summary>Initializes the background, the installation stamps and the two value texts.</summary>
    public void Init(in GUIRect Rect)
    {
        rect.x = Rect.x;
        rect.y = Rect.y;
        rect.w = Rect.w;
        rect.h = Rect.h;
        imageIncomeBoard = new ecImage(s_TextureRes.GetImage("income_board.png"));
        imageIncomeBoard2 = new ecImage(s_TextureRes.GetImage("income_board2.png"));
        imageStamp[0] = new ecImage(s_TextureRes.GetImage("stamp_fortress.png"));
        imageStamp[1] = new ecImage(s_TextureRes.GetImage("stamp_wire.png"));
        imageStamp[2] = new ecImage(s_TextureRes.GetImage("stamp_aagun.png"));
        imageStamp[3] = new ecImage(s_TextureRes.GetImage("stamp_radar.png"));
        // no area bound initially; SelectArea / SetArea fill the data when the selection changes
        installationType = 0;
        moneyText.Init(g_Num3);
        moneyText.SetColor(0xFF0F2632u);
        SetMoney(0);
        industryText.Init(g_Num3);
        industryText.SetColor(0xFF0F2632u);
        SetIndustry(0);
        country = null;
    }
}
