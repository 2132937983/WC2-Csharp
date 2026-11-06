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
/// <item><c>adjion&lt;id&gt;.bin</c>: up to 16 neighbours per region (the longest borders);</item>
/// <item><c>areamark&lt;id&gt;.raw</c>: the region under every 8x8 block of map units (touch picking);</item>
/// <item><c>data/json/maps/&lt;id&gt;/areas.json</c>: area types and taxes;</item>
/// <item><c>map&lt;id&gt;_&lt;n&gt;.png</c>: the background in 500 unit tiles (land, sea, region, country and
/// coast lines) and <c>m&lt;id&gt;_zone&lt;n&gt;_1.png/.xml</c>: atlases of the region masks the game tints
/// with the owner's colour;</item>
/// <item><c>generated/texture_formats.json</c>: tiles uploaded as RGB565, masks as Alpha8;</item>
/// <item>a conquest scenario (<c>scenario</c>) with the countries of <c>players</c>, their regions and
/// starting armies; the rest of the land starts unowned.</item>
/// </list>
/// </summary>
internal static partial class MapPipeline
{
    private static void ExportClassic(string outDir, JsonObject config, List<Region> regions, int[] raster, int width, int height)
    {
        var cfg = config["classicExport"]!.AsObject();
        int mapId = cfg["mapId"]!.GetValue<int>();
        int S = cfg["scale"]?.GetValue<int>() ?? 2;
        int mapW = width * S, mapH = height * S;
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
                w.Write(ax - 34); w.Write(ay - 22);      // building, up left of the army
                w.Write(ax - 40); w.Write(ay + 4);       // installation, left of the army
                w.Write(regions[r].sea ? 1 : 0);
            }
        }

        // ---- adjion<id>.bin: the 16 longest borders per region, kept only when both ends keep them
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

        // ---- area types and taxes
        var areas = new JsonObject();
        foreach (var r in regions.Where(r => !r.sea))
        {
            // about the classic map's range: 3 to 30 per area
            int tax = 3 + (int)Math.Min(8, r.areaKm2 / 50000) + (r.city != null ? (int)Math.Min(10, r.cityPopulation / 1000000) : 0) + (r.type == "capital" ? 10 : 0);
            areas[r.id.ToString()] = new JsonObject { ["type"] = r.type, ["tax"] = tax };
        }
        string areasDir = Path.Combine(outDir, "data", "json", "maps", mapId.ToString());
        Directory.CreateDirectory(areasDir);
        File.WriteAllText(Path.Combine(areasDir, "areas.json"), new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Area types and base taxes of the generated map (WC2.Tools build-map --classic-out).",
            ["areas"] = areas,
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) + "\n");

        // ---- background tiles (500 map units each, 6 units margin, 1 texel per unit)
        int tilesX = (mapW + 499) / 500, tilesY = (mapH + 499) / 500;
        var countryOf = regions.Select(r => r.sea ? "" : r.country).ToArray();
        int RegionAtUnit(int u, int v)
        {
            int x = u / S, y = v / S;
            if (u < 0 || v < 0 || x >= width || y >= height) return 0;
            return raster[y * width + x];
        }
        var formats = new JsonObject();
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
                        SKColor c;
                        if (sea)
                        {
                            int shade = (int)(8 * Math.Sin(u * 0.013) * Math.Cos(v * 0.011));
                            c = new SKColor((byte)(58 + shade), (byte)(98 + shade), (byte)(138 + shade));
                        }
                        else
                        {
                            int grain = ((u * 7349 + v * 9157) >> 3) % 9;
                            c = new SKColor((byte)(200 + grain), (byte)(184 + grain), (byte)(146 + grain));
                        }
                        // lines: coast, country border, region border
                        int[] others = { right, down };
                        foreach (int o in others)
                        {
                            if (o == id) continue;
                            bool oSea = o == 0 || regions[o - 1].sea;
                            if (sea && oSea) continue;
                            if (sea != oSea) c = new SKColor(40, 46, 52);
                            else if (countryOf[id - 1] != countryOf[o - 1]) c = new SKColor(70, 58, 44);
                            else c = new SKColor(150, 132, 100);
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
            if (w > Page || h > Page) throw new ToolException($"region {r} is larger than an atlas page");
            if (cx + w > Page) { cx = 0; cy += shelf; shelf = 0; }
            if (cy + h > Page) { pageIndex++; cx = 0; cy = 0; shelf = 0; }
            placed.Add((pageIndex, cx + 1, cy + 1, r));
            cx += w; shelf = Math.Max(shelf, h);
        }
        int pages = pageIndex + 1;
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

        // ---- the conquest scenario
        var players = cfg["players"]!.AsObject();
        string scenario = cfg["scenario"]!.GetValue<string>();
        var xml = new System.Text.StringBuilder();
        xml.Append($"<battle map=\"{mapId}\">\n    <list name=\"country\">\n");
        var present = new HashSet<string>(regions.Where(r => !r.sea).Select(r => r.country));
        var active = players.Where(kv => present.Contains(kv.Key)).ToList();
        foreach (var (id, node) in active)
        {
            var o = node!.AsObject();
            int owned = regions.Count(r => !r.sea && r.country == id);
            int[] rgb = o["color"]!.AsArray().Select(c => c!.GetValue<int>()).ToArray();
            xml.Append($"        <country id=\"{id}\" name=\"{id}\" commander=\"{o["commander"]?.GetValue<string>() ?? "common1"}\" ai=\"1\" defeated=\"land\" " +
                       $"taxfactor=\"1.200000\" money=\"{200 + 5 * owned}\" industry=\"{100 + 3 * owned}\" techlevel=\"3\" alliance=\"{o["alliance"]!.GetValue<string>()}\" " +
                       $"r=\"{rgb[0]}\" g=\"{rgb[1]}\" b=\"{rgb[2]}\" a=\"90\" />\n");
        }
        xml.Append("    </list>\n    <list name=\"area\">\n");
        var activeIds = active.Select(kv => kv.Key).ToHashSet();
        foreach (var r in regions.Where(r => !r.sea && activeIds.Contains(r.country)))
        {
            string construction = r.type == "capital" || r.type == "large city" ? "city" : r.airfield ? "airport" : "none";
            int level = construction == "none" ? 0 : r.type == "capital" ? 2 : 1;
            var units = new List<string>();
            if (r.type == "capital") units.AddRange(new[] { "infantry", "infantry", "tank" });
            else if (r.type == "large city") units.AddRange(new[] { "infantry", "artillery" });
            else if (r.type == "normal city" || r.type == "port") units.Add("infantry");
            xml.Append($"        <area id=\"{r.id}\" construction=\"{construction}\" level=\"{level}\" installation=\"{(r.type == "capital" ? "fort" : "none")}\" country=\"{r.country}\"");
            if (units.Count == 0) { xml.Append(" />\n"); continue; }
            xml.Append(">\n");
            foreach (var u in units) xml.Append($"            <army type=\"{u}\" level=\"1\" cards=\"0\" />\n");
            xml.Append("        </area>\n");
        }
        xml.Append("    </list>\n    <list name=\"dialogue\">\n    </list>\n</battle>\n");
        File.WriteAllText(Path.Combine(outDir, scenario), xml.ToString());

        File.WriteAllText(Path.Combine(outDir, "mod.json"), new JsonObject
        {
            ["id"] = cfg["modId"]!.GetValue<string>(),
            ["name"] = cfg["modName"]!.GetValue<string>(),
            ["version"] = "1.0",
            ["author"] = "WC2.Tools build-map",
            ["description"] = $"The {config["name"]!.GetValue<string>()} map as map {mapId} with the conquest {scenario}: generated from Natural Earth (MAP_PIPELINE.md). " +
                              "The classic map and every campaign are untouched; remove or disable the mod to revert.",
            ["priority"] = 0,
            ["enabled"] = true,
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Console.WriteLine($"build-map: classic export (map {mapId}, {mapW}x{mapH} units): {n} areas, {tilesX * tilesY} tiles, {pages} zone atlas pages, " +
                          $"{active.Count} countries in {scenario} -> {outDir}");
    }
}
