namespace WC2;

// Allow to load more map images
// Check if zone loading pattern is followed
// allow to set more areas
// Fix AI rocket melee attack

/// <summary>Tiled map background (MapExtension.cpp; RenderBox is in Camera.cs).</summary>
public sealed partial class CBackground
{
    public int[] mapSize = new int[2];
    public int[] mapTopRightPos = new int[2];
    public int mapCount;
    // The extended background replaces the fixed 112-tile arrays of the original with
    // mapCount-sized ones (ExtendedBackgroundTexture / ExtendedBackgroundImage in the C++).
    public ecTexture[] backgroundTexture = new ecTexture[112];
    public ecImage[] backgroundImage = new ecImage[112];
    public ecTexture[] boxTexture = new ecTexture[3];
    public ecImage[] boxImage = new ecImage[3];

    /// <summary>Extending background picture.</summary>
    public CBackground()
    {
        int i;
        for (i = 0; i < backgroundTexture.Length; i++)
        {
            backgroundTexture[i] = null;
            backgroundImage[i] = null;
        }
        boxTexture[0] = null;
        boxTexture[1] = null;
        boxTexture[2] = null;
        boxImage[0] = null;
        boxImage[1] = null;
        boxImage[2] = null;
    }

    /// <summary>Releases the images, textures and arrays used by the extended background.</summary>
    private void ReleaseExtendedBackground()
    {
        if (backgroundImage != null)
        {
            for (int i = 0; i < backgroundImage.Length; ++i)
                backgroundImage[i] = null;
        }

        if (backgroundTexture != null)
        {
            for (int i = 0; i < backgroundTexture.Length; ++i)
            {
                if (backgroundTexture[i] != null)
                {
                    ecGraphics.Instance().FreeTexture(backgroundTexture[i]);
                    backgroundTexture[i] = null;
                }
            }
        }

        backgroundTexture = new ecTexture[0];
        backgroundImage = new ecImage[0];
    }

    /// <summary>Destructor equivalent: releases the box images/textures and the extended background.</summary>
    public void Release()
    {
        int i;
        for (i = 0; i < 3; i++)
            boxImage[i] = null;
        for (i = 0; i < 3; i++)
        {
            ecGraphics.Instance().FreeTexture(boxTexture[i]);
            boxTexture[i] = null;
        }
        ReleaseExtendedBackground();
    }

    public void Init(int MapID, int TotalX, int TotalY, int TotalW, int TotalH, float EnableX,
                     float EnableY, float EnableW, float EnableH)
    {
        // Init may run again on a map switch: release the previous set before allocating new arrays.
        ReleaseExtendedBackground();

        mapTopRightPos[0] = TotalX;
        mapTopRightPos[1] = TotalY;
        mapSize[0] = TotalW;
        mapSize[1] = TotalH;
        float[] CurrentPos = { 0.0f, 0.0f };
        int[] MapGridSize = new int[2];
        MapGridSize[0] = (TotalW + 499) / 500;
        MapGridSize[1] = (TotalH + 499) / 500;
        int EnableMapCount = 0;
        mapCount = MapGridSize[0] * MapGridSize[1];
        int i;
        backgroundTexture = new ecTexture[mapCount];
        backgroundImage = new ecImage[mapCount];
        string FileName;
        for (i = 0; i < mapCount; i++)
        {
            if (CurrentPos[0] < (EnableX + EnableW)
                && CurrentPos[1] < (EnableY + EnableH)
                && (CurrentPos[0] + 500.0f) >= EnableX
                && (CurrentPos[1] + 500.0f) >= EnableY)
            {
                // map%d_%d.pkm resolves map%d_%d@2x.pkm (1024px, logical 512) automatically.
                FileName = Sprintf("map%d_%d.pkm", MapID, i + 1);
                backgroundTexture[i] = ecGraphics.Instance().LoadETCTexture(FileName);
                backgroundImage[i] = backgroundTexture[i] != null
                    ? new ecImage(backgroundTexture[i], 6.0f, 6.0f, 500.0f, 500.0f)
                    : null;
                EnableMapCount++;
            }
            else
            {
                backgroundTexture[i] = null;
                backgroundImage[i] = null;
            }
            CurrentPos[0] += 500.0f;
            if (CurrentPos[0] >= (float)mapSize[0])
            {
                CurrentPos[1] += 500.0f;
                CurrentPos[0] = 0.0f;
            }
        }
        printf("load num bg = %d\n", EnableMapCount);
        ecTextureRect[] rect =
        {
            new ecTextureRect(0.0f, 0.0f, 82.0f, 82.0f, 0.0f, 0.0f),
            new ecTextureRect(1.0f, 0.0f, 139.0f, 82.0f, 0.0f, 0.0f),
            new ecTextureRect(0.0f, 1.0f, 82.0f, 139.0f, 0.0f, 0.0f),
        };
        for (i = 0; i < 3; i++)
        {
            FileName = Sprintf("box%d.png", i + 1);
            boxTexture[i] = ecGraphics.Instance().LoadTexture(FileName);
            boxImage[i] = new ecImage(boxTexture[i], rect[i]);
        }
    }

    /// <summary>Draws the background tiles.</summary>
    public void Render(CCamera camera)
    {
        float[] CurrentPos = { 0.0f, 0.0f };
        int i;
        for (i = 0; i < mapCount; i++)
        {
            if (i < backgroundImage.Length && backgroundImage[i] != null)
                backgroundImage[i].Render(CurrentPos[0], CurrentPos[1]);
            CurrentPos[0] += 500.0f;
            if (CurrentPos[0] >= (float)mapSize[0])
            {
                CurrentPos[1] += 500.0f;
                CurrentPos[0] = 0.0f;
            }
        }
    }
}

/// <summary>Zone images and area tax loading (MapExtension.cpp).</summary>
public sealed partial class CScene
{
    // Check zone image
    public void InitAreaImage(int MapID)
    {
        string FileName;
        float[] CurPos = { 0.0f, 0.0f };
        float xDir = 1000.0f;
        float[] MapSize = new float[2];
        MapSize[0] = (float)areaMark.size[0] * 8.0f;
        MapSize[1] = (float)areaMark.size[1] * 8.0f;
        int ZoneCount = (((int)MapSize[0] - 1) / 1000 + 1) * (((int)MapSize[1] - 1) / 1000 + 1);
        int AreaCount = areaList.Count;
        bool[] ZoneLoaded = new bool[Math.Max(ZoneCount, 0)];
        int i, LoadCount = 0;
        for (i = 0; i < ZoneCount; i++)
        {
            if (CurPos[0] < (topLeftPos[0] + size[0])
                && CurPos[1] < (topLeftPos[1] + size[1])
                && (CurPos[0] + 1000.0f) >= topLeftPos[0]
                && (CurPos[1] + 1000.0f) >= topLeftPos[1])
            {
                if (g_contenscalefactor == 1.0f)
                {
                    FileName = Sprintf("m%d_zone%d.xml", MapID, i + 1);
                    if (areaTextureRes.LoadRes(FileName, false))
                        LoadCount += 1;
                }
                else if (g_contenscalefactor == 2.0f)
                {
                    FileName = Sprintf("m%d_zone%d_1.xml", MapID, i + 1);
                    if (areaTextureRes.LoadRes(FileName, false))
                        LoadCount += 1;
                    FileName = Sprintf("m%d_zone%d_2.xml", MapID, i + 1);
                    if (areaTextureRes.LoadRes(FileName, false))
                        LoadCount += 1;
                }
                ZoneLoaded[i] = true;
            }
            else
            {
                ZoneLoaded[i] = false;
            }
            CurPos[0] += xDir;
            if (CurPos[0] >= MapSize[0])
            {
                CurPos[0] -= 1000.0f;
                CurPos[1] += 1000.0f;
                xDir = -1000.0f;
            }
            else if (CurPos[0] < 0.0f)
            {
                CurPos[0] += 1000.0f;
                CurPos[1] += 1000.0f;
                xDir = 1000.0f;
            }
        }
        printf("load num zone = %d\n", LoadCount);
        zoneImage = new ecImage[AreaCount];
        for (i = 0; i < AreaCount; i++)
        {
            FileName = Sprintf("%04d.png", i);
            ecImageAttr p = areaTextureRes.GetImage(FileName);
            if (p != null)
            {
                zoneImage[i] = new ecImage(p);
                zoneImage[i].SetColor(0xFFFFFFFF, -1);
            }
            else
            {
                zoneImage[i] = null;
            }
        }
        // Validation
        int validAreaCount = Math.Min(AreaCount, areaList.Count);
        for (i = 0; i < validAreaCount; i++)
        {
            if (GetArea(i).info.enable && zoneImage[i] == null)
            {
                // Retry failed load
                for (i = 0; i < ZoneCount; i++)
                {
                    if (!ZoneLoaded[i])
                    {
                        if (g_contenscalefactor == 1.0f)
                        {
                            FileName = Sprintf("m%d_zone%d.xml", MapID, i + 1);
                            if (areaTextureRes.LoadRes(FileName, false))
                                LoadCount += 1;
                        }
                        else if (g_contenscalefactor == 2.0f)
                        {
                            FileName = Sprintf("m%d_zone%d_1.xml", MapID, i + 1);
                            if (areaTextureRes.LoadRes(FileName, false))
                                LoadCount += 1;
                            FileName = Sprintf("m%d_zone%d_2.xml", MapID, i + 1);
                            if (areaTextureRes.LoadRes(FileName, false))
                                LoadCount += 1;
                        }
                    }
                }
                for (i = 0; i < AreaCount; i++)
                {
                    if (zoneImage[i] == null)
                    {
                        FileName = Sprintf("%04d.png", i);
                        ecImageAttr p = areaTextureRes.GetImage(FileName);
                        if (p != null)
                        {
                            zoneImage[i] = new ecImage(p);
                            zoneImage[i].SetColor(0xFFFFFFFF, -1);
                        }
                        else
                        {
                            zoneImage[i] = null;
                        }
                    }
                }
                break;
            }
        }
    }

    /// <summary>Loads the area types and taxes; this also contains the extended area count support.</summary>
    /// <summary>One row of the area table: id, area type name, tax (null when not given).</summary>
    public sealed record AreaTableEntry(int id, string type, int? tax);

    /// <summary>Area table of a map from maps/&lt;id&gt;/areas.json; null when the package has none.</summary>
    public static List<AreaTableEntry> ReadAreaTableJson(int mapId)
    {
        var root = JsonData.LoadMerged(DataPaths.MapAreas(mapId));
        var areas = root?.Obj("areas");
        if (areas == null) return null;
        var list = new List<AreaTableEntry>();
        foreach (var (key, o) in areas.Entries())
        {
            int id = TryParseInt(key, out int parsed) ? parsed : 0;
            list.Add(new AreaTableEntry(id, o.Str("type"), o.TryInt("tax", out int tax) ? tax : null));
        }
        return list;
    }

    /// <summary>The original area table areatax&lt;id&gt;.xml; null when it cannot be read.</summary>
    public static List<AreaTableEntry> ReadAreaTableXml(int mapId)
    {
        var Xml = new TiXmlDocument(GetPath(Sprintf("areatax%d.xml", mapId), null));
        if (!Xml.LoadFile(TIXML_ENCODING_UNKNOWN)) return null;
        TiXmlNode AreaNode = Xml.FirstChild("areas");
        if (AreaNode == null) return null;
        var list = new List<AreaTableEntry>();
        for (TiXmlElement e = AreaNode.FirstChildElement(); e != null; e = e.NextSiblingElement())
        {
            if (e.QueryIntAttribute("id", out int AreaID) != TIXML_SUCCESS) AreaID = 0;
            list.Add(new AreaTableEntry(AreaID, e.Attribute("type"), e.QueryIntAttribute("tax", out int Tax) == TIXML_SUCCESS ? Tax : null));
        }
        return list;
    }

    /// <summary>Converts areatax&lt;id&gt;.xml to maps/&lt;id&gt;/areas.json (--convert-data).</summary>
    public static System.Text.Json.Nodes.JsonObject ConvertAreaTableXml(int mapId)
    {
        var entries = ReadAreaTableXml(mapId);
        if (entries == null) return null;
        var areas = new System.Text.Json.Nodes.JsonObject();
        foreach (var e in entries)
        {
            var o = new System.Text.Json.Nodes.JsonObject();
            if (e.type != null) o["type"] = e.type;
            if (e.tax.HasValue) o["tax"] = e.tax.Value;
            areas[e.id.ToString(System.Globalization.CultureInfo.InvariantCulture)] = o;
        }
        return new System.Text.Json.Nodes.JsonObject
        {
            ["format"] = 1,
            ["about"] = "Area table of map " + mapId + " (converted from areatax" + mapId + ".xml): area id -> area type (see data/terrain.json) and base tax. Areas missing here are plain areas with tax 100.",
            ["areas"] = areas,
        };
    }

    public void LoadAreaTax(int MapID)
    {
        ClearAreas();
        // JSON area table first (maps/<id>/areas.json), the original XML otherwise
        var entries = ReadAreaTableJson(MapID) ?? ReadAreaTableXml(MapID);
        if (entries != null)
        {
            {
                for (int i = 0; i < areaCount; i++)
                {
                    var Area = new CArea();
                    Area.Init(i, new AreaInfo(AREA_TYPE.NORMAL, 100, areaData[i].ArmyPos[0], areaData[i].ArmyPos[1]));
                    // land or sea comes from the map data even for an area the table leaves out
                    // (otherwise such a sea zone would be land and armies would walk onto it)
                    Area.info.sea = areaData[i].Type == TERRAIN_TYPE.WATER_SEA;
                    areaList.Add(Area);
                }
                foreach (var entry in entries)
                {
                    // Create and initialise the AreaInfo.
                    var areaInfo = new AreaInfo(AREA_TYPE.NORMAL, 0);
                    // area type names come from the terrain definitions (unknown names are normal areas)
                    areaInfo.areaType = Terrain.ParseAreaType(entry.type);
                    int AreaID = entry.id;
                    if (entry.tax.HasValue)
                        areaInfo.tax = entry.tax.Value;
                    if (AreaID < 0 || AreaID >= areaCount || AreaID >= areaList.Count)
                        continue;
                    areaInfo.armyPos[0] = areaData[AreaID].ArmyPos[0];
                    areaInfo.armyPos[1] = areaData[AreaID].ArmyPos[1];
                    areaInfo.constructionPos[0] = areaData[AreaID].ConstructPos[0];
                    areaInfo.constructionPos[1] = areaData[AreaID].ConstructPos[1];
                    areaInfo.installationPos[0] = areaData[AreaID].InstallationPos[0];
                    areaInfo.installationPos[1] = areaData[AreaID].InstallationPos[1];
                    areaList[AreaID].Init(AreaID, areaInfo);
                    areaList[AreaID].info.sea = areaData[AreaID].Type == TERRAIN_TYPE.WATER_SEA;
                }
            }
        }
    }
}
