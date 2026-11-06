namespace WC2;

public static class TextAlign
{
    public const int LEFT_ALIGNED = 0;
    public const int RIGHT_ALIGNED = 1;
    public const int MIDDLE_ALIGNED = 2;
}

/// <summary>
/// A string laid out with a bitmap font. Line breaks are '\n' glyphs; DrawText's "line"
/// argument is the alignment (0 left, 1 right, 2 middle) like the original API.
/// </summary>
public class ecText
{
    public readonly List<ecCharImage> textWithFont = new();
    public ecUniFont font;
    /// <summary>[0] extra spacing per glyph, [1] extra spacing per line.</summary>
    public readonly float[] fontSize = { 0f, 0f };
    public int alphaColor = unchecked((int)0xFFFFFFFFu);
    private string currentText = "";

    public ecText() { }

    public ecText(ecUniFont textFont) => Init(textFont);

    public void Init(ecUniFont textFont)
    {
        font = textFont;
        fontSize[0] = 0f;
        fontSize[1] = 0f;
        alphaColor = unchecked((int)0xFFFFFFFFu);
    }

    public void SetColor(uint color) => alphaColor = unchecked((int)color);
    public void SetColor(int color) => alphaColor = color;
    public void SetColor(long color) => alphaColor = unchecked((int)(uint)color);

    public void SetAlpha(float alpha)
    {
        if (alpha < 0f) alpha = 0f; else if (alpha > 1f) alpha = 1f;
        alphaColor = (alphaColor & 0x00FFFFFF) | ((int)(alpha * 255f) << 24);
    }

    public string Text => currentText;

    public void SetText(string text)
    {
        textWithFont.Clear();
        currentText = text ?? "";
        if (font == null || text == null) return;
        for (int i = 0; i < text.Length; i++)
        {
            uint codepoint;
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                codepoint = (uint)char.ConvertToUtf32(text[i], text[i + 1]);
                i++;
            }
            else codepoint = text[i];
            if (codepoint == '\r') continue;
            textWithFont.Add(font.GetCharImage(codepoint));
        }
    }

    public void DrawText(float x, float y, int line)
    {
        float drawX = x;
        // the original truncates the half width to whole pixels for centered text
        if (line == TextAlign.RIGHT_ALIGNED) drawX -= GetStringWidth(0, false);
        else if (line == TextAlign.MIDDLE_ALIGNED) drawX -= (int)(GetStringWidth(0, false) * 0.5f);

        uint color = unchecked((uint)alphaColor);
        for (int i = 0; i < textWithFont.Count; i++)
        {
            var glyph = textWithFont[i];
            if (glyph == null) continue;
            if (glyph.image != null)
            {
                glyph.image.SetColor(color, -1);
                glyph.image.Render(drawX, y);
                drawX += glyph.w + fontSize[0];
                continue;
            }
            y += (font == null ? 0f : font.fontHeight) + fontSize[1];
            if (line == TextAlign.RIGHT_ALIGNED) drawX = x - GetStringWidth(i + 1, false);
            else if (line == TextAlign.MIDDLE_ALIGNED) drawX = x - (int)(GetStringWidth(i + 1, false) * 0.5f);
            else drawX = x;
        }
    }

    /// <summary>Width of the line starting at glyph index <paramref name="line"/>; singleLine=true returns the longest line.</summary>
    public float GetStringWidth(int line, bool singleLine)
    {
        float current = 0f, longest = 0f;
        for (int i = Math.Max(0, line); i < textWithFont.Count; i++)
        {
            var glyph = textWithFont[i];
            if (glyph == null) continue;
            if (glyph.image != null) current += glyph.w + fontSize[0];
            else
            {
                if (!singleLine) return current;
                if (current > longest) longest = current;
                current = 0f;
            }
        }
        return current > longest ? current : longest;
    }

    public float GetHeight()
    {
        if (font == null) return 0f;
        float lineHeight = 0f, totalHeight = 0f;
        foreach (var glyph in textWithFont)
        {
            if (glyph == null) continue;
            if (glyph.image != null)
            {
                float glyphHeight = glyph.image.textureRect.h - glyph.image.textureRect.refy;
                if (glyphHeight > lineHeight) lineHeight = glyphHeight;
            }
            else
            {
                totalHeight += font.fontHeight + fontSize[1];
                lineHeight = 0f;
            }
        }
        return totalHeight + lineHeight;
    }

    public int GetNumLines()
    {
        int lines = 1;
        for (int i = 0; i + 1 < textWithFont.Count; i++)
        {
            var glyph = textWithFont[i];
            if (glyph != null && glyph.image == null) lines++;
        }
        return lines;
    }

    /// <summary>Breaks the text into lines no wider than maxWidth (used by label style widgets).</summary>
    public static string WrapText(ecUniFont font, string text, float maxWidth)
    {
        if (font == null || string.IsNullOrEmpty(text) || maxWidth <= 0f) return text ?? "";
        var sb = new StringBuilder();
        foreach (string paragraph in text.Replace("\r", "").Split('\n'))
        {
            float lineWidth = 0f;
            int lastBreak = -1;
            float widthAtBreak = 0f;
            int lineStart = sb.Length;
            for (int i = 0; i < paragraph.Length; i++)
            {
                char c = paragraph[i];
                var glyph = font.GetCharImage(c);
                float w = glyph?.w ?? 0f;
                if (c == ' ' || c > 0x2E80) { lastBreak = sb.Length; widthAtBreak = lineWidth; }
                if (lineWidth + w > maxWidth && sb.Length > lineStart)
                {
                    if (lastBreak > lineStart && c != ' ')
                    {
                        // break at the last opportunity
                        sb.Insert(lastBreak + (sb[lastBreak] == ' ' ? 1 : 1), '\n');
                        lineWidth -= widthAtBreak;
                    }
                    else
                    {
                        sb.Append('\n');
                        lineWidth = 0f;
                    }
                    lineStart = sb.ToString().LastIndexOf('\n') + 1;
                    lastBreak = -1;
                    if (c == ' ') continue;
                }
                sb.Append(c);
                lineWidth += w;
            }
            sb.Append('\n');
        }
        if (sb.Length > 0 && sb[sb.Length - 1] == '\n') sb.Length--;
        return sb.ToString();
    }
}

public static class ecTextExtensions
{
    /// <summary>ecTextDrawScaled: draws the text scaled around the top-left pen position.</summary>
    public static void ecTextDrawScaled(ecText text, float x, float y, int line, float scale)
    {
        if (text == null || text.font == null || scale <= 0f) return;
        if (scale == 1f) { text.DrawText(x, y, line); return; }

        float drawX = x;
        float firstLineWidth = text.GetStringWidth(0, false) * scale;
        if (line == TextAlign.RIGHT_ALIGNED) drawX -= firstLineWidth;
        else if (line == TextAlign.MIDDLE_ALIGNED) drawX -= firstLineWidth * 0.5f;

        uint color = unchecked((uint)text.alphaColor);
        for (int index = 0; index < text.textWithFont.Count; index++)
        {
            var glyph = text.textWithFont[index];
            if (glyph == null) continue;
            if (glyph.image != null)
            {
                glyph.image.SetColor(color, -1);
                glyph.image.RenderEx(drawX, y, 0f, scale, scale);
                drawX += (glyph.w + text.fontSize[0]) * scale;
                continue;
            }
            y += (text.font.fontHeight + text.fontSize[1]) * scale;
            float nextLineWidth = text.GetStringWidth(index + 1, false) * scale;
            if (line == TextAlign.RIGHT_ALIGNED) drawX = x - nextLineWidth;
            else if (line == TextAlign.MIDDLE_ALIGNED) drawX = x - nextLineWidth * 0.5f;
            else drawX = x;
        }
    }
}

/// <summary>
/// System font label of the original engine. Desktop has no JNI text rasterizer, so the label
/// is drawn with the bitmap text font, word wrapped to the requested width.
/// </summary>
public sealed class ecLabelText
{
    public string font = "";
    public int fontW;
    public int fontH;
    public int alphaColor = unchecked((int)0xFFFFFFFFu);
    public int spaceW;
    public int spaceH;
    public ecTexture textureText;
    public ecImage imageText;

    private readonly ecText text = new();
    private string rawText = "";

    public void Init(string fontName, int fontWIn, int fontHIn, int spaceWIn, int spaceHIn)
    {
        font = fontName ?? "";
        fontW = fontWIn; fontH = fontHIn; spaceW = spaceWIn; spaceH = spaceHIn;
        text.Init(FontGlobals.DefaultTextFont());
        text.SetColor(unchecked((uint)alphaColor));
    }

    public void SetText(string value)
    {
        rawText = value ?? "";
        if (text.font == null) text.Init(FontGlobals.DefaultTextFont());
        text.SetText(ecText.WrapText(text.font, rawText, spaceW));
    }

    public void SetColor(uint color) { alphaColor = unchecked((int)color); text.SetColor(color); }
    public void SetColor(int color) => SetColor(unchecked((uint)color));
    public void SetColor(long color) => SetColor(unchecked((uint)color));
    public void SetAlpha(float alpha) { text.SetAlpha(alpha); alphaColor = text.alphaColor; }

    public void DrawText(float x, float y)
    {
        if (text.font == null) return;
        float scale = text.font.fontHeight > 0 && fontH > 0 ? Math.Min(1f, (float)fontH / text.font.fontHeight) : 1f;
        ecTextExtensions.ecTextDrawScaled(text, x, y, TextAlign.LEFT_ALIGNED, scale);
    }
}
