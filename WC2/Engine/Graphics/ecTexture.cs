using Microsoft.Xna.Framework.Graphics;

namespace WC2;

/// <summary>
/// A loaded texture. <see cref="w"/>/<see cref="h"/> are the size in logical units
/// (pixel size divided by <see cref="xC"/>); <see cref="xC"/> is 2 for "@2x" / "_hd" art.
/// </summary>
public sealed class ecTexture
{
    public int refCount;
    /// <summary>Logical width (pixels / xC).</summary>
    public int w;
    /// <summary>Logical height (pixels / xC).</summary>
    public int h;
    /// <summary>Content scale of this texture (1 or 2).</summary>
    public float xC = 1.0f;
    public Texture2D handle;
    public bool x14;

    public int pixelW;
    public int pixelH;
    /// <summary>Cache key (resolved file path, lower case).</summary>
    public string key = "";
    public string fileName = "";

    /// <summary>Marks the texture as 2x art: logical size becomes half of the pixel size.</summary>
    public void SetHd(bool hd)
    {
        float scale = hd ? 2.0f : 1.0f;
        if (xC == scale) return;
        xC = scale;
        w = (int)(pixelW / scale);
        h = (int)(pixelH / scale);
    }
}

/// <summary>Sub rectangle of a texture in logical units plus the hot spot (refx/refy).</summary>
public struct ecTextureRect
{
    public float x;
    public float y;
    public float w;
    public float h;
    public float refx;
    public float refy;

    public ecTextureRect(float x, float y, float w, float h, float refx = 0f, float refy = 0f)
    {
        this.x = x; this.y = y; this.w = w; this.h = h; this.refx = refx; this.refy = refy;
    }
}

/// <summary>An atlas entry: texture plus logical rectangle and hot spot.</summary>
public sealed class ecImageAttr
{
    public ecTexture texture;
    public float x;
    public float y;
    public float w;
    public float h;
    public float refx;
    public float refy;
    public string name = "";
}

/// <summary>One textured, colored quad in logical coordinates (ecGraphics vertex buffer entry x4).</summary>
public sealed class ecQuad
{
    public readonly float[] x = new float[4];
    public readonly float[] y = new float[4];
    public readonly float[] u = new float[4];
    public readonly float[] v = new float[4];
    /// <summary>Vertex colors as 0xAABBGGRR (byte order R,G,B,A in memory, like GL).</summary>
    public readonly uint[] color = { 0xFFFFFFFFu, 0xFFFFFFFFu, 0xFFFFFFFFu, 0xFFFFFFFFu };
    public ecTexture texture;
    public int blendMode;
}

public sealed class ecLine
{
    public float x1, y1, x2, y2;
    public uint color = 0xFFFFFFFFu;
}

public sealed class ecTriple
{
    public readonly float[] x = new float[3];
    public readonly float[] y = new float[3];
    public readonly float[] u = new float[3];
    public readonly float[] v = new float[3];
    public readonly uint[] color = { 0xFFFFFFFFu, 0xFFFFFFFFu, 0xFFFFFFFFu };
    public ecTexture texture;
}
