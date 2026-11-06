namespace WC2;

/// <summary>
/// Push button made of atlas images: optional background, normal/pressed picture, text image,
/// glow and a bitmap font caption. Behaviour follows the original binary: a touch that begins
/// inside the button is consumed, a move outside cancels the press, and the release posts a
/// GUI BUTTON event (bubbling to the state) without consuming the touch end.
/// </summary>
public class GUIButton : GUIElement
{
    public ecUniFont textFont;
    public ecText text;
    public int textHeight;
    public ecImage imageNotPresed;
    public ecImage imagePressed;
    public ecImage imageGlow;
    public ecImage imageBackground;
    public ecImage imageText;
    /// <summary>1 while the finger is down inside the button (selects the pressed picture).</summary>
    public int iPressedDown;
    public bool bPressedDown;
    public int pressDownIndex;
    public float alpha = 1f;
    /// <summary>Brightness of the pictures (1 = unchanged).</summary>
    public float greyValue = 1f;
    /// <summary>When true the pressed picture is darkened instead of swapped.</summary>
    public bool greyEnable;
    /// <summary>Play the click sound on release.</summary>
    public bool soundBtn = true;
    public readonly float[] textOffset = { 0f, 0f };
    public int textAlign = TextAlign.MIDDLE_ALIGNED;

    /// <summary>Hook used to play the button click (set by the game layer).</summary>
    public static Action PlayClickSound;

    public GUIButton() { }

    public void Init(string notPressedImageName, string pressedImageName, in GUIRect rectIn, ecUniFont font)
    {
        imageNotPresed = MakeAtlasImage(notPressedImageName);
        imagePressed = MakeAtlasImage(pressedImageName);
        rect = rectIn;
        textFont = font;
        if (font != null)
        {
            text = new ecText();
            text.Init(font);
            textHeight = font.fontHeight;
        }
        iPressedDown = 0;
        bPressedDown = false;
        pressDownIndex = 0;
        greyEnable = false;
        alpha = 1f;
        greyValue = 1f;
    }

    protected static ecImage MakeAtlasImage(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var attr = s_TextureRes.GetImage(name);
        if (attr == null) { Log("GUIButton: atlas image not found: " + name); return null; }
        return new ecImage(attr);
    }

    public void SetGlow(string imageName) => imageGlow = MakeAtlasImage(imageName);
    public void SetBackground(string imageName) => imageBackground = MakeAtlasImage(imageName);
    public void SetTextImage(string imageName) => imageText = MakeAtlasImage(imageName);

    public void SetText(string value)
    {
        if (text == null)
        {
            textFont ??= FontGlobals.DefaultTextFont();
            if (textFont == null) return;
            text = new ecText();
            text.Init(textFont);
        }
        text.SetText(value);
        textHeight = (int)text.GetHeight();
    }

    public void SetTextColor(uint color) => text?.SetColor(color);
    public void SetTextColor(int color) => text?.SetColor(color);
    public void SetTextColor(long color) => text?.SetColor(color);
    public void SetTextOffset(float x, float y) { textOffset[0] = x; textOffset[1] = y; }
    public void SetTextAlign(int align) => textAlign = align;
    public void SetAlpha(float a) => alpha = a;

    /// <summary>0xAABBGGRR tint built from the alpha and a grey level (0..255).</summary>
    protected uint Tint(int grey)
    {
        uint g = (uint)Math.Clamp(grey, 0, 255);
        uint a = (uint)Math.Clamp((int)(alpha * 255f), 0, 255) << 24;
        return a | (g << 16) | (g << 8) | g;
    }

    public override void OnRender()
    {
        GetAbsRect(out var abs);
        int grey = (int)(greyValue * 255f);
        int greyPressed = (int)(greyValue * 210f);

        imageBackground?.Render(abs.x, abs.y);

        if (imageGlow != null && iPressedDown == 1)
        {
            imageGlow.SetColor(Tint(grey), -1);
            imageGlow.Render(abs.x, abs.y);
        }

        var picture = iPressedDown == 1 ? imagePressed : imageNotPresed;
        if (picture != null)
        {
            if ((flags & ENABLED) != 0)
                picture.SetColor(Tint(iPressedDown == 1 && greyEnable ? greyPressed : grey), -1);
            else
                picture.SetColor(Tint((int)(greyValue * 110f)), -1);
            picture.Render(abs.x, abs.y);
        }

        if (text != null)
        {
            text.DrawText(abs.x + abs.w * 0.5f + textOffset[0],
                          abs.y + (abs.h - textHeight) * 0.5f + textOffset[1],
                          textAlign);
        }

        if (imageText != null)
        {
            imageText.SetColor(Tint(greyEnable && iPressedDown == 1 ? greyPressed : grey), -1);
            imageText.Render(abs.x, abs.y);
        }
    }

    public override bool OnEvent(Event evt)
    {
        if (evt.type != Event.TOUCH) return base.OnEvent(evt);
        if ((flags & (ACTIVE | SHOWN | ENABLED)) != (ACTIVE | SHOWN | ENABLED)) return false;

        float x = evt.info.touch.pos[0], y = evt.info.touch.pos[1];
        int index = evt.info.touch.index;
        switch (evt.info.touch.type)
        {
            case Event.Info.TouchInfo.BEGIN:
                if (CheckInRect(x, y))
                {
                    bPressedDown = true;
                    pressDownIndex = index;
                    iPressedDown = 1;
                    return true;
                }
                break;
            case Event.Info.TouchInfo.MOVE:
                if (bPressedDown && pressDownIndex == index && !CheckInRect(x, y))
                {
                    bPressedDown = false;
                    iPressedDown = 0;
                }
                break;
            case Event.Info.TouchInfo.END:
                if (bPressedDown && pressDownIndex == index)
                {
                    bPressedDown = false;
                    iPressedDown = 0;
                    if (soundBtn) PlayClickSound?.Invoke();
                    OnClicked();
                }
                break;
        }
        return base.OnEvent(evt);
    }

    /// <summary>Posts the BUTTON event; it bubbles through the parents to the current state.</summary>
    protected virtual void OnClicked()
    {
        if (!string.IsNullOrEmpty(LogFile)) Log("Button clicked: " + GetType().Name + " in " + (parent?.GetType().Name ?? "?") + " rect=" + rect);
        OnEvent(Event.Button(this));
    }

    /// <summary>Resets the pressed state (used when a panel is hidden while a finger is down).</summary>
    public void ResetPress()
    {
        bPressedDown = false;
        iPressedDown = 0;
        pressDownIndex = 0;
    }
}

/// <summary>Button with an extra icon pair drawn at an offset (used for ok/arrow buttons).</summary>
public class GUIButtonEx : GUIButton
{
    public ecImage imageNotPressed;
    public new ecImage imagePressed;
    public readonly float[] imagePos = { 0f, 0f };

    public GUIButtonEx() { }

    public void SetImageText(string notPressedImageName, string pressImageName)
    {
        imageNotPressed = MakeAtlasImage(notPressedImageName);
        imagePressed = MakeAtlasImage(pressImageName);
    }

    public override void OnRender()
    {
        GetAbsRect(out var abs);
        base.OnRender();
        var icon = iPressedDown == 1 ? imagePressed : imageNotPressed;
        if (icon == null) return;
        uint a = (uint)Math.Clamp((int)(alpha * 255f), 0, 255) << 24;
        if ((flags & ENABLED) != 0)
            icon.SetColor(a | (iPressedDown == 1 ? 0x00D2D2D2u : 0x00FFFFFFu), -1);
        else
            icon.SetColor(a | 0x006E6E6Eu, -1);
        icon.Render(abs.x + imagePos[0], abs.y + imagePos[1]);
    }
}

/// <summary>Mutually exclusive button: renders the pressed picture while selected.</summary>
public class GUIRadioButton : GUIButton
{
    public bool selected;

    public GUIRadioButton() { }

    public override void OnRender()
    {
        GetAbsRect(out var abs);
        imageBackground?.Render(abs.x, abs.y);

        uint a = (uint)Math.Clamp((int)(alpha * 255f), 0, 255) << 24;
        if (selected)
        {
            if (imagePressed != null)
            {
                imagePressed.SetColor(a | 0x00FFFFFFu, -1);
                imagePressed.Render(abs.x, abs.y);
            }
        }
        else
        {
            var picture = iPressedDown == 1 ? imagePressed : imageNotPresed;
            if (picture != null)
            {
                if ((flags & ENABLED) != 0)
                    picture.SetColor(a | (iPressedDown == 1 ? 0x00D2D2D2u : 0x00FFFFFFu), -1);
                else
                    picture.SetColor(a | 0x00787878u, -1);
                picture.Render(abs.x, abs.y);
            }
        }

        if (text != null)
        {
            text.DrawText(abs.x + abs.w * 0.5f + textOffset[0],
                          abs.y + (abs.h - textHeight) * 0.5f + textOffset[1],
                          TextAlign.MIDDLE_ALIGNED);
        }
    }
}
