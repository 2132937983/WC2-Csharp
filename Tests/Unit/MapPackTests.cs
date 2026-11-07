namespace WC2.Tests;

/// <summary>
/// The generated Natural Earth map pack (assets/data/maps/natural-earth) and the pipeline that builds
/// it (WC2.Tools build-map, MAP_PIPELINE.md).
/// </summary>
[Collection("assets")]
public class MapPackTests
{
    private static MapPack LoadPack()
    {
        Support.MountRepositoryAssets();
        var pack = MapPack.Load("natural-earth");
        Assert.NotNull(pack);
        return pack;
    }

    [Fact]
    public void ThePackIsConsistent()
    {
        var pack = LoadPack();
        Assert.Empty(pack.Check());
        Assert.InRange(pack.regions.Count(r => !r.sea), 500, 3000);
        Assert.InRange(pack.regions.Count(r => r.sea), 100, 2000);
        Assert.True(pack.wrap);
        Assert.Equal(new[] { "1910", "1914", "1936", "1939", "1945", "1949", "2018" }, pack.eras.Keys.ToArray());
    }

    [Theory]
    // one base map for every era: the unit holding a place in a year (CShapes gwcode, Natural Earth today)
    [InlineData(16.37, 48.21, "1914", "gw:300")]   // Vienna: Austria-Hungary
    [InlineData(16.37, 48.21, "1939", "gw:305")]   // Vienna: the Austrian unit (annexed: the 1939 scenario gives it to Germany)
    [InlineData(21.0, 52.23, "1910", "gw:365")]    // Warsaw: the Russian Empire
    [InlineData(21.0, 52.23, "1939", "gw:290")]    // Warsaw: Poland
    [InlineData(24.94, 60.17, "1914", "gw:365")]   // Helsinki: Russian
    [InlineData(24.94, 60.17, "1936", "gw:375")]   // Helsinki: Finland
    [InlineData(126.98, 37.57, "1939", "gw:730")]  // Seoul: Korea (the Japanese colony)
    [InlineData(126.98, 37.57, "1949", "gw:732")]  // Seoul: the Republic of Korea
    [InlineData(13.4, 52.52, "2018", "ne:DEU")]    // Berlin today
    public void EveryRegionKnowsItsUnitInEveryEra(double lon, double lat, string era, string unit)
    {
        var region = LoadPack().LandRegionNear(lon, lat);
        Assert.NotNull(region);
        Assert.Equal(unit, region.owners[era]);
    }

    [Fact]
    public void HistoricalBordersSplitTodaysProvinces()
    {
        // Lviv was Austrian in 1914, Polish in 1939 and Soviet in 1945; Krakow was Austrian in 1914 and
        // Polish since: the 1945 border separates the two regions
        var pack = LoadPack();
        var lviv = pack.LandRegionNear(24.03, 49.84);
        var krakow = pack.LandRegionNear(19.94, 50.06);
        Assert.NotEqual(lviv.id, krakow.id);
        Assert.Equal(lviv.owners["1914"], krakow.owners["1914"]);
        Assert.NotEqual(lviv.owners["1945"], krakow.owners["1945"]);
    }

    [Theory]
    // coverage: north to southern Greenland, south to southern Madagascar and northern Australia
    [InlineData(-43.9, 59.9, "gl", null)]     // Cape Farewell
    [InlineData(55.0, 80.6, "ru", null)]      // Franz Josef Land (the northern edge: the Russian polar islands)
    [InlineData(16.0, 78.5, "no", null)]      // Svalbard
    [InlineData(-172.5, 65.5, "ru", null)]    // Chukotka east of the date line (the map wraps)
    [InlineData(45.1, -25.0, "mg", null)]     // southern Madagascar
    [InlineData(130.84, -12.46, "au", null)]  // Darwin
    // ownership and features
    [InlineData(2.35, 48.85, "fr", "capital")]     // Paris
    [InlineData(-0.12, 51.5, "gb", "capital")]     // London
    [InlineData(13.4, 52.52, "de", "capital")]     // Berlin
    [InlineData(37.6, 55.75, "ru", "capital")]     // Moscow
    [InlineData(139.7, 35.7, "ja", "capital")]     // Tokyo
    [InlineData(-77.0, 38.9, "am", "capital")]     // Washington
    [InlineData(30.3, 59.94, "ru", "capital")]     // Saint Petersburg, the capital of 1910 and 1914
    public void PlacesLieInTheRightCountries(double lon, double lat, string country, string type)
    {
        var region = LoadPack().LandRegionNear(lon, lat);
        Assert.NotNull(region);
        Assert.Equal(country, region.country);
        if (type != null) Assert.Equal(type, region.type);
    }

    [Fact]
    public void MarseilleIsAFrenchPortOnTheSea()
    {
        var pack = LoadPack();
        var marseille = pack.LandRegionNear(5.37, 43.30);
        Assert.Equal("fr", marseille.country);
        Assert.NotNull(marseille.port);
        Assert.True(marseille.coastal);
        Assert.Contains(marseille.neighbours, n => pack.regions[n].sea);
    }

    [Fact]
    public void ThePipelineBuildsRegionsAdjacencyAndFeatures()
    {
        // a tiny world: country A (two provinces side by side, one too small to stand alone), country B
        // to the east, a small island of A, and the sea around; a capital, a port and an airport
        static string F(double d) => d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        static string Square(string iso, string adm0, string name, double x0, double y0, double x1, double y1) =>
            "{ \"type\": \"Feature\", \"properties\": { \"name\": \"" + name + "\", \"iso_3166_2\": \"" + iso + "\", \"adm0_a3\": \"" + adm0 +
            "\", \"iso_a2\": \"" + adm0[..2] + "\" }, \"geometry\": { \"type\": \"Polygon\", \"coordinates\": [[[" +
            F(x0) + "," + F(y0) + "],[" + F(x1) + "," + F(y0) + "],[" + F(x1) + "," + F(y1) + "],[" + F(x0) + "," + F(y1) + "],[" + F(x0) + "," + F(y0) + "]]] } }";
        static string Point(string props, double lon, double lat) =>
            "{ \"type\": \"Feature\", \"properties\": " + props + ", \"geometry\": { \"type\": \"Point\", \"coordinates\": [" + F(lon) + "," + F(lat) + "] } }";
        string provinces = "{ \"type\": \"FeatureCollection\", \"features\": [" + string.Join(",",
            Square("AA-1", "AAA", "West", 0, 0, 10, 10),
            Square("AA-2", "AAA", "Corner", 10, 0, 11, 10),
            Square("BB-1", "BBB", "East", 11, 0, 20, 10),
            Square("AA-3", "AAA", "Islet", 25, 20, 25.2, 20.2)) + "] }";
        string places = "{ \"type\": \"FeatureCollection\", \"features\": [" + Point("""{ "NAME": "Westcity", "ADM0CAP": 1, "ADM0_A3": "AAA", "ISO_A2": "AA", "POP_MAX": 2000000 }""", 5, 5) + "] }";
        string ports = "{ \"type\": \"FeatureCollection\", \"features\": [" + Point("""{ "name": "Eastport", "scalerank": 3 }""", 19.95, 5) + "] }";
        string airports = "{ \"type\": \"FeatureCollection\", \"features\": [" + Point("""{ "name": "West Intl", "type": "major", "scalerank": 2 }""", 3, 3) + "] }";
        string config = """
        { "id": "test", "name": "Test", "bounds": { "west": -10, "east": 30, "north": 25, "south": -10 },
          "raster": { "width": 400 }, "regions": { "minAreaKm2": 200000, "maxPerCountry": 5, "minIslandKm2": 5000 },
          "eras": { "1900": { "date": "1900-01-01" }, "today": { "source": "naturalearth" } },
          "historicalBorders": { "file": "history.geojson", "minFragmentKm2": 4000, "minFragmentWidthPixels": 3 },
          "sea": { "coastRadiusDegrees": 3, "oceanRadiusDegrees": 8, "growth": 0.5, "relaxIterations": 1, "minSeaPixels": 50 }, "minBorderPixels": 2,
          "cities": { "minPopulation": 1000000, "largeCityPopulation": 5000000 },
          "ports": { "maxScalerank": 5 }, "airfields": { "types": ["major"], "maxScalerank": 4 },
          "countries": { "AAA": "aa", "BBB": "bb" },
          "classicExport": { "mapId": 7, "scale": 2, "modId": "map-test", "modName": "Test map",
            "scenarios": [ { "number": 11, "file": "conquest_test.xml", "era": "1900", "title": "TEST 1900", "centerLon": 10, "centerLat": 5,
              "fogOfWar": true, "leftRightWrap": false, "intro": "{country}", "victory": "win",
              "owners": { "aa": ["1"], "bb": ["2"] },
              "players": { "aa": { "alliance": "a", "color": [10, 20, 30] }, "bb": { "alliance": "b", "color": [40, 50, 60] } } } ] } }
        """;
        // the historical borders of 1900: unit 1 holds the west half of West, unit 2 everything east of it
        // (the border of 1900 cuts today's province West in two); a 0.1 degree sliver of unit 2 along
        // West's northern coast is two sources disagreeing and joins its neighbour
        static string Unit(int gw, double x0, double x1, double y0, double y1) =>
            "{ \"type\": \"Feature\", \"properties\": { \"cntry_name\": \"U" + gw + "\", \"gwcode\": " + gw + ", \"capname\": \"\", " +
            "\"gwsyear\": 1886, \"gwsmonth\": 1, \"gwsday\": 1, \"gweyear\": 1950, \"gwemonth\": 1, \"gweday\": 1 }, " +
            "\"geometry\": { \"type\": \"Polygon\", \"coordinates\": [[[" + F(x0) + "," + F(y0) + "],[" + F(x1) + "," + F(y0) + "],[" +
            F(x1) + "," + F(y1) + "],[" + F(x0) + "," + F(y1) + "],[" + F(x0) + "," + F(y0) + "]]] } }";
        string history = "{ \"type\": \"FeatureCollection\", \"features\": [" + string.Join(",",
            Unit(1, 0, 5, 0, 10), Unit(2, 5, 20, 0, 10), Unit(2, 0, 5, 9.9, 10)) + "] }";
        string source = Support.TempTree(
            ("ne_10m_admin_1_states_provinces.geojson", provinces), ("ne_50m_populated_places.geojson", places),
            ("ne_10m_ports.geojson", ports), ("ne_10m_airports.geojson", airports), ("config.json", config), ("history.geojson", history));
        string outDir = Path.Combine(source, "out");
        Assert.Equal(0, MapPipeline.Run(new Dictionary<string, string>
        {
            ["config"] = Path.Combine(source, "config.json"), ["source"] = source, ["out"] = outDir, ["preview-width"] = "200",
            ["classic-out"] = Path.Combine(source, "classic"),
        }));
        CheckClassicExport(Path.Combine(source, "classic"));

        AssetSystem.Reset();
        AssetSystem.Mount(new AssetLayer { Id = "test", Source = new DirectoryAssetSource(source) });
        JsonData.SchemaResolver = null;
        // the pack is read through the asset layer: give it the path the reader expects
        Directory.CreateDirectory(Path.Combine(source, "data", "maps"));
        Directory.Move(outDir, Path.Combine(source, "data", "maps", "test"));
        AssetSystem.Reset();
        AssetSystem.Mount(new AssetLayer { Id = "test", Source = new DirectoryAssetSource(source) });
        var pack = MapPack.Load("test");
        Assert.NotNull(pack);
        Assert.Empty(pack.Check());

        var land = pack.regions.Where(r => !r.sea).ToList();
        // the 1900 border splits West; the corner merged into the east half of West (the same units in
        // every era), the islet is dropped, the sliver made no region of its own
        Assert.Equal(3, land.Count);
        var westWest = pack.LandRegionNear(2, 5);
        var west = pack.LandRegionNear(7, 5);
        var east = pack.LandRegionNear(15, 5);
        Assert.NotEqual(westWest.id, west.id);
        Assert.Equal("gw:1", westWest.owners["1900"]);
        Assert.Equal("gw:2", west.owners["1900"]);
        Assert.Equal("ne:AAA", west.owners["today"]);
        Assert.Equal("ne:AAA", westWest.owners["today"]);
        Assert.Equal("aa", west.country);
        Assert.Equal("capital", west.type);
        Assert.Equal("Westcity", west.name);
        Assert.NotNull(westWest.airfield);
        Assert.Equal("bb", east.country);
        Assert.Equal("port", east.type);
        Assert.Contains(east.id, west.neighbours);                     // the two countries touch
        Assert.True(west.coastal && east.coastal);
        Assert.Null(pack.LandRegionNear(25.1, 20.1, 0));               // the islet is water now
        // the 1900 scenario: unit 1 is aa, unit 2 bb; its conquest entry carries the two options
        string scenario = File.ReadAllText(Path.Combine(source, "classic", "conquest_test.xml"));
        Assert.Contains($"<area id=\"{westWest.id}\" construction=\"airport\" level=\"1\" installation=\"none\" country=\"aa\"", scenario);
        Assert.Contains($"<area id=\"{west.id}\" construction=\"city\" level=\"2\" installation=\"fort\" country=\"bb\"", scenario);
        string conquests = File.ReadAllText(Path.Combine(source, "classic", "data", "json", "conquests", "conquests.json"));
        Assert.Contains("\"fogOfWar\": true", conquests);
        Assert.Contains("\"leftRightWrap\": false", conquests);
    }

    /// <summary>The map in the scene's own formats (map 7 here): what the game's loaders read.</summary>
    private static void CheckClassicExport(string dir)
    {
        // area7.bin: count, then 11 ints per area; type 1 = sea
        var area = File.ReadAllBytes(Path.Combine(dir, "area7.bin"));
        int count = BitConverter.ToInt32(area, 0);
        Assert.Equal(4 + count * 44, area.Length);
        int seas = Enumerable.Range(0, count).Count(i => BitConverter.ToInt32(area, 4 + i * 44 + 40) == 1);
        Assert.Equal(3, count - seas);                                  // the three land regions
        Assert.True(seas > 0);
        // the area table lists every area, the sea zones too (an area missing there would load as land)
        var table = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "data", "json", "maps", "7", "areas.json")))!["areas"]!.AsObject();
        Assert.Equal(count, table.Count);
        // adjion7.bin: count, then 17 ints per area (neighbour count + 16 ids); symmetric
        var adj = File.ReadAllBytes(Path.Combine(dir, "adjion7.bin"));
        Assert.Equal(count, BitConverter.ToInt32(adj, 0));
        int[] Neighbours(int a) => Enumerable.Range(0, BitConverter.ToInt32(adj, 4 + a * 68)).Select(k => BitConverter.ToInt32(adj, 4 + a * 68 + 4 + k * 4)).ToArray();
        for (int a = 0; a < count; a++)
            foreach (int b in Neighbours(a)) Assert.Contains(a, Neighbours(b));
        // areamark7.raw: width, height, one area id (or -1) per 8x8 block
        var mark = File.ReadAllBytes(Path.Combine(dir, "areamark7.raw"));
        int mw = BitConverter.ToInt32(mark, 0), mh = BitConverter.ToInt32(mark, 4);
        Assert.Equal(8 + mw * mh * 2, mark.Length);
        Assert.All(Enumerable.Range(0, mw * mh), i => Assert.InRange(BitConverter.ToInt16(mark, 8 + i * 2), -1, count - 1));
        // the scenario, the area table, the textures and their load formats, the mod manifest
        string scenario = File.ReadAllText(Path.Combine(dir, "conquest_test.xml"));
        Assert.Contains("map=\"7\"", scenario);
        Assert.Contains("country id=\"aa\"", scenario);
        Assert.Contains("country id=\"bb\"", scenario);
        Assert.Contains("type=\"tank\"", scenario);                    // the capital's army
        Assert.True(File.Exists(Path.Combine(dir, "data", "json", "maps", "7", "areas.json")));
        Assert.True(File.Exists(Path.Combine(dir, "map7_1.png")));
        Assert.True(File.Exists(Path.Combine(dir, "m7_zone1_1.xml")));
        string formats = File.ReadAllText(Path.Combine(dir, "generated", "texture_formats.json"));
        Assert.Contains("\"map7_1.png\": \"rgb565\"", formats);
        Assert.Contains("\"m7_zone1_1.png\": \"alpha8\"", formats);
        Assert.Contains("\"map-test\"", File.ReadAllText(Path.Combine(dir, "mod.json")));
    }
}
