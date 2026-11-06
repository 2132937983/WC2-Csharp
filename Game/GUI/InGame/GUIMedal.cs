namespace WC2;

/// <summary>
/// Medal counter (GUIMedal.cpp). The original's "buy medals" button, which opened the in-app store,
/// is removed: this build sells nothing.
/// </summary>
public sealed class GUIMedal : GUIElement
{
    public ecImage imageMedal;
    public readonly ecText medalNum = new();
    public int medal = -1;

    /// <summary>The count starts unsynchronized.</summary>
    public GUIMedal() { }

    /// <summary>Releases the medal image.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageMedal = null;
        base.Dispose();
    }

    /// <summary>Initializes the medal image and the count text.</summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;
        imageMedal = new ecImage(s_TextureRes.GetImage("player_medal.png"));
        medalNum.Init(g_Num1);
        SetMedal(0);
    }

    /// <summary>Synchronizes the medal count from the global commander.</summary>
    public override void OnUpdate(float deltaTimeS)
    {
        SetMedal(g_Commander.medal);
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
        float numberOffsetX = Layout.Tablet ? 40.0f : 23.0f;
        medalNum.DrawText(absoluteRect.x + numberOffsetX, absoluteRect.y + 1.0f, 0);
    }
}
