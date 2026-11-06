using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkiaSharp;

namespace WC2.Tools;

/// <summary>
/// build-map: a playable region map from Natural Earth GeoJSON (MAP_PIPELINE.md).
/// <list type="number">
/// <item>Admin-1 provinces inside the configured bounds are rasterized (equirectangular, one colour
/// per province, no antialiasing). The raster gives every province its area, centroid, coastline and
/// neighbours without polygon clipping.</item>
/// <item>Provinces merge within their country (smallest into its smallest neighbour) until every
/// region has <c>minAreaKm2</c> and a country has at most <c>maxPerCountry</c> regions; islands below
/// <c>minIslandKm2</c> are dropped.</item>
/// <item>Water becomes sea zones: a grid of <c>cellDegrees</c>, each cell split into its connected
/// water parts; parts below <c>minSeaPixels</c> join a neighbouring zone.</item>
/// <item>Adjacency: regions whose pixels touch (4-neighbourhood) along at least <c>minBorderPixels</c>.</item>
/// <item>Ownership from the country (ADM0_A3 mapped to the game's country ids), capitals from the
/// populated places marked as country capitals, cities by population, ports and airfields from the
/// point datasets.</item>
/// </list>
/// Output (the map pack): map.json, regions.json, regions.raster (the region of every pixel, uint16,
/// deflate), preview.png.
/// </summary>
internal static partial class MapPipeline
{
    private sealed class Province
    {
        public int index;
        public string name = "", iso = "", country = "", adm0 = "", admin = "";
        public long pixels;
        public double areaKm2, sumX, sumY;
        public int region = -1;
    }

    private sealed class Region
    {
        public int id;
        public bool sea;
        public string country = "", name = "";
        public long pixels;
        public double areaKm2, sumX, sumY;
        public readonly List<string> provinces = new();
        public readonly Dictionary<int, int> border = new();
        public string type = "normal";
        public bool port, airfield, coastal;
        public string city;
        public long cityPopulation;
        public string capitalOf;
        public string portName, airfieldName;
    }

    public static int Run(Dictionary<string, string> opts)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string configPath = opts["config"], source = opts["source"], outDir = opts["out"];
        var config = JsonNode.Parse(File.ReadAllText(configPath), null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })!.AsObject();
        var b = config["bounds"]!.AsObject();
        double west = b["west"]!.GetValue<double>(), east = b["east"]!.GetValue<double>(), north = b["north"]!.GetValue<double>(), south = b["south"]!.GetValue<double>();
        int width = config["raster"]?["width"]?.GetValue<int>() ?? 4096;
        double pxPerDeg = width / (east - west);
        int height = (int)Math.Round((north - south) * pxPerDeg);
        var rules = config["regions"]!.AsObject();
        double minAreaKm2 = rules["minAreaKm2"]!.GetValue<double>();
        int maxPerCountry = rules["maxPerCountry"]!.GetValue<int>();
        double minIslandKm2 = rules["minIslandKm2"]!.GetValue<double>();
        var seaRules = config["sea"]!.AsObject();
        double cellDegrees = seaRules["cellDegrees"]!.GetValue<double>();
        int minSeaPixels = seaRules["minSeaPixels"]!.GetValue<int>();
        int minBorder = config["minBorderPixels"]?.GetValue<int>() ?? 2;
        // per-country sizing (game id -> { minAreaKm2, maxPerCountry }), e.g. a finer Europe
        var overrides = config["regionOverrides"]?.AsObject() ?? new JsonObject();
        double MinArea(string c) => overrides[c]?["minAreaKm2"]?.GetValue<double>() ?? minAreaKm2;
        int MaxRegions(string c) => overrides[c]?["maxPerCountry"]?.GetValue<int>() ?? maxPerCountry;
        var countryIds = config["countries"]?.AsObject().ToDictionary(kv => kv.Key, kv => kv.Value!.GetValue<string>(), StringComparer.OrdinalIgnoreCase) ?? new();

        (double x, double y) Project(double lon, double lat) => ((lon - west) * pxPerDeg, (north - lat) * pxPerDeg);
        (double lon, double lat) Unproject(double x, double y) => (west + x / pxPerDeg, north - y / pxPerDeg);
        double KmPerPixel2(double y)
        {
            double lat = north - y / pxPerDeg;
            return (111.32 * Math.Cos(lat * Math.PI / 180) / pxPerDeg) * (110.57 / pxPerDeg);
        }

        // ---- 1. rasterize the provinces
        var provinces = new List<Province>();
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(0, 0, 0, 0));
            using var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill, BlendMode = SKBlendMode.Src };
            using var doc = JsonDocument.Parse(File.OpenRead(Path.Combine(source, "ne_10m_admin_1_states_provinces.geojson")));
            foreach (var f in doc.RootElement.GetProperty("features").EnumerateArray())
            {
                var geometry = f.GetProperty("geometry");
                if (geometry.ValueKind != JsonValueKind.Object) continue;
                using var path = new SKPath { FillType = SKPathFillType.EvenOdd };
                bool inside = false;
                foreach (var ring in Rings(geometry))
                {
                    bool first = true;
                    foreach (var (lon, lat) in ring)
                    {
                        if (lon >= west && lon <= east && lat >= south && lat <= north) inside = true;
                        var (x, y) = Project(lon, lat);
                        if (first) { path.MoveTo((float)x, (float)y); first = false; }
                        else path.LineTo((float)x, (float)y);
                    }
                    path.Close();
                }
                if (!inside) continue;
                var p = f.GetProperty("properties");
                var province = new Province
                {
                    index = provinces.Count,
                    name = Str(p, "name"),
                    iso = Str(p, "iso_3166_2"),
                    adm0 = Str(p, "adm0_a3"),
                    admin = Str(p, "admin"),
                };
                province.country = countryIds.TryGetValue(province.adm0, out var id) ? id : Str(p, "iso_a2").ToLowerInvariant();
                if (province.country.Length == 0 || province.country == "-99") province.country = province.adm0.ToLowerInvariant();
                provinces.Add(province);
                int code = provinces.Count;   // 1-based
                paint.Color = new SKColor((byte)(code & 0xFF), (byte)((code >> 8) & 0xFF), (byte)((code >> 16) & 0xFF), 255);
                canvas.DrawPath(path, paint);
            }
        }
        var raster = new int[width * height];   // province index + 1 (0 = water)
        var pixels = bitmap.GetPixelSpan();
        for (int i = 0; i < raster.Length; i++)
        {
            byte a = pixels[i * 4 + 3];
            raster[i] = a == 0 ? 0 : pixels[i * 4] | (pixels[i * 4 + 1] << 8) | (pixels[i * 4 + 2] << 16);
        }
        for (int y = 0; y < height; y++)
        {
            double km2 = KmPerPixel2(y + 0.5);
            for (int x = 0; x < width; x++)
            {
                int v = raster[y * width + x];
                if (v <= 0 || v > provinces.Count) { raster[y * width + x] = 0; continue; }
                var pr = provinces[v - 1];
                pr.pixels++; pr.areaKm2 += km2; pr.sumX += x; pr.sumY += y;
            }
        }
        Console.WriteLine($"build-map: {provinces.Count(p => p.pixels > 0)} provinces rasterized at {width}x{height}");

        // ---- 2. merge provinces into regions within their country
        var provinceBorder = Adjacency(raster, width, height, provinces.Count + 1);
        var parent = Enumerable.Range(0, provinces.Count).ToArray();
        int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
        var groupArea = provinces.Select(p => p.areaKm2).ToArray();
        bool merged = true;
        while (merged)
        {
            merged = false;
            // per country: groups below the minimum, or the smallest group while a country has too many
            var groups = provinces.Where(p => p.pixels > 0).GroupBy(p => Find(p.index)).ToList();
            var perCountry = groups.GroupBy(g => provinces[g.Key].country).ToDictionary(g => g.Key, g => g.Count());
            foreach (var g in groups.OrderBy(g => groupArea[g.Key]))
            {
                int root = Find(g.Key);
                if (root != g.Key) continue;
                string country = provinces[root].country;
                bool small = groupArea[root] < MinArea(country);
                bool tooMany = perCountry[country] > MaxRegions(country);
                if (!small && !tooMany) continue;
                // the smallest touching group of the same country
                int best = -1; double bestArea = double.MaxValue;
                foreach (var member in provinces.Where(p => p.pixels > 0 && Find(p.index) == root))
                    foreach (var (other, len) in provinceBorder[member.index + 1])
                    {
                        if (other <= 0 || len < minBorder) continue;
                        int o = Find(other - 1);
                        if (o == root || provinces[o].country != country) continue;
                        if (groupArea[o] < bestArea) { bestArea = groupArea[o]; best = o; }
                    }
                if (best < 0) continue;   // an island of its own
                parent[root] = best;
                groupArea[best] += groupArea[root];
                perCountry[country]--;
                merged = true;
            }
        }

        // ---- regions: land groups (islands below the minimum are dropped), then sea zones
        var regions = new List<Region>();
        var groupRegion = new Dictionary<int, Region>();
        foreach (var g in provinces.Where(p => p.pixels > 0).GroupBy(p => Find(p.index)).OrderBy(g => g.Key))
        {
            double area = g.Sum(p => p.areaKm2);
            if (area < minIslandKm2) continue;
            var biggest = g.OrderByDescending(p => p.areaKm2).First();
            var r = new Region { id = regions.Count, country = biggest.country, name = biggest.name };
            foreach (var p in g) { r.provinces.Add(p.iso.Length > 0 ? p.iso : p.name); p.region = r.id; }
            regions.Add(r);
            groupRegion[g.Key] = r;
        }
        var regionRaster = new int[raster.Length];   // region id + 1
        for (int i = 0; i < raster.Length; i++)
        {
            int v = raster[i];
            regionRaster[i] = v > 0 && provinces[v - 1].region >= 0 ? provinces[v - 1].region + 1 : 0;
        }
        // parts of a land region far from its main body (islands across the date line, e.g. the
        // Aleutians at the other edge of the map) are left out: a region stays one place on the map
        double maxSpan = rules["maxPartDistanceDegrees"]?.GetValue<double>() ?? 25;
        DropFarParts(regionRaster, width, height, regions.Count, maxSpan * pxPerDeg);
        int landRegions = regions.Count;
        int cellPx = Math.Max(4, (int)Math.Round(cellDegrees * pxPerDeg));
        var seaLabel = new int[raster.Length];
        var stack = new Stack<int>();
        for (int cy = 0; cy < height; cy += cellPx)
            for (int cx = 0; cx < width; cx += cellPx)
            {
                int x1 = Math.Min(width, cx + cellPx), y1 = Math.Min(height, cy + cellPx);
                for (int y = cy; y < y1; y++)
                    for (int x = cx; x < x1; x++)
                    {
                        int i0 = y * width + x;
                        if (regionRaster[i0] != 0 || seaLabel[i0] != 0) continue;
                        // flood fill the connected water inside this cell
                        var part = new List<int>();
                        int label = -(regions.Count + 1);
                        stack.Push(i0); seaLabel[i0] = label;
                        while (stack.Count > 0)
                        {
                            int i = stack.Pop(); part.Add(i);
                            int px = i % width, py = i / width;
                            foreach (int n in new[] { px > cx ? i - 1 : -1, px + 1 < x1 ? i + 1 : -1, py > cy ? i - width : -1, py + 1 < y1 ? i + width : -1 })
                                if (n >= 0 && regionRaster[n] == 0 && seaLabel[n] == 0) { seaLabel[n] = label; stack.Push(n); }
                        }
                        var r = new Region { id = regions.Count, sea = true, name = $"sea {cx / cellPx}-{cy / cellPx}" };
                        regions.Add(r);
                        foreach (int i in part) regionRaster[i] = r.id + 1;
                    }
            }
        // small water parts (lakes in a cell corner, slivers) join the neighbour they share the longest border with
        var seaBorder = Adjacency(regionRaster, width, height, regions.Count + 1);
        var seaPixels = new long[regions.Count + 1];
        foreach (int v in regionRaster) seaPixels[v]++;
        var redirect = Enumerable.Range(0, regions.Count + 1).ToArray();
        foreach (var r in regions.Where(r => r.sea).OrderBy(r => seaPixels[r.id + 1]))
        {
            if (seaPixels[r.id + 1] >= minSeaPixels) continue;
            var target = seaBorder[r.id + 1].Where(kv => kv.Key > 0 && regions[kv.Key - 1].sea && redirect[kv.Key] == kv.Key && kv.Key != r.id + 1)
                                            .OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault();
            if (target == 0) target = 0;   // an isolated lake: dropped
            redirect[r.id + 1] = target;
            if (target > 0) seaPixels[target] += seaPixels[r.id + 1];
        }
        int Resolve(int v) { while (redirect[v] != v) v = redirect[v]; return v; }
        for (int i = 0; i < regionRaster.Length; i++) regionRaster[i] = Resolve(regionRaster[i]);
        // renumber: land first, then the surviving sea zones
        var used = new bool[regions.Count + 1];
        foreach (int v in regionRaster) used[v] = true;
        var final = new List<Region>();
        var newId = new int[regions.Count + 1];
        foreach (var r in regions)
        {
            if (!used[r.id + 1]) continue;
            newId[r.id + 1] = final.Count + 1;
            r.id = final.Count;
            final.Add(r);
        }
        for (int i = 0; i < regionRaster.Length; i++) regionRaster[i] = newId[regionRaster[i]];
        regions = final;
        foreach (var r in regions) { r.pixels = 0; r.areaKm2 = 0; r.sumX = 0; r.sumY = 0; }
        for (int y = 0; y < height; y++)
        {
            double km2 = KmPerPixel2(y + 0.5);
            for (int x = 0; x < width; x++)
            {
                int v = regionRaster[y * width + x];
                if (v == 0) continue;
                var r = regions[v - 1];
                r.pixels++; r.areaKm2 += km2; r.sumX += x; r.sumY += y;
            }
        }
        var border = Adjacency(regionRaster, width, height, regions.Count + 1);
        foreach (var r in regions)
            foreach (var (other, len) in border[r.id + 1])
                if (other > 0 && other != r.id + 1 && len >= minBorder) r.border[other - 1] = len;
        foreach (var r in regions.Where(r => !r.sea)) r.coastal = r.border.Keys.Any(o => regions[o].sea);

        // ---- 3. features: capitals, cities, ports, airfields
        int RegionAt(double lon, double lat, bool landOnly, int searchPx = 3)
        {
            var (fx, fy) = Project(lon, lat);
            int x0 = (int)fx, y0 = (int)fy;
            for (int rad = 0; rad <= searchPx; rad++)
                for (int dy = -rad; dy <= rad; dy++)
                    for (int dx = -rad; dx <= rad; dx++)
                    {
                        int x = x0 + dx, y = y0 + dy;
                        if (x < 0 || y < 0 || x >= width || y >= height) continue;
                        int v = regionRaster[y * width + x];
                        if (v > 0 && (!landOnly || !regions[v - 1].sea)) return v - 1;
                    }
            return -1;
        }
        var cityRules = config["cities"]!.AsObject();
        long largeCity = cityRules["largeCityPopulation"]!.GetValue<long>(), minCity = cityRules["minPopulation"]!.GetValue<long>();
        var places = Points(Path.Combine(source, "ne_50m_populated_places.geojson")).ToList();
        // one capital per country: the most populous place marked as a country capital (Bolivia, Cote
        // d'Ivoire and others list two)
        var capitals = places.Where(f => Long(f.props, "ADM0CAP") == 1 && InBounds(f.lon, f.lat))
            .GroupBy(f => countryIds.TryGetValue(Str(f.props, "ADM0_A3"), out var cid) ? cid : Str(f.props, "ISO_A2").ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.OrderByDescending(f => Long(f.props, "POP_MAX")).First());
        foreach (var f in places)
        {
            if (!InBounds(f.lon, f.lat)) continue;
            int r = RegionAt(f.lon, f.lat, true);
            if (r < 0) continue;
            var reg = regions[r];
            long pop = Long(f.props, "POP_MAX");
            if (Long(f.props, "ADM0CAP") == 1)
            {
                string capCountry = countryIds.TryGetValue(Str(f.props, "ADM0_A3"), out var cid) ? cid : Str(f.props, "ISO_A2").ToLowerInvariant();
                bool chosen = capitals.TryGetValue(capCountry, out var cap) && cap.lon == f.lon && cap.lat == f.lat;
                if (chosen && capCountry == reg.country && reg.capitalOf == null) { reg.capitalOf = capCountry; reg.type = "capital"; reg.city = Str(f.props, "NAME"); reg.cityPopulation = pop; continue; }
            }
            if (pop >= minCity && pop > reg.cityPopulation)
            {
                reg.city = Str(f.props, "NAME");
                reg.cityPopulation = pop;
                if (reg.type != "capital") reg.type = pop >= largeCity ? "large city" : "normal city";
            }
        }
        int maxPortRank = config["ports"]?["maxScalerank"]?.GetValue<int>() ?? 10;
        foreach (var f in Points(Path.Combine(source, "ne_10m_ports.geojson")))
        {
            if (!InBounds(f.lon, f.lat) || Long(f.props, "scalerank") > maxPortRank) continue;
            int r = RegionAt(f.lon, f.lat, true, 6);
            if (r < 0 || !regions[r].coastal) continue;
            regions[r].port = true;
            regions[r].portName ??= Str(f.props, "name");
            if (regions[r].type == "normal") regions[r].type = "port";
        }
        var airRules = config["airfields"]!.AsObject();
        var airTypes = airRules["types"]!.AsArray().Select(t => t!.GetValue<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int maxAirRank = airRules["maxScalerank"]?.GetValue<int>() ?? 10;
        foreach (var f in Points(Path.Combine(source, "ne_10m_airports.geojson")))
        {
            if (!InBounds(f.lon, f.lat) || !airTypes.Contains(Str(f.props, "type")) || Long(f.props, "scalerank") > maxAirRank) continue;
            int r = RegionAt(f.lon, f.lat, true);
            if (r < 0) continue;
            regions[r].airfield = true;
            regions[r].airfieldName ??= Str(f.props, "name");
        }
        foreach (var r in regions.Where(r => !r.sea && r.city != null && r.type != "normal")) r.name = r.city;

        // ---- 4. write the pack
        Directory.CreateDirectory(outDir);
        var list = new JsonArray();
        foreach (var r in regions)
        {
            var (lon, lat) = Unproject(r.sumX / Math.Max(1, r.pixels), r.sumY / Math.Max(1, r.pixels));
            var o = new JsonObject
            {
                ["id"] = r.id,
                ["name"] = r.name,
            };
            if (r.sea) o["sea"] = true;
            else
            {
                o["country"] = r.country;
                o["type"] = r.type;
                if (r.city != null) o["city"] = new JsonObject { ["name"] = r.city, ["population"] = r.cityPopulation };
                if (r.port) o["port"] = r.portName != null ? JsonValue.Create(r.portName) : JsonValue.Create(true);
                if (r.airfield) o["airfield"] = r.airfieldName != null ? JsonValue.Create(r.airfieldName) : JsonValue.Create(true);
                if (r.coastal) o["coastal"] = true;
                o["provinces"] = new JsonArray(r.provinces.Select(p => (JsonNode)p).ToArray());
            }
            o["center"] = new JsonArray(Math.Round(lon, 2), Math.Round(lat, 2));
            o["pixel"] = new JsonArray((int)(r.sumX / Math.Max(1, r.pixels)), (int)(r.sumY / Math.Max(1, r.pixels)));
            o["areaKm2"] = (long)Math.Round(r.areaKm2);
            o["neighbours"] = new JsonArray(r.border.Keys.OrderBy(k => k).Select(k => (JsonNode)k).ToArray());
            list.Add(o);
        }
        var countries = regions.Where(r => !r.sea).GroupBy(r => r.country).OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => new JsonObject
            {
                ["regions"] = g.Count(),
                ["capital"] = g.FirstOrDefault(r => r.capitalOf != null)?.id is int c ? JsonValue.Create(c) : null,
            });
        var json = new JsonSerializerOptions { WriteIndented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var regionsDoc = new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Regions of the map pack (generated by WC2.Tools build-map from Natural Earth, MAP_PIPELINE.md): id, name, " +
                        "country (game id) and type (capital, port, large city, normal city, normal) of land regions, sea zones, " +
                        "city, port and airfield names, centre (lon, lat), pixel (in regions.raster), area and neighbours.",
            ["regions"] = list,
        };
        File.WriteAllText(Path.Combine(outDir, "regions.json"), FormatRegions(regionsDoc));
        var mapDoc = new JsonObject
        {
            ["format"] = 1,
            ["id"] = config["id"]!.GetValue<string>(),
            ["name"] = config["name"]!.GetValue<string>(),
            ["about"] = "A map pack generated by WC2.Tools build-map (MAP_PIPELINE.md). Source: Natural Earth (public domain), naturalearthdata.com.",
            ["projection"] = "equirectangular",
            ["bounds"] = new JsonObject { ["west"] = west, ["east"] = east, ["north"] = north, ["south"] = south },
            ["raster"] = new JsonObject { ["file"] = "regions.raster", ["width"] = width, ["height"] = height, ["pixelsPerDegree"] = Math.Round(pxPerDeg, 4) },
            ["landRegions"] = regions.Count(r => !r.sea),
            ["seaZones"] = regions.Count(r => r.sea),
            ["countries"] = new JsonObject(countries.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode)kv.Value))),
            // the build configuration (without the classic export, which describes the separate map mod)
            ["config"] = WithoutKey(config, "classicExport"),
        };
        File.WriteAllText(Path.Combine(outDir, "map.json"), mapDoc.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = json.Encoder }) + "\n");
        WriteRaster(Path.Combine(outDir, "regions.raster"), regionRaster, width, height);
        WritePreview(Path.Combine(outDir, "preview.png"), regionRaster, width, height, regions, opts.GetValueOrDefault("preview-width", "2048"));
        if (opts.TryGetValue("classic-out", out var classicOut) && config["classicExport"] != null)
            ExportClassic(classicOut, config, regions, regionRaster, width, height,
                provinces.GroupBy(p => p.country).ToDictionary(g => g.Key, g => g.First().admin));
        Console.WriteLine($"build-map: {regions.Count(r => !r.sea)} land regions in {countries.Count} countries, {regions.Count(r => r.sea)} sea zones, " +
                          $"{regions.Count(r => r.type == "capital")} capitals, {regions.Count(r => r.port)} ports, {regions.Count(r => r.airfield)} airfields, " +
                          $"{sw.ElapsedMilliseconds} ms -> {outDir}");
        return 0;

        bool InBounds(double lon, double lat) => lon >= west && lon <= east && lat >= south && lat <= north;
    }

    private static JsonObject WithoutKey(JsonObject o, string key)
    {
        var copy = (JsonObject)o.DeepClone();
        copy.Remove(key);
        return copy;
    }

    /// <summary>Clears the connected parts of each value whose centre is farther than maxDistance pixels from the value's largest part.</summary>
    private static void DropFarParts(int[] raster, int width, int height, int count, double maxDistance)
    {
        var label = new int[raster.Length];
        var parts = new List<(int value, List<int> pixels)>();
        var stack = new Stack<int>();
        for (int i = 0; i < raster.Length; i++)
        {
            if (raster[i] == 0 || label[i] != 0) continue;
            int v = raster[i];
            var pixels = new List<int>();
            label[i] = parts.Count + 1; stack.Push(i);
            while (stack.Count > 0)
            {
                int j = stack.Pop(); pixels.Add(j);
                int x = j % width, y = j / width;
                foreach (int k in new[] { x > 0 ? j - 1 : -1, x < width - 1 ? j + 1 : -1, y > 0 ? j - width : -1, y < height - 1 ? j + width : -1 })
                    if (k >= 0 && raster[k] == v && label[k] == 0) { label[k] = parts.Count + 1; stack.Push(k); }
            }
            parts.Add((v, pixels));
        }
        foreach (var group in parts.GroupBy(p => p.value))
        {
            var main = group.OrderByDescending(p => p.pixels.Count).First();
            double mx = main.pixels.Average(j => j % width), my = main.pixels.Average(j => j / width);
            foreach (var part in group)
            {
                if (ReferenceEquals(part.pixels, main.pixels)) continue;
                double px = part.pixels.Average(j => j % width), py = part.pixels.Average(j => j / width);
                if (Math.Sqrt((px - mx) * (px - mx) + (py - my) * (py - my)) > maxDistance)
                    foreach (int j in part.pixels) raster[j] = 0;
            }
        }
    }

    /// <summary>For every value of the raster: the touching values and the border length in pixels (4-neighbourhood).</summary>
    private static Dictionary<int, int>[] Adjacency(int[] raster, int width, int height, int count)
    {
        var result = new Dictionary<int, int>[count + 1];
        for (int i = 0; i < result.Length; i++) result[i] = new Dictionary<int, int>();
        void Add(int a, int b)
        {
            if (a == b || a <= 0 || b <= 0 || a > count || b > count) return;
            result[a][b] = result[a].GetValueOrDefault(b) + 1;
            result[b][a] = result[b].GetValueOrDefault(a) + 1;
        }
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x, v = raster[i];
                if (x + 1 < width) Add(v, raster[i + 1]);
                if (y + 1 < height) Add(v, raster[i + width]);
            }
        return result;
    }

    private static IEnumerable<List<(double lon, double lat)>> Rings(JsonElement geometry)
    {
        string type = geometry.GetProperty("type").GetString()!;
        var coords = geometry.GetProperty("coordinates");
        IEnumerable<JsonElement> polygons = type == "MultiPolygon" ? coords.EnumerateArray() : type == "Polygon" ? new[] { coords } : Array.Empty<JsonElement>();
        foreach (var polygon in polygons)
            foreach (var ring in polygon.EnumerateArray())
                yield return ring.EnumerateArray().Select(pt => (pt[0].GetDouble(), pt[1].GetDouble())).ToList();
    }

    private static IEnumerable<(double lon, double lat, JsonElement props)> Points(string file)
    {
        using var doc = JsonDocument.Parse(File.OpenRead(file));
        var list = new List<(double, double, JsonElement)>();
        foreach (var f in doc.RootElement.GetProperty("features").EnumerateArray())
        {
            var g = f.GetProperty("geometry");
            if (g.ValueKind != JsonValueKind.Object || g.GetProperty("type").GetString() != "Point") continue;
            var c = g.GetProperty("coordinates");
            list.Add((c[0].GetDouble(), c[1].GetDouble(), f.GetProperty("properties").Clone()));
        }
        return list;
    }

    private static string Str(JsonElement o, string key) =>
        o.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

    private static long Long(JsonElement o, string key) =>
        o.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? (long)v.GetDouble() : 0;

    /// <summary>One region per line (the file stays reviewable and diffable).</summary>
    private static string FormatRegions(JsonObject doc)
    {
        var compact = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var sb = new System.Text.StringBuilder();
        sb.Append("{\n");
        sb.Append($"  \"format\": {doc["format"]!.ToJsonString()},\n");
        sb.Append($"  \"about\": {doc["about"]!.ToJsonString(compact)},\n");
        sb.Append("  \"regions\": [\n");
        var list = doc["regions"]!.AsArray();
        for (int i = 0; i < list.Count; i++)
            sb.Append("    ").Append(list[i]!.ToJsonString(compact)).Append(i + 1 < list.Count ? ",\n" : "\n");
        sb.Append("  ]\n}\n");
        return sb.ToString();
    }

    /// <summary>"WCRG", version 1, width, height, then deflate compressed uint16 region id + 1 per pixel (0 = none).</summary>
    private static void WriteRaster(string path, int[] raster, int width, int height)
    {
        using var fs = File.Create(path);
        using (var w = new BinaryWriter(fs, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write("WCRG".ToCharArray());
            w.Write((ushort)1);
            w.Write((ushort)0);
            w.Write(width);
            w.Write(height);
        }
        var bytes = new byte[raster.Length * 2];
        for (int i = 0; i < raster.Length; i++) { bytes[i * 2] = (byte)raster[i]; bytes[i * 2 + 1] = (byte)(raster[i] >> 8); }
        using var deflate = new DeflateStream(fs, CompressionLevel.SmallestSize);
        deflate.Write(bytes, 0, bytes.Length);
    }

    private static void WritePreview(string path, int[] raster, int width, int height, List<Region> regions, string previewWidth)
    {
        int pw = int.Parse(previewWidth), ph = (int)Math.Round(height * (double)pw / width);
        using var full = new SKBitmap(width, height);
        var countryColour = new Dictionary<string, SKColor>();
        SKColor Colour(Region r)
        {
            if (r.sea) return new SKColor(70, 110, 150);
            if (!countryColour.TryGetValue(r.country, out var c))
            {
                // a stable hash (string.GetHashCode differs per process): the preview is reproducible
                uint fnv = 2166136261;
                foreach (char ch in r.country) fnv = (fnv ^ ch) * 16777619;
                int h = (int)(fnv & 0x7FFFFFFF);
                c = SKColor.FromHsl(h % 360, 45 + h / 360 % 30, 45 + h / 10800 % 20);
                countryColour[r.country] = c;
            }
            return c;
        }
        var colours = regions.Select(Colour).ToArray();
        var span = full.GetPixelSpan();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x, v = raster[i];
                SKColor c = v == 0 ? new SKColor(20, 30, 40) : colours[v - 1];
                // region borders in a darker shade
                if (v != 0 && ((x + 1 < width && raster[i + 1] != v) || (y + 1 < height && raster[i + width] != v)))
                    c = new SKColor((byte)(c.Red * 0.55), (byte)(c.Green * 0.55), (byte)(c.Blue * 0.55));
                full.SetPixel(x, y, c);
            }
        using var scaled = new SKBitmap(pw, ph);
        full.ScalePixels(scaled, new SKSamplingOptions(SKFilterMode.Linear));
        using var canvas = new SKCanvas(scaled);
        using var dot = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var cap = new SKPaint { Color = new SKColor(255, 220, 0), IsAntialias = true };
        double s = (double)pw / width;
        foreach (var r in regions.Where(r => !r.sea && (r.type != "normal")))
        {
            float x = (float)(r.sumX / r.pixels * s), y = (float)(r.sumY / r.pixels * s);
            canvas.DrawCircle(x, y, r.type == "capital" ? 3f : 1.5f, r.type == "capital" ? cap : dot);
        }
        using var data = scaled.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);
        data.SaveTo(file);
    }
}
