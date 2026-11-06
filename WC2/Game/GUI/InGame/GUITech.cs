namespace WC2;

/// <summary>Technology level display (GUITech.cpp).</summary>
public sealed class GUITech : GUIElement
{
    public ecImage imageTechnologyBoard;
    public ecImage[] imageTechnology = new ecImage[5];
    public int technology = 1;
    public bool showBoard;

    /// <summary>Starts at tech level one; the board visibility is set by Init / the owner.</summary>
    public GUITech() { }

    /// <summary>Releases the board and the five level images.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageTechnologyBoard = null;
        for (int i = 0; i < imageTechnology.Length; i++) imageTechnology[i] = null;
        base.Dispose();
    }

    /// <summary>Initializes the layout, the board and the five tech level images.</summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;

        imageTechnologyBoard = new ecImage(s_TextureRes.GetImage("technology_board.png"));

        string[] technologyImages =
        {
            "technology_1.png",
            "technology_2.png",
            "technology_3.png",
            "technology_4.png",
            "technology_5.png",
        };
        for (int index = 0; index < 5; ++index)
            imageTechnology[index] = new ecImage(s_TextureRes.GetImage(technologyImages[index]));

        showBoard = true;
    }

    /// <summary>Draws the board and the current level icon.</summary>
    public override void OnRender()
    {
        GetAbsRect(out var absoluteRect);

        float centerX = absoluteRect.x + absoluteRect.w * 0.5f;
        if (showBoard)
            imageTechnologyBoard.Render(centerX, absoluteRect.y);
        int level = Math.Clamp(technology, 1, 5);
        imageTechnology[level - 1].Render(centerX, absoluteRect.y);
    }

    /// <summary>Synchronizes the tech level from the current country.</summary>
    public override void OnUpdate(float deltaTimeS)
    {
        CCountry currentCountry = g_GameManager.GetCurCountry();
        if (currentCountry != null)
            technology = currentCountry.tech;
    }
}
