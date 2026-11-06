namespace WC2;

/// <summary>Medal counter with the "buy medals" button (GUIMedal.cpp).</summary>
public sealed class GUIMedal : GUIElement
{
    public ecImage imageMedal;
    public readonly ecText medalNum = new();
    public int medal = -1;
    public GUIButton buttonAdd;

    /// <summary>The count starts unsynchronized; the add button is created by Init.</summary>
    public GUIMedal() { }

    /// <summary>Releases the medal image.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageMedal = null;
        base.Dispose();
    }

    /// <summary>Initializes the medal image, the count text and the buy button.</summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;
        imageMedal = new ecImage(s_TextureRes.GetImage("player_medal.png"));

        GUIRect buttonRect = ecGraphics.Instance().widthMode == 3
            ? new GUIRect(142.0f, -4.0f, 64.0f, 54.0f)
            : new GUIRect(71.0f, -2.0f, 32.0f, 27.0f);

        buttonAdd = GUIManager.Instance().AddButton("buymedalbutton_add.png", "buymedalbutton_add_press.png", buttonRect, this, null);
        buttonAdd.SetBackground("buymedalbutton.png");

        medalNum.Init(g_Num1);
        SetMedal(0);
    }

    /// <summary>Synchronizes the medal count from the global commander.</summary>
    public override void OnUpdate(float deltaTimeS)
    {
        SetMedal(g_Commander.medal);
    }

    /// <summary>The buy button sends the custom event 13 to the upper layers.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.GUI &&
            evt.info.gui.type == Event.Info.GUIInfo.BUTTON &&
            ReferenceEquals(evt.info.gui.ptr, buttonAdd))
        {
            OnEvent(Event.Gui(Event.Info.GUIInfo.OTHER, this, 13));
        }

        return base.OnEvent(evt);
    }

    /// <summary>Updates the count text; unchanged values do not rebuild the text.</summary>
    public void SetMedal(int medalIn)
    {
        if (medal == medalIn) return;

        medal = medalIn;
        medalNum.SetText(Sprintf("%d", medalIn));
    }

    /// <summary>Draws the medal icon and the count.</summary>
    public override void OnRender()
    {
        GetAbsRect(out var absoluteRect);

        imageMedal.Render(absoluteRect.x, absoluteRect.y);
        float numberOffsetX = ecGraphics.Instance().widthMode == 3 ? 40.0f : 23.0f;
        medalNum.DrawText(absoluteRect.x + numberOffsetX, absoluteRect.y + 1.0f, 0);
    }
}
