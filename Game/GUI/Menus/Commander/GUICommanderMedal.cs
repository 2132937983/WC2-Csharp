namespace WC2;

/// <summary>Commander rank medal button of the commander screen (gui/menus/commander/GUICommanderMedal.cpp).</summary>
public sealed class GUICommanderMedal : GUIButton
{
    public ecImage[] imageCommanderLevel = new ecImage[5];
    public ecImage[] imageCommanderStar = new ecImage[3];
    public int level;
    public ecImage imageMedalArrow;
    public float arrowOffset;
    public float arrowSpeed;
    public bool selected;

    /// <summary>Creates the widget with the level and star pictures cleared.</summary>
    public GUICommanderMedal() { }

    /// <summary>Releases the level and star pictures.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;

        for (int i = 0; i < imageCommanderLevel.Length; i++)
        {
            imageCommanderLevel[i] = null;
        }

        for (int i = 0; i < imageCommanderStar.Length; i++)
        {
            imageCommanderStar[i] = null;
        }

        base.Dispose();
    }

    /// <summary>Bounces the selection arrow and reverses its speed at the limits.</summary>
    public override void OnUpdate(float deltaTimeS)
    {
        float minimumOffset = Layout.Tablet ? -10f : -5f;
        float previousSpeed = arrowSpeed;
        float nextOffset = arrowOffset + previousSpeed * deltaTimeS;

        if (nextOffset > minimumOffset && nextOffset < 0f)
        {
            arrowOffset = nextOffset;
            return;
        }

        arrowOffset = nextOffset >= 0f ? 0f : minimumOffset;
        arrowSpeed = -previousSpeed;
    }

    /// <summary>Initializes the medal: level pictures, star pictures and the selection arrow.</summary>
    public void Init(in GUIRect rectIn, int levelIn)
    {
        rect = rectIn;
        level = levelIn;

        // the big medal shape of the commander
        for (int levelIndex = 0; levelIndex < 5; ++levelIndex)
        {
            string imageName = Sprintf("commander_level_%d.png", levelIndex + 1);
            ecImageAttr imageAttr = s_TextureRes.GetImage(imageName);
            imageCommanderLevel[levelIndex] = new ecImage(imageAttr);
        }

        // the number of stars of the commander
        for (int starIndex = 0; starIndex < 3; ++starIndex)
        {
            string imageName = Sprintf("commander_star_%d.png", starIndex + 1);
            ecImageAttr imageAttr = s_TextureRes.GetImage(imageName);
            imageCommanderStar[starIndex] = new ecImage(imageAttr);
        }

        ecImageAttr arrowImageAttr = s_TextureRes.GetImage("medal_arrow.png");
        imageMedalArrow = new ecImage(arrowImageAttr);
        arrowOffset = 0f;
        selected = false;
        arrowSpeed = Layout.Tablet ? -50f : -25f;

        base.Init(null, null, rectIn, null);
    }

    /// <summary>Sets the arrow color (selected or selectable).</summary>
    public void SetArrowColor(uint color)
    {
        imageMedalArrow.SetColor(color, -1);
    }

    /// <summary>Renders the commander medal: shape by level and the stars.</summary>
    public override void OnRender()
    {
        GetAbsRect(out GUIRect curRect);
        float newX = curRect.x + curRect.w * 0.5f;
        imageCommanderLevel[level / 3].Render(newX, curRect.y);
        imageCommanderStar[level % 3].Render(newX, curRect.y);
        if (selected) imageMedalArrow.Render(newX, curRect.y + arrowOffset);
    }
}
