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
          "countries": { "AAA": "aa", "BBB": "bb" } }
        """;
        string source = Support.TempTree(
            ("ne_10m_admin_1_states_provinces.geojson", provinces), ("ne_50m_populated_places.geojson", places),
            ("ne_10m_ports.geojson", ports), ("ne_10m_airports.geojson", airports), ("config.json", config));
        string outDir = Path.Combine(source, "out");
        Assert.Equal(0, MapPipeline.Run(new Dictionary<string, string>
        {
            ["config"] = Path.Combine(source, "config.json"), ["source"] = source, ["out"] = outDir, ["preview-width"] = "200",
        }));

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
}
