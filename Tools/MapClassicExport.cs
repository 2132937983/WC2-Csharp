using System.Text.Json;
using System.Text.Json.Nodes;
using SkiaSharp;

namespace WC2.Tools;

/// <summary>
/// build-map --classic-out DIR: the generated map in the formats the game's scene already reads for
/// map 1 (MAP_PIPELINE.md, "Playing on the generated map"), written as a mod:
/// <list type="bullet">
/// <item><c>area&lt;id&gt;.bin</c>: per region the zone image position and size, the army, building and
/// installation anchors, land or sea (map units = raster pixels x <c>scale</c>);</item>
/// <item><c>adjion&lt;id&gt;.bin</c>: up to 16 neighbours per region (the longest borders, straits and the
/// seam of a wrapping map included);</item>
/// <item><c>areamark&lt;id&gt;.raw</c>: the region under every 8x8 block of map units (touch picking);</item>
/// <item><c>data/json/maps/&lt;id&gt;/areas.json</c>: area types and taxes of every area, sea zones included;</item>
/// <item><c>map&lt;id&gt;_&lt;n&gt;.png</c>: the background in 500 unit tiles (land, sea, coasts, region and sea
/// zone borders; no country borders, the map serves every era) and <c>m&lt;id&gt;_zone&lt;n&gt;_1.png/.xml</c>:
/// atlases of the region masks the game tints with the owner's colour;</item>
/// <item><c>generated/texture_formats.json</c>: tiles uploaded as RGB565, masks as Alpha8;</item>
/// <item>one conquest per entry of <c>scenarios</c>: the scenario XML (owners, cities, armies, fleets),
/// its conquest list entry (fogOfWar, leftRightWrap), button and menu texts.</item>
/// </list>
/// </summary>
internal static partial class MapPipeline
{
    /// <summary>The conquest list button: 320 x 109 pixels like the original's, named @2x (the menu atlases
    /// are high resolution: a sprite shows at half its pixel size); the map around the centre, a dark
    /// band and the title.</summary>
    private static void WriteConquestButton(string path, int[] raster, int width, int height, List<Region> regions, string title, int cx, int cy, int step)
    {
        const int W = 320, H = 109;
        using var bmp = new SKBitmap(W, H, SKColorType.Rgba8888, SKAlphaType.Premul);
        // 1 button pixel = step raster pixels around the centre
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int rx = ((cx + (x - W / 2) * step) % width + width) % width, ry = Math.Clamp(cy + (y - H / 2) * step, 0, height - 1);
                int v = raster[ry * width + rx];
                bool sea = v == 0 || regions[v - 1].sea;
                int right = raster[ry * width + Math.Min(width - 1, rx + step)];
                bool edge = right != v;
                var c = sea ? new SKColor(40, 70, 100) : new SKColor(170, 150, 110);
                if (edge && !sea) c = new SKColor(90, 70, 50);
                bmp.SetPixel(x, y, c);
            }
        using var canvas = new SKCanvas(bmp);
        using var band = new SKPaint { Color = new SKColor(0, 0, 0, 150) };
        canvas.DrawRect(0, 18, W, 66, band);
        using var frame = new SKPaint { Color = new SKColor(60, 45, 30), Style = SKPaintStyle.Stroke, StrokeWidth = 4 };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(2, 2, W - 2, H - 2), 8), frame);
        using var bold = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 22);
        using var regular = new SKFont(SKTypeface.FromFamilyName("Arial"), 18);
        using var white = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawText("CONQUER", 12, 44, SKTextAlign.Left, bold, white);
        canvas.DrawText(title, 12, 72, SKTextAlign.Left, regular, white);
        using var data = bmp.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);
        data.SaveTo(file);
    }

    /// <param name="scale">map units per pixel of <paramref name="raster"/> (1: the raster is drawn at map resolution)</param>
    private static void ExportClassic(string outDir, JsonObject config, List<Region> regions, int[] raster, int width, int height,
                                      int scale, List<ScenarioSpec> scenarios, bool wrap, string source = null)
    {
        var cfg = config["classicExport"]!.AsObject();
        int mapId = cfg["mapId"]!.GetValue<int>();
        int S = scale;
        int mapW = width * S, mapH = height * S;
        // a wrapping map repeats every mapW units: tiles (500), the area mark (8) and the scene's
        // width rounding (100) must all end exactly at the edge
        if (wrap && mapW % 1000 != 0) throw new ToolException($"a wrapping map needs a width in map units divisible by 1000 (is {mapW})");
        Directory.CreateDirectory(outDir);
        int n = regions.Count;

        // ---- region boxes and an interior anchor (the pixel farthest from the region's edge)
        var minX = Enumerable.Repeat(int.MaxValue, n).ToArray(); var minY = Enumerable.Repeat(int.MaxValue, n).ToArray();
        var maxX = new int[n]; var maxY = new int[n];
        var dist = new int[raster.Length];
        var queue = new Queue<int>();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x, v = raster[i];
                if (v == 0) continue;
                int r = v - 1;
                if (x < minX[r]) minX[r] = x; if (x > maxX[r]) maxX[r] = x;
                if (y < minY[r]) minY[r] = y; if (y > maxY[r]) maxY[r] = y;
                bool edge = x == 0 || y == 0 || x == width - 1 || y == height - 1 || raster[i - 1] != v || raster[i + 1] != v || raster[i - width] != v || raster[i + width] != v;
                if (edge) { dist[i] = 1; queue.Enqueue(i); }
            }
        while (queue.Count > 0)
        {
            int i = queue.Dequeue(), x = i % width, y = i / width, v = raster[i];
            foreach (int j in new[] { x > 0 ? i - 1 : -1, x < width - 1 ? i + 1 : -1, y > 0 ? i - width : -1, y < height - 1 ? i + width : -1 })
                if (j >= 0 && raster[j] == v && dist[j] == 0) { dist[j] = dist[i] + 1; queue.Enqueue(j); }
        }
        var anchor = new (int x, int y)[n];
        var best = new int[n];
        for (int i = 0; i < raster.Length; i++)
        {
            int v = raster[i];
            if (v == 0) continue;
            if (dist[i] > best[v - 1]) { best[v - 1] = dist[i]; anchor[v - 1] = (i % width, i / width); }
        }
        // a region over the seam of a wrapping map (pixels at both ends): its box is the whole width
        if (wrap)
        {
            var nearLeft = new bool[n]; var nearRight = new bool[n];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width / 8; x++) { int v = raster[y * width + x]; if (v > 0) nearLeft[v - 1] = true; }
                for (int x = width - width / 8; x < width; x++) { int v = raster[y * width + x]; if (v > 0) nearRight[v - 1] = true; }
            }
            for (int r = 0; r < n; r++)
                if (nearLeft[r] && nearRight[r] && maxX[r] - minX[r] > width / 2) { minX[r] = 0; maxX[r] = width - 1; }
        }
        var missing = Enumerable.Range(0, n).Where(r => minX[r] == int.MaxValue).ToList();
        if (missing.Count > 0) throw new ToolException($"regions without pixels at map resolution: {string.Join(", ", missing.Take(10))}");

        // harbours: as in the original, a port is the sea zone off the port city (the longest shared
        // border), where ships are built and heal; its building anchor (the port icon) sits on that coast
        var harbour = new int[n];
        Array.Fill(harbour, -1);
        foreach (var r in regions.Where(r => !r.sea && r.port).OrderBy(r => r.id))
        {
            int sea = r.border.Where(kv => regions[kv.Key].sea).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Select(kv => kv.Key).DefaultIfEmpty(-1).First();
            if (sea >= 0 && harbour[sea] < 0) harbour[sea] = r.id;
        }
        var coastX = new long[n]; var coastY = new long[n]; var coastCount = new long[n];
        for (int y = 0; y < height; y++)
            for (int x = 0; x + 1 < width; x++)
            {
                int i = y * width + x;
                foreach (int j in new[] { i + 1, y + 1 < height ? i + width : -1 })
                {
                    if (j < 0) continue;
                    int ra = raster[i] - 1, rb = raster[j] - 1;
                    if (ra < 0 || rb < 0 || ra == rb) continue;
                    int seaSide = harbour[ra] == rb ? ra : harbour[rb] == ra ? rb : -1;
                    if (seaSide < 0) continue;
                    coastX[seaSide] += x; coastY[seaSide] += y; coastCount[seaSide]++;
                }
            }

        // ---- area<id>.bin
        using (var w = new BinaryWriter(File.Create(Path.Combine(outDir, $"area{mapId}.bin"))))
        {
            w.Write(n);
            for (int r = 0; r < n; r++)
            {
                int ax = anchor[r].x * S + S / 2, ay = anchor[r].y * S + S / 2;
                w.Write(minX[r] * S); w.Write(minY[r] * S);
                w.Write((maxX[r] - minX[r] + 1) * S); w.Write((maxY[r] - minY[r] + 1) * S);
                w.Write(ax); w.Write(ay + 10);           // army (stands on the anchor)
                if (harbour[r] >= 0 && coastCount[r] > 0)
                {
                    // a harbour: the port icon on the coast of its port city
                    w.Write((int)(coastX[r] / coastCount[r]) * S + S / 2); w.Write((int)(coastY[r] / coastCount[r]) * S + S / 2);
                }
                else { w.Write(ax - 34); w.Write(ay - 22); }   // building, up left of the army
                w.Write(ax - 40); w.Write(ay + 4);       // installation, left of the army
                w.Write(regions[r].sea ? 1 : 0);
            }
        }

        // ---- adjion<id>.bin: every neighbour (version 2: "WCAJ", 2, count, most neighbours, then per area
        // the count and the neighbours) when some area has more than the original format's 16 (a coast of
        // many small provinces, straits); otherwise the 16 longest borders kept by both ends, as before
        int mostNeighbours = regions.Max(r => r.border.Count);
        if (mostNeighbours > 16)
        {
            using var w2 = new BinaryWriter(File.Create(Path.Combine(outDir, $"adjion{mapId}.bin")));
            w2.Write("WCAJ".ToCharArray()); w2.Write(2); w2.Write(n); w2.Write(mostNeighbours);
            for (int r = 0; r < n; r++)
            {
                var list = regions[r].border.Keys.OrderBy(o => o).ToList();
                w2.Write(list.Count);
                for (int k = 0; k < mostNeighbours; k++) w2.Write(k < list.Count ? list[k] : 0);
            }
        }
        else
        {
        var keep = new HashSet<(int, int)>();
        var top = regions.Select(r => r.border.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Take(16).Select(kv => kv.Key).ToHashSet()).ToArray();
        for (int r = 0; r < n; r++)
            foreach (int o in top[r]) if (top[o].Contains(r)) keep.Add((r, o));
        using (var w = new BinaryWriter(File.Create(Path.Combine(outDir, $"adjion{mapId}.bin"))))
        {
            w.Write(n);
            for (int r = 0; r < n; r++)
            {
                var list = top[r].Where(o => keep.Contains((r, o))).OrderBy(o => o).ToList();
                w.Write(list.Count);
                for (int k = 0; k < 16; k++) w.Write(k < list.Count ? list[k] : 0);
            }
        }
        }

        // ---- areamark<id>.raw
        int markW = (mapW + 7) / 8, markH = (mapH + 7) / 8;
        using (var w = new BinaryWriter(File.Create(Path.Combine(outDir, $"areamark{mapId}.raw"))))
        {
            w.Write(markW); w.Write(markH);
            for (int my = 0; my < markH; my++)
                for (int mx = 0; mx < markW; mx++)
                {
                    int x = Math.Min(width - 1, (mx * 8 + 4) / S), y = Math.Min(height - 1, (my * 8 + 4) / S);
                    int v = raster[y * width + x];
                    w.Write((short)(v - 1));
                }
        }

        // ---- area types and taxes: every area, sea zones included (type normal, no tax), like the
        // original's table; an area missing here would load as land
        var areas = new JsonObject();
        bool fine = cfg["areaTax"]?.GetValue<string>() == "area";
        var taxOf = new int[n];
        foreach (var r in regions)
        {
            if (r.sea)
            {
                // a harbour is a port area with tax 2, like the original's; other sea zones have none
                areas[r.id.ToString()] = harbour[r.id] >= 0 ? new JsonObject { ["type"] = "port", ["tax"] = 2 } : new JsonObject { ["type"] = "normal", ["tax"] = 0 };
                continue;
            }
            int cityTax = r.city != null ? (int)Math.Min(10, r.cityPopulation / 1000000) : 0;
            // "area": many small regions, so the tax follows the area (1 per 25 000 km2, at least 1);
            // otherwise about the classic map's range: 3 to 30 per area
            int tax = fine
                ? Math.Max(1, (int)Math.Round(r.areaKm2 / 25000)) + cityTax + (r.type == "capital" ? 8 : 0)
                : 3 + (int)Math.Min(8, r.areaKm2 / 50000) + cityTax + (r.type == "capital" ? 10 : 0);
            taxOf[r.id] = tax;
            // fit: the radius of the largest circle around the army anchor inside the region (map units);
            // the game scales the units of a small region down to fit (data/json/maps/<id>/areas.json display)
            areas[r.id.ToString()] = new JsonObject { ["type"] = r.type, ["tax"] = tax, ["fit"] = best[r.id] * S };
        }
        string areasDir = Path.Combine(outDir, "data", "json", "maps", mapId.ToString());
        Directory.CreateDirectory(areasDir);
        var areasDoc = new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Area types, base taxes and unit fit radii of the generated map (WC2.Tools build-map --classic-out).",
        };
        if (cfg["display"] is JsonObject display)
        {
            var shown = display.DeepClone().AsObject();
            // the terrain picture and the paper grain the export writes (MapTerrainExport)
            if (cfg["mesh"]?.GetValue<bool>() == true && cfg["terrain"] is JsonObject t)
            {
                shown["terrain"] = new JsonObject
                {
                    ["file"] = $"terrain{mapId}_{{0}}.webp", ["columns"] = t["columns"]?.GetValue<int>() ?? 8, ["rows"] = t["rows"]?.GetValue<int>() ?? 4,
                    ["ownerAlpha"] = t["ownerAlpha"]?.GetValue<double>() ?? 0.45,
                };
                shown["detail"] = new JsonObject { ["file"] = $"paper{mapId}.png", ["units"] = t["grainUnits"]?.GetValue<double>() ?? 600, ["minZoom"] = t["grainMinZoom"]?.GetValue<double>() ?? 0.12 };
            }
            areasDoc["display"] = shown;
        }
        areasDoc["areas"] = areas;
        File.WriteAllText(Path.Combine(areasDir, "areas.json"), areasDoc.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) + "\n");

        bool mesh = cfg["mesh"]?.GetValue<bool>() ?? false;
        int tilesX = 0, tilesY = 0, pages = 0;
        var formats = new JsonObject();
        if (mesh)
        {
            // the map as vertex buffers: no background tiles, no region masks
            var m = cfg["meshSettings"]?.AsObject();
            var stats = MapMeshExport.Write(Path.Combine(outDir, $"map{mapId}.mesh"), raster, width, height, S, n,
                                            m?["tolerance"]?.GetValue<double>() ?? 0.9, m?["smoothing"]?.GetValue<int>() ?? 1,
                                            m?["chunkWidth"]?.GetValue<float>() ?? 1500f);
            Console.WriteLine($"build-map: mesh: {stats.vertices} vertices, {stats.triangles} triangles, {stats.chains} border chains ({stats.points} points)");
            if (cfg["terrain"] is JsonObject terrainCfg)
            {
                foreach (var (name, format) in MapTerrainExport.Write(outDir, mapId, terrainCfg, config, regions.Select(r => r.sea).ToArray(), raster, width, height, source))
                    formats[name] = format;
                Directory.CreateDirectory(Path.Combine(outDir, "generated"));
                File.WriteAllText(Path.Combine(outDir, "generated", "texture_formats.json"), new JsonObject
                {
                    ["format"] = 1,
                    ["about"] = "Load-time formats of the generated map's textures (merged with the game's list).",
                    ["textures"] = formats,
                }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
            }
        }
        else
        {

        // ---- background tiles (500 map units each, 6 units margin, 1 texel per unit)
        tilesX = (mapW + 499) / 500; tilesY = (mapH + 499) / 500;
        int RegionAtUnit(int u, int v)
        {
            if (wrap) u = ((u % mapW) + mapW) % mapW;
            int x = u / S, y = v / S;
            if (u < 0 || v < 0 || x >= width || y >= height) return 0;
            return raster[y * width + x];
        }
        for (int ty = 0; ty < tilesY; ty++)
            for (int tx = 0; tx < tilesX; tx++)
            {
                using var tile = new SKBitmap(512, 512, SKColorType.Rgba8888, SKAlphaType.Opaque);
                for (int q = 0; q < 512; q++)
                    for (int p = 0; p < 512; p++)
                    {
                        int u = tx * 500 - 6 + p, v = ty * 500 - 6 + q;
                        int id = RegionAtUnit(u, v);
                        bool sea = id == 0 || regions[id - 1].sea;
                        int right = RegionAtUnit(u + 1, v), down = RegionAtUnit(u, v + 1);
                        int wu = wrap ? ((u % mapW) + mapW) % mapW : u;
                        SKColor c;
                        if (sea)
                        {
                            int shade = (int)(8 * Math.Sin(wu * Math.PI * 2 / Math.Max(1, mapW) * 18) * Math.Cos(v * 0.011));
                            c = new SKColor((byte)(58 + shade), (byte)(98 + shade), (byte)(138 + shade));
                        }
                        else
                        {
                            int grain = ((wu * 7349 + v * 9157) >> 3) % 9;
                            c = new SKColor((byte)(200 + grain), (byte)(184 + grain), (byte)(146 + grain));
                        }
                        // lines: coast, region border, sea zone border (faint)
                        foreach (int o in new[] { right, down })
                        {
                            if (o == id) continue;
                            bool oSea = o == 0 || regions[o - 1].sea;
                            if (sea && oSea)
                            {
                                if (id != 0 && o != 0) c = new SKColor(76, 116, 154);
                                continue;
                            }
                            c = sea != oSea ? new SKColor(40, 46, 52) : new SKColor(120, 104, 80);
                        }
                        tile.SetPixel(p, q, c);
                    }
                string name = $"map{mapId}_{ty * tilesX + tx + 1}.png";
                using (var data = tile.Encode(SKEncodedImageFormat.Png, 100))
                using (var file = File.Create(Path.Combine(outDir, name)))
                    data.SaveTo(file);
                formats[name] = "rgb565";
            }

        // ---- region masks packed into atlas pages (shelf packing), names "%04d.png" = region id
        const int Page = 2048;
        var order = Enumerable.Range(0, n).OrderByDescending(r => (maxY[r] - minY[r] + 1) * S).ThenBy(r => r).ToList();
        var placed = new List<(int page, int x, int y, int r)>();
        int pageIndex = 0, cx = 0, cy = 0, shelf = 0;
        foreach (int r in order)
        {
            int w = (maxX[r] - minX[r] + 1) * S + 2, h = (maxY[r] - minY[r] + 1) * S + 2;
            if (w > Page || h > Page) throw new ToolException($"region {r} ({regions[r].name}) is larger than an atlas page: x {minX[r]}-{maxX[r]}, y {minY[r]}-{maxY[r]} of {width}x{height}");
            if (cx + w > Page) { cx = 0; cy += shelf; shelf = 0; }
            if (cy + h > Page) { pageIndex++; cx = 0; cy = 0; shelf = 0; }
            placed.Add((pageIndex, cx + 1, cy + 1, r));
            cx += w; shelf = Math.Max(shelf, h);
        }
        pages = pageIndex + 1;
        for (int pg = 0; pg < pages; pg++)
        {
            using var atlas = new SKBitmap(Page, Page, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            atlas.Erase(SKColors.Transparent);
            var sb = new System.Text.StringBuilder();
            string texture = $"m{mapId}_zone{pg + 1}_1.png";
            sb.Append($"<Texture name=\"{texture}\" />\n<Images>\n");
            foreach (var (page, x0, y0, r) in placed.Where(p => p.page == pg))
            {
                int w = (maxX[r] - minX[r] + 1) * S, h = (maxY[r] - minY[r] + 1) * S;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int rx = minX[r] + x / S, ry = minY[r] + y / S;
                        if (raster[ry * width + rx] == r + 1) atlas.SetPixel(x0 + x, y0 + y, SKColors.White);
                    }
                sb.Append($"    <Image name=\"{r:D4}.png\" x=\"{x0}\" y=\"{y0}\" w=\"{w}\" h=\"{h}\" refx=\"0\" refy=\"0\" />\n");
            }
            sb.Append("</Images>\n");
            File.WriteAllText(Path.Combine(outDir, $"m{mapId}_zone{pg + 1}_1.xml"), sb.ToString());
            using (var data = atlas.Encode(SKEncodedImageFormat.Png, 100))
            using (var file = File.Create(Path.Combine(outDir, texture)))
                data.SaveTo(file);
            formats[texture] = "alpha8";
        }
        Directory.CreateDirectory(Path.Combine(outDir, "generated"));
        File.WriteAllText(Path.Combine(outDir, "generated", "texture_formats.json"), new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Load-time formats of the generated map's textures (merged with the game's list).",
            ["textures"] = formats,
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }

        // ---- the conquests
        var b = config["bounds"]!.AsObject();
        double west = b["west"]!.GetValue<double>(), east = b["east"]!.GetValue<double>(), north = b["north"]!.GetValue<double>();
        double unitsPerDeg = mapW / (east - west);
        var conquestEntries = new JsonObject();
        var strings = new JsonObject();
        var summary = new List<string>();
        foreach (var spec in scenarios)
        {
            var s = spec.config;
            string scenario = s["file"]!.GetValue<string>();
            int number = s["number"]!.GetValue<int>();
            var players = s["players"]!.AsObject();
            var owner = spec.owners;
            // the areas-enable file (count, then one byte per area): every area takes part; the conquest
            // preview of the menu needs it
            string enableFile = Path.ChangeExtension(scenario, ".bin");
            using (var enable = new BinaryWriter(File.Create(Path.Combine(outDir, enableFile))))
            {
                enable.Write(n);
                for (int r = 0; r < n; r++) enable.Write((byte)1);
            }
            var xml = new System.Text.StringBuilder();
            xml.Append($"<battle map=\"{mapId}\" areasenable=\"{enableFile}\">\n    <list name=\"country\">\n");
            var present = owner.Where(o => o != null).ToHashSet();
            var active = players.Where(kv => present.Contains(kv.Key)).ToList();
            foreach (var (id, node) in active)
            {
                var o = node!.AsObject();
                int owned = owner.Count(x => x == id);
                int[] rgb = o["color"]!.AsArray().Select(c => c!.GetValue<int>()).ToArray();
                int taxes = Enumerable.Range(0, n).Where(r => owner[r] == id).Sum(r => taxOf[r]);
                int money = o["money"]?.GetValue<int>() ?? (fine ? 200 + taxes : 200 + 5 * owned);
                int industry = o["industry"]?.GetValue<int>() ?? (fine ? 100 + taxes / 2 : 100 + 3 * owned);
                xml.Append($"        <country id=\"{id}\" name=\"{id}\" commander=\"{o["commander"]?.GetValue<string>() ?? "common1"}\" ai=\"1\" defeated=\"land\" " +
                           $"taxfactor=\"1.200000\" money=\"{money}\" industry=\"{industry}\" techlevel=\"{o["techlevel"]?.GetValue<int>() ?? 3}\" alliance=\"{o["alliance"]!.GetValue<string>()}\" " +
                           $"r=\"{rgb[0]}\" g=\"{rgb[1]}\" b=\"{rgb[2]}\" a=\"90\" />\n");
            }
            xml.Append("    </list>\n    <list name=\"area\">\n");
            var activeIds = active.Select(kv => kv.Key).ToHashSet();
            // stronger garrisons for the great powers (players' "garrison": extra infantry in capitals and large cities)
            int Garrison(string id) => players[id]?["garrison"]?.GetValue<int>() ?? 0;
            foreach (var r in regions.Where(r => !r.sea && owner[r.id] != null && activeIds.Contains(owner[r.id])))
            {
                string id = owner[r.id];
                string construction = r.type == "capital" || r.type == "large city" ? "city" : r.airfield ? "airport" : "none";
                int level = construction == "none" ? 0 : r.type == "capital" ? 2 : 1;
                var unitList = new List<string>();
                if (r.type == "capital") unitList.AddRange(new[] { "infantry", "infantry", "tank" });
                else if (r.type == "large city") unitList.AddRange(new[] { "infantry", "artillery" });
                else if (r.type == "normal city" || r.type == "port") unitList.Add("infantry");
                if (r.type == "capital" || r.type == "large city")
                    for (int k = 0; k < Garrison(id) && unitList.Count < 4; k++) unitList.Add("infantry");
                xml.Append($"        <area id=\"{r.id}\" construction=\"{construction}\" level=\"{level}\" installation=\"{(r.type == "capital" ? "fort" : "none")}\" country=\"{id}\"");
                if (unitList.Count == 0) { xml.Append(" />\n"); continue; }
                xml.Append(">\n");
                // cards (bits: 1 assault, 2 defence, 4 transport): units on a coast come with a transport
                // ship (transportCards, default true), as many coastal units of the original's conquests do
                int cards = r.coastal && (s["transportCards"]?.GetValue<bool>() ?? true) ? 4 : 0;
                foreach (var u in unitList) xml.Append($"            <army type=\"{u}\" level=\"1\" cards=\"{cards}\" />\n");
                xml.Append("        </area>\n");
            }
            // fleets: a destroyer in the sea zone along each port (the longest shared border), up to the
            // player's "fleets" (default 3); a sea area lists the fleet's country like the classic scenarios do
            var usedSeas = new HashSet<int>();
            int fleetCount = 0;
            foreach (var g in regions.Where(r => !r.sea && r.port && owner[r.id] != null && activeIds.Contains(owner[r.id])).GroupBy(r => owner[r.id]).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                int fleets = 0, maxFleets = players[g.Key]?["fleets"]?.GetValue<int>() ?? 3;
                foreach (var r in g.OrderByDescending(r => r.type == "capital").ThenByDescending(r => r.cityPopulation).ThenBy(r => r.id))
                {
                    if (fleets >= maxFleets) break;
                    // the harbour of this port (else the longest neighbouring sea zone)
                    int sea = r.border.Where(kv => regions[kv.Key].sea && !usedSeas.Contains(kv.Key))
                                      .OrderByDescending(kv => harbour[kv.Key] == r.id).ThenByDescending(kv => kv.Value).ThenBy(kv => kv.Key)
                                      .Select(kv => kv.Key).DefaultIfEmpty(-1).First();
                    if (sea < 0) continue;
                    usedSeas.Add(sea);
                    fleets++; fleetCount++;
                    xml.Append($"        <area id=\"{sea}\" construction=\"none\" level=\"0\" installation=\"none\" country=\"{g.Key}\">\n");
                    xml.Append("            <army type=\"destroyer\" level=\"1\" cards=\"0\" />\n");
                    xml.Append("        </area>\n");
                }
            }
            xml.Append("    </list>\n    <list name=\"dialogue\">\n    </list>\n</battle>\n");
            File.WriteAllText(Path.Combine(outDir, scenario), xml.ToString());

            // the conquest list entry (merged into the game's conquests.json) and the menu texts
            double lon = s["centerLon"]!.GetValue<double>(), lat = s["centerLat"]!.GetValue<double>();
            double cxUnits = (lon - west) * unitsPerDeg, cyUnits = (north - lat) * unitsPerDeg;
            // the preview scales centerX by map width / 1715 and centerY by map height / 750
            conquestEntries[$"conquest {number}"] = new JsonObject
            {
                ["centerX"] = (int)Math.Round(cxUnits / mapW * 1715),
                ["centerY"] = (int)Math.Round(cyUnits / mapH * 750),
                ["fogOfWar"] = s["fogOfWar"]?.GetValue<bool>() ?? false,
                ["leftRightWrap"] = s["leftRightWrap"]?.GetValue<bool>() ?? false,
                ["flags"] = new JsonArray(),
            };
            string title = s["title"]!.GetValue<string>();
            WriteConquestButton(Path.Combine(outDir, $"button_conquest{number}@2x.png"), raster, width, height, regions,
                                title, (int)(cxUnits / S), (int)(cyUnits / S), Math.Max(1, (int)Math.Round(mapW / 6.0 / 320 / S)));
            strings[$"conquest name {number}"] = title;
            foreach (var (id, _) in active)
            {
                string name = spec.names.GetValueOrDefault(id) ?? id;
                strings[$"conquest country name {number} {id}"] = name.ToUpperInvariant();
                strings[$"conquest country intro {number} {id}"] = s["intro"]!.GetValue<string>().Replace("{country}", name);
                strings[$"conquest country victory {number} {id}"] = s["victory"]!.GetValue<string>();
            }
            summary.Add($"{scenario}: {active.Count} countries, {owner.Count(o => o != null && activeIds.Contains(o))} areas owned, {fleetCount} fleets");
        }
        if (scenarios.Count > 0)
        {
            string conquestsDir = Path.Combine(outDir, "data", "json", "conquests");
            Directory.CreateDirectory(conquestsDir);
            File.WriteAllText(Path.Combine(conquestsDir, "conquests.json"), new JsonObject
            {
                ["about"] = "Adds the conquests of the generated map to the conquest list (merged into the game's conquests.json).",
                ["conquests"] = conquestEntries,
            }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
            foreach (var lang in new[] { "cn", "en", "ja", "kr", "ru", "tw" })
                foreach (var table in new[] { $"stringtable_{lang}", $"stringtable_{lang}_iPad" })
                {
                    string dir = Path.Combine(outDir, "data", "json", "text", lang);
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, table + ".json"), new JsonObject
                    {
                        ["about"] = "Menu texts of the generated conquests (English for every language; merged into the game's table).",
                        ["strings"] = strings.DeepClone(),
                    }.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
                }
        }

        File.WriteAllText(Path.Combine(outDir, "mod.json"), new JsonObject
        {
            ["id"] = cfg["modId"]!.GetValue<string>(),
            ["name"] = cfg["modName"]!.GetValue<string>(),
            ["version"] = "2.0",
            ["author"] = "WC2.Tools build-map",
            ["description"] = $"The {config["name"]!.GetValue<string>()} map as map {mapId} with the conquests " +
                              string.Join(", ", scenarios.Select(s => s.config["file"]!.GetValue<string>())) +
                              ": generated from Natural Earth and CShapes 2.0 (MAP_PIPELINE.md). The classic map and every campaign are untouched; remove or disable the mod to revert.",
            ["priority"] = 0,
            ["enabled"] = true,
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Console.WriteLine($"build-map: classic export (map {mapId}, {mapW}x{mapH} units{(wrap ? ", wraps" : "")}): {n} areas, {harbour.Count(h => h >= 0)} harbours, " +
                          (mesh ? "mesh" : $"{tilesX * tilesY} tiles, {pages} zone atlas pages") + "; " +
                          string.Join("; ", summary) + $" -> {outDir}");
    }
}
