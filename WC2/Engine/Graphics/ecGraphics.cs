using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;

namespace WC2;

/// <summary>
/// The 2D renderer of the original engine, implemented on top of MonoGame.
/// All coordinates handed to the renderer are logical units; <see cref="SetViewPoint"/> installs
/// the camera used for the map and the content scale maps logical units to window pixels.
/// Geometry is batched and flushed when the texture or blend mode changes.
/// </summary>
public sealed class ecGraphics
{
    private static ecGraphics s_instance;
    public static ecGraphics Instance() => s_instance ??= new ecGraphics();

    // ---- original public fields --------------------------------------------------------
    public readonly float[] layoutToScreenMultiplier = { 1f, 1f };
    /// <summary>Window (physical) width in pixels.</summary>
    public int layoutW;
    /// <summary>Window (physical) height in pixels.</summary>
    public int layoutH;
    /// <summary>Logical screen width used by all GUI code.</summary>
    public int screenW;
    /// <summary>Logical screen height used by all GUI code.</summary>
    public int screenH;
    public int orientation;
    /// <summary>3 = tablet (1024x768) layout, anything else = phone layout.</summary>
    public int widthMode;
    public int blendMode;
    public int renderShape;
    public ecTexture boundTexture;
    public int occupiedBuffer;
    public readonly Dictionary<string, ecTexture> mapTexture = new(StringComparer.OrdinalIgnoreCase);
    public float fadeRed;
    public float fadeGreen;
    public float fadeBlue;

    // ---- view ---------------------------------------------------------------------------
    private float viewX, viewY, viewScale = 1f;

    // ---- MonoGame objects ---------------------------------------------------------------
    public GraphicsDevice device;
    private BasicEffect effect;
    private Texture2D whiteTexture;
    private Texture2D currentTexture;
    private const int MaxVertices = 6 * 2000;
    private readonly VertexPositionColorTexture[] vertices = new VertexPositionColorTexture[MaxVertices];
    private int vertexCount;
    private Matrix projection = Matrix.Identity;
    private bool matricesDirty = true;
    private readonly SamplerState sampler = SamplerState.LinearClamp;

    /// <summary>Number of draw calls issued during the current frame (debug statistics).</summary>
    public int drawCalls;

    private ecGraphics() { }

    /// <summary>Binds the renderer to a MonoGame device. Must be called before Init().</summary>
    public void Attach(GraphicsDevice graphicsDevice)
    {
        device = graphicsDevice;
        effect?.Dispose();
        effect = new BasicEffect(device)
        {
            TextureEnabled = true,
            VertexColorEnabled = true,
            LightingEnabled = false,
            FogEnabled = false,
        };
        whiteTexture?.Dispose();
        whiteTexture = new Texture2D(device, 1, 1);
        whiteTexture.SetData(new[] { Color.White });
        matricesDirty = true;
    }

    /// <summary>
    /// Initializes the layout. <paramref name="logicalW"/>/<paramref name="logicalH"/> are the
    /// logical screen size (e.g. 568x320), <paramref name="screenWidth"/>/<paramref name="screenHeight"/>
    /// the window size in pixels.
    /// </summary>
    public void Init(int logicalW, int logicalH, int orientationIn, int screenWidth, int screenHeight)
    {
        screenW = logicalW;
        screenH = logicalH;
        orientation = orientationIn;
        Resize(screenWidth, screenHeight);
        // original: 1 = narrow (portrait phone), 2 = phone, 3 = tablet
        widthMode = logicalW <= 320 ? 1 : (logicalH <= 640 ? 2 : 3);
        blendMode = 0;
    }

    /// <summary>Pixel offset of the logical screen inside the window (letterbox bars).</summary>
    public float offsetX;
    public float offsetY;

    /// <summary>
    /// Updates the physical window size, keeping the logical layout. The logical screen is
    /// scaled uniformly and centered, so a window with another aspect ratio gets black bars
    /// instead of a distorted picture.
    /// </summary>
    public void Resize(int screenWidth, int screenHeight)
    {
        layoutW = Math.Max(1, screenWidth);
        layoutH = Math.Max(1, screenHeight);
        float sx = screenW > 0 ? (float)layoutW / screenW : 1f;
        float sy = screenH > 0 ? (float)layoutH / screenH : 1f;
        float scale = MathF.Min(sx, sy);
        layoutToScreenMultiplier[0] = scale;
        layoutToScreenMultiplier[1] = scale;
        offsetX = MathF.Floor((layoutW - screenW * scale) * 0.5f);
        offsetY = MathF.Floor((layoutH - screenH * scale) * 0.5f);
        projection = Matrix.CreateOrthographicOffCenter(0, layoutW, layoutH, 0, -1f, 1f);
        matricesDirty = true;
    }

    public void Shutdown()
    {
        Flush();
        foreach (var t in mapTexture.Values) t.handle?.Dispose();
        mapTexture.Clear();
    }

    public void SetOrientation(int orientationIn) => orientation = orientationIn;

    // ------------------------------------------------------------------------------------
    // Frame
    // ------------------------------------------------------------------------------------

    public void RenderBegin()
    {
        drawCalls = 0;
        vertexCount = 0;
        device.Clear(Color.Black);
        SetViewPoint(0f, 0f, 1f);
        SetBlendMode(0);
        currentTexture = null;
        boundTexture = null;
    }

    public void RenderEnd()
    {
        Flush();
    }

    /// <summary>Camera: world point (x,y) maps to the top-left corner; scale is the zoom.</summary>
    public void SetViewPoint(float x, float y, float scale)
    {
        if (x == viewX && y == viewY && scale == viewScale) return;
        Flush();
        viewX = x; viewY = y; viewScale = scale <= 0f ? 1f : scale;
        matricesDirty = true;
    }

    public void GetViewPoint(out float x, out float y, out float scale) { x = viewX; y = viewY; scale = viewScale; }

    /// <summary>1 = additive, 3 = multiply (destination color), anything else = alpha blend.</summary>
    public void SetBlendMode(int mode)
    {
        if (mode == blendMode) return;
        Flush();
        blendMode = mode;
    }

    private static readonly BlendState s_multiply = new BlendState
    {
        ColorSourceBlend = Blend.DestinationColor,
        AlphaSourceBlend = Blend.DestinationColor,
        ColorDestinationBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.Zero,
    };

    public void BindTexture(ecTexture texture)
    {
        var tex = texture?.handle;
        if (!ReferenceEquals(tex, currentTexture))
        {
            Flush();
            currentTexture = tex;
        }
        boundTexture = texture;
    }

    private BlendState CurrentBlendState()
    {
        switch (blendMode)
        {
            case 1: return BlendState.Additive;
            case 3: return s_multiply;
            default: return BlendState.NonPremultiplied;
        }
    }

    public void Flush()
    {
        if (vertexCount == 0 || device == null || effect == null) return;
        if (matricesDirty)
        {
            effect.World = Matrix.CreateTranslation(-viewX, -viewY, 0f)
                         * Matrix.CreateScale(viewScale, viewScale, 1f)
                         * Matrix.CreateScale(layoutToScreenMultiplier[0], layoutToScreenMultiplier[1], 1f)
                         * Matrix.CreateTranslation(offsetX, offsetY, 0f);
            effect.View = Matrix.Identity;
            effect.Projection = projection;
            matricesDirty = false;
        }
        effect.Texture = currentTexture ?? whiteTexture;
        device.BlendState = CurrentBlendState();
        device.SamplerStates[0] = sampler;
        device.RasterizerState = RasterizerState.CullNone;
        device.DepthStencilState = DepthStencilState.None;
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, vertexCount / 3);
        }
        drawCalls++;
        vertexCount = 0;
    }

    private static Color ToColor(uint abgr) => new Color { PackedValue = abgr };

    private void AddVertex(float x, float y, uint color, float u, float v)
    {
        if (vertexCount >= MaxVertices) Flush();
        vertices[vertexCount++] = new VertexPositionColorTexture(new Vector3(x, y, 0f), ToColor(color), new Vector2(u, v));
    }

    // ------------------------------------------------------------------------------------
    // Primitives
    // ------------------------------------------------------------------------------------

    public void RenderQuad(ecQuad quad)
    {
        if (quad == null) return;
        SetBlendMode(quad.blendMode);
        BindTexture(quad.texture);
        if (vertexCount + 6 > MaxVertices) Flush();
        AddVertex(quad.x[0], quad.y[0], quad.color[0], quad.u[0], quad.v[0]);
        AddVertex(quad.x[1], quad.y[1], quad.color[1], quad.u[1], quad.v[1]);
        AddVertex(quad.x[2], quad.y[2], quad.color[2], quad.u[2], quad.v[2]);
        AddVertex(quad.x[0], quad.y[0], quad.color[0], quad.u[0], quad.v[0]);
        AddVertex(quad.x[2], quad.y[2], quad.color[2], quad.u[2], quad.v[2]);
        AddVertex(quad.x[3], quad.y[3], quad.color[3], quad.u[3], quad.v[3]);
    }

    public void RenderTriple(ecTriple triple)
    {
        if (triple == null) return;
        BindTexture(triple.texture);
        if (vertexCount + 3 > MaxVertices) Flush();
        for (int i = 0; i < 3; i++) AddVertex(triple.x[i], triple.y[i], triple.color[i], triple.u[i], triple.v[i]);
    }

    public void RenderLine(ecLine line)
    {
        if (line == null) return;
        RenderLine(line.x1, line.y1, line.x2, line.y2, line.color);
    }

    public void RenderLine(float x1, float y1, float x2, float y2, uint color, float thickness = 1f)
    {
        BindTexture(null);
        float dx = x2 - x1, dy = y2 - y1;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len <= 0.0001f) return;
        float nx = -dy / len * thickness * 0.5f, ny = dx / len * thickness * 0.5f;
        if (vertexCount + 6 > MaxVertices) Flush();
        AddVertex(x1 + nx, y1 + ny, color, 0, 0);
        AddVertex(x2 + nx, y2 + ny, color, 0, 0);
        AddVertex(x2 - nx, y2 - ny, color, 0, 0);
        AddVertex(x1 + nx, y1 + ny, color, 0, 0);
        AddVertex(x2 - nx, y2 - ny, color, 0, 0);
        AddVertex(x1 - nx, y1 - ny, color, 0, 0);
    }

    /// <summary>Solid rectangle in the current view space. Color is 0xAABBGGRR.</summary>
    public void RenderRect(float x, float y, float w, float h, uint color)
    {
        BindTexture(null);
        if (vertexCount + 6 > MaxVertices) Flush();
        AddVertex(x, y, color, 0, 0);
        AddVertex(x + w, y, color, 0, 0);
        AddVertex(x + w, y + h, color, 0, 0);
        AddVertex(x, y, color, 0, 0);
        AddVertex(x + w, y + h, color, 0, 0);
        AddVertex(x, y + h, color, 0, 0);
    }

    /// <summary>
    /// Filled circle drawn as a fan whose center is fully transparent, which gives the soft
    /// radial shading of the original range indicator.
    /// </summary>
    public void RenderCircle(float x, float y, float radius, uint color)
    {
        BindTexture(null);
        const int segments = 36;
        float prevX = x + MathF.Sin(0f) * radius, prevY = y + MathF.Cos(0f) * radius;
        for (int i = 1; i <= segments; i++)
        {
            float a = i * 10f * MathF.PI / 180f;
            float px = x + MathF.Sin(a) * radius, py = y + MathF.Cos(a) * radius;
            if (vertexCount + 3 > MaxVertices) Flush();
            AddVertex(x, y, 0u, 0, 0);
            AddVertex(prevX, prevY, color, 0, 0);
            AddVertex(px, py, color, 0, 0);
            prevX = px; prevY = py;
        }
    }

    /// <summary>Ring between two radii, drawn as horizontal strips (port of the C++ helper).</summary>
    public void RenderRing(float centerX, float centerY, float innerRadius, float outerRadius, uint color)
    {
        if (innerRadius <= 0f || outerRadius <= 0f || innerRadius >= outerRadius) return;
        const float rowHeight = 2f;
        for (float offsetY = -outerRadius; offsetY < outerRadius; offsetY += rowHeight)
        {
            float y0 = offsetY;
            float y1 = MathF.Min(offsetY + rowHeight, outerRadius);
            float y = (y0 + y1) * 0.5f;
            float outerSq = outerRadius * outerRadius - y * y;
            if (outerSq <= 0f) continue;
            float outerHalf = MathF.Sqrt(outerSq);
            float innerHalf = 0f;
            if (MathF.Abs(y) < innerRadius)
            {
                float innerSq = innerRadius * innerRadius - y * y;
                if (innerSq > 0f) innerHalf = MathF.Sqrt(innerSq);
            }
            float seg = outerHalf - innerHalf;
            if (seg <= 0f) continue;
            if (innerHalf <= 0f)
                RenderRect(centerX - outerHalf, centerY + y0, outerHalf * 2f, y1 - y0, color);
            else
            {
                RenderRect(centerX - outerHalf, centerY + y0, seg, y1 - y0, color);
                RenderRect(centerX + innerHalf, centerY + y0, seg, y1 - y0, color);
            }
        }
    }

    /// <summary>Dims the whole screen with the fade color at the given alpha (0..1).</summary>
    public void Fade(float alpha)
    {
        if (alpha <= 0f) return;
        if (alpha > 1f) alpha = 1f;
        float sx = viewX, sy = viewY, ss = viewScale;
        int savedBlend = blendMode;
        SetViewPoint(0f, 0f, 1f);
        SetBlendMode(0);
        uint a = (uint)(alpha * 255f) << 24;
        uint r = (uint)(Math.Clamp(fadeRed, 0f, 1f) * 255f);
        uint g = (uint)(Math.Clamp(fadeGreen, 0f, 1f) * 255f) << 8;
        uint b = (uint)(Math.Clamp(fadeBlue, 0f, 1f) * 255f) << 16;
        float ox = offsetX / Math.Max(0.0001f, layoutToScreenMultiplier[0]);
        float oy = offsetY / Math.Max(0.0001f, layoutToScreenMultiplier[1]);
        RenderRect(-ox, -oy, screenW + ox * 2f, screenH + oy * 2f, a | b | g | r);
        Flush();
        SetViewPoint(sx, sy, ss);
        SetBlendMode(savedBlend);
    }

    // ------------------------------------------------------------------------------------
    // Textures
    // ------------------------------------------------------------------------------------

    public ecTexture LoadTexture(string textureName) => LoadTextureInternal(textureName);
    public ecTexture LoadPVRTexture(string textureName) => LoadTextureInternal(textureName);
    public ecTexture LoadETCTexture(string textureName) => LoadTextureInternal(textureName);
    public ecTexture LoadWEBPTexture(string textureName) => LoadTextureInternal(textureName);

    /// <summary>Not supported on desktop (system font rendering); ecLabelText falls back to bitmap fonts.</summary>
    public ecTexture CreateTextureWithString(string text, string fontName, int fontW, int fontH, int spaceW, int spaceH) => null;

    public ecTexture CreateTexture(int w, int h) => null;

    /// <summary>Decrements the reference count and disposes the GPU texture when unused.</summary>
    public void FreeTexture(ecTexture texture)
    {
        if (texture == null) return;
        texture.refCount--;
        if (texture.refCount > 0) return;
        if (texture.key.Length > 0) mapTexture.Remove(texture.key);
        if (ReferenceEquals(currentTexture, texture.handle)) { Flush(); currentTexture = null; }
        texture.handle?.Dispose();
        texture.handle = null;
    }

    /// <summary>Wraps raw RGBA pixels in a texture object (used for generated images).</summary>
    public ecTexture CreateTextureFromRgba(byte[] rgba, int width, int height, bool hd)
    {
        var tex2d = new Texture2D(device, width, height, false, SurfaceFormat.Color);
        tex2d.SetData(rgba);
        var tex = new ecTexture { handle = tex2d, pixelW = width, pixelH = height, w = width, h = height, refCount = 1 };
        if (hd) tex.SetHd(true);
        return tex;
    }

    private static readonly string[] FallbackExts = { ".png", ".webp", ".pkm", ".pvr", ".jpg" };

    /// <summary>
    /// Finds the file for a logical texture name. When running in HD mode the "@2x" variant
    /// is preferred, and the extension falls back through png / webp / pkm like the original
    /// format compatibility layer.
    /// </summary>
    public static string ResolveTextureFile(string textureName, out bool isHd)
    {
        isHd = false;
        if (string.IsNullOrEmpty(textureName)) return null;
        string ext = Ext(textureName);
        string baseName = ext.Length > 0 ? textureName.Substring(0, textureName.Length - ext.Length) : textureName;
        if (ext.Length == 0) ext = ".png";

        var bases = new List<string>(2);
        if (g_contenscalefactor == 2.0f && !baseName.EndsWith("@2x", StringComparison.OrdinalIgnoreCase))
            bases.Add(baseName + "@2x");
        bases.Add(baseName);

        var exts = new List<string>(6) { ext };
        foreach (var e in FallbackExts) if (!exts.Contains(e)) exts.Add(e);

        foreach (var b in bases)
        {
            foreach (var e in exts)
            {
                string candidate = GetPath(b + e);
                if (File.Exists(candidate))
                {
                    isHd = b.EndsWith("@2x", StringComparison.OrdinalIgnoreCase);
                    return candidate;
                }
            }
        }
        return null;
    }

    private ecTexture LoadTextureInternal(string textureName)
    {
        if (string.IsNullOrEmpty(textureName) || device == null) return null;
        string path = ResolveTextureFile(textureName, out bool isHd);
        if (path == null)
        {
            Log("Texture not found: " + textureName);
            return null;
        }
        string key = path.ToLowerInvariant();
        if (mapTexture.TryGetValue(key, out var cached) && cached.handle != null)
        {
            cached.refCount++;
            return cached;
        }
        try
        {
            byte[] data = File.ReadAllBytes(path);
            byte[] rgba;
            int w, h;
            if (Etc1Decoder.IsPkm(data))
            {
                rgba = Etc1Decoder.DecodePkm(data, out w, out h);
            }
            else
            {
                rgba = DecodeWithSkia(data, out w, out h);
            }
            if (rgba == null) { Log("Texture decode failed: " + path); return null; }
            var tex2d = new Texture2D(device, w, h, false, SurfaceFormat.Color);
            tex2d.SetData(rgba);
            var tex = new ecTexture
            {
                handle = tex2d,
                pixelW = w,
                pixelH = h,
                w = w,
                h = h,
                refCount = 1,
                key = key,
                fileName = Path.GetFileName(path),
            };
            if (isHd) tex.SetHd(true);
            mapTexture[key] = tex;
            return tex;
        }
        catch (Exception ex)
        {
            Log("Texture load failed: " + path + " : " + ex.Message);
            return null;
        }
    }

    private static byte[] DecodeWithSkia(byte[] data, out int width, out int height)
    {
        width = height = 0;
        using var codec = SKCodec.Create(new SKMemoryStream(data));
        if (codec == null) return null;
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput) return null;
        width = info.Width; height = info.Height;
        return bitmap.Bytes;
    }
}
