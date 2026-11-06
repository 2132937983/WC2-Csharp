namespace WC2;

/// <summary>Upgrade button of the commander screen: shows the medal cost (gui/menus/commander/GUIUpgradeButton.cpp).</summary>
public sealed class GUIUpgradeButton : GUIButton
{
    private const uint USE_BUTTON_STATE_COLOR = ENABLED;
    private const uint COLOR_WHITE = 0x00FFFFFF;
    private const uint COLOR_PRESSED = 0x00D2D2D2;
    private const uint COLOR_DISABLED = 0x006E6E6E;

    public ecImage imageMedal;
    public ecText textNeedMedal = new ecText();

    /// <summary>Creates the button with the medal picture cleared.</summary>
    public GUIUpgradeButton() { }

    /// <summary>Releases the medal picture; the text and the button resources go with the base.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageMedal = null;
        base.Dispose();
    }

    /// <summary>Draws the button background, the medal icon and the required medal count.</summary>
    public override void OnRender()
    {
        GetAbsRect(out GUIRect absoluteRect);

        ecImage buttonImage = iPressedDown == 1 ? imagePressed : imageNotPresed;
        if (buttonImage != null)
        {
            uint alphaChannel = (uint)(alpha * 255f) << 24;
            uint color = COLOR_DISABLED;
            if ((flags & USE_BUTTON_STATE_COLOR) != 0)
            {
                color = iPressedDown == 1 ? COLOR_PRESSED : COLOR_WHITE;
            }
            buttonImage.SetColor(alphaChannel | color, -1);
            buttonImage.Render(absoluteRect.x, absoluteRect.y);
        }

        bool isTabletLayout = ecGraphics.Instance().widthMode == 3;
        float textWidth = textNeedMedal.GetStringWidth(0, true);
        float contentWidth = textWidth + (isTabletLayout ? 34f : 17f);
        float medalX = MathF.Floor(absoluteRect.x + (absoluteRect.w - contentWidth) * 0.5f);

        if (isTabletLayout)
        {
            imageMedal.Render(medalX, absoluteRect.y + 10f);
            textNeedMedal.DrawText(medalX + 28f, absoluteRect.y + 8f, 0);
        }
        else
        {
            imageMedal.Render(medalX, absoluteRect.y + 6f);
            textNeedMedal.DrawText(medalX + 14f, absoluteRect.y + 5f, 0);
        }
    }

    /// <summary>Sets the medal cost; the first call creates the medal picture and the number text.</summary>
    public void SetNeedMedal(int needMedal)
    {
        if (imageMedal == null)
        {
            ecImageAttr medalImageAttr = s_TextureRes.GetImage("medal.png");
            imageMedal = new ecImage(medalImageAttr);
            textNeedMedal.Init(g_Num1);
        }

        string medalText = Sprintf("%d", needMedal);
        textNeedMedal.SetText(medalText);
    }
}
