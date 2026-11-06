namespace WC2;

/// <summary>Small preview of the card waiting for a target, drawn at the bottom of the screen (GUISmallCard.cpp).</summary>
public sealed class GUISmallCard : GUIElement
{
    public ecImage cardImage;

    public GUISmallCard() { }

    /// <summary>Releases the card face.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        cardImage = null;
        base.Dispose();
    }

    /// <summary>
    /// Sets the layout and allocates the (still empty) card face; the original only allocates
    /// the ecImage here, SetCard fills it through ecImage.Init.
    /// </summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;
        cardImage = new ecImage();
    }

    /// <summary>Draws the common frame and the card face at the bottom of the screen.</summary>
    public override void OnRender()
    {
        GetAbsRect(out var absoluteRect);

        if (cardImage == null) return;

        bool wideLayout = ecGraphics.Instance().widthMode == 3;
        float centerOffsetX = wideLayout ? 70.0f : 50.0f;
        float bottomOffset = wideLayout ? 110.0f : 70.0f;
        float scale = wideLayout ? 0.7f : 0.42f;
        float x = absoluteRect.x + centerOffsetX;
        float y = absoluteRect.y + ecGraphics.Instance().screenH - bottomOffset;

        if (g_GameRes.imageCardCommon != null)
        {
            g_GameRes.imageCardCommon.SetColor(0xFFFFFFFFu, -1);
            g_GameRes.imageCardCommon.RenderEx(x, y, 0.0f, scale, 0.0f);
        }
        cardImage.RenderEx(x, y + 7.0f, 0.0f, scale, 0.0f);
    }

    /// <summary>Initializes the small face from the picture of the card definition.</summary>
    public void SetCard(CardDef card)
    {
        ecImageAttr imageAttr = s_TextureRes.GetImage(card.image);
        if (imageAttr != null)
            cardImage.Init(imageAttr);
    }
}
