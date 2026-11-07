using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkiaSharp;

namespace WC2.Tools;

/// <summary>
/// build-scenario --config FILE [--out DIR] [--root DIR]: writes a conquest mod from a rules file
/// (MODDING.md, "A total conversion: build-scenario"). The rules name the countries (their faction,
/// tags, colours, flag, commander), the factions and relations, and who owns which region of a map
/// pack: a list of conditions over the regions (today's country, ISO province, the owner in a historical
/// era of the pack, longitude and latitude boxes, distance from a point, region ids), later rules winning.
/// From that the tool writes the scenario JSON (owners, cities, garrisons, fleets, money from the
/// taxes), the conquest list entry and button, the menu texts, the new countries' registration and their
/// pictures (flag, list button, small flag) drawn from simple designs, and mod.json.
/// </summary>
internal static class ScenarioBuilder
{
    private static readonly string[] Languages = { "cn", "en", "ja", "kr", "ru", "tw" };

    private sealed class Region
    {
        public int id;
        public bool sea, coastal;
        public string country = "", type = "normal", name = "";
        public string[] provinces = Array.Empty<string>();
        public Dictionary<string, string> owners = new();
        public double lon, lat;
        public int[] neighbours = Array.Empty<int>();
    }

    public static int Run(Dictionary<string, string> opts)
    {
        string configPath = opts["config"];
        string root = opts.GetValueOrDefault("root") ?? Directory.GetCurrentDirectory();
        var cfg = ReadJson(configPath);
        string outDir = opts.GetValueOrDefault("out") ?? Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(configPath)))!;
        var map = cfg["map"]!.AsObject();
        string packDir = Path.Combine(root, map["pack"]!.GetValue<string>());
        string areasPath = Path.Combine(root, map["areas"]!.GetValue<string>());
        int mapId = map["id"]!.GetValue<int>();
        int number = cfg["conquest"]!.GetValue<int>();

        var regions = ReadRegions(Path.Combine(packDir, "regions.json"));
        var mapJson = ReadJson(Path.Combine(packDir, "map.json"));
        var areaTable = ReadJson(areasPath)["areas"]!.AsObject();
        string AreaType(int id) => areaTable[id.ToString()]?["type"]?.GetValue<string>() ?? "normal";
        int Tax(int id) => areaTable[id.ToString()]?["tax"]?.GetValue<int>() ?? 0;

        // ---- countries, factions
        var countries = cfg["countries"]!.AsArray().Select(n => n!.AsObject()).ToList();
        var countryIds = countries.Select(c => c["id"]!.GetValue<string>()).ToList();
        if (countryIds.Distinct().Count() != countryIds.Count) throw new ToolException("country ids must be unique");
        var factions = cfg["factions"]?.AsArray().Select(n => n!.AsObject()).ToList() ?? new List<JsonObject>();
        var factionIds = factions.Select(f => f["id"]!.GetValue<string>()).ToHashSet();
        foreach (var c in countries)
            if (c["faction"]?.GetValue<string>() is string f && !factionIds.Contains(f))
                throw new ToolException($"country {c["id"]}: unknown faction {f}");

        // ---- owners: the rules in order, later ones win
        var owner = new string[regions.Count];
        int ruleNo = 0;
        foreach (var ruleNode in cfg["owners"]!.AsArray())
        {
            ruleNo++;
            var rule = ruleNode!.AsObject();
            if (rule["nearest"] is JsonArray points)
            {
                // the land region closest to each point (a trading post, a fort, a small island)
                var ids = new JsonArray();
                var list = points.Count > 0 && points[0] is JsonArray ? points.Select(n => n!.AsArray()).ToList() : new List<JsonArray> { points };
                foreach (var pt in list)
                {
                    double plon = pt[0]!.GetValue<double>(), plat = pt[1]!.GetValue<double>();
                    ids.Add(regions.Where(r => !r.sea).OrderBy(r => Km(plon, plat, r.lon, r.lat)).First().id);
                }
                rule = (JsonObject)rule.DeepClone();
                rule.Remove("nearest");
                rule["regions"] = ids;
            }
            string to = rule["to"]?.GetValue<string>();
            if (to != null && !countryIds.Contains(to)) throw new ToolException($"owner rule {ruleNo}: unknown country {to}");
            int matched = 0;
            foreach (var r in regions)
            {
                if (r.sea || !Matches(rule, r)) continue;
                owner[r.id] = to;
                matched++;
            }
            if (matched == 0) Console.WriteLine($"build-scenario: warning: owner rule {ruleNo} ({to ?? "nobody"}) matches no region");
        }

        // ---- per country: capital, garrisons, fleets
        var defaults = cfg["defaults"]?.AsObject() ?? new JsonObject();
        JsonNode Setting(JsonObject c, string key) => c[key] ?? defaults[key];
        var areasOut = new SortedDictionary<int, JsonObject>();
        var capitalOf = new Dictionary<string, int>();
        foreach (var c in countries)
        {
            string id = c["id"]!.GetValue<string>();
            var own = regions.Where(r => owner[r.id] == id).ToList();
            if (own.Count == 0) { Console.WriteLine($"build-scenario: warning: {id} owns no region"); continue; }
            if (c["capital"] is JsonArray cap)
            {
                double lon = cap[0]!.GetValue<double>(), lat = cap[1]!.GetValue<double>();
                var marked = own.Where(r => AreaType(r.id) == "capital").ToList();
                var pool = marked.Count > 0 ? marked : own;
                capitalOf[id] = pool.OrderBy(r => Km(lon, lat, r.lon, r.lat)).First().id;
            }
        }
        List<string> Units(JsonObject c, string kind) =>
            (Setting(c, "units")?[kind] as JsonArray ?? defaults["units"]?[kind] as JsonArray ?? new JsonArray())
            .Select(n => n!.GetValue<string>()).ToList();
        bool transports = defaults["transportCards"]?.GetValue<bool>() ?? true;
        foreach (var r in regions.Where(r => !r.sea && owner[r.id] != null))
        {
            string id = owner[r.id];
            var c = countries.First(x => x["id"]!.GetValue<string>() == id);
            string type = AreaType(r.id);
            bool mainCapital = capitalOf.TryGetValue(id, out int capId) && capId == r.id;
            string kind = mainCapital ? "capital" : type switch
            {
                "capital" => "largeCity", "large city" => "largeCity", "normal city" => "city", "port" => "port", _ => null,
            };
            var a = new JsonObject { ["id"] = r.id, ["country"] = id };
            if (type == "capital" || type == "large city")
            {
                a["construction"] = "city"; a["level"] = type == "capital" ? 2 : 1;
            }
            if (mainCapital) a["installation"] = "fort";
            var units = kind == null ? new List<string>() : Units(c, kind);
            int garrison = Setting(c, "garrison")?.GetValue<int>() ?? 0;
            if (mainCapital || kind == "largeCity")
                for (int k = 0; k < garrison && units.Count < 4; k++) units.Add("infantry");
            if (units.Count > 0)
            {
                int cards = r.coastal && transports ? 4 : 0;
                a["units"] = new JsonArray(units.Take(4).Select(u => (JsonNode)new JsonObject { ["type"] = u, ["level"] = 1, ["cards"] = cards }).ToArray());
            }
            areasOut[r.id] = a;
        }
        // extra armies near a point (fronts, garrisons the rules above do not cover)
        foreach (var armyNode in cfg["armies"]?.AsArray() ?? new JsonArray())
        {
            var army = armyNode!.AsObject();
            var at = army["near"]!.AsArray();
            double lon = at[0]!.GetValue<double>(), lat = at[1]!.GetValue<double>();
            string who = army["country"]?.GetValue<string>();
            var target = regions.Where(r => !r.sea && owner[r.id] != null && (who == null || owner[r.id] == who))
                                .OrderBy(r => Km(lon, lat, r.lon, r.lat)).FirstOrDefault();
            if (target == null) { Console.WriteLine($"build-scenario: warning: no area for the army at {lon},{lat}"); continue; }
            var a = areasOut.TryGetValue(target.id, out var existing) ? existing : areasOut[target.id] = new JsonObject { ["id"] = target.id, ["country"] = owner[target.id] };
            var list = a["units"] as JsonArray ?? new JsonArray();
            foreach (var u in army["units"]!.AsArray().Select(n => n!.GetValue<string>()))
                if (list.Count < 4) list.Add(new JsonObject { ["type"] = u, ["level"] = army["level"]?.GetValue<int>() ?? 1, ["cards"] = target.coastal && transports ? 4 : 0 });
            a["units"] = list;
        }
        // fleets: a destroyer in a harbour (or the longest coast's sea zone) of the country's ports
        int fleetsTotal = 0;
        var usedSeas = new HashSet<int>();
        foreach (var c in countries)
        {
            string id = c["id"]!.GetValue<string>();
            int fleets = Setting(c, "fleets")?.GetValue<int>() ?? 0;
            var ports = regions.Where(r => owner[r.id] == id && r.coastal)
                               .OrderByDescending(r => capitalOf.GetValueOrDefault(id, -1) == r.id)
                               .ThenByDescending(r => AreaType(r.id) is "capital" or "large city" or "port")
                               .ThenBy(r => r.id).ToList();
            foreach (var r in ports)
            {
                if (fleets <= 0) break;
                int sea = r.neighbours.Where(n => regions[n].sea && !usedSeas.Contains(n))
                                      .OrderByDescending(n => AreaType(n) == "port").ThenBy(n => n).DefaultIfEmpty(-1).First();
                if (sea < 0) continue;
                usedSeas.Add(sea);
                areasOut[sea] = new JsonObject
                {
                    ["id"] = sea, ["country"] = id,
                    ["units"] = new JsonArray(new JsonObject { ["type"] = Setting(c, "fleetUnit")?.GetValue<string>() ?? "destroyer", ["level"] = 1, ["cards"] = 0 }),
                };
                fleets--; fleetsTotal++;
            }
        }

        // ---- money and industry from the taxes
        var eco = cfg["economy"]?.AsObject() ?? new JsonObject();
        double moneyBase = eco["moneyBase"]?.GetValue<double>() ?? 200, moneyPerTax = eco["moneyPerTax"]?.GetValue<double>() ?? 1;
        double industryBase = eco["industryBase"]?.GetValue<double>() ?? 100, industryPerTax = eco["industryPerTax"]?.GetValue<double>() ?? 0.5;

        // ---- texts (English is every language's fallback)
        var strings = Languages.ToDictionary(l => l, _ => new JsonObject());
        void Text(string key, JsonNode node)
        {
            if (node == null) return;
            foreach (var lang in Languages)
            {
                string v = node is JsonObject o ? (o[lang] ?? o["en"])?.GetValue<string>() : node.GetValue<string>();
                if (v != null) strings[lang][key] = v;
            }
        }
        var texts = cfg["texts"]!.AsObject();
        Text($"conquest name {number}", texts["name"]);
        Text($"conquest age {number}", texts["age"]);
        Text($"conquest intro {number}", texts["intro"]);

        // ---- the scenario
        var scenarioCountries = new JsonArray();
        var summary = new List<string>();
        foreach (var c in countries)
        {
            string id = c["id"]!.GetValue<string>();
            var own = regions.Where(r => owner[r.id] == id).ToList();
            if (own.Count == 0) continue;
            int taxes = own.Sum(r => Tax(r.id));
            double factor = Setting(c, "economy")?.GetValue<double>() ?? 1.0;
            int money = (int)Math.Round((moneyBase + moneyPerTax * taxes) * factor);
            int industry = (int)Math.Round((industryBase + industryPerTax * taxes) * factor);
            var rgb = (c["color"] ?? new JsonArray(200, 200, 200)).AsArray().Select(n => n!.GetValue<int>()).ToArray();
            var o = new JsonObject
            {
                ["id"] = id,
                ["name"] = c["art"]?.GetValue<string>() ?? id,
                ["commander"] = Setting(c, "commander")?.GetValue<string>() ?? "common1",
                ["ai"] = 1,
            };
            if (c["art"] != null && c["flag"] != null) o["flag"] = FlagKey(c, number);
            if (c["faction"] is JsonNode f) o["faction"] = f.GetValue<string>();
            else o["alliance"] = c["alliance"]?.GetValue<string>() ?? "n";
            if (c["tags"] is JsonArray tags) o["tags"] = tags.DeepClone();
            o["defeated"] = "land";
            o["money"] = money;
            o["industry"] = industry;
            o["techLevel"] = Setting(c, "techLevel")?.GetValue<int>() ?? 3;
            o["taxFactor"] = Setting(c, "taxFactor")?.GetValue<double>() ?? 1.2;
            o["r"] = rgb[0]; o["g"] = rgb[1]; o["b"] = rgb[2]; o["a"] = 90;
            scenarioCountries.Add(o);
            Text($"conquest country name {number} {id}", c["name"]);
            Text($"conquest country intro {number} {id}", c["intro"] ?? defaults["intro"]);
            Text($"conquest country victory {number} {id}", c["victory"] ?? defaults["victory"]);
            summary.Add($"{id} {own.Count(r => !r.sea)}");
        }
        var scenarioFactions = new JsonArray();
        foreach (var f in factions)
        {
            string id = f["id"]!.GetValue<string>();
            var o = new JsonObject { ["id"] = id, ["name"] = $"faction {id}" };
            if (f["color"] is JsonArray color) o["color"] = color.DeepClone();
            if (f["leader"] is JsonNode leader) o["leader"] = leader.GetValue<string>();
            if (f["tags"] is JsonArray tags) o["tags"] = tags.DeepClone();
            scenarioFactions.Add(o);
            Text($"faction {id}", f["name"]);
        }
        var scenarioRelations = new JsonArray();
        int relationNo = 0;
        foreach (var relNode in cfg["relations"]?.AsArray() ?? new JsonArray())
        {
            relationNo++;
            var rel = relNode!.AsObject();
            var o = new JsonObject { ["between"] = rel["between"]!.DeepClone(), ["state"] = rel["state"]?.GetValue<string>() ?? "war" };
            if (rel["from"] is JsonNode from) o["from"] = from.GetValue<int>();
            if (rel["until"] is JsonNode until) o["until"] = until.GetValue<int>();
            if (rel["announce"] is JsonObject an)
            {
                var a = new JsonObject { ["commander"] = an["commander"]?.GetValue<string>() ?? "common1" };
                foreach (var which in new[] { "start", "end" })
                {
                    if (an[which] == null) continue;
                    string key = $"conquest {number} relation {relationNo} {which}";
                    a[which] = key;
                    Text(key, an[which]);
                }
                o["announce"] = a;
            }
            scenarioRelations.Add(o);
        }
        string scenarioName = $"conquest_{number}.xml";
        string enableFile = $"conquest_{number}.bin";
        var scenario = new JsonObject
        {
            ["format"] = 1,
            ["about"] = $"Generated by WC2.Tools build-scenario from {Path.GetFileName(configPath)}: edit the rules there and build again. Field reference: DATA_SCHEMA.md#scenarios.",
            ["map"] = mapId,
            ["areasEnable"] = enableFile,
        };
        if (scenarioFactions.Count > 0) scenario["factions"] = scenarioFactions;
        if (scenarioRelations.Count > 0) scenario["relations"] = scenarioRelations;
        scenario["countries"] = scenarioCountries;
        scenario["areas"] = new JsonArray(areasOut.Values.Select(a => (JsonNode)a).ToArray());

        // ---- write the mod
        Directory.CreateDirectory(outDir);
        string scenariosDir = Path.Combine(outDir, "data", "json", "scenarios");
        Directory.CreateDirectory(scenariosDir);
        WriteJsonLines(Path.Combine(scenariosDir, $"conquest_{number}.json"), scenario);
        using (var enable = new BinaryWriter(File.Create(Path.Combine(outDir, enableFile))))
        {
            enable.Write(regions.Count);
            for (int i = 0; i < regions.Count; i++) enable.Write((byte)1);
        }
        // the conquest list entry: the preview scales centerX by map width / 1715 and centerY by height / 750
        var bounds = mapJson["bounds"]!.AsObject();
        double west = bounds["west"]!.GetValue<double>(), east = bounds["east"]!.GetValue<double>();
        double north = bounds["north"]!.GetValue<double>(), south = bounds["south"]!.GetValue<double>();
        var camera = cfg["camera"]!.AsObject();
        double camLon = camera["lon"]!.GetValue<double>(), camLat = camera["lat"]!.GetValue<double>();
        string conquestsDir = Path.Combine(outDir, "data", "json", "conquests");
        Directory.CreateDirectory(conquestsDir);
        WriteJson(Path.Combine(conquestsDir, "conquests.json"), new JsonObject
        {
            ["about"] = "The conquest of this mod (merged into the game's conquest list).",
            ["conquests"] = new JsonObject
            {
                [$"conquest {number}"] = new JsonObject
                {
                    ["centerX"] = (int)Math.Round((camLon - west) / (east - west) * 1715),
                    ["centerY"] = (int)Math.Round((north - camLat) / (north - south) * 750),
                    ["fogOfWar"] = cfg["fogOfWar"]?.GetValue<bool>() ?? false,
                    ["leftRightWrap"] = cfg["leftRightWrap"]?.GetValue<bool>() ?? false,
                    ["flags"] = new JsonArray(),
                },
            },
        });
        foreach (var lang in Languages)
        {
            string dir = Path.Combine(outDir, "data", "json", "text", lang);
            Directory.CreateDirectory(dir);
            foreach (var table in new[] { $"stringtable_{lang}", $"stringtable_{lang}_iPad" })
                WriteJson(Path.Combine(dir, table + ".json"), new JsonObject
                {
                    ["about"] = "Texts of this mod's conquest (merged into the game's table; English where a language has none).",
                    ["strings"] = strings[lang].DeepClone(),
                });
        }
        // flags: a new country is registered (the game loads flags and art per registered id) and brings
        // flag_<id>.png; a country with the game's art and a flag of its own gets this scenario's flag key
        // (flag_<key>.png, the game's flag stays everywhere else)
        var newCountries = new JsonObject();
        string sourceDir = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        var symbols = cfg["symbols"]?.AsObject();
        int drawn = 0;
        foreach (var c in countries)
        {
            string id = c["id"]!.GetValue<string>();
            if (c["flag"] == null) continue;   // the game's own flag
            string key = c["art"] != null ? FlagKey(c, number) : id;
            if (c["art"] == null) newCountries[id] = new JsonObject();
            using var cloth = FlagCloth(c["flag"]!, sourceDir, symbols);
            SavePng(cloth, Path.Combine(outDir, $"flag_{key}.png"));
            using (var listButton = CountryButton(cloth)) SavePng(listButton, Path.Combine(outDir, $"button_{key}@2x.png"));
            using (var small = SmallFlag(cloth)) SavePng(small, Path.Combine(outDir, $"sflag_{key}@2x.png"));
            drawn++;
        }
        if (opts.GetValueOrDefault("preview") is string previewPath)
            WriteOwnerPreview(previewPath, packDir, mapJson, regions, owner, countries);
        if (newCountries.Count > 0)
        {
            string dir = Path.Combine(outDir, "data", "json", "countries");
            Directory.CreateDirectory(dir);
            WriteJson(Path.Combine(dir, "countries.json"), new JsonObject
            {
                ["about"] = "The countries this mod adds (merged into the game's list): each brings its flag, its conquest list button and its small preview flag, pictures named by its id (MODDING.md, section 4); units use the common art.",
                ["countries"] = newCountries,
            });
        }
        var button = cfg["button"]?.AsObject() ?? new JsonObject();
        WriteConquestButton(Path.Combine(outDir, $"button_conquest{number}@2x.png"), packDir, mapJson, regions, owner, countries,
            button["title"]?.GetValue<string>() ?? texts["name"]?["en"]?.GetValue<string>() ?? $"CONQUEST {number}",
            button["lon"]?.GetValue<double>() ?? camLon, button["lat"]?.GetValue<double>() ?? camLat, button["degreesWide"]?.GetValue<double>() ?? 60);
        var mod = cfg["mod"]!.AsObject();
        WriteJson(Path.Combine(outDir, "mod.json"), new JsonObject
        {
            ["id"] = mod["id"]!.GetValue<string>(),
            ["name"] = mod["name"]!.GetValue<string>(),
            ["version"] = mod["version"]?.GetValue<string>() ?? "1.0",
            ["author"] = mod["author"]?.GetValue<string>() ?? "WC2.Tools build-scenario",
            ["description"] = mod["description"]?.GetValue<string>() ?? "",
            ["priority"] = mod["priority"]?.GetValue<int>() ?? 1,
            ["enabled"] = mod["enabled"]?.GetValue<bool>() ?? true,
        });

        int landOwned = regions.Count(r => !r.sea && owner[r.id] != null), land = regions.Count(r => !r.sea);
        Console.WriteLine($"build-scenario: conquest {number} on map {mapId}: {scenarioCountries.Count} countries, {scenarioFactions.Count} factions, " +
                          $"{scenarioRelations.Count} relations; {landOwned} of {land} land areas owned, {fleetsTotal} fleets, {drawn} flags drawn -> {outDir}");
        Console.WriteLine("build-scenario: land areas per country: " + string.Join(", ", summary));
        return 0;
    }

    /// <summary>The scenario's flag key of a country with the game's art: "flagKey", else c&lt;conquest&gt;_&lt;id&gt;.</summary>
    private static string FlagKey(JsonObject c, int number) => c["flagKey"]?.GetValue<string>() ?? $"c{number}_{c["id"]!.GetValue<string>()}";

    // ---- rules ---------------------------------------------------------------------------------

    /// <summary>
    /// Whether a region meets every condition of a rule: <c>countries</c> (today's country code),
    /// <c>provinces</c> (ISO 3166-2 codes, a prefix ending in '-' takes a whole country), <c>era</c>
    /// ({"1939": ["gw:255", ...]}: the unit holding it in that era of the pack), <c>lon</c> / <c>lat</c>
    /// ([min, max]), <c>near</c> ([lon, lat, km]), <c>regions</c> (ids), <c>not</c> (a rule that must not match).
    /// </summary>
    private static bool Matches(JsonObject rule, Region r)
    {
        if (rule["countries"] is JsonArray cs && !cs.Any(c => c!.GetValue<string>() == r.country)) return false;
        if (rule["provinces"] is JsonArray ps && !ps.Any(p =>
            {
                string code = p!.GetValue<string>();
                return code.EndsWith('-') ? r.provinces.Any(x => x.StartsWith(code, StringComparison.Ordinal)) : r.provinces.Contains(code);
            })) return false;
        if (rule["era"] is JsonObject era)
            foreach (var (year, units) in era)
                if (!r.owners.TryGetValue(year, out var unit) || !units!.AsArray().Any(u => u!.GetValue<string>() == unit)) return false;
        if (rule["lon"] is JsonArray lon && (r.lon < lon[0]!.GetValue<double>() || r.lon > lon[1]!.GetValue<double>())) return false;
        if (rule["lat"] is JsonArray lat && (r.lat < lat[0]!.GetValue<double>() || r.lat > lat[1]!.GetValue<double>())) return false;
        if (rule["near"] is JsonArray near && Km(near[0]!.GetValue<double>(), near[1]!.GetValue<double>(), r.lon, r.lat) > near[2]!.GetValue<double>()) return false;
        if (rule["regions"] is JsonArray ids && !ids.Any(i => i!.GetValue<int>() == r.id)) return false;
        if (rule["not"] is JsonObject not && Matches(not, r)) return false;
        return true;
    }

    private static double Km(double lon1, double lat1, double lon2, double lat2)
    {
        double p1 = lat1 * Math.PI / 180, p2 = lat2 * Math.PI / 180, dl = (lon2 - lon1) * Math.PI / 180;
        double a = Math.Sin((p2 - p1) / 2) * Math.Sin((p2 - p1) / 2) + Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
        return 6371 * 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    // ---- flags ---------------------------------------------------------------------------------

    private const int ClothW = 150, ClothH = 102;

    /// <summary>
    /// The plain flag (150 x 102): a PNG next to the rules (<c>"flag": "file.png"</c>, scaled to fit) or a
    /// design: <c>{"bands": [colours], "vertical": false, "weights": [...], "shape": "rect", "overlays": [...]}</c>.
    /// <c>shape</c> <c>triangle</c> or <c>swallowtail</c> cuts the cloth (transparent outside). Overlays, each
    /// with a <c>color</c>, lengths as fractions of the flag (r of its height): <c>rect</c> (x, y, w, h),
    /// <c>cross</c> (x, y, width; a Nordic cross with x below 0.5), <c>saltire</c> (width), <c>raggedSaltire</c>
    /// (width: the Cross of Burgundy), <c>disc</c> (x, y, r), <c>ring</c> (x, y, r, width), <c>star</c> (x, y, r,
    /// points), <c>triangle</c> (w: a hoist triangle), <c>crescent</c> (x, y, r), <c>diamond</c> (x, y, w, h),
    /// <c>checker</c> (x, y, r, n, color2), <c>lozengy</c> (n, color2), <c>border</c> (width), <c>text</c> (text, x, y,
    /// h, bold, font) and <c>path</c>: SVG path data <c>d</c> or a <c>symbol</c> of the rules' <c>symbols</c>, in a
    /// <c>box</c> [w, h], centred at x, y with height h (or <c>full</c>: the whole cloth), <c>mirror</c> (also
    /// drawn mirrored), <c>flip</c>, <c>rotate</c> (degrees), <c>stroke</c> (a line of that width in box units),
    /// <c>at</c> ([[x, y], ...]: repeated) and <c>colors</c> ({role: colour} for a symbol's parts).
    /// </summary>
    private static SKBitmap FlagCloth(JsonNode design, string sourceDir, JsonObject symbols)
    {
        var bmp = new SKBitmap(ClothW, ClothH, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        if (design is JsonValue v && v.TryGetValue(out string file))
        {
            using var src = SKBitmap.Decode(Path.Combine(sourceDir, file)) ?? throw new ToolException("unreadable flag picture " + file);
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(src, new SKRect(0, 0, ClothW, ClothH), paint);
            return bmp;
        }
        var d = design.AsObject();
        // the cloth's outline: everything outside stays transparent
        using (var shape = new SKPath())
        {
            switch (d["shape"]?.GetValue<string>() ?? "rect")
            {
                case "triangle":
                    shape.MoveTo(0, 0); shape.LineTo(ClothW, ClothH / 2f); shape.LineTo(0, ClothH); shape.Close();
                    break;
                case "swallowtail":
                    shape.MoveTo(0, 0); shape.LineTo(ClothW, 0); shape.LineTo(ClothW * 0.72f, ClothH / 2f); shape.LineTo(ClothW, ClothH); shape.LineTo(0, ClothH); shape.Close();
                    break;
                default:
                    shape.AddRect(new SKRect(0, 0, ClothW, ClothH));
                    break;
            }
            canvas.ClipPath(shape, SKClipOperation.Intersect, true);
        }
        var bands = d["bands"]?.AsArray().Select(n => Color(n!.GetValue<string>())).ToList() ?? new List<SKColor> { SKColors.White };
        var weights = d["weights"]?.AsArray().Select(n => n!.GetValue<double>()).ToList() ?? bands.Select(_ => 1.0).ToList();
        bool vertical = d["vertical"]?.GetValue<bool>() ?? false;
        double total = weights.Sum(), at = 0;
        for (int i = 0; i < bands.Count; i++)
        {
            double a0 = at / total, a1 = (at + weights[i]) / total;
            at += weights[i];
            using var p = new SKPaint { Color = bands[i] };
            if (vertical) canvas.DrawRect(new SKRect((float)(a0 * ClothW), 0, (float)Math.Ceiling(a1 * ClothW), ClothH), p);
            else canvas.DrawRect(new SKRect(0, (float)(a0 * ClothH), ClothW, (float)Math.Ceiling(a1 * ClothH)), p);
        }
        foreach (var oNode in d["overlays"]?.AsArray() ?? new JsonArray())
        {
            var o = oNode!.AsObject();
            var positions = o["at"] is JsonArray list
                ? list.Select(n => (n![0]!.GetValue<double>(), n[1]!.GetValue<double>())).ToList()
                : new List<(double, double)> { (o["x"]?.GetValue<double>() ?? 0.5, o["y"]?.GetValue<double>() ?? 0.5) };
            foreach (var (px, py) in positions)
                Overlay(canvas, o, (float)px, (float)py, symbols);
        }
        return bmp;
    }

    private static void Overlay(SKCanvas canvas, JsonObject o, float fx, float fy, JsonObject symbols)
    {
        using var p = new SKPaint { Color = Color(o["color"]?.GetValue<string>() ?? "#ffffff"), IsAntialias = true };
        float F(string key, double fallback) => (float)(o[key]?.GetValue<double>() ?? fallback);
        float x = fx * ClothW, y = fy * ClothH, r = F("r", 0.25) * ClothH;
        switch (o["type"]!.GetValue<string>())
        {
            case "rect":
                canvas.DrawRect(new SKRect(F("x", 0) * ClothW, F("y", 0) * ClothH, (F("x", 0) + F("w", 1)) * ClothW, (F("y", 0) + F("h", 1)) * ClothH), p);
                break;
            case "cross":
            {
                float w = F("width", 0.2) * ClothH;
                canvas.DrawRect(new SKRect(x - w / 2, 0, x + w / 2, ClothH), p);
                canvas.DrawRect(new SKRect(0, y - w / 2, ClothW, y + w / 2), p);
                break;
            }
            case "saltire":
                p.Style = SKPaintStyle.Stroke; p.StrokeWidth = F("width", 0.18) * ClothH;
                canvas.DrawLine(0, 0, ClothW, ClothH, p);
                canvas.DrawLine(ClothW, 0, 0, ClothH, p);
                break;
            case "raggedSaltire":
            {
                // the Cross of Burgundy: two rough branches with the stubs of cut-off twigs
                float w = F("width", 0.11) * ClothH;
                p.Style = SKPaintStyle.Stroke; p.StrokeWidth = w; p.StrokeCap = SKStrokeCap.Round;
                float m = w * 0.8f;
                foreach (var (x0, y0, x1, y1) in new[] { (m, m, ClothW - m, ClothH - m), (ClothW - m, m, m, ClothH - m) })
                {
                    canvas.DrawLine(x0, y0, x1, y1, p);
                    float dx = x1 - x0, dy = y1 - y0, len = MathF.Sqrt(dx * dx + dy * dy), nx = -dy / len, ny = dx / len;
                    for (int k = 1; k <= 6; k++)
                    {
                        if (k == 3 || k == 4) continue;   // the middle stays clear where the branches cross
                        float t = k / 7f, side = k % 2 == 0 ? 1 : -1;
                        float sx = x0 + dx * t, sy = y0 + dy * t;
                        canvas.DrawLine(sx, sy, sx + (nx * side + dx / len * 0.6f) * w * 1.1f, sy + (ny * side + dy / len * 0.6f) * w * 1.1f, p);
                    }
                }
                break;
            }
            case "disc":
                canvas.DrawCircle(x, y, r, p);
                break;
            case "ring":
                p.Style = SKPaintStyle.Stroke; p.StrokeWidth = F("width", 0.05) * ClothH;
                canvas.DrawCircle(x, y, r, p);
                break;
            case "star":
            {
                using var star = Star(x, y, r, o["points"]?.GetValue<int>() ?? 5, F("inner", 0.4));
                canvas.DrawPath(star, p);
                break;
            }
            case "triangle":
            {
                using var path = new SKPath();
                path.MoveTo(0, 0); path.LineTo(F("w", 0.4) * ClothW, ClothH / 2f); path.LineTo(0, ClothH); path.Close();
                canvas.DrawPath(path, p);
                break;
            }
            case "crescent":
            {
                using var outer = new SKPath(); outer.AddCircle(x, y, r);
                using var inner = new SKPath(); inner.AddCircle(x + r * F("offset", 0.35), y, r * F("inner", 0.82));
                using var moon = outer.Op(inner, SKPathOp.Difference);
                canvas.DrawPath(moon, p);
                break;
            }
            case "border":
            {
                p.Style = SKPaintStyle.Stroke; float w = F("width", 0.08) * ClothH; p.StrokeWidth = w;
                canvas.DrawRect(new SKRect(w / 2, w / 2, ClothW - w / 2, ClothH - w / 2), p);
                break;
            }
            case "diamond":
            {
                float hw = F("w", 0.8) * ClothW / 2, hh = F("h", 0.8) * ClothH / 2;
                using var path = new SKPath();
                path.MoveTo(x - hw, y); path.LineTo(x, y - hh); path.LineTo(x + hw, y); path.LineTo(x, y + hh); path.Close();
                canvas.DrawPath(path, p);
                break;
            }
            case "checker":
            {
                int n = o["n"]?.GetValue<int>() ?? 5;
                float cell = 2 * r / n;
                using var p2 = new SKPaint { Color = Color(o["color2"]?.GetValue<string>() ?? "#ffffff") };
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        canvas.DrawRect(SKRect.Create(x - r + i * cell, y - r + j * cell, cell, cell), (i + j) % 2 == 0 ? p : p2);
                break;
            }
            case "lozengy":
            {
                // diamonds on a lattice, alternating the two colours (the Wittelsbach lozenges)
                int n = o["n"]?.GetValue<int>() ?? 7;
                float cw = (float)ClothW / n, ch = cw * F("aspect", 1.5);
                using var p2 = new SKPaint { Color = Color(o["color2"]?.GetValue<string>() ?? "#ffffff"), IsAntialias = true };
                canvas.DrawRect(new SKRect(0, 0, ClothW, ClothH), p2);
                for (int j = -2; j * ch / 2 < ClothH + ch; j++)
                    for (int i = -2; i * cw / 2 < ClothW + cw; i++)
                    {
                        if (((i + j) & 1) != 0 || (i & 1) != 0) continue;
                        float cx = i * cw / 2, cy = j * ch / 2;
                        using var path = new SKPath();
                        path.MoveTo(cx - cw / 2, cy); path.LineTo(cx, cy - ch / 2); path.LineTo(cx + cw / 2, cy); path.LineTo(cx, cy + ch / 2); path.Close();
                        canvas.DrawPath(path, p);
                    }
                break;
            }
            case "text":
            {
                bool bold = o["bold"]?.GetValue<bool>() ?? true;
                using var font = new SKFont(SKTypeface.FromFamilyName(o["font"]?.GetValue<string>() ?? "Times New Roman",
                    bold ? SKFontStyle.Bold : SKFontStyle.Normal), F("h", 0.2) * ClothH);
                canvas.DrawText(o["text"]!.GetValue<string>(), x, y + font.Size * 0.36f, SKTextAlign.Center, font, p);
                break;
            }
            case "path":
            {
                // the parts to draw: a symbol's (each with its own role and colour) or one path of this overlay
                JsonObject sym = o["symbol"] is JsonNode sn
                    ? symbols?[sn.GetValue<string>()]?.AsObject() ?? throw new ToolException("unknown symbol " + sn.GetValue<string>())
                    : null;
                var box = (o["box"] ?? sym?["box"] ?? new JsonArray(100.0, 100.0)).AsArray();
                float bw = (float)box[0]!.GetValue<double>(), bh = (float)box[1]!.GetValue<double>();
                var parts = sym?["parts"]?.AsArray().Select(n => n!.AsObject()).ToList()
                            ?? new List<JsonObject> { new JsonObject { ["d"] = o["d"]!.GetValue<string>() } };
                float sx, sy, cx = x, cy = y;
                if (o["full"]?.GetValue<bool>() == true) { sx = ClothW / bw; sy = ClothH / bh; cx = ClothW / 2f; cy = ClothH / 2f; }
                else { sy = F("h", 0.8) * ClothH / bh; sx = o["w"] != null ? F("w", 0.5) * ClothW / bw : sy; }
                bool flipAll = o["flip"]?.GetValue<bool>() ?? false, mirrorAll = o["mirror"]?.GetValue<bool>() ?? false;
                var colors = o["colors"]?.AsObject();
                foreach (var part in parts)
                {
                    string role = part["role"]?.GetValue<string>();
                    string colour = (role != null ? colors?[role]?.GetValue<string>() : null) ?? part["color"]?.GetValue<string>() ?? o["color"]?.GetValue<string>() ?? "#000000";
                    if (colour == "none") continue;
                    using var pp = new SKPaint { Color = Color(colour), IsAntialias = true };
                    float? stroke = (float?)(part["stroke"]?.GetValue<double>() ?? o["stroke"]?.GetValue<double>());
                    if (stroke is float sw) { pp.Style = SKPaintStyle.Stroke; pp.StrokeWidth = sw; pp.StrokeCap = SKStrokeCap.Round; pp.StrokeJoin = SKStrokeJoin.Round; }
                    using var path = SKPath.ParseSvgPathData(part["d"]!.GetValue<string>()) ?? throw new ToolException("bad path " + part["d"]);
                    bool mirror = mirrorAll || (part["mirror"]?.GetValue<bool>() ?? false);
                    foreach (bool flipped in mirror ? new[] { false, true } : new[] { false })
                    {
                        canvas.Save();
                        canvas.Translate(cx, cy);
                        canvas.RotateDegrees(F("rotate", 0));
                        canvas.Scale(sx * ((flipped ^ flipAll) ? -1 : 1), sy);
                        canvas.Translate(-bw / 2, -bh / 2);
                        canvas.DrawPath(path, pp);
                        canvas.Restore();
                    }
                }
                break;
            }
            default:
                throw new ToolException("unknown flag overlay " + o["type"]);
        }
    }

    private static SKPath Star(float cx, float cy, float r, int points, float inner = 0.4f)
    {
        var path = new SKPath();
        for (int i = 0; i < points * 2; i++)
        {
            double a = -Math.PI / 2 + i * Math.PI / points;
            float rr = i % 2 == 0 ? r : r * inner;
            float px = cx + (float)(Math.Cos(a) * rr), py = cy + (float)(Math.Sin(a) * rr);
            if (i == 0) path.MoveTo(px, py); else path.LineTo(px, py);
        }
        path.Close();
        return path;
    }

    private static SKColor Color(string hex) => SKColor.TryParse(hex, out var c) ? c : throw new ToolException("bad colour " + hex);

    /// <summary>The conquest list button (151 x 105 at 2x, like the menu atlas's): the flag on an old,
    /// rounded board with a gold rim and a shadow; the game draws it 8 x 5 pixels up and left.</summary>
    private static SKBitmap CountryButton(SKBitmap cloth)
    {
        const int W = 151, H = 105;
        var bmp = new SKBitmap(W, H, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        var board = new SKRoundRect(new SKRect(6, 4, W - 8, H - 12), 6, 6);
        board.SetRectRadii(new SKRect(6, 4, W - 8, H - 12), new[] { new SKPoint(3, 3), new SKPoint(16, 16), new SKPoint(16, 16), new SKPoint(3, 3) });
        using (var shadow = new SKPaint { Color = new SKColor(40, 20, 0, 120), ImageFilter = SKImageFilter.CreateBlur(4, 4), IsAntialias = true })
        {
            canvas.Save(); canvas.Translate(3, 5); canvas.DrawRoundRect(board, shadow); canvas.Restore();
        }
        canvas.Save();
        canvas.ClipRoundRect(board, SKClipOperation.Intersect, true);
        using (var p = new SKPaint { IsAntialias = true })
            canvas.DrawBitmap(cloth, board.Rect, p);
        // age: a warm wash, darker towards the edges
        using (var wash = new SKPaint { Color = new SKColor(150, 110, 40, 60) }) canvas.DrawRect(board.Rect, wash);
        using (var vignette = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(new SKPoint(W / 2f, H / 2f), W * 0.62f,
                new[] { new SKColor(0, 0, 0, 0), new SKColor(60, 30, 0, 140) }, SKShaderTileMode.Clamp),
        }) canvas.DrawRect(board.Rect, vignette);
        canvas.Restore();
        using (var rim = new SKPaint { Color = new SKColor(196, 150, 60), Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true })
            canvas.DrawRoundRect(board, rim);
        return bmp;
    }

    /// <summary>The small preview flag (66 x 43 at 2x): the flag with a thin dark edge.</summary>
    private static SKBitmap SmallFlag(SKBitmap cloth)
    {
        const int W = 66, H = 43;
        var bmp = new SKBitmap(W, H, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        using (var p = new SKPaint { IsAntialias = true }) canvas.DrawBitmap(cloth, new SKRect(0, 0, W, H), p);
        using (var edge = new SKPaint { Color = new SKColor(20, 20, 20, 180), Style = SKPaintStyle.Stroke, StrokeWidth = 1 })
            canvas.DrawRect(new SKRect(0.5f, 0.5f, W - 0.5f, H - 0.5f), edge);
        return bmp;
    }

    /// <summary>The conquest list button (320 x 109 at 2x): the owners' colours around a point, a dark band and the title.</summary>
    private static void WriteConquestButton(string path, string packDir, JsonObject mapJson, List<Region> regions, string[] owner,
                                            List<JsonObject> countries, string title, double lon, double lat, double degreesWide)
    {
        const int W = 320, H = 109;
        var raster = mapJson["raster"]!.AsObject();
        int width = raster["width"]!.GetValue<int>(), height = raster["height"]!.GetValue<int>();
        var ids = ReadRaster(Path.Combine(packDir, raster["file"]?.GetValue<string>() ?? "regions.raster"), width, height);
        var b = mapJson["bounds"]!.AsObject();
        double west = b["west"]!.GetValue<double>(), north = b["north"]!.GetValue<double>();
        double ppd = raster["pixelsPerDegree"]?.GetValue<double>() ?? width / (b["east"]!.GetValue<double>() - west);
        var colorOf = countries.ToDictionary(c => c["id"]!.GetValue<string>(),
            c => { var a = (c["color"] ?? new JsonArray(200, 200, 200)).AsArray(); return new SKColor((byte)a[0]!.GetValue<int>(), (byte)a[1]!.GetValue<int>(), (byte)a[2]!.GetValue<int>()); });
        double step = degreesWide * ppd / W;
        using var bmp = new SKBitmap(W, H, SKColorType.Rgba8888, SKAlphaType.Premul);
        int cx = (int)((lon - west) * ppd), cy = (int)((north - lat) * ppd);
        int At(int x, int y) => ids[Math.Clamp(y, 0, height - 1) * width + ((x % width) + width) % width];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int rx = cx + (int)((x - W / 2) * step), ry = cy + (int)((y - H / 2) * step);
                int v = At(rx, ry);
                bool sea = v == 0 || regions[v - 1].sea;
                SKColor c = new SKColor(40, 70, 100);
                if (!sea)
                {
                    string o = owner[v - 1];
                    c = o != null && colorOf.TryGetValue(o, out var oc) ? oc : new SKColor(170, 150, 110);
                    int right = At(rx + (int)Math.Max(1, step), ry), down = At(rx, ry + (int)Math.Max(1, step));
                    bool border = (right != v && right > 0 && owner[right - 1] != o) || (down != v && down > 0 && owner[down - 1] != o);
                    if (border) c = new SKColor((byte)(c.Red / 2), (byte)(c.Green / 2), (byte)(c.Blue / 2));
                }
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
        SavePng(bmp, path);
    }

    /// <summary>--preview FILE: the whole map, 2 raster pixels per picture pixel, in the owners' colours
    /// (country borders dark, unowned land sand, water blue).</summary>
    private static void WriteOwnerPreview(string path, string packDir, JsonObject mapJson, List<Region> regions, string[] owner, List<JsonObject> countries)
    {
        var raster = mapJson["raster"]!.AsObject();
        int width = raster["width"]!.GetValue<int>(), height = raster["height"]!.GetValue<int>();
        var ids = ReadRaster(Path.Combine(packDir, raster["file"]?.GetValue<string>() ?? "regions.raster"), width, height);
        var colorOf = countries.ToDictionary(c => c["id"]!.GetValue<string>(),
            c => { var a = (c["color"] ?? new JsonArray(200, 200, 200)).AsArray(); return new SKColor((byte)a[0]!.GetValue<int>(), (byte)a[1]!.GetValue<int>(), (byte)a[2]!.GetValue<int>()); });
        int W = width / 2, H = height / 2;
        using var bmp = new SKBitmap(W, H, SKColorType.Rgba8888, SKAlphaType.Premul);
        string OwnerAt(int x, int y) { int v = ids[Math.Clamp(y, 0, height - 1) * width + (x % width + width) % width]; return v == 0 || regions[v - 1].sea ? "~" : owner[v - 1] ?? ""; }
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                string o = OwnerAt(2 * x, 2 * y);
                SKColor c = o == "~" ? new SKColor(60, 90, 130) : o == "" ? new SKColor(215, 200, 160) : colorOf[o];
                if (o != "~" && (OwnerAt(2 * x + 2, 2 * y) != o || OwnerAt(2 * x, 2 * y + 2) != o))
                    c = new SKColor((byte)(c.Red * 0.45), (byte)(c.Green * 0.45), (byte)(c.Blue * 0.45));
                bmp.SetPixel(x, y, c);
            }
        SavePng(bmp, path);
    }

    // ---- files ---------------------------------------------------------------------------------

    private static JsonObject ReadJson(string path)
    {
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        return JsonNode.Parse(File.ReadAllText(path), documentOptions: options)!.AsObject();
    }

    private static List<Region> ReadRegions(string path)
    {
        var list = new List<Region>();
        foreach (var n in ReadJson(path)["regions"]!.AsArray())
        {
            var o = n!.AsObject();
            var center = o["center"]!.AsArray();
            list.Add(new Region
            {
                id = o["id"]!.GetValue<int>(),
                name = o["name"]?.GetValue<string>() ?? "",
                sea = o["sea"]?.GetValue<bool>() ?? false,
                coastal = o["coastal"]?.GetValue<bool>() ?? false,
                country = o["country"]?.GetValue<string>() ?? "",
                type = o["type"]?.GetValue<string>() ?? "normal",
                provinces = o["provinces"]?.AsArray().Select(p => p!.GetValue<string>()).ToArray() ?? Array.Empty<string>(),
                owners = o["owners"]?.AsObject().ToDictionary(kv => kv.Key, kv => kv.Value!.GetValue<string>()) ?? new(),
                lon = center[0]!.GetValue<double>(), lat = center[1]!.GetValue<double>(),
                neighbours = o["neighbours"]?.AsArray().Select(x => x!.GetValue<int>()).ToArray() ?? Array.Empty<int>(),
            });
        }
        for (int i = 0; i < list.Count; i++)
            if (list[i].id != i) throw new ToolException("regions.json: ids must run 0, 1, 2, ...");
        return list;
    }

    /// <summary>"WCRG", version, width, height, deflate compressed uint16 (region id + 1, 0 = none).</summary>
    private static ushort[] ReadRaster(string path, int width, int height)
    {
        byte[] data = File.ReadAllBytes(path);
        var bytes = new byte[width * height * 2];
        using (var z = new DeflateStream(new MemoryStream(data, 16, data.Length - 16), CompressionMode.Decompress))
        {
            int read = 0, n;
            while (read < bytes.Length && (n = z.Read(bytes, read, bytes.Length - read)) > 0) read += n;
        }
        var result = new ushort[width * height];
        Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
        return result;
    }

    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static void WriteJson(string path, JsonObject o) => File.WriteAllText(path, o.ToJsonString(Indented) + "\n");

    /// <summary>A scenario with one line per country and per area (readable diffs, small files).</summary>
    private static void WriteJsonLines(string path, JsonObject o)
    {
        var compact = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var sb = new System.Text.StringBuilder("{\n");
        int i = 0;
        foreach (var (key, value) in o)
        {
            sb.Append("  ").Append(JsonSerializer.Serialize(key)).Append(": ");
            if (value is JsonArray a && a.Count > 0 && a[0] is JsonObject)
            {
                sb.Append("[\n");
                for (int k = 0; k < a.Count; k++)
                    sb.Append("    ").Append(a[k]!.ToJsonString(compact)).Append(k + 1 < a.Count ? ",\n" : "\n");
                sb.Append("  ]");
            }
            else sb.Append(value?.ToJsonString(compact) ?? "null");
            sb.Append(++i < o.Count ? ",\n" : "\n");
        }
        sb.Append("}\n");
        File.WriteAllText(path, sb.ToString());
    }

    private static void SavePng(SKBitmap bmp, string path)
    {
        using var data = bmp.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);
        data.SaveTo(file);
    }
}
