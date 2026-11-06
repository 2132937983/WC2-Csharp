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
    public void LoadAreaTax(int MapID)
    {
        ClearAreas();
        string TaxFileName = Sprintf("areatax%d.xml", MapID);
        var Xml = new TiXmlDocument(GetPath(TaxFileName, null));
        if (Xml.LoadFile(TIXML_ENCODING_UNKNOWN))
        {
            TiXmlNode AreaNode = Xml.FirstChild("areas");
            if (AreaNode != null)
            {
                for (int i = 0; i < areaCount; i++)
                {
                    var Area = new CArea();
                    Area.Init(i, new AreaInfo(AREA_TYPE.NORMAL, 100, areaData[i].ArmyPos[0], areaData[i].ArmyPos[1]));
                    areaList.Add(Area);
                }
                // Loop over the whole areas node.
                for (TiXmlElement AreasElement = AreaNode.FirstChildElement(); AreasElement != null; AreasElement = AreasElement.NextSiblingElement())
                {
                    // Create and initialise the AreaInfo.
                    var areaInfo = new AreaInfo(AREA_TYPE.NORMAL, 0);
                    int AreaID, Tax;
                    string Type = AreasElement.Attribute("type");
                    areaInfo.areaType = Type != null ?
                                        Type == "capital" ? CAPITAL :
                                        Type == "port" ? PORT :
                                        Type == "large city" ? LARGE_CITY :
                                        Type == "normal city" ? NORMAL_CITY : NORMAL
                                        : AREA_TYPE.NORMAL;
                    if (AreasElement.QueryIntAttribute("id", out AreaID) != TIXML_SUCCESS)
                        AreaID = 0;
                    if (AreasElement.QueryIntAttribute("tax", out Tax) == TIXML_SUCCESS)
                        areaInfo.tax = Tax;
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
