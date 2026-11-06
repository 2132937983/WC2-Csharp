namespace WC2;

// this class belongs to the commander overview screen

/// <summary>Medal decorations on the commander's chest (gui/menus/commander/GUIAdornMedal.cpp).</summary>
public sealed class GUIAdornMedal : GUIElement
{
    private static readonly string[] WAR_MEDAL_IMAGE_NAMES =
    {
        "soldiers",
        "airforce",
        "cannon",
        "panzer",
        "navy",
        "honor"
    };

    public ecImage[] imageCommanderLevel = new ecImage[5];
    public ecImage[] imageCommanderStar = new ecImage[3];
    public ecImage[] imageWarMedal = new ecImage[6];
    public int level;

    /// <summary>Creates the widget with every picture reference cleared.</summary>
    public GUIAdornMedal()
    {
        level = 0;
    }

    /// <summary>Releases the commander level, star and war medal pictures.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;

        for (int i = 0; i < 5; ++i)
        {
            imageCommanderLevel[i] = null;
        }

        for (int i = 0; i < 3; ++i)
        {
            imageCommanderStar[i] = null;
        }

        for (int i = 0; i < 6; ++i)
        {
            imageWarMedal[i] = null;
        }

        base.Dispose();
    }

    /// <summary>Draws the six war medals and the level/star decoration of the commander.</summary>
    public override void OnRender()
    {
        int commanderLevelIndex = level / 3;
        int commanderStarIndex = level % 3;
        bool isTabletLayout = Layout.Tablet;

        for (int medalIndex = 5; medalIndex >= 0; --medalIndex)
        {
            ecImage medalImage = imageWarMedal[medalIndex];
            if (medalImage == null)
            {
                continue;
            }

            int column = medalIndex % 3;
            int row = medalIndex / 3;
            float x = isTabletLayout
                    ? 230f + column * 32f
                    : 91f + column * 16f;
            float y = isTabletLayout
                    ? 461f + row * 60f
                    : 205f + row * 30f;
            medalImage.RenderEx(x, y, 0f, 0.7f, 0f);
        }

        float commanderX = isTabletLayout ? 187f : 70f;
        float commanderY = isTabletLayout ? 422f : 185f;
        imageCommanderLevel[commanderLevelIndex].RenderEx(commanderX, commanderY, 0f, 0.7f, 0f);
        imageCommanderStar[commanderStarIndex].RenderEx(commanderX, commanderY, 0f, 0.7f, 0f);
    }

    /// <summary>Refreshes the commander level and recreates the pictures of the earned war medals.</summary>
    public void Refresh()
    {
        level = g_Commander.level;

        for (int medalIndex = 0; medalIndex < 6; ++medalIndex)
        {
            imageWarMedal[medalIndex] = null;

            var medalId = (WARMEDAL_ID)medalIndex;
            int medalLevel = g_Commander.GetWarMedalLevel(medalId);
            if (medalLevel <= 0)
            {
                continue;
            }

            string imageName = Sprintf("medal_%s_%d.png", WAR_MEDAL_IMAGE_NAMES[medalIndex], medalLevel);
            ecImageAttr imageAttr = s_TextureRes.GetImage(imageName);
            imageWarMedal[medalIndex] = new ecImage(imageAttr);
        }
    }

    /// <summary>Initializes the layout and loads the commander level and star pictures.</summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;

        for (int levelIndex = 0; levelIndex < 5; ++levelIndex)
        {
            string imageName = Sprintf("commander_level_%d.png", levelIndex + 1);
            ecImageAttr imageAttr = s_TextureRes.GetImage(imageName);
            imageCommanderLevel[levelIndex] = new ecImage(imageAttr);
        }

        for (int starIndex = 0; starIndex < 3; ++starIndex)
        {
            string imageName = Sprintf("commander_star_%d.png", starIndex + 1);
            ecImageAttr imageAttr = s_TextureRes.GetImage(imageName);
            imageCommanderStar[starIndex] = new ecImage(imageAttr);
        }

        Refresh();
    }
}
