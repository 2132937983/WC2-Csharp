namespace WC2;

/// <summary>
/// The layout facts the code branches on, in one place. They come from the screen layout the game
/// selected (the layout profile, <c>data/json/layout/profiles.json</c>, applied to <see cref="ecGraphics"/>
/// when the game initialises): the original code tested <c>widthMode == 3</c> and the logical width in
/// some 140 places; those tests now read these properties.
/// </summary>
public static class Layout
{
    /// <summary>Id of the selected layout profile (phone-640, phone-568, phone-534, tablet, phone-480).</summary>
    public static string ProfileId = "";

    /// <summary>The 1024 x 768 tablet layout (the original's width mode 3).</summary>
    public static bool Tablet => ecGraphics.Instance().widthMode == 3;

    /// <summary>Logical screen width (480, 534, 568, 640 or 1024).</summary>
    public static int Width => ecGraphics.Instance().screenW;

    /// <summary>Logical screen height (320 or 768).</summary>
    public static int Height => ecGraphics.Instance().screenH;

    /// <summary>The classic 480 x 320 layout (or narrower).</summary>
    public static bool Width480 => ecGraphics.Instance().screenW <= 480;

    /// <summary>A layout at most 568 logical pixels wide (480, 534, 568).</summary>
    public static bool WidthUpTo568 => ecGraphics.Instance().screenW <= 568;
}
