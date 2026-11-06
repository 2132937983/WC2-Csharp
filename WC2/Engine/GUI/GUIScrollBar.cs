namespace WC2;

/// <summary>
/// Slider, ported from the original binary. The knob image is drawn at (x + xScroll, y + yScroll);
/// along the scroll axis the knob travels from xBase/2 to length - xBase/2 and GetScrollPos()
/// maps that to 0..scrollPosMultiplier. The other axis is centered. A touch that begins inside
/// the bar is consumed; moves update the knob and post SCROLL_BAR events.
/// </summary>
public class GUIScrollBar : GUIElement
{
    public ecImage imageSliderNotPressed;
    public ecImage imageSliderPressed;
    public int touched;
    public float xScroll;
    public float yScroll;
    public float xBase;
    public float yBase;
    public int touchIndex;
    public int scrollPosMultiplier = 100;

    /// <summary>The original keeps the orientation in bit 0 of the flags.</summary>
    public bool horizontal
    {
        get => (flags & 1u) != 0;
        set { if (value) flags |= 1u; else flags &= ~1u; }
    }

    public void Init(in GUIRect rectIn, string sliderNotPressedImageName, string sliderPressedImageName, int xBaseIn, int yBaseIn, int initPos, int scrollPosMultiplierIn, int horizontalIn)
    {
        var attr = sliderNotPressedImageName != null ? s_TextureRes.GetImage(sliderNotPressedImageName) : null;
        imageSliderNotPressed = attr != null ? new ecImage(attr) : null;
        attr = sliderPressedImageName != null ? s_TextureRes.GetImage(sliderPressedImageName) : null;
        imageSliderPressed = attr != null ? new ecImage(attr) : null;
        touched = 0;
        rect = rectIn;
        scrollPosMultiplier = scrollPosMultiplierIn;
        flags = (flags & 0xFFFF0000u) | (horizontalIn != 0 ? 1u : 0u);
        xBase = xBaseIn;
        yBase = yBaseIn;
        SetScrollPos(initPos);
    }

    public int GetScrollPos()
    {
        float pos, length;
        if (horizontal) { pos = xScroll; length = rect.w - xBase; }
        else { pos = yScroll; length = rect.h - yBase; }
        if (length == 0f) return 0;
        return (int)(scrollPosMultiplier * pos / length);
    }

    public void SetScrollPos(int posMultiplied)
    {
        float mult = scrollPosMultiplier != 0 ? scrollPosMultiplier : 1f;
        if (horizontal)
        {
            xScroll = (rect.w - xBase) * posMultiplied / mult;
            yScroll = (rect.h - yBase) * 0.5f;
        }
        else
        {
            yScroll = (rect.h - yBase) * posMultiplied / mult;
            xScroll = (rect.w - xBase) * 0.5f;
        }
    }

    private void SetFromTouch(float x, float y, in GUIRect abs)
    {
        if (horizontal)
        {
            float half = xBase * 0.5f;
            float v = x - abs.x;
            if (v < half) v = half;
            else if (v > abs.w - half) v = abs.w - half;
            xScroll = v - half;
        }
        else
        {
            float half = yBase * 0.5f;
            float v = y - abs.y;
            if (v < half) v = half;
            else if (v > abs.h - half) v = abs.h - half;
            yScroll = v - half;
        }
    }

    public override void OnRender()
    {
        var knob = touched != 0 ? imageSliderPressed : imageSliderNotPressed;
        if (knob == null) return;
        GetAbsRect(out var abs);
        knob.Render(abs.x + xScroll, abs.y + yScroll);
    }

    public override bool OnEvent(Event evt)
    {
        if (evt.type != Event.TOUCH) return base.OnEvent(evt);
        if ((flags & (ACTIVE | SHOWN | ENABLED)) != (ACTIVE | SHOWN | ENABLED)) return false;
        float x = evt.info.touch.pos[0], y = evt.info.touch.pos[1];
        switch (evt.info.touch.type)
        {
            case Event.Info.TouchInfo.BEGIN:
            {
                GetAbsRect(out var abs);
                if (!CheckInRect(x, y, abs)) return base.OnEvent(evt);
                touchIndex = evt.info.touch.index;
                touched = 1;
                SetFromTouch(x, y, abs);
                PostScroll();
                return true;
            }
            case Event.Info.TouchInfo.MOVE:
                if (touched == 1 && evt.info.touch.index == touchIndex)
                {
                    GetAbsRect(out var abs);
                    SetFromTouch(x, y, abs);
                    PostScroll();
                }
                return base.OnEvent(evt);
            case Event.Info.TouchInfo.END:
                if (touched == 1) touched = 0;
                return base.OnEvent(evt);
        }
        return false;
    }

    private void PostScroll() => OnEvent(Event.Gui(Event.Info.GUIInfo.SCROLL_BAR, this, GetScrollPos()));
}
