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
    /// Window pixels of the logical screen. Drawing is clipped to it, so whatever the original placed
    /// off the screen (controls waiting to slide in, map edges) stays invisible in letterbox bars.
    /// </summary>
    private Rectangle screenClip;
    private static readonly RasterizerState s_clipToScreen = new() { CullMode = CullMode.None, ScissorTestEnable = true };

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
        screenClip = new Rectangle((int)offsetX, (int)offsetY,
            Math.Min(layoutW - (int)offsetX, (int)MathF.Ceiling(screenW * scale)),
            Math.Min(layoutH - (int)offsetY, (int)MathF.Ceiling(screenH * scale)));
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
        if (TextureStatsEvery > 0 && ++statsFrames >= TextureStatsEvery)
        {
            statsFrames = 0;
            Log(TextureStats());
        }
    }

    /// <summary><c>--texture-stats</c>: log the live texture memory every this many frames (0 = off).</summary>
    public static int TextureStatsEvery;
    private int statsFrames;

    /// <summary>Live textures and their GPU size (compressed and 16 bit formats counted as stored).</summary>
    public string TextureStats()
    {
        long bytes = 0;
        int count = 0;
        var byFolder = new Dictionary<string, long>();
        foreach (var (key, t) in mapTexture)
        {
            if (t.handle == null || t.handle.IsDisposed) continue;
            count++;
            long pixels = (long)t.handle.Width * t.handle.Height;
            long size = t.handle.Format switch
            {
                SurfaceFormat.RgbEtc1 or SurfaceFormat.Rgb8Etc2 => pixels / 2,
                SurfaceFormat.Bgra4444 or SurfaceFormat.Bgr565 or SurfaceFormat.Bgra5551 => pixels * 2,
                SurfaceFormat.Alpha8 => pixels,
                _ => pixels * 4,
            };
            bytes += size;
            // group by the first three folders of the package path (image/map/tiles, image/ui/hd, ...)
            var parts = key.Split('/');
            string folder = parts.Length > 3 ? string.Join('/', parts.Take(3)) : (parts.Length > 1 ? string.Join('/', parts.Take(parts.Length - 1)) : "(root)");
            byFolder[folder] = byFolder.GetValueOrDefault(folder) + size;
        }
        string groups = string.Join(", ", byFolder.OrderByDescending(kv => kv.Value).Take(8).Select(kv => $"{kv.Key} {kv.Value / 1e6:F0}"));
        return $"Textures: {count} live, {bytes / 1e6:F0} MB ({groups})";
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
        device.SamplerStates[0] = sampler;
        device.RasterizerState = s_clipToScreen;
        device.ScissorRectangle = screenClip;
        device.DepthStencilState = DepthStencilState.None;
        if (AlphaTexture && alphaSampling == AlphaSampling.Luminance)
        {
            DrawCoverageBatch();
            drawCalls += 2;
            vertexCount = 0;
            return;
        }
        if (AlphaTexture)
        {
            // the coverage is in alpha, the colour black: fog at full strength (start == end) gives
            // rgb = fog colour * alpha (premultiplied), alpha = texture alpha * vertex alpha
            effect.FogEnabled = true;
            effect.FogStart = 0f;
            effect.FogEnd = 0f;
            effect.FogColor = new Vector3((alphaTint & 0xFF) / 255f, ((alphaTint >> 8) & 0xFF) / 255f, ((alphaTint >> 16) & 0xFF) / 255f);
            device.BlendState = blendMode == 1 ? s_premultipliedAdditive : blendMode == 3 ? s_multiply : BlendState.AlphaBlend;
        }
        else
        {
            effect.FogEnabled = false;
            device.BlendState = CurrentBlendState();
        }
        DrawBatch();
        drawCalls++;
        vertexCount = 0;
    }

    private static Color ToColor(uint abgr) => new Color { PackedValue = abgr };

    /// <summary>
    /// Colour of the current batch on an alpha-only texture (Alpha8 samples as black with alpha): the
    /// batch is drawn with BasicEffect's fog at full strength in this colour, which keeps the texture's
    /// alpha times the vertex alpha and replaces the colour. A different vertex colour starts a new
    /// batch (at a triangle boundary).
    /// </summary>
    private uint alphaTint;
    private bool AlphaTexture => currentTexture != null && currentTexture.Format == SurfaceFormat.Alpha8;

    /// <summary>How the GPU samples an Alpha8 texture (probed once, see <see cref="ProbeAlpha8"/>).</summary>
    private enum AlphaSampling { Unknown, AlphaChannel, Luminance, Unsupported }
    private AlphaSampling alphaSampling = AlphaSampling.Unknown;

    private void DrawBatch()
    {
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, vertexCount / 3);
        }
    }

    // coverage in the colour channels (an Alpha8 texture uploaded as luminance, MonoGame on OpenGL):
    // pass 1 darkens the destination by the coverage, pass 2 adds the colour times the coverage;
    // together dst * (1 - L * a) + c * L * a, which is ordinary alpha blending of colour c
    private static readonly BlendState s_coverageErase = new BlendState
    {
        ColorSourceBlend = Blend.Zero, ColorDestinationBlend = Blend.InverseSourceColor,
        AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One,
    };
    private static readonly BlendState s_coverageAdd = new BlendState
    {
        ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One,
    };
    private Color[] coverageColors = Array.Empty<Color>();

    private void DrawCoverageBatch()
    {
        effect.FogEnabled = false;
        if (coverageColors.Length < vertexCount) coverageColors = new Color[vertices.Length];
        for (int i = 0; i < vertexCount; i++) coverageColors[i] = vertices[i].Color;
        if (blendMode != 1)
        {
            for (int i = 0; i < vertexCount; i++)
            {
                byte a = coverageColors[i].A;
                vertices[i].Color = new Color(a, a, a, a);
            }
            device.BlendState = s_coverageErase;
            DrawBatch();
        }
        for (int i = 0; i < vertexCount; i++)
        {
            var c = coverageColors[i];
            vertices[i].Color = new Color(c.R * c.A / 255, c.G * c.A / 255, c.B * c.A / 255, c.A);
        }
        device.BlendState = s_coverageAdd;
        DrawBatch();
        for (int i = 0; i < vertexCount; i++) vertices[i].Color = coverageColors[i];
    }

    /// <summary>
    /// Draws one Alpha8 pixel of value 255 into a 1x1 target and reads it back: (0,0,0,1) means the
    /// coverage arrives in alpha (GL_ALPHA), (1,1,1,1) in the colour channels (luminance, MonoGame's
    /// OpenGL mapping). Anything else, or an error, and Alpha8 textures are expanded to RGBA4444.
    /// </summary>
    private AlphaSampling ProbeAlpha8()
    {
        try
        {
            using var tex = new Texture2D(device, 1, 1, false, SurfaceFormat.Alpha8);
            tex.SetData(new byte[] { 255 });
            using var target = new RenderTarget2D(device, 1, 1, false, SurfaceFormat.Color, DepthFormat.None);
            var previous = device.GetRenderTargets();
            device.SetRenderTarget(target);
            device.Clear(Color.Transparent);
            using var fx = new BasicEffect(device)
            {
                TextureEnabled = true, VertexColorEnabled = true, Texture = tex,
                Projection = Matrix.CreateOrthographicOffCenter(0, 1, 1, 0, -1, 1), View = Matrix.Identity, World = Matrix.Identity,
            };
            device.BlendState = BlendState.Opaque;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;
            device.SamplerStates[0] = SamplerState.PointClamp;
            var quad = new[]
            {
                new VertexPositionColorTexture(new Vector3(0, 0, 0), Color.White, new Vector2(0, 0)),
                new VertexPositionColorTexture(new Vector3(1, 0, 0), Color.White, new Vector2(1, 0)),
                new VertexPositionColorTexture(new Vector3(0, 1, 0), Color.White, new Vector2(0, 1)),
                new VertexPositionColorTexture(new Vector3(1, 0, 0), Color.White, new Vector2(1, 0)),
                new VertexPositionColorTexture(new Vector3(1, 1, 0), Color.White, new Vector2(1, 1)),
                new VertexPositionColorTexture(new Vector3(0, 1, 0), Color.White, new Vector2(0, 1)),
            };
            foreach (var pass in fx.CurrentTechnique.Passes) { pass.Apply(); device.DrawUserPrimitives(PrimitiveType.TriangleList, quad, 0, 2); }
            device.SetRenderTargets(previous);
            var pixel = new Color[1];
            target.GetData(pixel);
            var p = pixel[0];
            var mode = p.A > 200 && p.R < 50 ? AlphaSampling.AlphaChannel
                     : p.A > 200 && p.R > 200 ? AlphaSampling.Luminance
                     : AlphaSampling.Unsupported;
            Log($"Textures: Alpha8 samples as ({p.R},{p.G},{p.B},{p.A}) -> {mode}");
            return mode;
        }
        catch (Exception ex)
        {
            Log("Textures: Alpha8 probe failed (" + ex.Message + "), alpha pages become RGBA4444");
            try { device.SetRenderTarget(null); } catch { }
            return AlphaSampling.Unsupported;
        }
    }

    private static readonly BlendState s_premultipliedAdditive = new BlendState
    {
        ColorSourceBlend = Blend.One, AlphaSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One, AlphaDestinationBlend = Blend.One,
    };

    private void AddVertex(float x, float y, uint color, float u, float v)
    {
        if (vertexCount >= MaxVertices) Flush();
        if (AlphaTexture && alphaSampling == AlphaSampling.AlphaChannel && (color & 0x00FFFFFFu) != alphaTint && vertexCount % 3 == 0)
        {
            Flush();
            alphaTint = color & 0x00FFFFFFu;
        }
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
                string candidate = AssetSystem.Resolve(b + e);
                if (candidate != null)
                {
                    isHd = b.EndsWith("@2x", StringComparison.OrdinalIgnoreCase);
                    // a runtime format generated by the package build (generated/<path>.wctex) wins when
                    // it sits in the same layer as its source, so a mod that replaces the source image still wins
                    string generated = "generated/" + Path.ChangeExtension(candidate, GeneratedTextureExtension).Replace('\\', '/');
                    if (AssetSystem.Exists(generated) && AssetSystem.GetLayer(generated) == AssetSystem.GetLayer(candidate))
                        return AssetSystem.Resolve(generated);
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
            byte[] data = AssetSystem.ReadAllBytes(path);
            if (data == null) { Log("Texture unreadable: " + path); return null; }

            if (path.EndsWith(GeneratedTextureExtension, StringComparison.OrdinalIgnoreCase))
            {
                var generated = LoadGeneratedTexture(data, path);
                if (generated == null) { Log("Texture unreadable: " + path); return null; }
                generated.key = key;
                generated.fileName = Path.GetFileName(path);
                if (isHd) generated.SetHd(true);
                mapTexture[key] = generated;
                return generated;
            }

            // PKM (ETC1) goes to the GPU as it is where the GPU supports it (every Android device):
            // 0.5 MB per 1024 px map tile instead of 4 MB decoded
            Texture2D native = Etc1Decoder.IsPkm(data) && SupportsNativeEtc1() ? CreateEtc1Texture(data) : null;
            if (native != null)
            {
                var etc = new ecTexture
                {
                    handle = native,
                    pixelW = native.Width,
                    pixelH = native.Height,
                    w = native.Width,
                    h = native.Height,
                    refCount = 1,
                    key = key,
                    fileName = Path.GetFileName(path),
                };
                if (isHd) etc.SetHd(true);
                mapTexture[key] = etc;
                return etc;
            }
            byte[] rgba;
            int w, h;
            bool opaque = false;
            if (Etc1Decoder.IsPkm(data))
            {
                rgba = Etc1Decoder.DecodePkm(data, out w, out h);
                opaque = true;   // ETC1 has no alpha
            }
            else
            {
                rgba = DecodeWithSkia(data, out w, out h);
            }
            if (rgba == null) { Log("Texture decode failed: " + path); return null; }
            Texture2D tex2d;
            if (LoadFormat(path) == "rgba5551")
            {
                // load-time runtime format (assets/build.json, generated/texture_formats.json)
                tex2d = new Texture2D(device, w, h, false, SurfaceFormat.Bgra5551);
                tex2d.SetData(PackRgba5551(rgba, w * h));
            }
            else if (opaque && DecodeEtcTo565)
            {
                // a GPU without ETC: 16 bit RGB565 instead of RGBA8 halves the map tiles (ETC1 carries
                // about 5 bits per channel itself)
                tex2d = new Texture2D(device, w, h, false, SurfaceFormat.Bgr565);
                tex2d.SetData(PackRgb565(rgba, w * h));
            }
            else
            {
                tex2d = new Texture2D(device, w, h, false, SurfaceFormat.Color);
                tex2d.SetData(rgba);
            }
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

    /// <summary>CPU-decoded ETC1 textures (no ETC support on the GPU) are uploaded as RGB565.</summary>
    public static bool DecodeEtcTo565 = true;

    private static Dictionary<string, string> s_loadFormats;

    /// <summary>Forgets the load-time format list (after the asset layers change).</summary>
    public static void ResetLoadFormats() => s_loadFormats = null;

    /// <summary>The load-time format of a package path (generated/texture_formats.json), or null.</summary>
    private static string LoadFormat(string path)
    {
        if (s_loadFormats == null)
        {
            s_loadFormats = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string text = AssetSystem.ReadAllText("generated/texture_formats.json");
            if (text != null && JsonData.Parse(text) is System.Text.Json.Nodes.JsonObject o && o.Obj("textures") is System.Text.Json.Nodes.JsonObject list)
                foreach (var (key, value) in list) s_loadFormats[key] = value?.Text();
        }
        return s_loadFormats.TryGetValue(path, out var f) ? f : null;
    }

    /// <summary>RGBA8 to GL_UNSIGNED_SHORT_5_5_5_1 (R in the high bits, alpha in bit 0; MonoGame's Bgra5551 on OpenGL).</summary>
    private static ushort[] PackRgba5551(byte[] rgba, int pixels)
    {
        var packed = new ushort[pixels];
        for (int i = 0, j = 0; i < pixels; i++, j += 4)
        {
            int r = (rgba[j] * 31 + 127) / 255, g = (rgba[j + 1] * 31 + 127) / 255, b = (rgba[j + 2] * 31 + 127) / 255;
            packed[i] = (ushort)((r << 11) | (g << 6) | (b << 1) | (rgba[j + 3] >= 128 ? 1 : 0));
        }
        return packed;
    }

    /// <summary>RGBA8 to GL_UNSIGNED_SHORT_5_6_5 (R in the high bits; MonoGame's Bgr565 on OpenGL).</summary>
    private static ushort[] PackRgb565(byte[] rgba, int pixels)
    {
        var packed = new ushort[pixels];
        for (int i = 0, j = 0; i < pixels; i++, j += 4)
        {
            int r = (rgba[j] * 31 + 127) / 255, g = (rgba[j + 1] * 63 + 127) / 255, b = (rgba[j + 2] * 31 + 127) / 255;
            packed[i] = (ushort)((r << 11) | (g << 5) | b);
        }
        return packed;
    }

    /// <summary>Extension of the textures generated by the package build (WC2.Tools RuntimeFormats).</summary>
    public const string GeneratedTextureExtension = ".wctex";

    /// <summary>
    /// A generated texture: "WCTX", version 1, format (1 = RGBA4444, 2 = RGBA5551, 3 = Alpha8), width,
    /// height, then the deflate compressed pixels (16 bit GL_UNSIGNED_SHORT_4_4_4_4 / 5_5_5_1, or 8 bit
    /// alpha). An Alpha8 texture samples as (0, 0, 0, a); Flush tints it (see <see cref="alphaTint"/>).
    /// </summary>
    private ecTexture LoadGeneratedTexture(byte[] data, string path)
    {
        if (data.Length < 16 || data[0] != 'W' || data[1] != 'C' || data[2] != 'T' || data[3] != 'X') return null;
        int version = BitConverter.ToUInt16(data, 4);
        int format = BitConverter.ToUInt16(data, 6);
        int width = BitConverter.ToInt32(data, 8);
        int height = BitConverter.ToInt32(data, 12);
        if (version != 1 || format < 1 || format > 3 || width <= 0 || height <= 0) return null;
        var pixels = new byte[width * height * (format == 3 ? 1 : 2)];
        using (var input = new System.IO.Compression.DeflateStream(new MemoryStream(data, 16, data.Length - 16), System.IO.Compression.CompressionMode.Decompress))
        {
            int read = 0;
            while (read < pixels.Length)
            {
                int n = input.Read(pixels, read, pixels.Length - read);
                if (n <= 0) break;
                read += n;
            }
            if (read != pixels.Length) return null;
        }
        if (format == 3)
        {
            if (alphaSampling == AlphaSampling.Unknown) alphaSampling = ProbeAlpha8();
            if (alphaSampling == AlphaSampling.Unsupported)
            {
                // expand to RGBA4444: white with the alpha in the low nibble
                var expanded = new byte[width * height * 2];
                for (int i = 0; i < pixels.Length; i++)
                {
                    int a = (pixels[i] * 15 + 127) / 255;
                    expanded[i * 2] = (byte)(0xF0 | a);
                    expanded[i * 2 + 1] = 0xFF;
                }
                pixels = expanded;
                format = 1;
            }
        }
        // MonoGame maps Bgra4444 / Bgra5551 to GL RGBA with UNSIGNED_SHORT_4_4_4_4 / 5_5_5_1 on OpenGL and OpenGL ES
        var tex2d = new Texture2D(device, width, height, false,
            format == 3 ? SurfaceFormat.Alpha8 : format == 2 ? SurfaceFormat.Bgra5551 : SurfaceFormat.Bgra4444);
        tex2d.SetData(pixels);
        return new ecTexture { handle = tex2d, pixelW = width, pixelH = height, w = width, h = height, refCount = 1 };
    }

    /// <summary>Lets the host turn the native ETC upload off (it is used whenever the GPU supports it).</summary>
    public static bool NativeEtc1Allowed = true;
    private bool? nativeEtc1;
    private SurfaceFormat etcFormat = SurfaceFormat.RgbEtc1;

    /// <summary>
    /// Whether the device takes the PKM data as it is. ETC1 data is valid ETC2, so ETC2 (every
    /// OpenGL ES 3 device, many desktop drivers) is preferred, then ETC1 (every OpenGL ES 2 device).
    /// </summary>
    private bool SupportsNativeEtc1()
    {
        if (nativeEtc1.HasValue) return nativeEtc1.Value;
        bool etc1 = false, etc2 = false, found = false;
        try
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            object caps = typeof(GraphicsDevice).GetProperty("GraphicsCapabilities", flags)?.GetValue(device);
            found = caps != null;
            etc1 = caps?.GetType().GetProperty("SupportsEtc1", flags)?.GetValue(caps) is bool b1 && b1;
            etc2 = caps?.GetType().GetProperty("SupportsEtc2", flags)?.GetValue(caps) is bool b2 && b2;
        }
        catch { }
        // debug override for testing the upload path on a desktop driver: WC2_FORCE_ETC=etc1|etc2
        string force = Environment.GetEnvironmentVariable("WC2_FORCE_ETC");
        if (force == "etc1") etc1 = true;
        if (force == "etc2") etc2 = true;
        etcFormat = etc2 ? SurfaceFormat.Rgb8Etc2 : SurfaceFormat.RgbEtc1;
        nativeEtc1 = (etc1 || etc2) && NativeEtc1Allowed;
        Log("Textures: native ETC upload " + (nativeEtc1.Value ? "on (" + etcFormat + ")" : "off, PKM files are decoded on the CPU")
            + $" [ETC1 {etc1}, ETC2 {etc2}, capabilities {(found ? "read" : "not found")}]");
        return nativeEtc1.Value;
    }

    /// <summary>A PKM file as an ETC texture; null (and CPU decoding from then on) when the upload fails.</summary>
    private Texture2D CreateEtc1Texture(byte[] pkm)
    {
        int extW = (pkm[8] << 8) | pkm[9];
        int extH = (pkm[10] << 8) | pkm[11];
        int size = (extW / 4) * (extH / 4) * 8;
        if (extW <= 0 || extH <= 0 || pkm.Length < 16 + size) return null;
        try
        {
            var tex = new Texture2D(device, extW, extH, false, etcFormat);
            tex.SetData(pkm, 16, size);
            return tex;
        }
        catch (Exception ex)
        {
            Log("Textures: native ETC1 upload failed (" + ex.Message + "), decoding on the CPU instead");
            nativeEtc1 = false;
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
