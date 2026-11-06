namespace WC2;

/// <summary>Gold and industry HUD (GUIGold.cpp).</summary>
public sealed class GUIGold : GUIElement
{
    public ecImage imageAssetBoard;
    public readonly ecText textMoney = new();
    public int money = -1;
    public readonly ecText textIndustry = new();
    public int industry = -1;

    /// <summary>The cached values start at -1 so the first set forces a text refresh.</summary>
    public GUIGold() { }

    /// <summary>Releases the asset board image.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageAssetBoard = null;
        base.Dispose();
    }

    /// <summary>Initializes the resident HUD: asset board, money text and industry text.</summary>
    public void Init(in GUIRect rect_in)
    {
        rect.x = rect_in.x;
        rect.y = rect_in.y;
        rect.w = rect_in.w;
        rect.h = rect_in.h;
        imageAssetBoard = new ecImage(s_TextureRes.GetImage("asset_board.png"));
        textMoney.Init(g_Num3);
        textMoney.SetColor(0xFF0F2632u);
        // -1/0 is the default spacing of the original UI font, kept to match the board layout
        textMoney.fontSize[0] = -1.0f;
        textMoney.fontSize[1] = 0.0f;
        SetMoney(0);
        textIndustry.Init(g_Num3);
        textIndustry.SetColor(0xFF0F2632u);
        textIndustry.fontSize[0] = -1.0f;
        textIndustry.fontSize[1] = 0.0f;
        SetIndustry(0);
    }

    /// <summary>Renders the board and the two values at the absolute position.</summary>
    public override void OnRender()
    {
        GetAbsRect(out var tmpRect);
        imageAssetBoard.Render(tmpRect.x, tmpRect.y);
        textMoney.DrawText(tmpRect.x + 15.0f, tmpRect.y + 3.0f, 0);
        textIndustry.DrawText(tmpRect.x + 60.0f, tmpRect.y + 3.0f, 0);
    }

    /// <summary>Updates the money text; unchanged values do not rebuild the text.</summary>
    public void SetMoney(int moneyIn)
    {
        if (money == moneyIn) return;

        money = moneyIn;
        textMoney.SetText(Sprintf("%d", moneyIn));
    }

    /// <summary>Updates the industry text; unchanged values do not rebuild the text.</summary>
    public void SetIndustry(int industryIn)
    {
        if (industry == industryIn) return;

        industry = industryIn;
        textIndustry.SetText(Sprintf("%d", industryIn));
    }

    /// <summary>Synchronizes money and industry from the current country.</summary>
    public override void OnUpdate(float deltaTimeS)
    {
        CCountry currentCountry = g_GameManager.GetCurCountry();
        if (currentCountry != null)
        {
            SetMoney(currentCountry.money);
            SetIndustry(currentCountry.industry);
        }
    }
}
