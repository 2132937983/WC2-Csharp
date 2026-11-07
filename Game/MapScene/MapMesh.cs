using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WC2;

/// <summary>
/// A map drawn from vertex buffers instead of image tiles and region masks (<c>map&lt;id&gt;.mesh</c>, written
/// by <c>WC2.Tools build-map</c>, MAP_PIPELINE.md): every area as triangles, every border as a line.
/// The fill colour of an area follows its owner; a border between two areas is a national border
/// (dark) when their owners differ and a province border (light) otherwise. Areas and borders are stored
/// in vertical strips (chunks), so only the strips in view are drawn, each set in one draw call.
/// </summary>
public sealed class CMapMesh : IDisposable
{
    // colours (0xAABBGGRR as ecGraphics uses them)
    public const uint SeaColor = 0xFF8A623Au;          // rgb 58, 98, 138
    private static readonly Color Land = new(200, 184, 146);
    private static readonly Color Sea = new(58, 98, 138);
    private static readonly Color Coast = new(40, 46, 52);
    private static readonly Color National = new(45, 34, 24, 235);
    private static readonly Color Province = new(105, 92, 70, 110);
    private static readonly Color SeaBorder = new(84, 124, 160, 160);

    public int regionCount;
    private int[] regionFirstVertex, regionVertexCount, regionFirstIndex, regionIndexCount;
    private VertexPositionColor[] fill;
    private int[] indices;
    private int[] chainLeft, chainRight, chainFirstLine, chainLineCount;
    private VertexPositionColor[] lines;
    private List<int>[] regionChains;
    private int[] chunkFirstIndex, chunkIndexCount, chunkFirstLine, chunkLineCount;
    private float[] chunkMinX, chunkMaxX;

    private GraphicsDevice device;
    // the background picture and the paper grain (display terrain / detail)
    private MapDisplay display;
    private ecTexture[] terrain;
    private ecTexture detail;
    private readonly ecQuad quad = new();
    private bool Translucent => display?.terrainFile != null;
    private DynamicVertexBuffer fillBuffer, lineBuffer;
    private IndexBuffer indexBuffer;
    private DynamicIndexBuffer setBuffer;
    private int setVersion = -1, setKey;
    private int setTriangles;

    // what the colours were made from: the owner and the sea flag of every area
    private CCountry[] owner;
    private bool[] isSea, ownerKnown;
    private int ownerVersion;
    private int fillDirtyMin = int.MaxValue, fillDirtyMax = -1, lineDirtyMin = int.MaxValue, lineDirtyMax = -1;

    /// <summary>Reads a mesh from the asset layer; null when there is none or it cannot be read.</summary>
    public static CMapMesh Load(string name, MapDisplay display = null)
    {
        if (!AssetSystem.Exists(name)) return null;
        try
        {
            using var r = new BinaryReader(new MemoryStream(AssetSystem.ReadAllBytes(name)));
            if (new string(r.ReadChars(4)) != "WCMH" || r.ReadInt32() != 2) return null;
            var m = new CMapMesh();
            int regions = r.ReadInt32(), vertexCount = r.ReadInt32(), indexCount = r.ReadInt32();
            int chains = r.ReadInt32(), pointCount = r.ReadInt32(), chunks = r.ReadInt32();
            m.regionCount = regions;
            var pos = new Vector2[vertexCount];
            for (int i = 0; i < vertexCount; i++) pos[i] = new Vector2(r.ReadUInt16(), r.ReadUInt16());
            var local = new ushort[indexCount];
            for (int i = 0; i < indexCount; i++) local[i] = r.ReadUInt16();
            m.regionFirstVertex = new int[regions]; m.regionVertexCount = new int[regions];
            m.regionFirstIndex = new int[regions]; m.regionIndexCount = new int[regions];
            for (int i = 0; i < regions; i++)
            {
                m.regionFirstVertex[i] = r.ReadInt32(); m.regionVertexCount[i] = r.ReadInt32();
                m.regionFirstIndex[i] = r.ReadInt32(); m.regionIndexCount[i] = r.ReadInt32();
            }
            // the indices are relative to their region's first vertex
            m.indices = new int[indexCount];
            for (int i = 0; i < regions; i++)
                for (int k = 0; k < m.regionIndexCount[i]; k++)
                    m.indices[m.regionFirstIndex[i] + k] = m.regionFirstVertex[i] + local[m.regionFirstIndex[i] + k];
            m.fill = new VertexPositionColor[vertexCount];
            for (int i = 0; i < vertexCount; i++) m.fill[i] = new VertexPositionColor(new Vector3(pos[i], 0f), Land);

            m.chainLeft = new int[chains]; m.chainRight = new int[chains];
            var chainFirstPoint = new int[chains]; var chainPoints = new int[chains];
            for (int i = 0; i < chains; i++)
            {
                m.chainLeft[i] = r.ReadInt32(); m.chainRight[i] = r.ReadInt32();
                chainFirstPoint[i] = r.ReadInt32(); chainPoints[i] = r.ReadInt32();
            }
            var points = new Vector2[pointCount];
            for (int i = 0; i < pointCount; i++) points[i] = new Vector2(r.ReadUInt16(), r.ReadUInt16());
            // the chains as a line list: (points - 1) lines each
            m.chainFirstLine = new int[chains]; m.chainLineCount = new int[chains];
            int lineCount = 0;
            for (int i = 0; i < chains; i++) { m.chainFirstLine[i] = lineCount; m.chainLineCount[i] = Math.Max(0, chainPoints[i] - 1); lineCount += m.chainLineCount[i]; }
            m.lines = new VertexPositionColor[lineCount * 2];
            m.regionChains = new List<int>[regions];
            for (int i = 0; i < chains; i++)
            {
                for (int k = 0; k < m.chainLineCount[i]; k++)
                {
                    int v = (m.chainFirstLine[i] + k) * 2;
                    m.lines[v] = new VertexPositionColor(new Vector3(points[chainFirstPoint[i] + k], 0f), Province);
                    m.lines[v + 1] = new VertexPositionColor(new Vector3(points[chainFirstPoint[i] + k + 1], 0f), Province);
                }
                if (m.chainLeft[i] >= 0) (m.regionChains[m.chainLeft[i]] ??= new List<int>()).Add(i);
                if (m.chainRight[i] >= 0) (m.regionChains[m.chainRight[i]] ??= new List<int>()).Add(i);
            }
            m.chunkFirstIndex = new int[chunks]; m.chunkIndexCount = new int[chunks];
            m.chunkFirstLine = new int[chunks]; m.chunkLineCount = new int[chunks];
            m.chunkMinX = new float[chunks]; m.chunkMaxX = new float[chunks];
            for (int c = 0; c < chunks; c++)
            {
                m.chunkFirstIndex[c] = r.ReadInt32(); m.chunkIndexCount[c] = r.ReadInt32();
                int firstChain = r.ReadInt32(), chainCount = r.ReadInt32();
                m.chunkFirstLine[c] = chainCount > 0 ? m.chainFirstLine[firstChain] : 0;
                m.chunkLineCount[c] = chainCount > 0 ? m.chainFirstLine[firstChain + chainCount - 1] + m.chainLineCount[firstChain + chainCount - 1] - m.chunkFirstLine[c] : 0;
                m.chunkMinX[c] = r.ReadSingle(); m.chunkMaxX[c] = r.ReadSingle();
            }
            m.display = display;
            if (display?.terrainFile != null)
                for (int i = 0; i < vertexCount; i++) m.fill[i].Color = Color.Transparent;
            m.owner = new CCountry[regions];
            m.isSea = new bool[regions];
            m.ownerKnown = new bool[regions];
            return m;
        }
        catch (Exception ex)
        {
            Log($"Map mesh {name} unreadable: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        fillBuffer?.Dispose(); lineBuffer?.Dispose(); indexBuffer?.Dispose(); setBuffer?.Dispose();
        fillBuffer = null; lineBuffer = null; indexBuffer = null; setBuffer = null;
        var g = ecGraphics.Instance();
        if (terrain != null) foreach (var t in terrain) if (t != null) g.FreeTexture(t);
        terrain = null;
        if (detail != null) g.FreeTexture(detail);
        detail = null;
    }

    /// <summary>Bytes of the GPU buffers (statistics).</summary>
    public long GpuBytes => (long)fill.Length * VertexPositionColor.VertexDeclaration.VertexStride
                          + (long)lines.Length * VertexPositionColor.VertexDeclaration.VertexStride + (long)indices.Length * 4;

    private void EnsureBuffers()
    {
        var g = ecGraphics.Instance().device;
        if (g == null) return;
        if (device == g && fillBuffer != null) return;
        Dispose();
        device = g;
        fillBuffer = new DynamicVertexBuffer(g, VertexPositionColor.VertexDeclaration, Math.Max(1, fill.Length), BufferUsage.WriteOnly);
        fillBuffer.SetData(fill);
        lineBuffer = new DynamicVertexBuffer(g, VertexPositionColor.VertexDeclaration, Math.Max(2, lines.Length), BufferUsage.WriteOnly);
        if (lines.Length > 0) lineBuffer.SetData(lines);
        indexBuffer = new IndexBuffer(g, IndexElementSize.ThirtyTwoBits, Math.Max(1, indices.Length), BufferUsage.WriteOnly);
        indexBuffer.SetData(indices);
        fillDirtyMin = lineDirtyMin = int.MaxValue; fillDirtyMax = lineDirtyMax = -1;
    }

    private static Color Blend(Color a, Color b, float t) =>
        new((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t), (byte)255);

    /// <summary>
    /// Takes over the owners of the scene's areas: changed areas get their new fill colour and their
    /// borders are classified again. Cheap when nothing changed (one comparison per area).
    /// </summary>
    public void SyncOwners(CScene scene)
    {
        int count = Math.Min(regionCount, scene.areaList.Count);
        for (int i = 0; i < count; i++)
        {
            CArea area = scene.areaList[i];
            if (area == null) continue;
            if (ownerKnown[i] && owner[i] == area.country && isSea[i] == area.info.sea) continue;
            ownerKnown[i] = true;
            owner[i] = area.country;
            isSea[i] = area.info.sea;
            ownerVersion++;
            Color c;
            if (Translucent)
            {
                // over the terrain picture: the owner's colour, translucent; nothing for the sea and unowned land
                if (area.info.sea || area.country == null || !area.info.enable) c = Color.Transparent;
                else
                {
                    var oc = new Color { PackedValue = area.country.ColorRGBA };
                    c = new Color(oc.R, oc.G, oc.B, (byte)(255 * display.ownerAlpha));
                }
            }
            else if (area.info.sea) c = Sea;
            else if (area.country == null || !area.info.enable) c = Land;
            else
            {
                // the owner's colour over the land, stronger than the classic map's tint (no texture below)
                var oc = new Color { PackedValue = area.country.ColorRGBA };
                c = Blend(Land, new Color(oc.R, oc.G, oc.B), Math.Min(0.85f, oc.A / 255f * 1.9f));
            }
            int first = regionFirstVertex[i], n = regionVertexCount[i];
            for (int k = 0; k < n; k++) fill[first + k].Color = c;
            if (n > 0) { fillDirtyMin = Math.Min(fillDirtyMin, first); fillDirtyMax = Math.Max(fillDirtyMax, first + n - 1); }
            if (regionChains[i] != null)
                foreach (int ch in regionChains[i]) ColourChain(ch);
        }
    }

    private void ColourChain(int ch)
    {
        int a = chainLeft[ch], b = chainRight[ch];
        bool aSea = a < 0 || isSea[a], bSea = b < 0 || isSea[b];
        Color c;
        if (a == -2 || b == -2) c = Color.Transparent;              // the map's edge (the seam of a wrapping map)
        else if (aSea && bSea) c = SeaBorder;
        else if (aSea != bSea) c = Coast;
        else c = owner[a] != owner[b] ? National : Province;
        int first = chainFirstLine[ch] * 2, n = chainLineCount[ch] * 2;
        for (int k = 0; k < n; k++) lines[first + k].Color = c;
        if (n > 0) { lineDirtyMin = Math.Min(lineDirtyMin, first); lineDirtyMax = Math.Max(lineDirtyMax, first + n - 1); }
    }

    private void Upload()
    {
        EnsureBuffers();
        if (fillBuffer == null) return;
        if (fillDirtyMax >= fillDirtyMin)
        {
            int stride = VertexPositionColor.VertexDeclaration.VertexStride;
            fillBuffer.SetData(fillDirtyMin * stride, fill, fillDirtyMin, fillDirtyMax - fillDirtyMin + 1, stride, SetDataOptions.None);
            fillDirtyMin = int.MaxValue; fillDirtyMax = -1;
        }
        if (lineDirtyMax >= lineDirtyMin)
        {
            int stride = VertexPositionColor.VertexDeclaration.VertexStride;
            lineBuffer.SetData(lineDirtyMin * stride, lines, lineDirtyMin, lineDirtyMax - lineDirtyMin + 1, stride, SetDataOptions.None);
            lineDirtyMin = int.MaxValue; lineDirtyMax = -1;
        }
    }

    /// <summary>The chunks overlapping [left, right] in map units: first and last (or -1 when none).</summary>
    private (int first, int last) VisibleChunks(float left, float right)
    {
        int first = -1, last = -1;
        for (int c = 0; c < chunkMinX.Length; c++)
        {
            if (chunkIndexCount[c] == 0 && chunkLineCount[c] == 0) continue;
            if (chunkMaxX[c] < left || chunkMinX[c] > right) continue;
            if (first < 0) first = c;
            last = c;
        }
        return (first, last);
    }

    /// <summary>
    /// Draws the map visible in [left, right] x [top, bottom]: the terrain picture (or the sea colour),
    /// the areas in their owners' colours, the paper grain, then the borders, soft (a faded second
    /// stroke one pixel beside) and sharp. Everything is clipped to the map (mapWidth x mapHeight), so the
    /// copies of a wrapping map, drawn one after another, never cover each other.
    /// </summary>
    public void Render(CScene scene, float left, float right, float top, float bottom, float mapWidth, float mapHeight, float cameraScale)
    {
        SyncOwners(scene);
        Upload();
        if (fillBuffer == null) return;
        var g = ecGraphics.Instance();
        float x0 = Math.Max(left, 0f), x1 = Math.Min(right, mapWidth), y0 = Math.Max(top, 0f), y1 = Math.Min(bottom, mapHeight);
        if (x1 <= x0 || y1 <= y0) return;
        if (Translucent) RenderTerrain(x0, x1, y0, y1, mapWidth, mapHeight);
        else g.RenderRect(x0, y0, x1 - x0, y1 - y0, SeaColor);   // the water below everything (lakes without an area)
        var (c0, c1) = VisibleChunks(left, right);
        if (c0 < 0) return;
        int firstIndex = chunkFirstIndex[c0], lastIndex = firstIndex;
        int firstLine = int.MaxValue, lastLine = 0;
        for (int c = c0; c <= c1; c++)
        {
            if (chunkIndexCount[c] > 0) { firstIndex = Math.Min(firstIndex, chunkFirstIndex[c]); lastIndex = Math.Max(lastIndex, chunkFirstIndex[c] + chunkIndexCount[c]); }
            if (chunkLineCount[c] > 0) { firstLine = Math.Min(firstLine, chunkFirstLine[c]); lastLine = Math.Max(lastLine, chunkFirstLine[c] + chunkLineCount[c]); }
        }
        g.DrawMesh(fillBuffer, indexBuffer, firstIndex, (lastIndex - firstIndex) / 3);
        if (display?.detailFile != null && cameraScale >= display.detailMinZoom) RenderGrain(x0, x1, y0, y1, cameraScale);
        if (lastLine > firstLine)
        {
            if (Translucent) g.DrawLines(lineBuffer, firstLine * 2, lastLine - firstLine, 1f, 0.45f);
            g.DrawLines(lineBuffer, firstLine * 2, lastLine - firstLine);
        }
    }

    /// <summary>The terrain tiles over [x0, x1] x [y0, y1] (loaded when first seen).</summary>
    private void RenderTerrain(float x0, float x1, float y0, float y1, float mapWidth, float mapHeight)
    {
        var g = ecGraphics.Instance();
        int cols = display.terrainColumns, rows = display.terrainRows;
        terrain ??= new ecTexture[cols * rows];
        float tw = mapWidth / cols, th = mapHeight / rows;
        for (int r = Math.Max(0, (int)(y0 / th)); r <= Math.Min(rows - 1, (int)(y1 / th)); r++)
            for (int c = Math.Max(0, (int)(x0 / tw)); c <= Math.Min(cols - 1, (int)(x1 / tw)); c++)
            {
                int i = r * cols + c;
                terrain[i] ??= g.LoadTexture(string.Format(System.Globalization.CultureInfo.InvariantCulture, display.terrainFile, i));
                if (terrain[i]?.handle == null) { g.RenderRect(c * tw, r * th, tw, th, SeaColor); continue; }
                Quad(terrain[i], c * tw, r * th, tw, th, 0f, 0f, 1f, 1f, 0xFFFFFFFFu);
            }
    }

    /// <summary>The paper grain: a tile repeated over [x0, x1] x [y0, y1], fading in above detailMinZoom.</summary>
    private void RenderGrain(float x0, float x1, float y0, float y1, float cameraScale)
    {
        var g = ecGraphics.Instance();
        detail ??= g.LoadTexture(display.detailFile);
        if (detail?.handle == null) return;
        float fade = Math.Clamp((cameraScale - display.detailMinZoom) / display.detailMinZoom, 0f, 1f);
        uint color = ((uint)(fade * 255f) << 24) | 0x00FFFFFFu;
        float s = display.detailUnits;
        for (float y = MathF.Floor(y0 / s) * s; y < y1; y += s)
            for (float x = MathF.Floor(x0 / s) * s; x < x1; x += s)
                Quad(detail, x, y, s, s, 0f, 0f, 1f, 1f, color);
    }

    private void Quad(ecTexture texture, float x, float y, float w, float h, float u0, float v0, float u1, float v1, uint color)
    {
        quad.texture = texture;
        quad.blendMode = 0;
        quad.x[0] = x; quad.y[0] = y; quad.u[0] = u0; quad.v[0] = v0;
        quad.x[1] = x + w; quad.y[1] = y; quad.u[1] = u1; quad.v[1] = v0;
        quad.x[2] = x + w; quad.y[2] = y + h; quad.u[2] = u1; quad.v[2] = v1;
        quad.x[3] = x; quad.y[3] = y + h; quad.u[3] = u0; quad.v[3] = v1;
        for (int k = 0; k < 4; k++) quad.color[k] = color;
        ecGraphics.Instance().RenderQuad(quad);
    }

    /// <summary>One area in a single colour (selection, fights); 0xAABBGGRR.</summary>
    public void RenderRegion(int region, uint color)
    {
        if (region < 0 || region >= regionCount || regionIndexCount[region] == 0 || fillBuffer == null) return;
        ecGraphics.Instance().DrawMesh(fillBuffer, indexBuffer, regionFirstIndex[region], regionIndexCount[region] / 3, color);
    }

    /// <summary>
    /// The areas of one country in a single colour (the new round flash, the conquest preview); the
    /// triangles of the set are gathered once per owner change into an index buffer.
    /// </summary>
    public void RenderCountry(CCountry country, uint color)
    {
        if (country == null || fillBuffer == null) return;
        int key = country.GetHashCode();
        if (setVersion != ownerVersion || setKey != key)
        {
            var list = new List<int>();
            for (int i = 0; i < regionCount; i++)
                if (owner[i] == country && !isSea[i])
                    for (int k = 0; k < regionIndexCount[i]; k++) list.Add(indices[regionFirstIndex[i] + k]);
            setTriangles = list.Count / 3;
            if (setBuffer == null || setBuffer.IndexCount < Math.Max(3, list.Count))
            {
                setBuffer?.Dispose();
                setBuffer = new DynamicIndexBuffer(device, IndexElementSize.ThirtyTwoBits, Math.Max(3, list.Count), BufferUsage.WriteOnly);
            }
            if (list.Count > 0) setBuffer.SetData(list.ToArray());
            setVersion = ownerVersion; setKey = key;
        }
        if (setTriangles > 0) ecGraphics.Instance().DrawMesh(fillBuffer, setBuffer, 0, setTriangles, color);
    }
}
