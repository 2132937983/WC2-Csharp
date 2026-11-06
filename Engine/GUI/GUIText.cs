namespace WC2;

/// <summary>Static text element.</summary>
public class GUIText : GUIElement
{
    public ecUniFont font;
    public ecText text;
    public int fontHeight;
    public int align = TextAlign.LEFT_ALIGNED;

    public void Init(in GUIRect rectIn, ecUniFont fontIn)
    {
        rect = rectIn;
        font = fontIn ?? FontGlobals.DefaultTextFont();
        text = new ecText();
        text.Init(font);
        fontHeight = font?.fontHeight ?? 0;
    }

    public void SetText(string value)
    {
        if (text == null) return;
        text.SetText(value);
        fontHeight = font?.fontHeight ?? fontHeight;
    }

    public void SetTextColor(uint color) => text?.SetColor(color);
    public void SetAlign(int textAlign) => align = textAlign;

    /// <summary>Like the original, the text is anchored at the element's x: right aligned text ends there, centered text is centered on it.</summary>
    public override void OnRender()
    {
        if (text == null) return;
        GetAbsRect(out var abs);
        text.DrawText(abs.x, abs.y, align);
    }
}
