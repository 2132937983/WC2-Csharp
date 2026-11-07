using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkiaSharp;

namespace WC2.Tools;

/// <summary>
/// build-map: one playable base map for every era, from GeoJSON (MAP_PIPELINE.md).
/// <list type="number">
/// <item>Natural Earth admin-1 provinces are rasterized (equirectangular, no antialiasing): every
/// pixel knows its province.</item>
/// <item>Every era of <c>eras</c> is rasterized too: a CShapes 2.0 date (the states and colonies of that
/// day) or Natural Earth's countries (today). Gaps along the coasts take the province's unit, small or
/// thin fragments (two sources disagreeing by a few kilometres) join their neighbour.</item>
/// <item>A pixel's signature is its unit in every era (plus the occupation zones of the scenarios); the
/// provinces split along every era's borders and merge again only with neighbours of the same
/// signature, until regions reach the configured size. So the base map holds the borders of all eras,
/// and a scenario of any era only assigns owners.</item>
/// <item>Far island groups become regions of their own (or are dropped when tiny); a wrapping map
/// splits regions at the seam.</item>
/// <item>Water becomes sea zones of irregular shape: seeds spaced by the distance from the coast (small
/// zones along the coasts, large ones in the open ocean), grown through the water and relaxed.</item>
/// <item>Adjacency (across the seam on a wrapping map, plus configured straits), capitals of every era,
/// cities, ports and airfields.</item>
/// </list>
/// Output (the map pack): map.json, regions.json, regions.raster (the region of every pixel, uint16,
/// deflate), preview.png; with --classic-out the playable map mod (MapClassicExport.cs).
/// </summary>
internal static partial class MapPipeline
{
    private sealed class Province
    {
        public int index;
        public string name = "", iso = "", country = "", adm0 = "", admin = "";
        public long pixels;
        public double areaKm2;
        public int occupation;   // index into the occupation keys (0: none)
        public double sumX, sumY;
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
        public int signature = -1;
        public string[] owners = Array.Empty<string>();   // per era: the unit holding the region
        public string occupation = "";                     // scenario occupation key ("" none)
        public int province = -1;                          // its largest province (naming)
        public string placeName;                           // the largest town inside (naming)
    }

    /// <summary>One era of the base map: CShapes on a date, or Natural Earth (today).</summary>
    private sealed class Era
    {
        public string key = "";
        public bool naturalEarth;
        public (int y, int m, int d) date;
        public int[] unit = Array.Empty<int>();   // per pixel: unit index + 1 (0 = none)
    }

    /// <summary>A political unit: a CShapes state or colony ("gw:290") or a Natural Earth country ("ne:POL").</summary>
    private sealed class Unit
    {
        public string key = "";
        public readonly Dictionary<string, (string name, string capital, double lon, double lat)> eras = new();
    }

    public static int Run(Dictionary<string, string> opts)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string configPath = opts["config"], source = opts["source"], outDir = opts["out"];
        var config = JsonNode.Parse(File.ReadAllText(configPath), null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })!.AsObject();
        var b = config["bounds"]!.AsObject();
        double west = b["west"]!.GetValue<double>(), east = b["east"]!.GetValue<double>(), north = b["north"]!.GetValue<double>(), south = b["south"]!.GetValue<double>();
        bool wrap = config["wrap"]?.GetValue<bool>() ?? false;
        if (wrap && Math.Abs(east - west - 360) > 1e-6) throw new ToolException("a wrapping map spans 360 degrees (west -180, east 180)");
        int width = config["raster"]?["width"]?.GetValue<int>() ?? 4096;
        double pxPerDeg = width / (east - west);
        int height = (int)Math.Round((north - south) * pxPerDeg);
        int n = width * height;
        var rules = config["regions"]!.AsObject();
        double minAreaKm2 = rules["minAreaKm2"]?.GetValue<double>() ?? 0;
        int maxPerCountry = rules["maxPerCountry"]?.GetValue<int>() ?? int.MaxValue;
        double minIslandKm2 = rules["minIslandKm2"]!.GetValue<double>();
        int minBorder = config["minBorderPixels"]?.GetValue<int>() ?? 2;
        // per-country sizing (game id -> { minAreaKm2, maxPerCountry }), e.g. a finer Europe
        var overrides = config["regionOverrides"]?.AsObject() ?? new JsonObject();
        double MinArea(string c) => overrides[c]?["minAreaKm2"]?.GetValue<double>() ?? minAreaKm2;
        int MaxRegions(string c) => overrides[c]?["maxPerCountry"]?.GetValue<int>() ?? maxPerCountry;
        var countryIds = config["countries"]?.AsObject().ToDictionary(kv => kv.Key, kv => kv.Value!.GetValue<string>(), StringComparer.OrdinalIgnoreCase) ?? new();

        (double x, double y) Project(double lon, double lat) => ((lon - west) * pxPerDeg, (north - lat) * pxPerDeg);
        (double lon, double lat) Unproject(double x, double y) => (west + x / pxPerDeg, north - y / pxPerDeg);
        var rowKm2 = new double[height];
        for (int y = 0; y < height; y++)
        {
            double lat = north - (y + 0.5) / pxPerDeg;
            rowKm2[y] = (111.32 * Math.Cos(lat * Math.PI / 180) / pxPerDeg) * (110.57 / pxPerDeg);
        }
        var scenarios = config["classicExport"]?["scenarios"]?.AsArray().Select(s => s!.AsObject()).ToList() ?? new();

        // ---- 1. rasterize the provinces
        var provinces = new List<Province>();
        // occupation zones of the scenarios (game id per province): their borders are kept like an era's
        var occupationKeys = new List<string> { "" };
        var occupationIndex = new Dictionary<string, int> { [""] = 0 };
        string OccupationOf(string adm0, string iso, string name)
        {
            var parts = new List<string>();
            for (int s = 0; s < scenarios.Count; s++)
                foreach (var (owner, list) in scenarios[s]["provinceOwners"]?.AsObject() ?? new JsonObject())
                    foreach (var p in list!.AsArray())
                    {
                        string key = p!.GetValue<string>();
                        if (key == iso || key == name || key == adm0 + "/" + name || key == adm0) parts.Add($"{s}:{owner}");
                    }
            return string.Join("|", parts);
        }
        using var admin1 = JsonDocument.Parse(File.OpenRead(Path.Combine(source, "ne_10m_admin_1_states_provinces.geojson")));
        var carve = WaterCarver(config, source, west, north, pxPerDeg);
        var raster = Rasterize(admin1.RootElement.GetProperty("features"), west, east, north, south, pxPerDeg, width, height, p =>
        {
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
            string occupation = OccupationOf(province.adm0, province.iso, province.name);
            if (!occupationIndex.TryGetValue(occupation, out int oi)) { oi = occupationKeys.Count; occupationKeys.Add(occupation); occupationIndex[occupation] = oi; }
            province.occupation = oi;
            provinces.Add(province);
            return provinces.Count;   // 1-based
        }, false, carve);
        for (int i = 0; i < n; i++)
        {
            int v = raster[i];
            if (v <= 0 || v > provinces.Count) { raster[i] = 0; continue; }
            provinces[v - 1].pixels++;
            provinces[v - 1].areaKm2 += rowKm2[i / width];
            provinces[v - 1].sumX += i % width;
            provinces[v - 1].sumY += i / width;
        }
        Console.WriteLine($"build-map: {provinces.Count(p => p.pixels > 0)} provinces rasterized at {width}x{height}");

        // ---- 2. the eras: one unit per land pixel and era
        var units = new List<Unit>();
        var unitIndex = new Dictionary<string, int>();
        int UnitOf(string key)
        {
            if (!unitIndex.TryGetValue(key, out int u)) { u = units.Count; units.Add(new Unit { key = key }); unitIndex[key] = u; }
            return u;
        }
        var eras = new List<Era>();
        var hist = config["historicalBorders"]?.AsObject();
        JsonDocument cshapes = null;
        foreach (var (key, node) in config["eras"]?.AsObject() ?? new JsonObject())
        {
            var era = new Era { key = key };
            if (node!["source"]?.GetValue<string>() == "naturalearth")
            {
                era.naturalEarth = true;
                era.unit = new int[n];
                for (int i = 0; i < n; i++)
                    if (raster[i] > 0)
                    {
                        var p = provinces[raster[i] - 1];
                        int u = UnitOf("ne:" + p.adm0);
                        units[u].eras.TryAdd(key, (p.admin, "", double.NaN, double.NaN));
                        era.unit[i] = u + 1;
                    }
            }
            else
            {
                var d = node["date"]!.GetValue<string>().Split('-').Select(int.Parse).ToArray();
                era.date = (d[0], d[1], d[2]);
                cshapes ??= JsonDocument.Parse(File.OpenRead(Path.Combine(source, hist!["file"]!.GetValue<string>())));
                era.unit = Rasterize(cshapes.RootElement.GetProperty("features"), west, east, north, south, pxPerDeg, width, height, p =>
                {
                    var start = ((int)Long(p, "gwsyear"), (int)Long(p, "gwsmonth"), (int)Long(p, "gwsday"));
                    var end = ((int)Long(p, "gweyear"), (int)Long(p, "gwemonth"), (int)Long(p, "gweday"));
                    if (start.CompareTo(era.date) > 0 || end.CompareTo(era.date) < 0) return 0;
                    int u = UnitOf("gw:" + Long(p, "gwcode"));
                    units[u].eras[era.key] = (Str(p, "cntry_name"), Str(p, "capname"), Dbl(p, "caplong"), Dbl(p, "caplat"));
                    return u + 1;
                });
                var fallback = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var (adm0, gw) in hist!["fallback"]?.AsObject() ?? new JsonObject())
                {
                    int u = UnitOf("gw:" + gw!.GetValue<string>());
                    fallback[adm0] = u + 1;
                }
                int moved = FillAndClean(era.unit, raster, provinces, width, height, rowKm2, fallback,
                                         hist["minFragmentKm2"]?.GetValue<double>() ?? 4000, hist["minFragmentWidthPixels"]?.GetValue<int>() ?? 3);
                // corrections of the source: a polygon held by another unit in the listed eras (CShapes draws
                // the German Empire without the Memel Territory, and Memel Lithuanian in September 1939)
                foreach (var fix in hist["corrections"]?.AsArray() ?? new JsonArray())
                {
                    if (!fix!["eras"]!.AsArray().Any(e => e!.GetValue<string>() == key)) continue;
                    int u = UnitOf("gw:" + fix["unit"]!.GetValue<string>());
                    var ring = fix["polygon"]!.AsArray().Select(v => v!.GetValue<double>()).ToArray();
                    var mask = RasterizeRing(ring, west, north, pxPerDeg, width, height);
                    int painted = 0;
                    for (int i = 0; i < n; i++)
                        if (mask[i] && raster[i] != 0) { era.unit[i] = u + 1; painted++; }
                    units[u].eras.TryAdd(key, (fix["unitName"]?.GetValue<string>() ?? units[u].key, "", double.NaN, double.NaN));
                    Console.WriteLine($"build-map: era {key}: {fix["name"]} ({painted} pixels) to gw:{fix["unit"]}");
                }
                Console.WriteLine($"build-map: era {key}: {era.unit.Where(v => v > 0).Distinct().Count()} units, {moved} pixels of small fragments joined their neighbours");
            }
            eras.Add(era);
        }
        cshapes?.Dispose();
        foreach (var u in units)
            foreach (var e in eras.Where(e => !u.eras.ContainsKey(e.key)))
                u.eras[e.key] = (u.key, "", double.NaN, double.NaN);

        // ---- 3. signatures (the unit of every era + the occupation zone) and atoms (province x signature)
        var pairIds = new Dictionary<(int, int), int>();
        var pairs = new List<(int prev, int unit)> { (0, 0) };
        int Pair(int a, int u)
        {
            if (!pairIds.TryGetValue((a, u), out int id)) { id = pairs.Count; pairs.Add((a, u)); pairIds[(a, u)] = id; }
            return id;
        }
        var atomIds = new Dictionary<(int province, int sig), int>();
        var atomProvince = new List<int>();
        var atomSig = new List<int>();
        var atomArea = new List<double>();
        var atomSumX = new List<double>(); var atomSumY = new List<double>(); var atomPixels = new List<long>();
        var atomRaster = new int[n];   // atom id + 1
        for (int i = 0; i < n; i++)
        {
            int pv = raster[i];
            if (pv == 0) continue;
            int s = Pair(0, -provinces[pv - 1].occupation);
            foreach (var era in eras) s = Pair(s, era.unit[i]);
            if (!atomIds.TryGetValue((pv - 1, s), out int atom))
            {
                atom = atomProvince.Count;
                atomIds[(pv - 1, s)] = atom;
                atomProvince.Add(pv - 1); atomSig.Add(s); atomArea.Add(0);
                atomSumX.Add(0); atomSumY.Add(0); atomPixels.Add(0);
            }
            atomArea[atom] += rowKm2[i / width];
            atomSumX[atom] += i % width; atomSumY[atom] += i / width; atomPixels[atom]++;
            atomRaster[i] = atom + 1;
        }
        // signature -> units per era (walk the pair chain back)
        int[] SigUnits(int sig)
        {
            var list = new int[eras.Count];
            for (int e = eras.Count - 1; e >= 0; e--) { list[e] = pairs[sig].unit; sig = pairs[sig].prev; }
            return list;
        }
        int SigOccupation(int sig)
        {
            for (int e = 0; e < eras.Count; e++) sig = pairs[sig].prev;
            return -pairs[sig].unit;
        }
        Console.WriteLine($"build-map: {atomProvince.Count} province parts in {atomSig.Distinct().Count()} era signatures");

        // ---- 4. regions of the size the place calls for. The target area comes from "detail" (a country,
        // else the first zone containing the part, else the default) or, without it, from the old rules
        // (minAreaKm2, maxPerCountry). Parts merge only with neighbours of the same signature (every era's
        // border stays), smallest first, until they have mergeBelow x the target; afterwards groups above
        // splitAbove x the target are cut into cells of about the target area.
        int atoms = atomProvince.Count;
        var detail = config["detail"]?.AsObject();
        double defaultKm2 = detail?["defaultKm2"]?.GetValue<double>() ?? minAreaKm2;
        double mergeBelow = detail?["mergeBelow"]?.GetValue<double>() ?? 1.0;
        double splitAbove = detail?["splitAbove"]?.GetValue<double>() ?? double.PositiveInfinity;
        var detailZones = (detail?["zones"]?.AsArray() ?? new JsonArray()).Select(z => (
            w: z!["west"]!.GetValue<double>(), e: z["east"]!.GetValue<double>(), s: z["south"]!.GetValue<double>(),
            nn: z["north"]!.GetValue<double>(), km2: z["km2"]!.GetValue<double>())).ToList();
        var detailCountries = (detail?["countries"]?.AsObject() ?? new JsonObject()).ToDictionary(kv => kv.Key, kv => kv.Value!.GetValue<double>());
        var gBorder = Adjacency(atomRaster, width, height, atoms, false);   // index atom + 1
        var parent = Enumerable.Range(0, atoms).ToArray();
        var alive = Enumerable.Repeat(true, atoms).ToArray();
        var gArea = atomArea.ToArray();
        var gSumX = atomSumX.ToArray(); var gSumY = atomSumY.ToArray(); var gPixels = atomPixels.ToArray();
        string GCountry(int g) => provinces[atomProvince[g]].country;
        double Target(int g)
        {
            string c = GCountry(g);
            if (detail == null) return MinArea(c);
            if (detailCountries.TryGetValue(c, out double km2)) return km2;
            var (lon, lat) = Unproject(gSumX[g] / Math.Max(1, gPixels[g]), gSumY[g] / Math.Max(1, gPixels[g]));
            foreach (var z in detailZones)
                if (lon >= z.w && lon <= z.e && lat >= z.s && lat <= z.nn) return z.km2;
            return defaultKm2;
        }
        var perCountry = Enumerable.Range(0, atoms).GroupBy(GCountry).ToDictionary(g => g.Key, g => g.Count());
        void Merge(int a, int into)
        {
            parent[a] = into; gArea[into] += gArea[a]; alive[a] = false;
            gSumX[into] += gSumX[a]; gSumY[into] += gSumY[a]; gPixels[into] += gPixels[a];
            foreach (var (o1, len) in gBorder[a + 1])
            {
                int o = o1 - 1;
                if (o == into || o < 0) continue;
                gBorder[into + 1][o + 1] = gBorder[into + 1].GetValueOrDefault(o + 1) + len;
                gBorder[o + 1].Remove(a + 1);
                gBorder[o + 1][into + 1] = gBorder[o + 1].GetValueOrDefault(into + 1) + len;
            }
            gBorder[into + 1].Remove(a + 1);
            gBorder[a + 1].Clear();
        }
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (int g in Enumerable.Range(0, atoms).Where(g => alive[g]).OrderBy(g => gArea[g]).ThenBy(g => g).ToList())
            {
                if (!alive[g]) continue;
                string c = GCountry(g);
                bool small = gArea[g] < mergeBelow * Target(g), tooMany = detail == null && perCountry[c] > MaxRegions(c);
                if (!small && !tooMany) continue;
                int best = -1; double bestArea = double.MaxValue;
                foreach (var (o1, len) in gBorder[g + 1])
                {
                    int o = o1 - 1;
                    if (o < 0 || len < minBorder || !alive[o] || atomSig[o] != atomSig[g]) continue;
                    if (gArea[o] < bestArea || (gArea[o] == bestArea && o < best)) { bestArea = gArea[o]; best = o; }
                }
                if (best < 0) continue;
                Merge(g, best);
                perCountry[c]--;
                changed = true;
            }
        }
        // islands a scenario needs although they are small (regions.keepIslands: Barbados, Martinique, Minorca,
        // Malta, ...): the group at each point (within 3 pixels) stays a region of its own
        var keep = new HashSet<int>();
        foreach (var k in rules["keepIslands"]?.AsArray() ?? new JsonArray())
        {
            var (kx, ky) = Project(k!["lon"]!.GetValue<double>(), k["lat"]!.GetValue<double>());
            int found = -1; double bestD = double.MaxValue;
            for (int dy = -3; dy <= 3; dy++)
                for (int dx = -3; dx <= 3; dx++)
                {
                    int x = (int)kx + dx, y = (int)ky + dy;
                    if (wrap) x = (x % width + width) % width;
                    if (x < 0 || y < 0 || x >= width || y >= height || atomRaster[y * width + x] == 0) continue;
                    if (dx * dx + dy * dy < bestD) { bestD = dx * dx + dy * dy; found = atomRaster[y * width + x] - 1; }
                }
            if (found < 0) { Console.WriteLine($"build-map: keepIslands: no land at {k["name"]}"); continue; }
            keep.Add(found);
        }
        // a kept island may be several provinces (parishes) that merge with each other first: a group is
        // kept while it holds one of the atoms found
        bool Kept(int g) => keep.Any(a => { while (parent[a] != a) a = parent[a]; return a == g; });
        // what is still below minIslandKm2: an enclave joins the neighbour it shares the longest border
        // with (its own borders are lost), an island is left out
        var dropped = new bool[atoms];
        foreach (int g in Enumerable.Range(0, atoms).Where(g => alive[g] && gArea[g] < minIslandKm2).OrderBy(g => gArea[g]).ThenBy(g => g).ToList())
        {
            if (!alive[g] || gArea[g] >= minIslandKm2) continue;
            if (!gBorder[g + 1].Keys.Any(o1 => o1 > 0 && alive[o1 - 1]) && Kept(g)) continue;
            int best = -1, bestLen = 0;
            foreach (var (o1, len) in gBorder[g + 1])
            {
                int o = o1 - 1;
                if (o >= 0 && alive[o] && (len > bestLen || (len == bestLen && o < best))) { bestLen = len; best = o; }
            }
            if (best >= 0) Merge(g, best);
            else { alive[g] = false; dropped[g] = true; }
        }
        int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }

        // ---- regions from the groups; a group well above its target is cut into cells
        var regions = new List<Region>();
        var groupRegion = new Dictionary<int, int>();
        var split = new Dictionary<int, int>();   // group -> number of cells
        foreach (int g in Enumerable.Range(0, atoms).Where(g => alive[g] && gArea[g] > splitAbove * Target(g)))
            split[g] = Math.Max(2, (int)Math.Round(gArea[g] / Target(g)));
        var splitPixels = split.Keys.ToDictionary(g => g, _ => new List<int>());
        for (int i = 0; i < n; i++)
        {
            int a = atomRaster[i];
            if (a != 0 && splitPixels.TryGetValue(Find(a - 1), out var pl)) pl.Add(i);
        }
        var regionRaster = new int[n];   // region id + 1
        var scratch = new int[n];
        var members = Enumerable.Range(0, atoms).Where(a => !dropped[Find(a)]).GroupBy(Find).OrderBy(g => g.Key);
        Region NewRegion(IGrouping<int, int> g)
        {
            var biggestAtom = g.OrderByDescending(a => atomArea[a]).First();
            var biggest = provinces[atomProvince[biggestAtom]];
            var r = new Region { id = regions.Count, country = biggest.country, name = biggest.name, signature = atomSig[g.Key], province = biggest.index };
            foreach (var p in g.Select(a => provinces[atomProvince[a]]).Distinct().OrderByDescending(p => p.areaKm2))
                r.provinces.Add(p.iso.Length > 0 ? p.iso : p.name);
            var su = SigUnits(r.signature);
            r.owners = su.Select(u => u > 0 ? units[u - 1].key : "").ToArray();
            r.occupation = occupationKeys[SigOccupation(r.signature)];
            regions.Add(r);
            return r;
        }
        int cells = 0;
        foreach (var g in members)
        {
            if (!split.TryGetValue(g.Key, out int k))
            {
                groupRegion[g.Key] = NewRegion(g).id;
                continue;
            }
            var pixels = splitPixels[g.Key];
            var cellOf = Subdivide(pixels, k, width, height, scratch);
            var cellRegion = new int[k];
            for (int c = 0; c < k; c++) cellRegion[c] = -1;
            for (int j = 0; j < pixels.Count; j++)
            {
                int c = cellOf[j];
                if (cellRegion[c] < 0) { cellRegion[c] = NewRegion(g).id; cells++; }
                regionRaster[pixels[j]] = cellRegion[c] + 1;
            }
        }
        for (int i = 0; i < n; i++)
        {
            int a = atomRaster[i];
            if (a == 0 || regionRaster[i] != 0) continue;
            int root = Find(a - 1);
            regionRaster[i] = groupRegion.TryGetValue(root, out int rid) ? rid + 1 : 0;
        }
        if (split.Count > 0) Console.WriteLine($"build-map: {split.Count} large groups cut into {cells} cells");

        // ---- 5. one landmass, one region: a region in several pieces (left behind by the era borders)
        // keeps its largest; a piece of minPartKm2 becomes a region of its own, a smaller piece joins the
        // land region it touches most, small islands stay with their coast
        // (a kept island that is a piece of a bigger region becomes a region of its own: Minorca)
        var keepPixels = new HashSet<int>();
        foreach (var k in rules["keepIslands"]?.AsArray() ?? new JsonArray())
        {
            var (kx, ky) = Project(k!["lon"]!.GetValue<double>(), k["lat"]!.GetValue<double>());
            int best = -1; double bestD = double.MaxValue;
            for (int dy = -3; dy <= 3; dy++)
                for (int dx = -3; dx <= 3; dx++)
                {
                    int x = (int)kx + dx, y = (int)ky + dy;
                    if (wrap) x = (x % width + width) % width;
                    if (x < 0 || y < 0 || x >= width || y >= height || regionRaster[y * width + x] == 0) continue;
                    if (dx * dx + dy * dy < bestD) { bestD = dx * dx + dy * dy; best = y * width + x; }
                }
            if (best >= 0) keepPixels.Add(best);
        }
        SeparatePieces(regionRaster, width, height, wrap, rowKm2, regions, rules["minPartKm2"]?.GetValue<double>() ?? 1500, keepPixels);
        // far parts: island groups far from their region become regions of their own (tiny ones far away
        // are left out); a region may reach over the seam of a wrapping map (-180 and 180 are one meridian)
        var partRules = rules;
        SplitFarParts(regionRaster, width, height, wrap, rowKm2, regions,
                      (partRules["islandSplitDegrees"]?.GetValue<double>() ?? 6) * pxPerDeg,
                      (partRules["islandClusterDegrees"]?.GetValue<double>() ?? 1.5) * pxPerDeg,
                      (partRules["maxPartDistanceDegrees"]?.GetValue<double>() ?? 25) * pxPerDeg,
                      minIslandKm2);

        // ---- 6. sea zones of irregular shape
        BuildSeaZones(regionRaster, width, height, pxPerDeg, config["sea"]!.AsObject(), regions, wrap);
        // renumber: land first, then the sea zones that kept pixels
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
        for (int i = 0; i < n; i++) regionRaster[i] = newId[regionRaster[i]];
        regions = final;
        foreach (var r in regions) { r.pixels = 0; r.areaKm2 = 0; r.sumX = 0; r.sumY = 0; }
        // x is summed relative to the region's first pixel, the short way over the seam, so a region on
        // both sides of the date line has its centre there and not in the middle of the map
        var refX = new int[regions.Count];
        for (int i = 0; i < n; i++)
        {
            int v = regionRaster[i];
            if (v == 0) continue;
            var r = regions[v - 1];
            int x = i % width;
            if (r.pixels == 0) refX[v - 1] = x;
            int dx = x - refX[v - 1];
            if (wrap) { if (dx > width / 2) dx -= width; else if (dx < -width / 2) dx += width; }
            r.pixels++; r.areaKm2 += rowKm2[i / width]; r.sumX += refX[v - 1] + dx; r.sumY += i / width;
        }
        if (wrap)
            foreach (var r in regions.Where(r => r.pixels > 0))
            {
                double cx = r.sumX / r.pixels;
                double wrapped = ((cx % width) + width) % width;
                r.sumX += (wrapped - cx) * r.pixels;
            }

        // ---- 7. adjacency (over the seam on a wrapping map) and the straits the raster closes
        var border = Adjacency(regionRaster, width, height, regions.Count, wrap);
        foreach (var r in regions)
            foreach (var (other, len) in border[r.id + 1])
                if (other > 0 && other != r.id + 1 && len >= minBorder) r.border[other - 1] = len;
        int RegionAt(double lon, double lat, Func<Region, bool> accept, int searchPx = 3)
        {
            var (fx, fy) = Project(lon, lat);
            int x0 = (int)fx, y0 = (int)fy;
            for (int rad = 0; rad <= searchPx; rad++)
                for (int dy = -rad; dy <= rad; dy++)
                    for (int dx = -rad; dx <= rad; dx++)
                    {
                        int x = x0 + dx, y = y0 + dy;
                        if (wrap) x = ((x % width) + width) % width;
                        if (x < 0 || y < 0 || x >= width || y >= height) continue;
                        int v = regionRaster[y * width + x];
                        if (v > 0 && accept(regions[v - 1])) return v - 1;
                    }
            return -1;
        }
        int straits = 0;
        foreach (var s in config["straits"]?.AsArray() ?? new JsonArray())
        {
            var p = s!["points"]!.AsArray().Select(v => v!.GetValue<double>()).ToArray();
            int a = RegionAt(p[0], p[1], r => r.sea, 12), c = RegionAt(p[2], p[3], r => r.sea, 12);
            if (a < 0 || c < 0) { Console.WriteLine($"build-map: strait {s["name"]}: no sea zone at one of its points"); continue; }
            if (a == c) continue;   // open in the raster already
            regions[a].border[c] = Math.Max(regions[a].border.GetValueOrDefault(c), minBorder);
            regions[c].border[a] = Math.Max(regions[c].border.GetValueOrDefault(a), minBorder);
            straits++;
        }
        // a region that touches nothing (an island in a lake too small for a sea zone) is linked to the
        // nearest region across the water, so every province can be reached
        int ferries = 0;
        foreach (var r in regions.Where(r => r.border.Count == 0))
        {
            var seen = new HashSet<int>();
            var frontier = new Queue<int>();
            for (int i = 0; i < n; i++) if (regionRaster[i] == r.id + 1) { frontier.Enqueue(i); seen.Add(i); }
            int found = -1;
            while (frontier.Count > 0 && found < 0)
            {
                int i = frontier.Dequeue(), x = i % width, y = i / width;
                foreach (int j in new[] { x > 0 ? i - 1 : (wrap ? i + width - 1 : -1), x < width - 1 ? i + 1 : (wrap ? i - width + 1 : -1), y > 0 ? i - width : -1, y < height - 1 ? i + width : -1 })
                {
                    if (j < 0 || !seen.Add(j)) continue;
                    int v = regionRaster[j];
                    if (v != 0 && v != r.id + 1) { found = v - 1; break; }
                    if (v == 0) frontier.Enqueue(j);
                }
            }
            if (found < 0) continue;
            r.border[found] = minBorder;
            regions[found].border[r.id] = minBorder;
            ferries++;
        }
        if (ferries > 0) Console.WriteLine($"build-map: {ferries} isolated regions linked across the water");
        foreach (var r in regions.Where(r => !r.sea)) r.coastal = r.border.Keys.Any(o => regions[o].sea);

        // ---- 8. features: capitals of today and of every era, cities, ports, airfields
        var cityRules = config["cities"]!.AsObject();
        long largeCity = cityRules["largeCityPopulation"]!.GetValue<long>(), minCity = cityRules["minPopulation"]!.GetValue<long>();
        var places = Points(Path.Combine(source, "ne_50m_populated_places.geojson")).ToList();
        // one capital per country today: the most populous place marked as a country capital (Bolivia,
        // Cote d'Ivoire and others list two)
        var capitals = places.Where(f => Long(f.props, "ADM0CAP") == 1 && InBounds(f.lon, f.lat))
            .GroupBy(f => countryIds.TryGetValue(Str(f.props, "ADM0_A3"), out var cid) ? cid : Str(f.props, "ISO_A2").ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.OrderByDescending(f => Long(f.props, "POP_MAX")).First());
        foreach (var f in places)
        {
            if (!InBounds(f.lon, f.lat)) continue;
            int r = RegionAt(f.lon, f.lat, r => !r.sea);
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
        // the capitals the game's countries had in the other eras (Saint Petersburg, Nanking, Bonn, ...)
        var homeUnits = new Dictionary<string, string>();
        foreach (var (gameId, gwList) in config["homeUnits"]?.AsObject() ?? new JsonObject())
            foreach (var gw in gwList!.AsArray()) homeUnits["gw:" + gw!.GetValue<string>()] = gameId;
        int eraCapitals = 0;
        foreach (var u in units.Where(u => homeUnits.ContainsKey(u.key)))
            foreach (var (eraKey, info) in u.eras)
            {
                if (double.IsNaN(info.lon) || !InBounds(info.lon, info.lat)) continue;
                int r = RegionAt(info.lon, info.lat, r => !r.sea, 4);
                if (r < 0 || regions[r].type == "capital") continue;
                regions[r].type = "capital";
                regions[r].capitalOf = homeUnits[u.key];
                regions[r].city ??= info.capital;
                eraCapitals++;
            }
        int maxPortRank = config["ports"]?["maxScalerank"]?.GetValue<int>() ?? 10;
        foreach (var f in Points(Path.Combine(source, "ne_10m_ports.geojson")))
        {
            if (!InBounds(f.lon, f.lat) || Long(f.props, "scalerank") > maxPortRank) continue;
            int r = RegionAt(f.lon, f.lat, r => !r.sea, 6);
            if (r < 0 || !regions[r].coastal) continue;
            regions[r].port = true;
            regions[r].portName ??= Str(f.props, "name");
            // the land region keeps its type: the original's port areas are the sea zones off the port
            // (the harbour, where ships are built), which the map mod marks
        }
        var airRules = config["airfields"]!.AsObject();
        var airTypes = airRules["types"]!.AsArray().Select(t => t!.GetValue<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int maxAirRank = airRules["maxScalerank"]?.GetValue<int>() ?? 10;
        foreach (var f in Points(Path.Combine(source, "ne_10m_airports.geojson")))
        {
            if (!InBounds(f.lon, f.lat) || !airTypes.Contains(Str(f.props, "type")) || Long(f.props, "scalerank") > maxAirRank) continue;
            int r = RegionAt(f.lon, f.lat, r => !r.sea);
            if (r < 0) continue;
            regions[r].airfield = true;
            regions[r].airfieldName ??= Str(f.props, "name");
        }
        foreach (var r in regions.Where(r => !r.sea && r.city != null && r.type != "normal")) r.name = r.city;
        // the other land regions: the largest town inside (Natural Earth 10m populated places when the
        // source has them), else their province's name, with a compass point when the province has several
        string townFile = Path.Combine(source, "ne_10m_populated_places.geojson");
        foreach (var f in (File.Exists(townFile) ? Points(townFile) : places).OrderByDescending(f => Long(f.props, "POP_MAX")))
        {
            if (!InBounds(f.lon, f.lat)) continue;
            int r = RegionAt(f.lon, f.lat, r => !r.sea, 1);
            if (r >= 0) regions[r].placeName ??= Str(f.props, "NAME");
        }
        foreach (var r in regions.Where(r => !r.sea && (r.city == null || r.type == "normal") && r.placeName != null)) r.name = r.placeName;
        foreach (var same in regions.Where(r => !r.sea).GroupBy(r => (r.country, r.name)).Where(g => g.Count() > 1))
        {
            foreach (var r in same)
            {
                if (r.province < 0 || provinces[r.province].pixels == 0) continue;
                var pr = provinces[r.province];
                double dx = r.sumX / Math.Max(1, r.pixels) - pr.sumX / pr.pixels, dy = r.sumY / Math.Max(1, r.pixels) - pr.sumY / pr.pixels;
                if (Math.Abs(dx) < 1 && Math.Abs(dy) < 1) continue;
                string[] points = { "E", "SE", "S", "SW", "W", "NW", "N", "NE" };
                int octant = (int)Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4));
                r.name += " (" + points[(octant % 8 + 8) % 8] + ")";
            }
            foreach (var dup in same.GroupBy(r => r.name).Where(g => g.Count() > 1))
            {
                int k = 1;
                foreach (var r in dup.Skip(1)) r.name += " " + (++k);
            }
        }

        // ---- 9. write the pack
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
                if (eras.Count > 0)
                {
                    var owners = new JsonObject();
                    for (int e = 0; e < eras.Count; e++) owners[eras[e].key] = r.owners[e];
                    o["owners"] = owners;
                }
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
                ["capital"] = g.FirstOrDefault(r => r.capitalOf == g.Key)?.id is int c ? JsonValue.Create(c) : null,
            });
        var json = new JsonSerializerOptions { WriteIndented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var regionsDoc = new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Regions of the map pack (generated by WC2.Tools build-map, MAP_PIPELINE.md): id, name, country (game id " +
                        "today) and type (capital, port, large city, normal city, normal) of land regions, sea zones, city, port and " +
                        "airfield names, the unit holding the region in every era (owners: map.json eras), centre (lon, lat), pixel " +
                        "(in regions.raster), area and neighbours.",
            ["regions"] = list,
        };
        File.WriteAllText(Path.Combine(outDir, "regions.json"), FormatRegions(regionsDoc));
        var erasDoc = new JsonObject();
        foreach (var e in eras)
        {
            var present = regions.Where(r => !r.sea).Select(r => r.owners[eras.IndexOf(e)]).Where(k => k.Length > 0).ToHashSet();
            var unitsDoc = new JsonObject();
            foreach (var u in units.Where(u => present.Contains(u.key)).OrderBy(u => u.key, StringComparer.Ordinal))
            {
                var info = u.eras[e.key];
                var uo = new JsonObject { ["name"] = info.name };
                if (!string.IsNullOrEmpty(info.capital)) uo["capital"] = info.capital;
                unitsDoc[u.key] = uo;
            }
            erasDoc[e.key] = new JsonObject
            {
                ["source"] = e.naturalEarth ? "Natural Earth admin-0 (today)" : $"CShapes 2.0 on {e.date.y:D4}-{e.date.m:D2}-{e.date.d:D2}",
                ["units"] = unitsDoc,
            };
        }
        var mapDoc = new JsonObject
        {
            ["format"] = 1,
            ["id"] = config["id"]!.GetValue<string>(),
            ["name"] = config["name"]!.GetValue<string>(),
            ["about"] = "A map pack generated by WC2.Tools build-map (MAP_PIPELINE.md). Sources: Natural Earth (public domain, " +
                        "naturalearthdata.com); historical borders: CShapes 2.0 (Schvitz, Ruegger, Girardin, Cederman, Weidmann, " +
                        "Gleditsch; CC BY-NC-SA 4.0, icr.ethz.ch/data/cshapes).",
            ["projection"] = "equirectangular",
            ["wrap"] = wrap,
            ["bounds"] = new JsonObject { ["west"] = west, ["east"] = east, ["north"] = north, ["south"] = south },
            ["raster"] = new JsonObject { ["file"] = "regions.raster", ["width"] = width, ["height"] = height, ["pixelsPerDegree"] = Math.Round(pxPerDeg, 4) },
            ["landRegions"] = regions.Count(r => !r.sea),
            ["seaZones"] = regions.Count(r => r.sea),
            ["countries"] = new JsonObject(countries.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode)kv.Value))),
            ["eras"] = erasDoc,
            // the build configuration (without the classic export, which describes the separate map mod)
            ["config"] = WithoutKey(config, "classicExport"),
        };
        File.WriteAllText(Path.Combine(outDir, "map.json"), mapDoc.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = json.Encoder }) + "\n");
        WriteRaster(Path.Combine(outDir, "regions.raster"), regionRaster, width, height);
        string previewEra = config["previewEra"]?.GetValue<string>();
        int previewIndex = previewEra != null ? eras.FindIndex(e => e.key == previewEra) : -1;
        WritePreview(Path.Combine(outDir, "preview.png"), regionRaster, width, height, regions,
                     opts.GetValueOrDefault("preview-width", "2048"), r => previewIndex >= 0 ? r.owners[previewIndex] : r.country);
        if (opts.TryGetValue("preview-dir", out var previewDir))
        {
            Directory.CreateDirectory(previewDir);
            for (int e = 0; e < eras.Count; e++)
            {
                int ei = e;
                WritePreview(Path.Combine(previewDir, $"era-{eras[e].key}.png"), regionRaster, width, height, regions, "4096", r => r.owners[ei]);
                // close-ups at full raster resolution (zoomed 2x) for checking borders
                foreach (var crop in config["previewCrops"]?.AsArray() ?? new JsonArray())
                {
                    var (cx0, cy0) = Project(crop!["west"]!.GetValue<double>(), crop["north"]!.GetValue<double>());
                    var (cx1, cy1) = Project(crop["east"]!.GetValue<double>(), crop["south"]!.GetValue<double>());
                    WriteCrop(Path.Combine(previewDir, $"{crop["name"]}-{eras[e].key}.png"), regionRaster, width, height, regions, r => r.owners[ei],
                              (int)cx0, (int)cy0, (int)(cx1 - cx0), (int)(cy1 - cy0), 2);
                }
            }
        }

        // ---- 10. the playable map mod
        if (opts.TryGetValue("classic-out", out var classicOut) && config["classicExport"] != null)
        {
            // the map in the scene's units: scale map units per region raster pixel; the provinces are
            // drawn again at detailRaster times the region raster's resolution, so coasts and province
            // borders are finer than the region raster (without detailRaster: one pixel per map unit)
            int S = config["classicExport"]!["scale"]?.GetValue<int>() ?? 2;
            int hiFactor = config["classicExport"]!["detailRaster"]?.GetValue<int>() ?? S;
            if (S % hiFactor != 0) throw new ToolException("classicExport: scale must be a multiple of detailRaster");
            int hiW = width * hiFactor, hiH = height * hiFactor;
            var hiProvince = Rasterize(admin1.RootElement.GetProperty("features"), west, east, north, south, pxPerDeg * hiFactor, hiW, hiH, _ => 1, true, carve);
            // the regions every province ended up in
            var provinceRegions = new List<int>[provinces.Count + 1];
            for (int i = 0; i < n; i++)
            {
                int v = regionRaster[i], p = raster[i];
                if (v == 0 || p == 0 || regions[v - 1].sea) continue;
                var l = provinceRegions[p] ??= new List<int>(2);
                if (!l.Contains(v)) l.Add(v);
            }
            var hi = new int[hiW * hiH];
            for (int y = 0; y < hiH; y++)
                for (int x = 0; x < hiW; x++)
                {
                    int lx = x / hiFactor, ly = y / hiFactor, lo = regionRaster[ly * width + lx];
                    int p = hiProvince[y * hiW + x];
                    var candidates = p > 0 && p <= provinces.Count ? provinceRegions[p] : null;
                    int value = 0;
                    if (candidates != null)
                    {
                        // land here: the region of this province under the pixel, or next to it
                        if (candidates.Contains(lo)) value = lo;
                        else value = Near(regionRaster, width, height, lx, ly, wrap, v => candidates.Contains(v));
                    }
                    if (value == 0)
                    {
                        // water here (or a left out island): the sea zone under the pixel or next to it
                        if (lo > 0 && regions[lo - 1].sea) value = lo;
                        else value = Near(regionRaster, width, height, lx, ly, wrap, v => v > 0 && regions[v - 1].sea);
                        if (value == 0) value = lo;
                    }
                    hi[y * hiW + x] = value;
                }
            // a region the finer outlines left without a pixel (a sliver along a coast) keeps its own
            // pixels of the region raster
            var hiCount = new int[regions.Count + 1];
            foreach (int v in hi) hiCount[v]++;
            for (int i = 0; i < n; i++)
            {
                int v = regionRaster[i];
                if (v == 0 || hiCount[v] > 0) continue;
                int lx = i % width, ly = i / width;
                for (int dy = 0; dy < hiFactor; dy++)
                    for (int dx = 0; dx < hiFactor; dx++) hi[(ly * hiFactor + dy) * hiW + lx * hiFactor + dx] = v;
            }
            var scenarioSpecs = new List<ScenarioSpec>();
            foreach (var s in scenarios)
            {
                string eraKey = s["era"]!.GetValue<string>();
                int e = eras.FindIndex(x => x.key == eraKey);
                if (e < 0) throw new ToolException($"scenario {s["file"]}: no era {eraKey}");
                var unitOwner = new Dictionary<string, string>();
                foreach (var (gameId, unitList) in s["owners"]?.AsObject() ?? new JsonObject())
                    foreach (var gw in unitList!.AsArray())
                    {
                        string k = gw!.GetValue<string>();
                        unitOwner[k.Contains(':') ? k : "gw:" + k] = gameId;
                    }
                string prefix = scenarios.IndexOf(s) + ":";
                var owner = regions.Select(r =>
                {
                    if (r.sea) return null;
                    string occupied = r.occupation.Split('|').FirstOrDefault(o => o.StartsWith(prefix, StringComparison.Ordinal));
                    if (occupied != null) return occupied[prefix.Length..];
                    string unit = r.owners[e];
                    if (unitOwner.TryGetValue(unit, out var id)) return id;
                    // today's map: the countries table maps Natural Earth countries to game ids
                    if (unit.StartsWith("ne:", StringComparison.Ordinal) && countryIds.TryGetValue(unit[3..], out var neId)) return neId;
                    return null;
                }).ToArray();
                var names = new Dictionary<string, string>();
                foreach (var (id, name) in s["names"]?.AsObject() ?? new JsonObject()) names[id] = name!.GetValue<string>();
                foreach (var p in provinces) names.TryAdd(p.country, p.admin);
                scenarioSpecs.Add(new ScenarioSpec(s, owner, names));
            }
            ExportClassic(classicOut, config, regions, hi, hiW, hiH, S / hiFactor, scenarioSpecs, wrap, source);
        }
        Console.WriteLine($"build-map: {regions.Count(r => !r.sea)} land regions in {countries.Count} countries, {regions.Count(r => r.sea)} sea zones, " +
                          $"{regions.Count(r => r.type == "capital")} capitals ({eraCapitals} of earlier eras), {regions.Count(r => r.port)} ports, " +
                          $"{regions.Count(r => r.airfield)} airfields, {straits} straits, {sw.ElapsedMilliseconds} ms -> {outDir}");
        return 0;

        bool InBounds(double lon, double lat) => lon >= west && lon <= east && lat >= south && lat <= north;
    }

    /// <summary>A scenario of the map mod: its configuration, the owner (game id or null) of every region and the country names.</summary>
    private sealed record ScenarioSpec(JsonObject config, string[] owners, Dictionary<string, string> names);

    /// <summary>A value next to (x, y) in the raster (3 x 3) that matches, 0 when none does.</summary>
    private static int Near(int[] raster, int width, int height, int x, int y, bool wrap, Func<int, bool> match)
    {
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (wrap) nx = (nx + width) % width;
                if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                int v = raster[ny * width + nx];
                if (v != 0 && match(v)) return v;
            }
        return 0;
    }

    /// <summary>
    /// Draws GeoJSON features into a raster of codes (no antialiasing, later features over earlier
    /// ones). <paramref name="codeOf"/> gives the code of a feature inside the bounds (0: skip it).
    /// With <paramref name="counter"/> the codes are 1, 2, 3, ... in the order of the features inside
    /// the bounds (the same numbering a first pass with a counting codeOf gives).
    /// </summary>
    private static int[] Rasterize(JsonElement features, double west, double east, double north, double south, double pxPerDeg,
                                   int width, int height, Func<JsonElement, int> codeOf, bool counter = false, Action<SKCanvas, double> carve = null)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        int count = 0;
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(0, 0, 0, 0));
            using var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill, BlendMode = SKBlendMode.Src };
            foreach (var f in features.EnumerateArray())
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
                        float x = (float)((lon - west) * pxPerDeg), y = (float)((north - lat) * pxPerDeg);
                        if (first) { path.MoveTo(x, y); first = false; }
                        else path.LineTo(x, y);
                    }
                    path.Close();
                }
                if (!inside) continue;
                int code = counter ? ++count : codeOf(f.GetProperty("properties"));
                if (code <= 0) continue;
                paint.Color = new SKColor((byte)(code & 0xFF), (byte)((code >> 8) & 0xFF), (byte)((code >> 16) & 0xFF), 255);
                canvas.DrawPath(path, paint);
            }
            // water cut out of the land afterwards (lakes, straits and canals): drawn transparent
            carve?.Invoke(canvas, pxPerDeg);
        }
        var raster = new int[width * height];
        var pixels = bitmap.GetPixelSpan();
        for (int i = 0; i < raster.Length; i++)
            raster[i] = pixels[i * 4 + 3] == 0 ? 0 : pixels[i * 4] | (pixels[i * 4 + 1] << 8) | (pixels[i * 4 + 2] << 16);
        return raster;
    }

    /// <summary>
    /// The water cut out of the provinces (config "water"): lakes from Natural Earth (scalerank up to
    /// maxScalerank; the province layer covers them as land), historical lakes by name (the Aral Sea of
    /// 1960), and channels: straits and canals as lines of widthPixels region raster pixels, so ships
    /// sail through them and land units cannot walk across.
    /// </summary>
    private static Action<SKCanvas, double> WaterCarver(JsonObject config, string source, double west, double north, double basePxPerDeg)
    {
        var water = config["water"]?.AsObject();
        if (water == null) return null;
        var polygons = new List<List<(double lon, double lat)>>();
        void AddLakes(string file, Func<JsonElement, bool> take)
        {
            string path = Path.Combine(source, file);
            if (!File.Exists(path)) throw new ToolException($"water: {file} missing in {source}");
            using var doc = JsonDocument.Parse(File.OpenRead(path));
            foreach (var f in doc.RootElement.GetProperty("features").EnumerateArray())
            {
                var g = f.GetProperty("geometry");
                if (g.ValueKind != JsonValueKind.Object || !take(f.GetProperty("properties"))) continue;
                polygons.AddRange(Rings(g));
            }
        }
        if (water["lakes"] is JsonObject lakes)
        {
            int maxRank = lakes["maxScalerank"]?.GetValue<int>() ?? 2;
            AddLakes(lakes["file"]!.GetValue<string>(), p => Long(p, "scalerank") <= maxRank || Str(p, "scalerank") is string s && int.TryParse(s, out int r) && r <= maxRank);
        }
        if (water["historicLakes"] is JsonObject historic)
        {
            var names = historic["names"]!.AsArray().Select(n => n!.GetValue<string>()).ToHashSet();
            AddLakes(historic["file"]!.GetValue<string>(), p => names.Contains(Str(p, "name")));
        }
        var channels = (water["channels"]?.AsArray() ?? new JsonArray()).Select(c => (
            pts: c!["path"]!.AsArray().Select(v => v!.GetValue<double>()).ToArray(),
            width: c["widthPixels"]?.GetValue<double>() ?? 2.5)).ToList();
        return (canvas, pxPerDeg) =>
        {
            using var clear = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill, BlendMode = SKBlendMode.Src, Color = SKColors.Transparent };
            using var path = new SKPath { FillType = SKPathFillType.EvenOdd };
            foreach (var ring in polygons)
            {
                bool first = true;
                foreach (var (lon, lat) in ring)
                {
                    float x = (float)((lon - west) * pxPerDeg), y = (float)((north - lat) * pxPerDeg);
                    if (first) { path.MoveTo(x, y); first = false; } else path.LineTo(x, y);
                }
                path.Close();
            }
            canvas.DrawPath(path, clear);
            foreach (var (pts, width) in channels)
            {
                using var stroke = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Stroke, BlendMode = SKBlendMode.Src, Color = SKColors.Transparent,
                                                 StrokeWidth = (float)(width * pxPerDeg / basePxPerDeg), StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
                using var line = new SKPath();
                for (int k = 0; k + 1 < pts.Length; k += 2)
                {
                    float x = (float)((pts[k] - west) * pxPerDeg), y = (float)((north - pts[k + 1]) * pxPerDeg);
                    if (k == 0) line.MoveTo(x, y); else line.LineTo(x, y);
                }
                canvas.DrawPath(line, stroke);
            }
        };
    }

    /// <summary>A closed polygon (lon, lat pairs) drawn into a mask of the raster.</summary>
    private static bool[] RasterizeRing(double[] ring, double west, double north, double pxPerDeg, int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Alpha8, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using var path = new SKPath();
            for (int k = 0; k + 1 < ring.Length; k += 2)
            {
                float x = (float)((ring[k] - west) * pxPerDeg), y = (float)((north - ring[k + 1]) * pxPerDeg);
                if (k == 0) path.MoveTo(x, y); else path.LineTo(x, y);
            }
            path.Close();
            using var paint = new SKPaint { IsAntialias = false, Color = SKColors.White, Style = SKPaintStyle.Fill };
            canvas.DrawPath(path, paint);
        }
        var span = bitmap.GetPixelSpan();
        var mask = new bool[width * height];
        for (int i = 0; i < mask.Length; i++) mask[i] = span[i] != 0;
        return mask;
    }

    /// <summary>
    /// One era's units on the land: water is cleared; land without a unit takes the configured unit of
    /// its Natural Earth country, else the province's main unit, else the nearest unit. Then, inside
    /// every province, a fragment of another unit than the province's main one that is smaller than
    /// minKm2 or nowhere minWidth pixels thick (two sources tracing the same border a few kilometres
    /// apart) takes the unit of the fragment it shares the longest border with. Returns the pixels moved.
    /// </summary>
    private static int FillAndClean(int[] unit, int[] province, List<Province> provinces, int width, int height, double[] rowKm2,
                                    Dictionary<string, int> fallback, double minKm2, int minWidth)
    {
        int n = unit.Length;
        int Dominant(int[] u, out int[] dom)
        {
            var counts = new Dictionary<int, double>[provinces.Count + 1];
            for (int i = 0; i < n; i++)
            {
                int p = province[i];
                if (p == 0 || u[i] == 0) continue;
                var c = counts[p] ??= new Dictionary<int, double>();
                c[u[i]] = c.GetValueOrDefault(u[i]) + rowKm2[i / width];
            }
            dom = new int[provinces.Count + 1];
            for (int p = 1; p <= provinces.Count; p++)
                if (counts[p] != null) dom[p] = counts[p].OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
            return 0;
        }
        for (int i = 0; i < n; i++) if (province[i] == 0) unit[i] = 0;
        Dominant(unit, out var dominant);
        for (int i = 0; i < n; i++)
        {
            int p = province[i];
            if (p == 0 || unit[i] != 0) continue;
            if (fallback.TryGetValue(provinces[p - 1].adm0, out int f)) unit[i] = f;
            else if (dominant[p] != 0) unit[i] = dominant[p];
        }
        // the rest: the nearest unit (breadth first through land and water)
        var near = (int[])unit.Clone();
        var queue = new Queue<int>();
        for (int i = 0; i < n; i++) if (near[i] != 0) queue.Enqueue(i);
        while (queue.Count > 0)
        {
            int i = queue.Dequeue(), x = i % width, y = i / width;
            foreach (int j in new[] { x > 0 ? i - 1 : -1, x < width - 1 ? i + 1 : -1, y > 0 ? i - width : -1, y < height - 1 ? i + width : -1 })
                if (j >= 0 && near[j] == 0) { near[j] = near[i]; queue.Enqueue(j); }
        }
        for (int i = 0; i < n; i++) if (province[i] != 0 && unit[i] == 0) unit[i] = near[i];
        Dominant(unit, out dominant);

        // fragments: connected pixels of one province and one unit
        var comp = new int[n];
        var compProvince = new List<int> { 0 }; var compUnit = new List<int> { 0 }; var compKm2 = new List<double> { 0 };
        var stack = new Stack<int>();
        for (int i0 = 0; i0 < n; i0++)
        {
            if (province[i0] == 0 || comp[i0] != 0) continue;
            int id = compProvince.Count, p = province[i0], u = unit[i0];
            compProvince.Add(p); compUnit.Add(u); compKm2.Add(0);
            comp[i0] = id; stack.Push(i0);
            double km2 = 0;
            while (stack.Count > 0)
            {
                int i = stack.Pop(), x = i % width, y = i / width;
                km2 += rowKm2[y];
                foreach (int j in new[] { x > 0 ? i - 1 : -1, x < width - 1 ? i + 1 : -1, y > 0 ? i - width : -1, y < height - 1 ? i + width : -1 })
                    if (j >= 0 && comp[j] == 0 && province[j] == p && unit[j] == u) { comp[j] = id; stack.Push(j); }
            }
            compKm2[id] = km2;
        }
        // thickness: distance from the fragment's edge
        var dist = new int[n];
        var q = new Queue<int>();
        for (int i = 0; i < n; i++)
        {
            if (comp[i] == 0) continue;
            int x = i % width, y = i / width;
            bool edge = x == 0 || y == 0 || x == width - 1 || y == height - 1 ||
                        comp[i - 1] != comp[i] || comp[i + 1] != comp[i] || comp[i - width] != comp[i] || comp[i + width] != comp[i];
            if (edge) { dist[i] = 1; q.Enqueue(i); }
        }
        var compThickness = new int[compProvince.Count];
        while (q.Count > 0)
        {
            int i = q.Dequeue(), x = i % width, y = i / width;
            if (dist[i] > compThickness[comp[i]]) compThickness[comp[i]] = dist[i];
            foreach (int j in new[] { x > 0 ? i - 1 : -1, x < width - 1 ? i + 1 : -1, y > 0 ? i - width : -1, y < height - 1 ? i + width : -1 })
                if (j >= 0 && comp[j] == comp[i] && dist[j] == 0) { dist[j] = dist[i] + 1; q.Enqueue(j); }
        }
        var small = new bool[compProvince.Count];
        for (int c = 1; c < compProvince.Count; c++)
            small[c] = compUnit[c] != dominant[compProvince[c]] && (compKm2[c] < minKm2 || compThickness[c] < minWidth);
        // the neighbour fragment in the same province with the longest shared border
        var shared = new Dictionary<int, Dictionary<int, int>>();
        void Count(int a, int b)
        {
            if (a == b || a == 0 || b == 0 || !small[a] || compProvince[a] != compProvince[b] || small[b]) return;
            var d = shared.TryGetValue(a, out var x) ? x : shared[a] = new Dictionary<int, int>();
            d[b] = d.GetValueOrDefault(b) + 1;
        }
        for (int i = 0; i < n; i++)
        {
            int x = i % width;
            if (x + 1 < width) { Count(comp[i], comp[i + 1]); Count(comp[i + 1], comp[i]); }
            if (i + width < n) { Count(comp[i], comp[i + width]); Count(comp[i + width], comp[i]); }
        }
        var target = new int[compProvince.Count];
        for (int c = 1; c < compProvince.Count; c++)
            if (small[c])
                target[c] = shared.TryGetValue(c, out var d) && d.Count > 0
                    ? compUnit[d.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key]
                    : dominant[compProvince[c]];
        int moved = 0;
        for (int i = 0; i < n; i++)
        {
            int c = comp[i];
            if (c != 0 && small[c] && target[c] != 0 && target[c] != unit[i]) { unit[i] = target[c]; moved++; }
        }
        return moved;
    }

    /// <summary>
    /// Parts of a land region far from its main body: groups of islands (parts closer than clusterPx to
    /// each other) farther than splitPx become regions of their own when they have minKm2, and are left
    /// out when they are smaller and farther than dropPx. Horizontal distances wrap on a wrapping map.
    /// </summary>
    private static void SplitFarParts(int[] raster, int width, int height, bool wrap, double[] rowKm2, List<Region> regions,
                                      double splitPx, double clusterPx, double dropPx, double minKm2)
    {
        var label = new int[raster.Length];
        var parts = new List<(int value, List<int> pixels, double cx, double cy, double km2, int minX, int maxX, int minY, int maxY)>();
        var stack = new Stack<int>();
        for (int i = 0; i < raster.Length; i++)
        {
            if (raster[i] == 0 || label[i] != 0) continue;
            int v = raster[i];
            var pixels = new List<int>();
            label[i] = parts.Count + 1; stack.Push(i);
            double sx = 0, sy = 0, km2 = 0; int x0 = int.MaxValue, x1 = 0, y0 = int.MaxValue, y1 = 0;
            while (stack.Count > 0)
            {
                int j = stack.Pop(); pixels.Add(j);
                int x = j % width, y = j / width;
                sx += x; sy += y; km2 += rowKm2[y];
                x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
                foreach (int k in new[] { x > 0 ? j - 1 : -1, x < width - 1 ? j + 1 : -1, y > 0 ? j - width : -1, y < height - 1 ? j + width : -1 })
                    if (k >= 0 && raster[k] == v && label[k] == 0) { label[k] = parts.Count + 1; stack.Push(k); }
            }
            parts.Add((v, pixels, sx / pixels.Count, sy / pixels.Count, km2, x0, x1, y0, y1));
        }
        double Dx(double a, double b)
        {
            double d = Math.Abs(a - b);
            return wrap ? Math.Min(d, width - d) : d;
        }
        double Gap((int value, List<int> pixels, double cx, double cy, double km2, int minX, int maxX, int minY, int maxY) a,
                   (int value, List<int> pixels, double cx, double cy, double km2, int minX, int maxX, int minY, int maxY) b)
        {
            // gap between the boxes (0 when they overlap)
            double gx = Math.Max(0, Dx((a.minX + a.maxX) * 0.5, (b.minX + b.maxX) * 0.5) - (a.maxX - a.minX + b.maxX - b.minX) * 0.5);
            double gy = Math.Max(0, Math.Max(a.minY, b.minY) - Math.Min(a.maxY, b.maxY));
            return Math.Sqrt(gx * gx + gy * gy);
        }
        int split = 0, droppedParts = 0;
        foreach (var group in parts.GroupBy(p => p.value).ToList())
        {
            var main = group.OrderByDescending(p => p.km2).First();
            var far = group.Where(p => !ReferenceEquals(p.pixels, main.pixels) &&
                                       Math.Sqrt(Dx(p.cx, main.cx) * Dx(p.cx, main.cx) + (p.cy - main.cy) * (p.cy - main.cy)) > splitPx).ToList();
            if (far.Count == 0) continue;
            // clusters of far parts (single linkage)
            var cluster = Enumerable.Range(0, far.Count).ToArray();
            int Root(int i) { while (cluster[i] != i) i = cluster[i] = cluster[cluster[i]]; return i; }
            for (int a = 0; a < far.Count; a++)
                for (int c = a + 1; c < far.Count; c++)
                    if (Gap(far[a], far[c]) <= clusterPx) cluster[Root(a)] = Root(c);
            var source = regions[group.Key - 1];
            foreach (var cl in Enumerable.Range(0, far.Count).GroupBy(Root))
            {
                var members = cl.Select(i => far[i]).ToList();
                double km2 = members.Sum(p => p.km2);
                double cx = members.Sum(p => p.cx * p.km2) / km2, cy = members.Sum(p => p.cy * p.km2) / km2;
                double distance = Math.Sqrt(Dx(cx, main.cx) * Dx(cx, main.cx) + (cy - main.cy) * (cy - main.cy));
                if (km2 >= minKm2)
                {
                    var r = new Region
                    {
                        id = regions.Count, country = source.country, name = source.name + " Islands",
                        signature = source.signature, owners = source.owners, occupation = source.occupation, province = source.province,
                    };
                    r.provinces.AddRange(source.provinces);
                    regions.Add(r);
                    foreach (var p in members) foreach (int j in p.pixels) raster[j] = r.id + 1;
                    split++;
                }
                else if (distance > dropPx)
                {
                    foreach (var p in members) foreach (int j in p.pixels) raster[j] = 0;
                    droppedParts++;
                }
            }
        }
        Console.WriteLine($"build-map: {split} island groups became regions, {droppedParts} far groups left out");
    }

    /// <summary>
    /// Cuts the pixels of one group into k cells of about equal size: seeds by farthest point sampling
    /// from the group's middle, then three rounds of growing the cells from the seeds through the group
    /// (chamfer distance, so a cell is one piece where the group is) and moving each seed to its cell's
    /// middle. Pixels no seed reaches (islands of the group) join the nearest seed. Returns the cell of
    /// every pixel (aligned with <paramref name="pixels"/>).
    /// </summary>
    private static int[] Subdivide(List<int> pixels, int k, int width, int height, int[] scratch)
    {
        int m = pixels.Count;
        for (int j = 0; j < m; j++) scratch[pixels[j]] = j + 1;
        double cx = 0, cy = 0;
        foreach (int i in pixels) { cx += i % width; cy += i / width; }
        cx /= m; cy /= m;
        var dmin = new double[m];
        Array.Fill(dmin, double.MaxValue);
        var seeds = new List<int>();
        int next = 0; double best = double.MaxValue;
        for (int j = 0; j < m; j++)
        {
            double dx = pixels[j] % width - cx, dy = pixels[j] / width - cy, d = dx * dx + dy * dy;
            if (d < best) { best = d; next = j; }
        }
        for (int s = 0; s < k && s < m; s++)
        {
            seeds.Add(next);
            int sx = pixels[next] % width, sy = pixels[next] / width;
            double far = -1; int farJ = 0;
            for (int j = 0; j < m; j++)
            {
                double dx = pixels[j] % width - sx, dy = pixels[j] / width - sy, d = dx * dx + dy * dy;
                if (d < dmin[j]) dmin[j] = d;
                if (dmin[j] > far) { far = dmin[j]; farJ = j; }
            }
            next = farJ;
        }
        var cell = new int[m];
        var cost = new int[m];
        for (int round = 0; round < 4; round++)
        {
            Array.Fill(cell, -1);
            Array.Fill(cost, int.MaxValue);
            var pq = new PriorityQueue<int, long>();
            for (int s = 0; s < seeds.Count; s++) { cell[seeds[s]] = s; cost[seeds[s]] = 0; pq.Enqueue(seeds[s], s); }
            while (pq.TryDequeue(out int j, out long prio))
            {
                int c = (int)(prio >> 20);
                if (c > cost[j]) continue;
                int i = pixels[j], x = i % width, y = i / width;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        int nj = scratch[ny * width + nx] - 1;
                        if (nj < 0) continue;
                        int nc = c + (dx != 0 && dy != 0 ? 7 : 5);
                        if (nc >= cost[nj]) continue;
                        cost[nj] = nc; cell[nj] = cell[j];
                        pq.Enqueue(nj, ((long)nc << 20) | (uint)(cell[j] & 0xFFFFF));
                    }
            }
            // pixels on islands of the group no seed reached: the nearest seed
            for (int j = 0; j < m; j++)
            {
                if (cell[j] >= 0) continue;
                int x = pixels[j] % width, y = pixels[j] / width, bestS = 0; double bd = double.MaxValue;
                for (int s = 0; s < seeds.Count; s++)
                {
                    double dx = pixels[seeds[s]] % width - x, dy = pixels[seeds[s]] / width - y, d = dx * dx + dy * dy;
                    if (d < bd) { bd = d; bestS = s; }
                }
                cell[j] = bestS;
            }
            if (round == 3) break;
            // each seed to the pixel of its cell nearest the cell's middle
            var sx = new double[seeds.Count]; var sy = new double[seeds.Count]; var cnt = new int[seeds.Count];
            for (int j = 0; j < m; j++) { sx[cell[j]] += pixels[j] % width; sy[cell[j]] += pixels[j] / width; cnt[cell[j]]++; }
            var bestJ = new int[seeds.Count]; var bestD = new double[seeds.Count];
            Array.Fill(bestD, double.MaxValue);
            for (int j = 0; j < m; j++)
            {
                int c = cell[j];
                double dx = pixels[j] % width - sx[c] / cnt[c], dy = pixels[j] / width - sy[c] / cnt[c], d = dx * dx + dy * dy;
                if (d < bestD[c]) { bestD[c] = d; bestJ[c] = j; }
            }
            for (int s = 0; s < seeds.Count; s++) if (cnt[s] > 0) seeds[s] = bestJ[s];
        }
        for (int j = 0; j < m; j++) scratch[pixels[j]] = 0;
        return cell;
    }

    /// <summary>
    /// Every region one landmass: the connected pieces of a region (4-neighbourhood, over the seam of a
    /// wrapping map) are found; the largest stays; a piece of minKm2 or more becomes a region of its own;
    /// a smaller piece that touches other land joins the land region it shares the longest border with;
    /// a small island stays with its region (far ones are handled by SplitFarParts).
    /// </summary>
    private static void SeparatePieces(int[] raster, int width, int height, bool wrap, double[] rowKm2, List<Region> regions, double minKm2,
                                       HashSet<int> keepPixels = null)
    {
        // a piece that joins a neighbour can leave another piece of that neighbour on its own: repeat
        // until every region is one landmass (or islands)
        int totalSplit = 0, totalJoined = 0;
        for (int pass = 0; pass < 8; pass++)
        {
            var (split, joined) = SeparatePiecesOnce(raster, width, height, wrap, rowKm2, regions, minKm2, keepPixels ?? new HashSet<int>());
            totalSplit += split; totalJoined += joined;
            if (split + joined == 0) break;
        }
        Console.WriteLine($"build-map: regions in several pieces: {totalSplit} pieces became regions, {totalJoined} small pieces joined their neighbour");
    }

    private static (int split, int joined) SeparatePiecesOnce(int[] raster, int width, int height, bool wrap, double[] rowKm2, List<Region> regions, double minKm2,
                                                              HashSet<int> keepPixels)
    {
        int n = raster.Length;
        var label = new int[n];
        var pieces = new List<(int value, List<int> pixels, double km2)>();
        var stack = new Stack<int>();
        int Left(int i) { int x = i % width; return x > 0 ? i - 1 : wrap ? i + width - 1 : -1; }
        int Right(int i) { int x = i % width; return x < width - 1 ? i + 1 : wrap ? i - width + 1 : -1; }
        for (int i0 = 0; i0 < n; i0++)
        {
            int v = raster[i0];
            if (v == 0 || label[i0] != 0 || regions[v - 1].sea) continue;
            var pixels = new List<int>();
            double km2 = 0;
            label[i0] = pieces.Count + 1; stack.Push(i0);
            while (stack.Count > 0)
            {
                int i = stack.Pop(); pixels.Add(i); km2 += rowKm2[i / width];
                foreach (int j in new[] { Left(i), Right(i), i >= width ? i - width : -1, i + width < n ? i + width : -1 })
                    if (j >= 0 && raster[j] == v && label[j] == 0) { label[j] = pieces.Count + 1; stack.Push(j); }
            }
            pieces.Add((v, pixels, km2));
        }
        int split = 0, joined = 0;
        foreach (var group in pieces.GroupBy(p => p.value).Where(g => g.Count() > 1).ToList())
        {
            var source = regions[group.Key - 1];
            var main = group.OrderByDescending(p => p.km2).First();
            foreach (var piece in group.OrderByDescending(p => p.km2))
            {
                if (ReferenceEquals(piece.pixels, main.pixels)) continue;
                // the land it touches (other regions)
                var touch = new Dictionary<int, int>();
                foreach (int i in piece.pixels)
                    foreach (int j in new[] { Left(i), Right(i), i >= width ? i - width : -1, i + width < n ? i + width : -1 })
                    {
                        if (j < 0) continue;
                        int o = raster[j];
                        if (o != 0 && o != piece.value && !regions[o - 1].sea) touch[o] = touch.GetValueOrDefault(o) + 1;
                    }
                // a piece of minKm2, or an island the configuration keeps (keepIslands), is a region of its own
                if (piece.km2 >= minKm2 || (touch.Count == 0 && piece.pixels.Any(keepPixels.Contains)))
                {
                    var r = new Region
                    {
                        id = regions.Count, country = source.country, name = source.name, signature = source.signature,
                        owners = source.owners, occupation = source.occupation, province = source.province,
                    };
                    r.provinces.AddRange(source.provinces);
                    regions.Add(r);
                    foreach (int i in piece.pixels) raster[i] = r.id + 1;
                    split++;
                }
                else if (touch.Count > 0)
                {
                    int target = touch.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
                    foreach (int i in piece.pixels) raster[i] = target;
                    joined++;
                }
            }
        }
        return (split, joined);
    }

    /// <summary>
    /// Sea zones on the water of the raster (value 0): seeds spaced by the distance from the coast (the
    /// radius grows from coastRadiusDegrees by growth per degree of open water up to oceanRadiusDegrees),
    /// zones grown from the seeds through the water (chamfer distance, so a zone never crosses land),
    /// relaxed relaxIterations times (each seed moves to its zone's middle); water bodies no seed
    /// reaches get one when they have minSeaPixels; zones below minSeaPixels join the neighbour zone
    /// they share the longest border with. The seam of a wrapping map is a zone border.
    /// </summary>
    private static void BuildSeaZones(int[] raster, int width, int height, double pxPerDeg, JsonObject cfg, List<Region> regions, bool wrap = false)
    {
        int n = raster.Length;
        int WrapX(int x) => wrap ? ((x % width) + width) % width : x;
        double rCoast = (cfg["coastRadiusDegrees"]?.GetValue<double>() ?? 1.4) * pxPerDeg;
        double rOcean = (cfg["oceanRadiusDegrees"]?.GetValue<double>() ?? 9) * pxPerDeg;
        double growth = cfg["growth"]?.GetValue<double>() ?? 0.6;
        int iterations = cfg["relaxIterations"]?.GetValue<int>() ?? 2;
        int minSeaPixels = cfg["minSeaPixels"]?.GetValue<int>() ?? 300;
        var water = new bool[n];
        for (int i = 0; i < n; i++) water[i] = raster[i] == 0;

        // distance from the coast (chamfer 3-4, in pixels x 3); a wrapping map is padded with the other
        // side's columns, so the distance reaches over the seam
        const int Inf = int.MaxValue / 4;
        var coast = ChamferDistance(water, width, height, wrap ? Math.Min(width, (int)(rOcean * 1.5) + 8) : 0);
        double Radius(int i) => Math.Min(rOcean, rCoast + growth * Math.Min(coast[i], Inf / 2) / 3.0);

        // seeds: candidates on a jittered grid, coast first, each kept when no seed lies within its radius
        static uint Hash(uint v) { v ^= v >> 16; v *= 0x7feb352d; v ^= v >> 15; v *= 0x846ca68b; v ^= v >> 16; return v; }
        int step = Math.Max(2, (int)(rCoast / 3));
        var candidates = new List<int>();
        for (int y = step / 2; y < height; y += step)
            for (int x = step / 2; x < width; x += step)
            {
                uint h = Hash((uint)(y * width + x));
                int jx = Math.Clamp(x + (int)(h % (uint)step) - step / 2, 0, width - 1);
                int jy = Math.Clamp(y + (int)((h >> 8) % (uint)step) - step / 2, 0, height - 1);
                int i = jy * width + jx;
                if (water[i]) candidates.Add(i);
            }
        candidates = candidates.OrderBy(i => (int)(Radius(i) / rCoast)).ThenBy(i => Hash((uint)i * 2654435761u)).ToList();
        int cell = Math.Max(1, (int)rCoast);
        int gw = width / cell + 1, gh = height / cell + 1;
        var grid = new List<int>[gw * gh];
        var seeds = new List<int>();
        foreach (int i in candidates)
        {
            int x = i % width, y = i / width;
            double r = Radius(i);
            int cx = x / cell, cy = y / cell, rc = (int)Math.Ceiling(r / cell);
            bool free = true;
            for (int gy = Math.Max(0, cy - rc); gy <= Math.Min(gh - 1, cy + rc) && free; gy++)
                for (int gx0 = cx - rc; gx0 <= cx + rc && free; gx0++)
                {
                    int gx = wrap ? ((gx0 % gw) + gw) % gw : gx0;
                    if (gx < 0 || gx >= gw) continue;
                    foreach (int s in grid[gy * gw + gx] ?? (IEnumerable<int>)Array.Empty<int>())
                    {
                        int dx = s % width - x, dy = s / width - y;
                        if (wrap) { if (dx > width / 2) dx -= width; else if (dx < -width / 2) dx += width; }
                        if (dx * dx + dy * dy < r * r) { free = false; break; }
                    }
                }
            if (!free) continue;
            seeds.Add(i);
            (grid[cy * gw + cx] ??= new List<int>()).Add(i);
        }

        // grow the zones through the water (chamfer 5-7), then relax
        var zone = new int[n];
        void Grow()
        {
            Array.Clear(zone);
            var cost = new int[n];
            Array.Fill(cost, int.MaxValue);
            var pq = new PriorityQueue<int, long>();
            for (int s = 0; s < seeds.Count; s++) { zone[seeds[s]] = s + 1; cost[seeds[s]] = 0; pq.Enqueue(seeds[s], s); }
            while (pq.TryDequeue(out int i, out long prio))
            {
                int c = (int)(prio >> 20);
                if (c > cost[i]) continue;
                int x = i % width, y = i / width;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = WrapX(x + dx), ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        int j = ny * width + nx;
                        if (!water[j]) continue;
                        int nc = c + (dx != 0 && dy != 0 ? 7 : 5);
                        if (nc >= cost[j]) continue;
                        cost[j] = nc; zone[j] = zone[i];
                        pq.Enqueue(j, ((long)nc << 20) | (uint)(zone[i] & 0xFFFFF));
                    }
            }
        }
        // seeds' priority carries the seed index in its low bits so equal costs resolve the same way every run
        for (int it = 0; it <= iterations; it++)
        {
            Grow();
            // water no seed reached (lakes, enclosed seas): a seed in each part that is large enough
            var label = new int[n];
            var st = new Stack<int>();
            bool added = false;
            for (int i0 = 0; i0 < n; i0++)
            {
                if (!water[i0] || zone[i0] != 0 || label[i0] != 0) continue;
                var part = new List<int>();
                label[i0] = 1; st.Push(i0);
                while (st.Count > 0)
                {
                    int i = st.Pop(); part.Add(i);
                    int x = i % width, y = i / width;
                    foreach (int j in new[] { x > 0 ? i - 1 : wrap ? i + width - 1 : -1, x < width - 1 ? i + 1 : wrap ? i - width + 1 : -1, y > 0 ? i - width : -1, y < height - 1 ? i + width : -1 })
                        if (j >= 0 && water[j] && zone[j] == 0 && label[j] == 0) { label[j] = 1; st.Push(j); }
                }
                if (part.Count < minSeaPixels) continue;
                double mx = part.Average(i => i % width), my = part.Average(i => i / width);
                seeds.Add(part.OrderBy(i => (i % width - mx) * (i % width - mx) + (i / width - my) * (i / width - my)).ThenBy(i => i).First());
                added = true;
            }
            if (added) Grow();
            if (it == iterations) break;
            // relax: every seed to the zone pixel nearest the zone's centre
            var sx = new double[seeds.Count + 1]; var sy = new double[seeds.Count + 1]; var cnt = new long[seeds.Count + 1];
            double Dx(int x, int z)
            {
                // x relative to the zone's seed, the short way over the seam
                int d = x - seeds[z - 1] % width;
                if (wrap) { if (d > width / 2) d -= width; else if (d < -width / 2) d += width; }
                return d;
            }
            for (int i = 0; i < n; i++) { int z = zone[i]; if (z == 0) continue; sx[z] += Dx(i % width, z); sy[z] += i / width; cnt[z]++; }
            var best = new int[seeds.Count + 1]; var bestD = new double[seeds.Count + 1];
            Array.Fill(bestD, double.MaxValue);
            for (int i = 0; i < n; i++)
            {
                int z = zone[i]; if (z == 0) continue;
                double dx = Dx(i % width, z) - sx[z] / cnt[z], dy = i / width - sy[z] / cnt[z], d = dx * dx + dy * dy;
                if (d < bestD[z]) { bestD[z] = d; best[z] = i; }
            }
            for (int s = 0; s < seeds.Count; s++) if (cnt[s + 1] > 0) seeds[s] = best[s + 1];
        }

        // regions for the zones; small zones join their longest neighbour
        int first = regions.Count;
        var size = new long[seeds.Count + 1];
        for (int i = 0; i < n; i++) size[zone[i]]++;
        var zoneBorder = Adjacency(zone, width, height, seeds.Count, wrap);
        var redirect = Enumerable.Range(0, seeds.Count + 1).ToArray();
        int Resolve(int v) { while (redirect[v] != v) v = redirect[v]; return v; }
        foreach (int z in Enumerable.Range(1, seeds.Count).OrderBy(z => size[z]).ThenBy(z => z))
        {
            if (size[Resolve(z)] >= minSeaPixels || Resolve(z) != z) continue;
            int target = zoneBorder[z].Where(kv => kv.Key > 0 && Resolve(kv.Key) != z)
                                      .OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Select(kv => Resolve(kv.Key)).FirstOrDefault();
            redirect[z] = target;
            if (target > 0) size[target] += size[z];
        }
        var regionOf = new int[seeds.Count + 1];
        for (int z = 1; z <= seeds.Count; z++)
        {
            if (Resolve(z) != z || size[z] == 0) continue;
            var r = new Region { id = regions.Count, sea = true, name = $"sea {z}" };
            regions.Add(r);
            regionOf[z] = r.id + 1;
        }
        for (int i = 0; i < n; i++)
            if (water[i] && zone[i] != 0) raster[i] = regionOf[Resolve(zone[i])];
        Console.WriteLine($"build-map: {regions.Count - first} sea zones from {seeds.Count} seeds");
    }

    /// <summary>
    /// Chamfer (3-4) distance of every pixel from the nearest pixel that is not <paramref name="far"/>
    /// (0 there), in pixels x 3. With <paramref name="pad"/> columns of the other side added left and right,
    /// the distance reaches over the seam of a wrapping map.
    /// </summary>
    internal static int[] ChamferDistance(bool[] far, int width, int height, int pad)
    {
        const int Inf = int.MaxValue / 4;
        int pw = width + 2 * pad;
        var d = new int[pw * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < pw; x++)
            {
                int sx = ((x - pad) % width + width) % width;
                d[y * pw + x] = far[y * width + sx] ? Inf : 0;
            }
        for (int y = 0; y < height; y++)
            for (int x = 0; x < pw; x++)
            {
                int i = y * pw + x;
                if (d[i] == 0) continue;
                int v = d[i];
                if (x > 0) v = Math.Min(v, d[i - 1] + 3);
                if (y > 0) v = Math.Min(v, d[i - pw] + 3);
                if (x > 0 && y > 0) v = Math.Min(v, d[i - pw - 1] + 4);
                if (x + 1 < pw && y > 0) v = Math.Min(v, d[i - pw + 1] + 4);
                d[i] = v;
            }
        for (int y = height - 1; y >= 0; y--)
            for (int x = pw - 1; x >= 0; x--)
            {
                int i = y * pw + x;
                if (d[i] == 0) continue;
                int v = d[i];
                if (x + 1 < pw) v = Math.Min(v, d[i + 1] + 3);
                if (y + 1 < height) v = Math.Min(v, d[i + pw] + 3);
                if (x + 1 < pw && y + 1 < height) v = Math.Min(v, d[i + pw + 1] + 4);
                if (x > 0 && y + 1 < height) v = Math.Min(v, d[i + pw - 1] + 4);
                d[i] = v;
            }
        if (pad == 0) return d;
        var result = new int[width * height];
        for (int y = 0; y < height; y++) Array.Copy(d, y * pw + pad, result, y * width, width);
        return result;
    }

    private static JsonObject WithoutKey(JsonObject o, string key)
    {
        var copy = (JsonObject)o.DeepClone();
        copy.Remove(key);
        return copy;
    }

    /// <summary>For every value of the raster: the touching values and the border length in pixels
    /// (4-neighbourhood; with <paramref name="wrap"/> the last column touches the first).</summary>
    private static Dictionary<int, int>[] Adjacency(int[] raster, int width, int height, int count, bool wrap)
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
                else if (wrap) Add(v, raster[y * width]);
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

    private static double Dbl(JsonElement o, string key) =>
        o.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : double.NaN;

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

    /// <summary>A part of the raster at full resolution, zoomed, coloured like the preview, region borders dark.</summary>
    private static void WriteCrop(string path, int[] raster, int width, int height, List<Region> regions, Func<Region, string> colourKey,
                                  int x0, int y0, int w, int h, int zoom)
    {
        using var bmp = new SKBitmap(w * zoom, h * zoom);
        var cache = new Dictionary<string, SKColor>();
        SKColor Colour(int v)
        {
            if (v == 0) return new SKColor(20, 30, 40);
            var r = regions[v - 1];
            if (r.sea) return new SKColor(70, 110, 150);
            string key = colourKey(r) ?? "";
            if (!cache.TryGetValue(key, out var c))
            {
                uint fnv = 2166136261;
                foreach (char ch in key) fnv = (fnv ^ ch) * 16777619;
                int hsh = (int)(fnv & 0x7FFFFFFF);
                c = SKColor.FromHsl(hsh % 360, 45 + hsh / 360 % 30, 45 + hsh / 10800 % 20);
                cache[key] = c;
            }
            return c;
        }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int gx = ((x0 + x) % width + width) % width, gy = Math.Clamp(y0 + y, 0, height - 1);
                int v = raster[gy * width + gx];
                var c = Colour(v);
                int right = raster[gy * width + (gx + 1) % width], down = gy + 1 < height ? raster[(gy + 1) * width + gx] : v;
                if (right != v || down != v) c = new SKColor((byte)(c.Red * 0.5), (byte)(c.Green * 0.5), (byte)(c.Blue * 0.5));
                for (int dy = 0; dy < zoom; dy++)
                    for (int dx = 0; dx < zoom; dx++) bmp.SetPixel(x * zoom + dx, y * zoom + dy, c);
            }
        using var data = bmp.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);
        data.SaveTo(file);
    }

    /// <summary>The map coloured by <paramref name="colourKey"/> (a country or an era's unit), region borders darker.</summary>
    private static void WritePreview(string path, int[] raster, int width, int height, List<Region> regions, string previewWidth, Func<Region, string> colourKey)
    {
        int pw = int.Parse(previewWidth), ph = (int)Math.Round(height * (double)pw / width);
        using var full = new SKBitmap(width, height);
        var countryColour = new Dictionary<string, SKColor>();
        SKColor Colour(Region r)
        {
            if (r.sea) return new SKColor(70, 110, 150);
            string key = colourKey(r) ?? "";
            if (!countryColour.TryGetValue(key, out var c))
            {
                // a stable hash (string.GetHashCode differs per process): the preview is reproducible
                uint fnv = 2166136261;
                foreach (char ch in key) fnv = (fnv ^ ch) * 16777619;
                int h = (int)(fnv & 0x7FFFFFFF);
                c = SKColor.FromHsl(h % 360, 45 + h / 360 % 30, 45 + h / 10800 % 20);
                countryColour[key] = c;
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
