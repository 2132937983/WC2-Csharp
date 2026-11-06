using System.IO.Compression;
using System.Text.Json.Nodes;

namespace WC2;

/// <summary>A region of a map pack (land region or sea zone).</summary>
public sealed class MapRegion
{
    public int id;
    public string name = "";
    public bool sea;
    /// <summary>Game country id of a land region (data/json/countries/countries.json, or an ISO code).</summary>
    public string country = "";
    /// <summary>Terrain area type name (capital, port, large city, normal city, normal).</summary>
    public string type = "normal";
    public string city, port, airfield;
    public long cityPopulation;
    public bool coastal;
    public double lon, lat;
    public int pixelX, pixelY;
    public long areaKm2;
    public int[] neighbours = Array.Empty<int>();
    public override string ToString() => $"{id} {name} ({(sea ? "sea" : country + ", " + type)})";
}

/// <summary>
/// A generated map pack (<c>data/maps/&lt;id&gt;/</c>, built by <c>WC2.Tools build-map</c> from Natural
/// Earth, MAP_PIPELINE.md): <c>map.json</c> (bounds, projection, raster size, country summary),
/// <c>regions.json</c> (regions, sea zones, ownership, cities, ports, airfields, adjacency) and
/// <c>regions.raster</c> (the region of every pixel). The classic map (map 1) is not touched; a pack is
/// an extra map that a mode or a mod selects, so it can be removed again without affecting campaigns.
/// </summary>
public sealed class MapPack
{
    public string id = "", name = "";
    public double west, east, north, south, pixelsPerDegree;
    public int width, height;
    public readonly List<MapRegion> regions = new();
    private ushort[] raster;

    public static string Folder(string id) => $"data/maps/{id}/";

    /// <summary>Reads a pack through the asset layer (mods may replace or extend its files); null when missing.</summary>
    public static MapPack Load(string id)
    {
        var map = JsonData.LoadMerged(Folder(id) + "map.json");
        var list = JsonData.LoadMerged(Folder(id) + "regions.json");
        if (map == null || list == null) return null;
        var pack = new MapPack { id = map.Str("id", id), name = map.Str("name", id) };
        var b = map.Obj("bounds");
        pack.west = b.Double("west", -180); pack.east = b.Double("east", 180);
        pack.north = b.Double("north", 90); pack.south = b.Double("south", -90);
        var r = map.Obj("raster");
        pack.width = r.Int("width", 0); pack.height = r.Int("height", 0);
        pack.pixelsPerDegree = r.Double("pixelsPerDegree", pack.width / Math.Max(1e-9, pack.east - pack.west));
        foreach (var node in list.Arr("regions") ?? new JsonArray())
        {
            if (node is not JsonObject o) continue;
            var center = o.Arr("center");
            var pixel = o.Arr("pixel");
            var city = o.Obj("city");
            pack.regions.Add(new MapRegion
            {
                id = o.Int("id", pack.regions.Count),
                name = o.Str("name", ""),
                sea = o.Bool("sea", false),
                country = o.Str("country", ""),
                type = o.Str("type", "normal"),
                city = city?.Str("name"),
                cityPopulation = city != null ? (long)city.Double("population", 0) : 0,
                port = o.Get("port") is JsonValue pv ? (pv.TryGetValue(out string ps) ? ps : "port") : null,
                airfield = o.Get("airfield") is JsonValue av ? (av.TryGetValue(out string a) ? a : "airfield") : null,
                coastal = o.Bool("coastal", false),
                lon = center != null && center.Count > 1 ? center[0]!.GetValue<double>() : 0,
                lat = center != null && center.Count > 1 ? center[1]!.GetValue<double>() : 0,
                pixelX = pixel != null && pixel.Count > 1 ? pixel[0]!.GetValue<int>() : 0,
                pixelY = pixel != null && pixel.Count > 1 ? pixel[1]!.GetValue<int>() : 0,
                areaKm2 = (long)o.Double("areaKm2", 0),
                neighbours = (o.Arr("neighbours") ?? new JsonArray()).Select(n => n!.GetValue<int>()).ToArray(),
            });
        }
        pack.raster = ReadRaster(AssetSystem.ReadAllBytes(Folder(id) + r.Str("file", "regions.raster")), pack.width, pack.height);
        return pack;
    }

    /// <summary>"WCRG", version, width, height, deflate compressed uint16 (region id + 1, 0 = none).</summary>
    private static ushort[] ReadRaster(byte[] data, int width, int height)
    {
        if (data == null || data.Length < 16 || data[0] != 'W' || data[1] != 'C' || data[2] != 'R' || data[3] != 'G') return null;
        int w = BitConverter.ToInt32(data, 8), h = BitConverter.ToInt32(data, 12);
        if (w != width || h != height) return null;
        var bytes = new byte[w * h * 2];
        using (var z = new DeflateStream(new MemoryStream(data, 16, data.Length - 16), CompressionMode.Decompress))
        {
            int read = 0, n;
            while (read < bytes.Length && (n = z.Read(bytes, read, bytes.Length - read)) > 0) read += n;
            if (read != bytes.Length) return null;
        }
        var result = new ushort[w * h];
        Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
        return result;
    }

    /// <summary>The region at a longitude / latitude (null outside the map or where no region is).</summary>
    public MapRegion RegionAt(double lon, double lat)
    {
        if (raster == null) return null;
        int x = (int)((lon - west) * pixelsPerDegree), y = (int)((north - lat) * pixelsPerDegree);
        if (x < 0 || y < 0 || x >= width || y >= height) return null;
        int v = raster[y * width + x];
        return v > 0 && v <= regions.Count ? regions[v - 1] : null;
    }

    /// <summary>The land region at (or within a few pixels of) a longitude / latitude.</summary>
    public MapRegion LandRegionNear(double lon, double lat, int searchPixels = 4)
    {
        for (int r = 0; r <= searchPixels; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    var region = RegionAt(lon + dx / pixelsPerDegree, lat - dy / pixelsPerDegree);
                    if (region != null && !region.sea) return region;
                }
        return null;
    }

    /// <summary>Consistency problems (empty when the pack is sound).</summary>
    public List<string> Check()
    {
        var problems = new List<string>();
        if (raster == null) problems.Add("regions.raster missing or of another size");
        for (int i = 0; i < regions.Count; i++)
        {
            var r = regions[i];
            if (r.id != i) problems.Add($"region {i} has id {r.id}");
            if (!r.sea && string.IsNullOrEmpty(r.country)) problems.Add($"land region {r.id} has no country");
            foreach (int n in r.neighbours)
            {
                if (n < 0 || n >= regions.Count) { problems.Add($"region {r.id}: neighbour {n} does not exist"); continue; }
                if (n == r.id) problems.Add($"region {r.id} is its own neighbour");
                if (!regions[n].neighbours.Contains(r.id)) problems.Add($"region {r.id} -> {n} is not mutual");
            }
            if (!r.sea && r.neighbours.Length == 0) problems.Add($"land region {r.id} ({r.name}) touches nothing");
            if (r.port != null && !r.coastal) problems.Add($"region {r.id} ({r.name}) has a port but no coast");
        }
        foreach (var g in regions.Where(r => !r.sea).GroupBy(r => r.country))
            if (g.Count(r => r.type == "capital") > 1) problems.Add($"country {g.Key} has {g.Count(r => r.type == "capital")} capitals");
        return problems;
    }
}
