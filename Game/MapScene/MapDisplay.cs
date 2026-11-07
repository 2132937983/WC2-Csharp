namespace WC2;

/// <summary>
/// Display settings of a map (data/json/maps/&lt;id&gt;/areas.json "display", DATA_SCHEMA.md): how far the
/// camera zooms in, how the units of small areas are scaled down to fit (an area's "fit" radius against
/// unitFitRadius, never below minUnitScale), and below which on-screen unit size (camera zoom x unit
/// scale) the armies are shown as a flag marker of constant size. The classic maps have none.
/// </summary>
public sealed class MapDisplay
{
    public float maxZoom = 1f;
    public float unitFitRadius = 46f;
    public float minUnitScale = 0.35f;
    public float markerZoom = 0.32f;
    /// <summary>"armies": a flag only in areas with units or a building (the mesh shows the owner anyway); "all" (the classic maps): every owned area.</summary>
    public bool flagsWithArmiesOnly;
    /// <summary>The background picture (relief, natural colours, ocean): tiles named terrainFile with {0} = index, terrainColumns x terrainRows over the whole map; null: flat colours.</summary>
    public string terrainFile;
    public int terrainColumns = 1, terrainRows = 1;
    /// <summary>The paper grain over the map: a tile of detailUnits map units, shown from detailMinZoom.</summary>
    public string detailFile;
    public float detailUnits = 600f, detailMinZoom = 0.12f;
    /// <summary>Opacity of the owners' colours over the terrain picture.</summary>
    public float ownerAlpha = 0.45f;

    public static MapDisplay Read(int mapId)
    {
        var o = JsonData.LoadMerged(DataPaths.MapAreas(mapId))?.Obj("display");
        if (o == null) return null;
        return new MapDisplay
        {
            maxZoom = (float)o.Double("maxZoom", 1),
            unitFitRadius = (float)Math.Max(1, o.Double("unitFitRadius", 46)),
            minUnitScale = (float)o.Double("minUnitScale", 0.35),
            markerZoom = (float)o.Double("markerZoom", 0.32),
            flagsWithArmiesOnly = o.Str("flags", "all") == "armies",
            terrainFile = o.Obj("terrain")?.Str("file"),
            terrainColumns = Math.Max(1, o.Obj("terrain")?.Int("columns", 1) ?? 1),
            terrainRows = Math.Max(1, o.Obj("terrain")?.Int("rows", 1) ?? 1),
            ownerAlpha = (float)(o.Obj("terrain")?.Double("ownerAlpha", 0.45) ?? 0.45),
            detailFile = o.Obj("detail")?.Str("file"),
            detailUnits = (float)(o.Obj("detail")?.Double("units", 600) ?? 600),
            detailMinZoom = (float)(o.Obj("detail")?.Double("minZoom", 0.12) ?? 0.12),
        };
    }
}
