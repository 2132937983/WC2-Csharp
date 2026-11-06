namespace WC2;

/// <summary>"Loading..." hint drawn above the fade while a state switch is in progress (GUILoading.cpp).</summary>
public sealed class GUILoading : GUIElement
{
    public ecTexture textureLoading;
    public ecImage imageLoading;

    public GUILoading() { }

    /// <summary>Releases the image and the standalone texture of the hint.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageLoading = null;
        if (textureLoading != null)
        {
            ecGraphics.Instance().FreeTexture(textureLoading);
            textureLoading = null;
        }
        base.Dispose();
    }

    /// <summary>Loads the "loading" picture matching the screen layout and the language.</summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;

        ecGraphics graphics = ecGraphics.Instance();
        bool wideLayout = Layout.Tablet;
        string language = g_LocalizableStrings.GetString("language");
        bool chinese = language != null && language == "cn";

        if (wideLayout)
        {
            textureLoading = graphics.LoadTexture(chinese ? "loading_cn@2x.png" : "loading@2x.png");
            imageLoading = new ecImage(textureLoading, 0.0f, 0.0f, 156.0f, 22.0f);
        }
        else
        {
            textureLoading = graphics.LoadTexture(chinese ? "loading_cn.png" : "loading.png");
            imageLoading = new ecImage(textureLoading, 0.0f, 0.0f, 78.0f, 11.0f);
        }
    }

    /// <summary>Swallows the back key while shown; everything else goes to the base element.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.BACK_PRESSED && (flags & SHOWN) != 0) return true;
        return base.OnEvent(evt);
    }

    /// <summary>Lets the hint appear with the fade timer during the second half of the fade out.</summary>
    public override void OnRender()
    {
        float fadeTimer = GUIManager.Instance().fadeTimer;
        if (fadeTimer < 0.5f) return;
        if (imageLoading == null) return;

        uint intensity = (uint)(((fadeTimer - 0.5f) * 2.0f) * 255.0f);
        uint color = unchecked(0x01010101U * intensity);
        imageLoading.SetColor(color, -1);

        ecGraphics graphics = ecGraphics.Instance();
        if (Layout.Tablet)
            imageLoading.Render(850.0f, 730.0f);
        else
            imageLoading.Render(graphics.screenW - 86.0f, 300.0f);
    }
}
