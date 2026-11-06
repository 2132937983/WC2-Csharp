namespace WC2;

/// <summary>A static picture, either from the shared atlas or from a standalone texture. Also used as an empty container.</summary>
public class GUIImage : GUIElement
{
    public ecTexture texture;
    public ecImage image;
    public float xScale = 1f;
    public float yScale = 1f;

    public GUIImage() { }

    /// <summary>Loads a standalone texture and shows the given sub rectangle.</summary>
    public bool Init(string textureName, in ecTextureRect textureRect, in GUIRect rectIn, bool pvr, bool etc, bool webp)
    {
        rect = rectIn;
        ReleaseOwnedTexture();
        if (string.IsNullOrEmpty(textureName)) { image = null; return false; }
        texture = ecGraphics.Instance().LoadTexture(textureName);
        if (texture == null) { image = null; return false; }
        var tr = textureRect;
        if (tr.w <= 0f || tr.h <= 0f) tr = new ecTextureRect(0f, 0f, texture.w, texture.h, tr.refx, tr.refy);
        image = new ecImage(texture, tr);
        return true;
    }

    /// <summary>Uses an image of the shared atlas (null name creates an empty container).</summary>
    public void Init(string imageName, in GUIRect rectIn)
    {
        rect = rectIn;
        ReleaseOwnedTexture();
        image = null;
        if (string.IsNullOrEmpty(imageName)) return;
        var attr = s_TextureRes.GetImage(imageName);
        if (attr != null) image = new ecImage(attr);
        else Log("GUIImage: atlas image not found: " + imageName);
    }

    public void SetAlpha(float alpha) => image?.SetAlpha(alpha, -1);

    public void SetColor(uint color) => image?.SetColor(color, -1);

    public void SetImage(string textureName, in ecTextureRect textureRect, bool pvr)
    {
        ReleaseOwnedTexture();
        image = null;
        if (string.IsNullOrEmpty(textureName)) return;
        var attr = s_TextureRes.GetImage(textureName);
        if (attr != null && textureRect.w <= 0f)
        {
            image = new ecImage(attr);
            return;
        }
        texture = ecGraphics.Instance().LoadTexture(textureName);
        if (texture != null) image = new ecImage(texture, textureRect);
    }

    private void ReleaseOwnedTexture()
    {
        if (texture != null)
        {
            ecGraphics.Instance().FreeTexture(texture);
            texture = null;
        }
    }

    public override void OnRender()
    {
        if (image == null) return;
        GetAbsRect(out var abs);
        if (xScale == 1f && yScale == 1f) image.Render(abs.x, abs.y);
        else image.RenderEx(abs.x, abs.y, 0f, xScale, yScale);
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        base.Dispose();
        ReleaseOwnedTexture();
        image = null;
    }
}
