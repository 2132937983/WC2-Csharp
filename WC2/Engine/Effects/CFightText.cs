namespace WC2;

/// <summary>A floating damage number that rises and fades out (port of the original: 40 px/s upwards, alpha fades 0.5/s and the text is dropped at 0.2).</summary>
public sealed class CFightText
{
    public float x;
    public float y;
    public float alpha = 1f;
    public readonly ecText text = new();

    public bool Init(float px, float py, ecUniFont font)
    {
        x = px; y = py;
        text.Init(font);
        alpha = 1f;
        return true;
    }

    public void SetText(string value) => text.SetText(value);
    public void SetColor(uint color) => text.SetColor(color);

    /// <summary>Returns true while the text is still visible.</summary>
    public bool Update(float deltaTimeS)
    {
        alpha -= 0.5f * deltaTimeS;
        y -= deltaTimeS * 40f;
        if (alpha <= 0.2f)
        {
            alpha = 0.2f;
            text.SetAlpha(0.2f);
            return false;
        }
        text.SetAlpha(alpha);
        return true;
    }

    public void Render() => text.DrawText(x, y, TextAlign.MIDDLE_ALIGNED);
}

/// <summary>Manages the floating combat numbers shown on the map (own "num2" font, at most 100 texts).</summary>
public sealed class CFightTextMgr
{
    public readonly List<CFightText> existingText = new();
    public readonly ecUniFont font = new();
    public const int MaxTexts = 100;

    public void Init()
    {
        if (g_contenscalefactor == 2.0f) font.Init("num2_hd.fnt", true);
        else font.Init("num2.fnt", false);
        existingText.Clear();
    }

    public void Release()
    {
        font.Release();
        existingText.Clear();
    }

    private ecUniFont ActiveFont()
    {
        if (font.IsLoaded) return font;
        if (FontGlobals.g_Num3.IsLoaded) return FontGlobals.g_Num3;
        if (FontGlobals.g_Num1.IsLoaded) return FontGlobals.g_Num1;
        return FontGlobals.DefaultTextFont();
    }

    public void AddText(float x, float y, string value, uint color)
    {
        if (existingText.Count >= MaxTexts) return;
        var t = new CFightText();
        t.Init(x, y, ActiveFont());
        t.SetText(value);
        t.SetColor(color);
        existingText.Add(t);
    }

    public void AddText(float x, float y, string value, int color) => AddText(x, y, value, unchecked((uint)color));
    public void AddText(float x, float y, string value, long color) => AddText(x, y, value, unchecked((uint)color));

    public void Update(float deltaTimeS)
    {
        for (int i = existingText.Count - 1; i >= 0; i--)
        {
            if (!existingText[i].Update(deltaTimeS)) existingText.RemoveAt(i);
        }
    }

    public void Render()
    {
        foreach (var t in existingText) t.Render();
    }
}
