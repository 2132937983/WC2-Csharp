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
    }

    [Theory]
    // coverage: north to southern Greenland, south to southern Madagascar and northern Australia
    [InlineData(-43.9, 59.9, "gl", null)]     // Cape Farewell
    [InlineData(45.1, -25.0, "mg", null)]     // southern Madagascar
    [InlineData(130.84, -12.46, "au", null)]  // Darwin
    // ownership and features
    [InlineData(2.35, 48.85, "fr", "capital")]     // Paris
    [InlineData(-0.12, 51.5, "gb", "capital")]     // London
    [InlineData(13.4, 52.52, "de", "capital")]     // Berlin
    [InlineData(37.6, 55.75, "ru", "capital")]     // Moscow
    [InlineData(139.7, 35.7, "ja", "capital")]     // Tokyo
    [InlineData(-77.0, 38.9, "am", "capital")]     // Washington
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
          "sea": { "cellDegrees": 20, "minSeaPixels": 50 }, "minBorderPixels": 2,
          "cities": { "minPopulation": 1000000, "largeCityPopulation": 5000000 },
          "ports": { "maxScalerank": 5 }, "airfields": { "types": ["major"], "maxScalerank": 4 },
          "countries": { "AAA": "aa", "BBB": "bb" },
          "classicExport": { "mapId": 7, "scale": 2, "scenario": "conquest_test.xml", "modId": "map-test", "modName": "Test map",
            "players": { "aa": { "alliance": "a", "color": [10, 20, 30] }, "bb": { "alliance": "b", "color": [40, 50, 60] } } } }
        """;
        string source = Support.TempTree(
            ("ne_10m_admin_1_states_provinces.geojson", provinces), ("ne_50m_populated_places.geojson", places),
            ("ne_10m_ports.geojson", ports), ("ne_10m_airports.geojson", airports), ("config.json", config));
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
        Assert.Equal(2, land.Count);                                   // the corner merged into West, the islet is dropped
        var west = pack.LandRegionNear(5, 5);
        var east = pack.LandRegionNear(15, 5);
        Assert.Equal("aa", west.country);
        Assert.Equal("capital", west.type);
        Assert.Equal("Westcity", west.name);
        Assert.NotNull(west.airfield);
        Assert.Equal("bb", east.country);
        Assert.Equal("port", east.type);
        Assert.Contains(east.id, west.neighbours);                     // the two countries touch
        Assert.True(west.coastal && east.coastal);
        Assert.Null(pack.LandRegionNear(25.1, 20.1, 0));               // the islet is water now
    }

    /// <summary>The map in the scene's own formats (map 7 here): what the game's loaders read.</summary>
    private static void CheckClassicExport(string dir)
    {
        // area7.bin: count, then 11 ints per area; type 1 = sea
        var area = File.ReadAllBytes(Path.Combine(dir, "area7.bin"));
        int count = BitConverter.ToInt32(area, 0);
        Assert.Equal(4 + count * 44, area.Length);
        int seas = Enumerable.Range(0, count).Count(i => BitConverter.ToInt32(area, 4 + i * 44 + 40) == 1);
        Assert.Equal(2, count - seas);                                  // the two land regions
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
