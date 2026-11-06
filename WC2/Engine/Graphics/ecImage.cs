namespace WC2;

/// <summary>
/// A sprite (HGE style): a texture sub-rectangle with a hot spot, per vertex colors, blend
/// mode and flips. All sizes are logical units. Render(x,y) draws the sprite so that its hot
/// spot (textureRect.refx/refy) lands on (x,y).
/// </summary>
public class ecImage
{
    public ecTexture texture;
    public ecTextureRect textureRect;
    public float textureW;
    public float textureH;
    public readonly ecQuad quad = new ecQuad();
    public int blendMode;
    /// <summary>[0] flip X, [1] flip Y, [2] flip the hot spot too.</summary>
    public readonly bool[] flip = new bool[3];

    public ecImage() { }

    public ecImage(ecTexture tex, float x, float y, float w, float h) => Init(tex, x, y, w, h);

    public ecImage(ecTexture tex, in ecTextureRect rect)
    {
        Init(tex, rect.x, rect.y, rect.w, rect.h);
        textureRect.refx = rect.refx;
        textureRect.refy = rect.refy;
    }

    public ecImage(ecImageAttr attr) => Init(attr);

    public float width => textureRect.w;
    public float height => textureRect.h;

    public void Init(ecTexture tex, float x, float y, float w, float h)
    {
        texture = tex;
        textureW = tex != null && tex.w > 0 ? tex.w : 1f;
        textureH = tex != null && tex.h > 0 ? tex.h : 1f;
        textureRect = new ecTextureRect(x, y, w, h, 0f, 0f);
        blendMode = 2;
        flip[0] = flip[1] = flip[2] = false;
        for (int i = 0; i < 4; i++) quad.color[i] = 0xFFFFFFFFu;
        quad.texture = tex;
        UpdateUV();
    }

    public void Init(ecImageAttr attr)
    {
        if (attr == null)
        {
            texture = null; textureW = textureH = 1f; textureRect = default; quad.texture = null;
            return;
        }
        Init(attr.texture, attr.x, attr.y, attr.w, attr.h);
        textureRect.refx = attr.refx;
        textureRect.refy = attr.refy;
    }

    private void UpdateUV()
    {
        float u0 = textureRect.x / textureW, v0 = textureRect.y / textureH;
        float u1 = (textureRect.x + textureRect.w) / textureW, v1 = (textureRect.y + textureRect.h) / textureH;
        if (flip[0]) (u0, u1) = (u1, u0);
        if (flip[1]) (v0, v1) = (v1, v0);
        quad.u[0] = u0; quad.v[0] = v0;
        quad.u[1] = u1; quad.v[1] = v0;
        quad.u[2] = u1; quad.v[2] = v1;
        quad.u[3] = u0; quad.v[3] = v1;
    }

    private void Submit()
    {
        quad.texture = texture;
        quad.blendMode = blendMode;
        ecGraphics.Instance().RenderQuad(quad);
    }

    // ------------------------------------------------------------------------------------
    // Rendering
    // ------------------------------------------------------------------------------------

    public void Render(float x, float y)
    {
        float x1 = x - textureRect.refx, y1 = y - textureRect.refy;
        float x2 = x1 + textureRect.w, y2 = y1 + textureRect.h;
        quad.x[0] = x1; quad.y[0] = y1;
        quad.x[1] = x2; quad.y[1] = y1;
        quad.x[2] = x2; quad.y[2] = y2;
        quad.x[3] = x1; quad.y[3] = y2;
        Submit();
    }

    /// <summary>Renders with an explicit size (hot spot applied unscaled).</summary>
    public void Render(float x, float y, float w, float h)
    {
        float x1 = x - textureRect.refx, y1 = y - textureRect.refy;
        float x2 = x1 + w, y2 = y1 + h;
        quad.x[0] = x1; quad.y[0] = y1;
        quad.x[1] = x2; quad.y[1] = y1;
        quad.x[2] = x2; quad.y[2] = y2;
        quad.x[3] = x1; quad.y[3] = y2;
        Submit();
    }

    /// <summary>Rotation (radians) around the hot spot and scaling; vScale 0 means "same as hScale".</summary>
    public void RenderEx(float x, float y, float angle, float xScale, float yScale)
    {
        if (yScale == 0f) yScale = xScale;
        float tx1 = -textureRect.refx * xScale;
        float ty1 = -textureRect.refy * yScale;
        float tx2 = (textureRect.w - textureRect.refx) * xScale;
        float ty2 = (textureRect.h - textureRect.refy) * yScale;
        if (angle != 0f)
        {
            float c = MathF.Cos(angle), s = MathF.Sin(angle);
            quad.x[0] = tx1 * c - ty1 * s + x; quad.y[0] = tx1 * s + ty1 * c + y;
            quad.x[1] = tx2 * c - ty1 * s + x; quad.y[1] = tx2 * s + ty1 * c + y;
            quad.x[2] = tx2 * c - ty2 * s + x; quad.y[2] = tx2 * s + ty2 * c + y;
            quad.x[3] = tx1 * c - ty2 * s + x; quad.y[3] = tx1 * s + ty2 * c + y;
        }
        else
        {
            quad.x[0] = tx1 + x; quad.y[0] = ty1 + y;
            quad.x[1] = tx2 + x; quad.y[1] = ty1 + y;
            quad.x[2] = tx2 + x; quad.y[2] = ty2 + y;
            quad.x[3] = tx1 + x; quad.y[3] = ty2 + y;
        }
        Submit();
    }

    public void RenderStretch(float xUL, float yUL, float xBR, float yBR)
    {
        quad.x[0] = xUL; quad.y[0] = yUL;
        quad.x[1] = xBR; quad.y[1] = yUL;
        quad.x[2] = xBR; quad.y[2] = yBR;
        quad.x[3] = xUL; quad.y[3] = yBR;
        Submit();
    }

    public void Render4V(float xUL, float yUL, float xUR, float yUR, float xBR, float yBR, float xBL, float yBL)
    {
        quad.x[0] = xUL; quad.y[0] = yUL;
        quad.x[1] = xUR; quad.y[1] = yUR;
        quad.x[2] = xBR; quad.y[2] = yBR;
        quad.x[3] = xBL; quad.y[3] = yBL;
        Submit();
    }

    /// <summary>
    /// Growing quad used for the attack arrows (port of the original). The top edge (UL, UR)
    /// slides from the bottom edge (BL, BR) towards its final position as visibility goes
    /// from 0 to 1, and the quad fades towards the bottom. white = 0 draws the texture with a
    /// white-to-transparent gradient, white = 1 draws a dark translucent gradient, other values
    /// keep the sprite colors. For visibility in (1, 2) the full quad pulses in alpha.
    /// </summary>
    public void Render4VC(float xUL, float yUL, float xUR, float yUR, float xBR, float yBR, float xBL, float yBL, int white, float visibility)
    {
        float dxL = xUL - xBL, dyL = yUL - yBL;
        float dxR = xUR - xBR, dyR = yUR - yBR;
        float lenL = MathF.Sqrt(dxL * dxL + dyL * dyL);
        float lenR = MathF.Sqrt(dxR * dxR + dyR * dyR);
        float dirLx = lenL > 0f ? dxL / lenL : 0f, dirLy = lenL > 0f ? dyL / lenL : 0f;
        float dirRx = lenR > 0f ? dxR / lenR : 0f, dirRy = lenR > 0f ? dyR / lenR : 0f;
        float vis = visibility <= 1f ? visibility : 1f;

        quad.x[0] = xBL + vis * lenL * dirLx; quad.y[0] = yBL + vis * lenL * dirLy;
        quad.x[1] = xBR + vis * lenR * dirRx; quad.y[1] = yBR + vis * lenR * dirRy;
        quad.x[2] = xBR; quad.y[2] = yBR;
        quad.x[3] = xBL; quad.y[3] = yBL;

        if (visibility <= 1f)
        {
            if (white == 0)
            {
                quad.color[0] = quad.color[1] = 0xFFFFFFFFu;
                quad.color[2] = quad.color[3] = 0x00FFFFFFu;
            }
            else if (white == 1)
            {
                quad.color[0] = quad.color[1] = 0x77000000u;
                quad.color[2] = quad.color[3] = 0u;
            }
        }
        else
        {
            float t = MathF.Abs(0.5f - (visibility - 1f)) * 2f;
            if (white == 0)
            {
                uint top = ((uint)(t * 255f) << 24) + 0x00FFFFFFu;
                quad.color[0] = quad.color[1] = top;
                quad.color[2] = quad.color[3] = 0x00FFFFFFu;
            }
            else if (white == 1)
            {
                uint top = (uint)(t * 119f) << 24;
                quad.color[0] = quad.color[1] = top;
                quad.color[2] = quad.color[3] = 0u;
            }
        }
        Submit();
    }

    // ------------------------------------------------------------------------------------
    // Setters
    // ------------------------------------------------------------------------------------

    public void SetTexture(ecTexture tex)
    {
        texture = tex;
        textureW = tex != null && tex.w > 0 ? tex.w : 1f;
        textureH = tex != null && tex.h > 0 ? tex.h : 1f;
        quad.texture = tex;
        UpdateUV();
    }

    /// <summary>Color is 0xAABBGGRR; type -1 sets all vertices, 0..3 a single vertex.</summary>
    public void SetColor(uint color, int type = -1)
    {
        if (type < 0 || type > 3) { for (int i = 0; i < 4; i++) quad.color[i] = color; }
        else quad.color[type] = color;
    }

    public void SetColor(int color, int type = -1) => SetColor(unchecked((uint)color), type);
    public void SetColor(long color, int type = -1) => SetColor(unchecked((uint)color), type);

    public uint GetColor(int i = 0) => quad.color[Math.Clamp(i, 0, 3)];

    public void SetAlpha(float alpha, int type = -1)
    {
        uint a = (uint)(Math.Clamp(alpha, 0f, 1f) * 255f) << 24;
        if (type < 0 || type > 3)
        {
            for (int i = 0; i < 4; i++) quad.color[i] = (quad.color[i] & 0x00FFFFFFu) | a;
        }
        else quad.color[type] = (quad.color[type] & 0x00FFFFFFu) | a;
    }

    public void SetBlendMode(int mode) => blendMode = mode;

    public void SetFlip(bool flipX, bool flipY, bool flipRef)
    {
        // undo the hot spot mirroring applied by the previous flip state
        if (flip[2] && flip[0]) textureRect.refx = textureRect.w - textureRect.refx;
        if (flip[2] && flip[1]) textureRect.refy = textureRect.h - textureRect.refy;
        flip[2] = flipRef;
        if (flip[2] && flipX) textureRect.refx = textureRect.w - textureRect.refx;
        if (flip[2] && flipY) textureRect.refy = textureRect.h - textureRect.refy;
        flip[0] = flipX;
        flip[1] = flipY;
        UpdateUV();
    }

    public void SetTextureRect(float x, float y, float w, float h)
    {
        textureRect.x = x; textureRect.y = y; textureRect.w = w; textureRect.h = h;
        UpdateUV();
    }

    public void SetTextureRect(in ecTextureRect rect)
    {
        textureRect = rect;
        UpdateUV();
    }

    /// <summary>Sets the hot spot (HGE SetHotSpot).</summary>
    public void SetHotSpot(float refx, float refy)
    {
        textureRect.refx = refx;
        textureRect.refy = refy;
    }
}
