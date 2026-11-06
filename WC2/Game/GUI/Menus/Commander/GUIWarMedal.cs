namespace WC2;

/// <summary>One of the six war medal buttons of the commander screen (gui/menus/commander/GUIWarMedal.cpp).</summary>
public sealed class GUIWarMedal : GUIButton
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

    private const uint USE_BUTTON_STATE_COLOR = ENABLED;

    public WARMEDAL_ID warMedalId;
    public int level;
    public ecImage imageMedalArrow;
    public float arrowOffset;
    public float arrowSpeed;
    public bool selected;

    /// <summary>Creates the medal with the selection arrow cleared.</summary>
    public GUIWarMedal() { }

    /// <summary>Releases the selection arrow; the other resources go with the GUIButton base.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageMedalArrow = null;
        base.Dispose();
    }

    /// <summary>Initializes the medal type, level, layout, selection arrow and the button state.</summary>
    public void Init(in GUIRect rectIn, WARMEDAL_ID warMedalIdIn, int levelIn)
    {
        warMedalId = warMedalIdIn;
        SetLevel(levelIn);
        rect = rectIn;

        ecImageAttr arrowImageAttr = s_TextureRes.GetImage("medal_arrow.png");
        imageMedalArrow = new ecImage(arrowImageAttr);
        arrowOffset = 0f;

        alpha = 1f;
        iPressedDown = 0;
        bPressedDown = false;
        pressDownIndex = 0;
        greyEnable = false;
        selected = false;

        arrowSpeed = ecGraphics.Instance().widthMode == 3 ? -50f : -25f;
    }

    /// <summary>Sets the medal level and recreates the picture of the type/level.</summary>
    public void SetLevel(int levelIn)
    {
        if (imageNotPresed != null)
        {
            imageNotPresed = null;
        }

        level = levelIn;

        string imageName = Sprintf("medal_%s_%d.png", WAR_MEDAL_IMAGE_NAMES[(int)warMedalId], levelIn);

        ecImageAttr imageAttr = s_TextureRes.GetImage(imageName);
        if (imageAttr != null)
        {
            imageNotPresed = new ecImage(imageAttr);
        }
    }

    /// <summary>Draws the medal and, when selected, the bouncing arrow.</summary>
    public override void OnRender()
    {
        GetAbsRect(out GUIRect absoluteRect);

        if (imageNotPresed != null)
        {
            if ((flags & USE_BUTTON_STATE_COLOR) != 0)
            {
                uint alphaChannel = (uint)(alpha * 255f) << 24;
                uint rgb = iPressedDown == 1 ? 0x00D2D2D2u : 0x00FFFFFFu;
                imageNotPresed.SetColor(alphaChannel + rgb, -1);
            }

            imageNotPresed.Render(absoluteRect.x + absoluteRect.w * 0.5f, absoluteRect.y);
        }

        if (selected)
        {
            imageMedalArrow.Render(absoluteRect.x + absoluteRect.w * 0.5f, absoluteRect.y + arrowOffset);
        }
    }

    /// <summary>Bounces the selection arrow and reverses its speed at the limits.</summary>
    public override void OnUpdate(float deltaTimeS)
    {
        float minimumOffset = ecGraphics.Instance().widthMode == 3 ? -10f : -5f;
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

    /// <summary>Sets the color of the arrow above the medal.</summary>
    public void SetArrowColor(uint arrowColor)
    {
        imageMedalArrow.SetColor(arrowColor, -1);
    }
}
