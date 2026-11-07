using LibTessDotNet;

namespace WC2.Tools;

/// <summary>
/// The vector map of a generated map (<c>map&lt;id&gt;.mesh</c>): every region as triangles and every border
/// as a polyline, drawn by the game from vertex buffers instead of image tiles and region masks
/// (MAP_PIPELINE.md, "The map as a mesh").
/// <para>The borders are traced once from the region raster: a chain is a run of pixel edges between the
/// same two regions from one junction (a corner where three regions meet) to the next. Each chain is
/// simplified (Douglas-Peucker) and smoothed (Chaikin) once, and both regions build their outline from
/// that same chain, so neighbours meet without gaps or overlaps. The outlines (holes included) are
/// triangulated with LibTessDotNet.</para>
/// File (little endian): "WCMH", version 2, region count, vertex count, index count, chain count, chain
/// point count, chunk count; vertices (uint16 x, y in map units); indices (uint16 triangles, relative to
/// the first vertex of their region, regions in file order); per region (by id) first vertex, vertex
/// count, first index, index count; per chain the two sides (region id, -1 water without a region, -2
/// outside the map), first point, point count; chain points (uint16 x, y); per chunk (a vertical strip
/// of the map) first index, index count, first chain, chain count, min x, max x (float).
/// </summary>
internal static class MapMeshExport
{
    private const int Outside = -1;   // raster value outside the map (regions are 1.., water without a region 0)

    public static (int vertices, int triangles, int chains, int points) Write(string path, int[] raster, int width, int height,
                                                                            float unitsPerPixel, int regionCount, double tolerance, int smoothing, float chunkWidth)
    {
        int P(int x, int y) => x < 0 || y < 0 || x >= width || y >= height ? Outside : raster[y * width + x];
        int cw = width + 1;
        // a corner is a junction when three or more values meet there, or two in a checkerboard (saddle)
        bool IsNode(int cx, int cy)
        {
            int tl = P(cx - 1, cy - 1), tr = P(cx, cy - 1), bl = P(cx - 1, cy), br = P(cx, cy);
            int distinct = 1;
            if (tr != tl) distinct++;
            if (bl != tl && bl != tr) distinct++;
            if (br != tl && br != tr && br != bl) distinct++;
            if (distinct >= 3) return true;
            return distinct == 2 && tl == br && tr == bl && tl != tr;
        }
        // boundary edges: horizontal at corner row cy between columns cx and cx+1; vertical at corner
        // column cx between rows cy and cy+1
        bool HEdge(int cx, int cy) => cx >= 0 && cx < width && cy >= 0 && cy <= height && P(cx, cy - 1) != P(cx, cy);
        bool VEdge(int cx, int cy) => cy >= 0 && cy < height && cx >= 0 && cx <= width && P(cx - 1, cy) != P(cx, cy);
        var hDone = new bool[(height + 1) * width];
        var vDone = new bool[(width + 1) * height];
        var chains = new List<(int left, int right, List<(int x, int y)> pts)>();

        // directions: 0 right (+x), 1 down (+y), 2 left (-x), 3 up (-y); the value on the left of a step
        int LeftOf(int cx, int cy, int dir) => dir switch
        {
            0 => P(cx, cy - 1),       // moving right along row cy: the pixel above
            1 => P(cx, cy),           // moving down along column cx: the pixel to the right
            2 => P(cx - 1, cy),       // moving left (from cx to cx-1): the pixel below
            _ => P(cx - 1, cy - 1),   // moving up (from cy to cy-1): the pixel to the left
        };
        int RightOf(int cx, int cy, int dir) => dir switch
        {
            0 => P(cx, cy),
            1 => P(cx - 1, cy),
            2 => P(cx - 1, cy - 1),
            _ => P(cx, cy - 1),
        };
        bool EdgeFrom(int cx, int cy, int dir) => dir switch
        {
            0 => HEdge(cx, cy),
            1 => VEdge(cx, cy),
            2 => HEdge(cx - 1, cy),
            _ => VEdge(cx, cy - 1),
        };
        bool Done(int cx, int cy, int dir) => dir switch
        {
            0 => hDone[cy * width + cx],
            1 => vDone[cx * height + cy],
            2 => hDone[cy * width + cx - 1],
            _ => vDone[cx * height + cy - 1],
        };
        void Mark(int cx, int cy, int dir)
        {
            switch (dir)
            {
                case 0: hDone[cy * width + cx] = true; break;
                case 1: vDone[cx * height + cy] = true; break;
                case 2: hDone[cy * width + cx - 1] = true; break;
                default: vDone[cx * height + cy - 1] = true; break;
            }
        }
        (int, int) Step(int cx, int cy, int dir) => dir switch { 0 => (cx + 1, cy), 1 => (cx, cy + 1), 2 => (cx - 1, cy), _ => (cx, cy - 1) };

        void Trace(int sx, int sy, int dir)
        {
            int left = LeftOf(sx, sy, dir), right = RightOf(sx, sy, dir);
            var pts = new List<(int, int)> { (sx, sy) };
            int cx = sx, cy = sy;
            while (true)
            {
                Mark(cx, cy, dir);
                (cx, cy) = Step(cx, cy, dir);
                pts.Add((cx, cy));
                if ((cx == sx && cy == sy) || IsNode(cx, cy)) break;
                // the one other boundary edge at this corner that keeps the same two sides
                int next = -1;
                for (int d = 0; d < 4; d++)
                {
                    if (d == (dir + 2) % 4 || !EdgeFrom(cx, cy, d)) continue;
                    if (LeftOf(cx, cy, d) == left && RightOf(cx, cy, d) == right) { next = d; break; }
                }
                if (next < 0) break;
                dir = next;
            }
            if (left == Outside && right == Outside) return;
            chains.Add((left, right, pts));
        }
        // chains from the junctions, then the closed loops without one
        for (int cy = 0; cy <= height; cy++)
            for (int cx = 0; cx <= width; cx++)
            {
                if (!IsNode(cx, cy)) continue;
                for (int d = 0; d < 4; d++)
                    if (EdgeFrom(cx, cy, d) && !Done(cx, cy, d)) Trace(cx, cy, d);
            }
        for (int cy = 0; cy <= height; cy++)
            for (int cx = 0; cx < width; cx++)
                if (HEdge(cx, cy) && !hDone[cy * width + cx])
                {
                    // walk right along this edge (the loop has no junction, any start works)
                    Trace(cx, cy, 0);
                }
        for (int cx = 0; cx <= width; cx++)
            for (int cy = 0; cy < height; cy++)
                if (VEdge(cx, cy) && !vDone[cx * height + cy]) Trace(cx, cy, 1);

        // simplify and smooth every chain once (its two ends stay where they are)
        var shapes = new List<(int left, int right, List<(float x, float y)> pts)>(chains.Count);
        foreach (var (left, right, pts) in chains)
        {
            var simple = Simplify(pts, tolerance);
            var smooth = simple.Select(p => ((float)p.x, (float)p.y)).ToList();
            for (int s = 0; s < smoothing && smooth.Count > 2; s++) smooth = Chaikin(smooth, pts[0] == pts[^1]);
            shapes.Add((left, right, smooth.Select(p => (p.Item1 * unitsPerPixel, p.Item2 * unitsPerPixel)).ToList()));
        }

        // outlines per region: its chains with the region on the left, joined end to start
        var byRegion = new List<(List<(float x, float y)> pts, bool reverse)>[regionCount + 1];
        foreach (var c in shapes)
        {
            if (c.left > 0) (byRegion[c.left] ??= new()).Add((c.pts, false));
            if (c.right > 0) (byRegion[c.right] ??= new()).Add((c.pts, true));
        }
        var regionVerts = new List<float>[regionCount];
        var regionIdx = new List<int>[regionCount];
        var minX = new float[regionCount];
        var maxX = new float[regionCount];
        int empty = 0;
        for (int r = 1; r <= regionCount; r++)
        {
            regionVerts[r - 1] = new List<float>();
            regionIdx[r - 1] = new List<int>();
            minX[r - 1] = float.MaxValue; maxX[r - 1] = float.MinValue;
            var parts = byRegion[r];
            if (parts == null) continue;
            var directed = parts.Select(p => p.reverse ? Enumerable.Reverse(p.pts).ToList() : p.pts).ToList();
            var starts = new Dictionary<(float, float), List<int>>();
            for (int i = 0; i < directed.Count; i++)
            {
                var key = directed[i][0];
                if (!starts.TryGetValue(key, out var l)) starts[key] = l = new List<int>();
                l.Add(i);
            }
            var used = new bool[directed.Count];
            var tess = new Tess();
            int contours = 0;
            for (int i0 = 0; i0 < directed.Count; i0++)
            {
                if (used[i0]) continue;
                var ring = new List<(float x, float y)>();
                int i = i0;
                var start = directed[i0][0];
                while (i >= 0 && !used[i])
                {
                    used[i] = true;
                    var seg = directed[i];
                    for (int k = 0; k < seg.Count - 1; k++) ring.Add(seg[k]);
                    var end = seg[^1];
                    if (end == start) break;
                    i = -1;
                    if (starts.TryGetValue(end, out var next))
                        foreach (int j in next) if (!used[j]) { i = j; break; }
                }
                if (ring.Count < 3) continue;
                tess.AddContour(ring.Select(p => new ContourVertex(new Vec3(p.x, p.y, 0))).ToArray(), ContourOrientation.Original);
                contours++;
            }
            if (contours == 0) { empty++; continue; }
            tess.Tessellate(WindingRule.NonZero, ElementType.Polygons, 3);
            if (tess.Vertices == null || tess.ElementCount == 0) { empty++; continue; }
            var verts = regionVerts[r - 1];
            foreach (var v in tess.Vertices)
            {
                verts.Add(v.Position.X); verts.Add(v.Position.Y);
                minX[r - 1] = Math.Min(minX[r - 1], v.Position.X); maxX[r - 1] = Math.Max(maxX[r - 1], v.Position.X);
            }
            for (int k = 0; k < tess.ElementCount * 3; k++) regionIdx[r - 1].Add(tess.Elements[k]);
        }

        if (empty > 0) Console.WriteLine($"build-map: mesh: {empty} regions without an outline (smaller than the simplification)");
        // chunks: vertical strips; regions and chains are stored strip by strip so the game draws only
        // the strips in view
        int chunkCount = Math.Max(1, (int)Math.Ceiling(width * unitsPerPixel / chunkWidth));
        int ChunkOf(float x) => Math.Clamp((int)(x / chunkWidth), 0, chunkCount - 1);
        var regionOrder = Enumerable.Range(0, regionCount)
            .OrderBy(r => regionIdx[r].Count == 0 ? 0 : ChunkOf((minX[r] + maxX[r]) * 0.5f)).ThenBy(r => r).ToList();
        var chainOrder = Enumerable.Range(0, shapes.Count)
            .OrderBy(c => ChunkOf((shapes[c].pts.Min(p => p.x) + shapes[c].pts.Max(p => p.x)) * 0.5f)).ThenBy(c => c).ToList();

        var regionFirstVertex = new int[regionCount]; var regionFirstIndex = new int[regionCount];
        var vertexList = new List<float>(); var indexList = new List<int>();
        var chunkFirstIndex = new int[chunkCount]; var chunkIndexCount = new int[chunkCount];
        var chunkMin = Enumerable.Repeat(float.MaxValue, chunkCount).ToArray(); var chunkMax = Enumerable.Repeat(float.MinValue, chunkCount).ToArray();
        for (int c = 0; c < chunkCount; c++) chunkFirstIndex[c] = -1;
        foreach (int r in regionOrder)
        {
            int baseVertex = vertexList.Count / 2;
            regionFirstVertex[r] = baseVertex;
            regionFirstIndex[r] = indexList.Count;
            vertexList.AddRange(regionVerts[r]);
            foreach (int k in regionIdx[r]) indexList.Add(baseVertex + k);
            if (regionIdx[r].Count == 0) continue;
            int chunk = ChunkOf((minX[r] + maxX[r]) * 0.5f);
            if (chunkFirstIndex[chunk] < 0) chunkFirstIndex[chunk] = regionFirstIndex[r];
            chunkIndexCount[chunk] += regionIdx[r].Count;
            chunkMin[chunk] = Math.Min(chunkMin[chunk], minX[r]);
            chunkMax[chunk] = Math.Max(chunkMax[chunk], maxX[r]);
        }
        var chunkFirstChain = new int[chunkCount]; var chunkChainCount = new int[chunkCount];
        for (int c = 0; c < chunkCount; c++) chunkFirstChain[c] = -1;
        for (int k = 0; k < chainOrder.Count; k++)
        {
            var pts = shapes[chainOrder[k]].pts;
            float lo = pts.Min(p => p.x), hi = pts.Max(p => p.x);
            int chunk = ChunkOf((lo + hi) * 0.5f);
            if (chunkFirstChain[chunk] < 0) chunkFirstChain[chunk] = k;
            chunkChainCount[chunk]++;
            chunkMin[chunk] = Math.Min(chunkMin[chunk], lo);
            chunkMax[chunk] = Math.Max(chunkMax[chunk], hi);
        }

        using var w = new BinaryWriter(File.Create(path));
        w.Write("WCMH".ToCharArray());
        w.Write(2);
        int pointCount = shapes.Sum(s => s.pts.Count);
        w.Write(regionCount); w.Write(vertexList.Count / 2); w.Write(indexList.Count); w.Write(shapes.Count); w.Write(pointCount); w.Write(chunkCount);
        // positions in whole map units (uint16; a shared border point rounds the same way on both sides)
        static ushort U(float v) => (ushort)Math.Clamp((int)Math.Round(v), 0, ushort.MaxValue);
        foreach (float f in vertexList) w.Write(U(f));
        // indices relative to the region's first vertex (uint16)
        foreach (int r in regionOrder)
        {
            if (regionVerts[r].Count / 2 > ushort.MaxValue) throw new ToolException($"region {r} has more than 65535 outline vertices");
            foreach (int k in regionIdx[r]) w.Write((ushort)k);
        }
        for (int r = 0; r < regionCount; r++)
        {
            w.Write(regionFirstVertex[r]); w.Write(regionVerts[r].Count / 2);
            w.Write(regionFirstIndex[r]); w.Write(regionIdx[r].Count);
        }
        int first = 0;
        foreach (int c in chainOrder)
        {
            var s = shapes[c];
            w.Write(s.left > 0 ? s.left - 1 : s.left == 0 ? -1 : -2);
            w.Write(s.right > 0 ? s.right - 1 : s.right == 0 ? -1 : -2);
            w.Write(first); w.Write(s.pts.Count);
            first += s.pts.Count;
        }
        foreach (int c in chainOrder) foreach (var (x, y) in shapes[c].pts) { w.Write(U(x)); w.Write(U(y)); }
        for (int c = 0; c < chunkCount; c++)
        {
            w.Write(Math.Max(0, chunkFirstIndex[c])); w.Write(chunkIndexCount[c]);
            w.Write(Math.Max(0, chunkFirstChain[c])); w.Write(chunkChainCount[c]);
            w.Write(chunkMin[c] == float.MaxValue ? 0f : chunkMin[c]); w.Write(chunkMax[c] == float.MinValue ? 0f : chunkMax[c]);
        }
        return (vertexList.Count / 2, indexList.Count / 3, shapes.Count, pointCount);
    }

    /// <summary>Douglas-Peucker, iterative; the first and the last point stay.</summary>
    private static List<(int x, int y)> Simplify(List<(int x, int y)> pts, double tolerance)
    {
        if (pts.Count <= 2) return pts;
        bool closed = pts[0] == pts[^1];
        var keep = new bool[pts.Count];
        keep[0] = keep[^1] = true;
        var stack = new Stack<(int a, int b)>();
        if (closed && pts.Count > 3)
        {
            // a loop: split at the point farthest from the start so both halves have distinct ends
            int far = 1; double best = -1;
            for (int i = 1; i < pts.Count - 1; i++)
            {
                double d = (pts[i].x - pts[0].x) * (double)(pts[i].x - pts[0].x) + (pts[i].y - pts[0].y) * (double)(pts[i].y - pts[0].y);
                if (d > best) { best = d; far = i; }
            }
            keep[far] = true;
            stack.Push((0, far)); stack.Push((far, pts.Count - 1));
        }
        else stack.Push((0, pts.Count - 1));
        while (stack.Count > 0)
        {
            var (a, b) = stack.Pop();
            if (b - a < 2) continue;
            double ax = pts[a].x, ay = pts[a].y, bx = pts[b].x, by = pts[b].y;
            double dx = bx - ax, dy = by - ay, len = Math.Sqrt(dx * dx + dy * dy);
            int idx = -1; double max = -1;
            for (int i = a + 1; i < b; i++)
            {
                double d = len < 1e-9
                    ? Math.Sqrt((pts[i].x - ax) * (pts[i].x - ax) + (pts[i].y - ay) * (pts[i].y - ay))
                    : Math.Abs(dy * pts[i].x - dx * pts[i].y + bx * ay - by * ax) / len;
                if (d > max) { max = d; idx = i; }
            }
            if (max > tolerance) { keep[idx] = true; stack.Push((a, idx)); stack.Push((idx, b)); }
        }
        var result = new List<(int, int)>();
        for (int i = 0; i < pts.Count; i++) if (keep[i]) result.Add(pts[i]);
        return result;
    }

    /// <summary>One round of Chaikin corner cutting; the ends stay (a loop keeps its start point).</summary>
    private static List<(float, float)> Chaikin(List<(float x, float y)> pts, bool closed)
    {
        var result = new List<(float, float)> { pts[0] };
        for (int i = 0; i < pts.Count - 1; i++)
        {
            var (x0, y0) = pts[i]; var (x1, y1) = pts[i + 1];
            if (i > 0) result.Add((x0 * 0.75f + x1 * 0.25f, y0 * 0.75f + y1 * 0.25f));
            if (i < pts.Count - 2) result.Add((x0 * 0.25f + x1 * 0.75f, y0 * 0.25f + y1 * 0.75f));
        }
        result.Add(pts[^1]);
        return result;
    }
}
