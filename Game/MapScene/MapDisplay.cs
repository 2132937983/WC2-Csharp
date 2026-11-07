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
        };
    }
}
