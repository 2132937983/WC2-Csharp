namespace WC2;

/// <summary>One glyph: sprite, advance width and code point.</summary>
public sealed class ecCharImage
{
    public ecImage image;
    public float w;
    public uint encode;
}

/// <summary>
/// Bitmap font loaded from the game's ".fnt" format (header: glyph count, font height;
/// then 12 byte glyph records) with a PNG atlas of the same base name.
/// </summary>
public sealed class ecUniFont
{
    public readonly Dictionary<uint, ecCharImage> encoding = new();
    public ecCharImage[] charImage;
    public ecImage[] fontImage;
    public ecTexture fontTexture;
    public int fontHeight;
    public string name = "";

    public bool IsLoaded => fontTexture != null && charImage != null;

    public void Release()
    {
        fontImage = null;
        encoding.Clear();
        charImage = null;
        if (fontTexture != null)
        {
            ecGraphics.Instance().FreeTexture(fontTexture);
            fontTexture = null;
        }
        fontHeight = 0;
    }

    /// <summary>
    /// Loads "name.fnt" + "name.png". When <paramref name="hd"/> is true the atlas is 2x art
    /// and every metric is halved to logical units.
    /// </summary>
    public void Init(string fileName, bool hd)
    {
        if (string.IsNullOrEmpty(fileName)) return;
        Release();
        name = fileName;
        if (fileName.Length < 4) return;
        string textureName = WithExt(fileName, ".png");
        fontTexture = ecGraphics.Instance().LoadTexture(textureName);
        if (fontTexture == null)
        {
            Log("Font texture missing: " + textureName);
            return;
        }
        if (hd && fontTexture.xC == 1.0f) fontTexture.SetHd(true);

        byte[] data = AssetSystem.ReadAllBytes(fileName);
        if (data == null) { Log("Font file missing: " + fileName); Release(); return; }
        if (data.Length < 8) { Release(); return; }
        uint glyphCount = BitConverter.ToUInt32(data, 0);
        uint headerHeight = BitConverter.ToUInt32(data, 4);
        if (glyphCount == 0 || glyphCount > 0x100000 || 8 + glyphCount * 12 > data.Length) { Release(); return; }

        float scale = hd ? 0.5f : 1.0f;
        fontHeight = (int)(headerHeight * scale);
        charImage = new ecCharImage[glyphCount + 1];
        fontImage = new ecImage[glyphCount];
        int offset = 8;
        for (uint i = 0; i < glyphCount; i++, offset += 12)
        {
            ushort codepoint = BitConverter.ToUInt16(data, offset);
            ushort atlasX = BitConverter.ToUInt16(data, offset + 2);
            ushort atlasY = BitConverter.ToUInt16(data, offset + 4);
            byte width = data[offset + 6];
            byte height = data[offset + 7];
            sbyte offsetX = (sbyte)data[offset + 8];
            sbyte offsetY = (sbyte)data[offset + 9];
            byte advance = data[offset + 10];

            var img = new ecImage(fontTexture, atlasX * scale, atlasY * scale, width * scale, height * scale);
            img.textureRect.refx = offsetX * scale;
            img.textureRect.refy = offsetY * scale;
            fontImage[i] = img;
            var glyph = new ecCharImage { encode = codepoint, w = advance * scale, image = img };
            charImage[i] = glyph;
            encoding[codepoint] = glyph;
        }
        var newline = new ecCharImage { encode = 10, w = 0f, image = null };
        charImage[glyphCount] = newline;
        encoding[10] = newline;
    }

    public ecCharImage GetCharImage(uint codepoint) => encoding.TryGetValue(codepoint, out var g) ? g : null;
    public ecCharImage GetCharImage(char c) => GetCharImage((uint)c);
}

/// <summary>The global fonts (ecUniFont.h externs). Initialized by ecGameInit.</summary>
public static class FontGlobals
{
    public static readonly ecUniFont g_Num8 = new();
    public static readonly ecUniFont g_Num7 = new();
    public static readonly ecUniFont g_Num5 = new();
    public static readonly ecUniFont g_Num4b = new();
    public static readonly ecUniFont g_Num4 = new();
    public static readonly ecUniFont g_Num3 = new();
    public static readonly ecUniFont g_Num1 = new();
    public static readonly ecUniFont g_Font7 = new();
    public static readonly ecUniFont g_Font6 = new();
    public static readonly ecUniFont g_Font3 = new();
    public static readonly ecUniFont g_Font2 = new();
    public static readonly ecUniFont g_Font1 = new();
    public static readonly ecUniFont g_FontKt = new();
    public static readonly ecUniFont g_FontHt = new();

    /// <summary>Best general purpose text font that is actually loaded.</summary>
    public static ecUniFont DefaultTextFont()
    {
        if (g_FontHt.IsLoaded) return g_FontHt;
        if (g_Font2.IsLoaded) return g_Font2;
        if (g_Font3.IsLoaded) return g_Font3;
        if (g_FontKt.IsLoaded) return g_FontKt;
        if (g_Font6.IsLoaded) return g_Font6;
        return g_Font1;
    }
}
