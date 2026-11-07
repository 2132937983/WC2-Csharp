namespace WC2;

/// <summary>Scene rendering (CSceneRender.cpp).</summary>
public sealed partial class CScene
{
    private const int kRenderSpatialBucketSize = 256;

    private static int GetRenderSpatialBucketCoordinate(float position)
    {
        return (int)MathF.Floor(position / (float)kRenderSpatialBucketSize);
    }

    /// <summary>Draws a filled circle or a ring for a combat range (uses the current camera and blend mode).</summary>
    private static void RenderCombatRange(float centerX, float centerY, float minRange, float maxRange, uint color)
    {
        if (maxRange <= 0.0f)
            return;
        if (minRange <= 0.0f)
        {
            ecGraphics.Instance().RenderCircle(centerX, centerY, maxRange, color);
            return;
        }
        if (minRange >= maxRange)
            return;

        // Ring drawing is done by ecGraphics, keeping the current camera and blend mode.
        ecGraphics.Instance().RenderRing(centerX, centerY, minRange, maxRange, color);
    }

    /// <summary>
    /// Refreshes the cache of the areas inside the camera view.
    /// The cache only filters visibility; the area order and the layer order are unchanged.
    /// When neither the camera position nor the scale changed, the previous result is reused.
    /// </summary>
    public void EnsureVisibleRenderAreaList()
    {
        float centerX = camera.centerPos[0];
        float centerY = camera.centerPos[1];
        float scale = camera.scale;
        if (visibleRenderAreaListValid &&
            visibleCameraCenter[0] == centerX &&
            visibleCameraCenter[1] == centerY &&
            visibleCameraScale == scale)
        {
            return;
        }

        visibleRenderAreaList.Clear();
        if (visibleRenderAreaList.Capacity < renderAreaList.Count)
            visibleRenderAreaList.Capacity = renderAreaList.Count;
        if (areaData != null && renderAreaList.Count > 0)
        {
            CombatSpatialIndex spatialIndex = combatSpatialIndex;
            float halfWidth = camera.screenHalfSize[0] / scale;
            float halfHeight = camera.screenHalfSize[1] / scale;
            float viewLeft = centerX - halfWidth - (float)kRenderSpatialBucketSize;
            float viewRight = centerX + halfWidth + (float)kRenderSpatialBucketSize;
            float viewTop = centerY - halfHeight - (float)kRenderSpatialBucketSize;
            float viewBottom = centerY + halfHeight + (float)kRenderSpatialBucketSize;

            int firstBucketX = GetRenderSpatialBucketCoordinate(viewLeft) - spatialIndex.minX;
            int lastBucketX = GetRenderSpatialBucketCoordinate(viewRight) - spatialIndex.minX;
            int firstBucketY = GetRenderSpatialBucketCoordinate(viewTop) - spatialIndex.minY;
            int lastBucketY = GetRenderSpatialBucketCoordinate(viewBottom) - spatialIndex.minY;

            bool indexAvailable =
                spatialIndex.columns > 0 && spatialIndex.rows > 0 &&
                spatialIndex.buckets.Count > 0 && firstBucketX <= lastBucketX &&
                firstBucketY <= lastBucketY;
            bool useSpatialIndex = indexAvailable;
            int clippedFirstX = firstBucketX;
            int clippedLastX = lastBucketX;
            int clippedFirstY = firstBucketY;
            int clippedLastY = lastBucketY;
            if (useSpatialIndex)
            {
                clippedFirstX = Math.Max(0, clippedFirstX);
                clippedLastX = Math.Min(spatialIndex.columns - 1, clippedLastX);
                clippedFirstY = Math.Max(0, clippedFirstY);
                clippedLastY = Math.Min(spatialIndex.rows - 1, clippedLastY);
                long bucketCount = (long)(clippedLastX - clippedFirstX + 1) *
                                   (long)(clippedLastY - clippedFirstY + 1);
                // When the view covers almost the whole map the buckets give nothing; use the linear path.
                useSpatialIndex = clippedFirstX <= clippedLastX &&
                                  clippedFirstY <= clippedLastY &&
                                  bucketCount > 0 &&
                                  bucketCount < (long)spatialIndex.buckets.Count;
            }

            if (useSpatialIndex)
            {
                for (int bucketY = clippedFirstY; bucketY <= clippedLastY; ++bucketY)
                {
                    for (int bucketX = clippedFirstX; bucketX <= clippedLastX; ++bucketX)
                    {
                        int bucketIndex = bucketY * spatialIndex.columns + bucketX;
                        if (bucketIndex < 0 || bucketIndex >= spatialIndex.buckets.Count) continue;

                        foreach (int areaId in spatialIndex.buckets[bucketIndex])
                        {
                            if (areaId < 0 || areaId >= areaCount ||
                                areaId >= areaList.Count ||
                                areaList[areaId] == null)
                            {
                                continue;
                            }
                            AreaData data = areaData[areaId];
                            if (!IsRectInSecen(
                                    (float)data.AreaPos[0], (float)data.AreaPos[1],
                                    (float)data.AreaSize[0], (float)data.AreaSize[1]) ||
                                !camera.IsRectInCamera(
                                    (float)data.AreaPos[0], (float)data.AreaPos[1],
                                    (float)data.AreaSize[0], (float)data.AreaSize[1]))
                            {
                                continue;
                            }
                            CArea area = areaList[areaId];
                            visibleRenderAreaList.Add(new RenderAreaEntry((float)area.info.armyPos[1], area));
                        }
                    }
                }
                visibleRenderAreaList.Sort((left, right) => left.sortY.CompareTo(right.sortY));
            }
            else
            {
                foreach (RenderAreaEntry entry in renderAreaList)
                {
                    CArea area = entry.area;
                    if (area == null || area.id < 0 || area.id >= areaCount)
                        continue;

                    AreaData data = areaData[area.id];
                    if (camera.IsRectInCamera(
                            (float)data.AreaPos[0], (float)data.AreaPos[1],
                            (float)data.AreaSize[0], (float)data.AreaSize[1]))
                    {
                        visibleRenderAreaList.Add(entry);
                    }
                }
            }
        }

        visibleCameraCenter[0] = centerX;
        visibleCameraCenter[1] = centerY;
        visibleCameraScale = scale;
        visibleRenderAreaListValid = true;
    }

    /// <summary>Renders the map scene (every visible copy of a wrapping map).</summary>
    public void Render()
    {
        if (areaList.Count == 0)
            return;
        ForEachWrapCopy(RenderView);
    }

    /// <summary>
    /// Runs <paramref name="draw"/> once per copy of the map inside the view. A map that does not wrap
    /// has one copy; a wrapping map (leftRightWrap) shows the copies left and right of the seam: the
    /// camera is moved by the map width for each, so everything drawn in scene coordinates (areas,
    /// armies, effects, fight texts) appears in each copy. The camera is restored afterwards.
    /// </summary>
    public void ForEachWrapCopy(Action draw)
    {
        renderFrame++;   // the fog of war is worked out again once per frame
        if (!wrapX || size[0] <= 0.0f)
        {
            draw();
            return;
        }
        float center = camera.centerPos[0];
        float period = size[0];
        float halfWidth = camera.screenHalfSize[0] / camera.scale;
        for (int copy = -1; copy <= 1; copy++)
        {
            float left = topLeftPos[0] + copy * period;
            if (left + period < center - halfWidth || left > center + halfWidth)
                continue;
            camera.centerPos[0] = center - copy * period;
            camera.Apply();
            draw();
        }
        camera.centerPos[0] = center;
        camera.Apply();
    }

    // fog of war: visibility of every area for the player's alliance, refreshed once per frame
    private bool[] fogVisible;
    private int fogFrame = -1, renderFrame;

    /// <summary>
    /// Fog of war (conquest JSON fogOfWar): an area is visible when the player's alliance owns it or
    /// has an army in it, or when it touches such an area. Without fog every area is visible.
    /// </summary>
    public bool IsVisibleToPlayer(CArea area)
    {
        if (!fogOfWar || area == null || area.id < 0) return true;
        int frame = renderFrame;
        if (fogVisible == null || fogVisible.Length != areaCount || fogFrame != frame)
        {
            fogFrame = frame;
            fogVisible ??= new bool[areaCount];
            if (fogVisible.Length != areaCount) fogVisible = new bool[areaCount];
            Array.Clear(fogVisible);
            CCountry player = g_GameManager.GetPlayerCountry();
            if (player == null) { Array.Fill(fogVisible, true); return true; }
            int count = Math.Min(areaCount, areaList.Count);
            for (int i = 0; i < count; i++)
            {
                CArea a = areaList[i];
                if (a == null) continue;
                bool own = a.country != null && a.country.alliance == player.alliance;
                if (!own && a.armyCount > 0 && a.GetArmy(0)?.country is CCountry armyOwner)
                    own = armyOwner.alliance == player.alliance;
                if (!own) continue;
                fogVisible[i] = true;
                int neighbours = GetNumAdjacentAreas(i);
                for (int n = 0; n < neighbours; n++)
                {
                    int other = GetAdjacentAreaID(i, n);
                    if (other >= 0 && other < fogVisible.Length) fogVisible[other] = true;
                }
            }
        }
        return area.id >= fogVisible.Length || fogVisible[area.id];
    }

    /// <summary>Renders one copy of the map scene in the fixed layer order.</summary>
    public void RenderView()
    {
        if (areaList.Count == 0)
            return;
        float xFactor = g_contenscalefactor != 1.0f ? 1.0f : 2.0f;
        camera.Apply();
        // Reuse the visible area list of the previous frame when the camera did not change.
        EnsureVisibleRenderAreaList();
        if (mapMesh != null)
        {
            RenderMeshLayer();
        }
        else
        {
        background.Render(camera);

        int SelectedAreaID = -1;
        if (!g_GameManager.SkipMode)
        {
            if (areaSelected != null && areaSelected.id >= 0)
            {
                SelectedAreaID = areaSelected.id;
                ecImage selectedZone = zoneImage[SelectedAreaID];
                if (selectedZone != null)
                {
                    if (areaSelected.country != null)
                        selectedZone.SetColor(areaSelected.country.ColorRGBA, -1);
                    else
                        selectedZone.SetColor(0x80FF0000, -1);
                    selectedZone.SetAlpha(flashTimer, -1);
                    selectedZone.RenderEx(areaData[SelectedAreaID].AreaPos[0],
                                          areaData[SelectedAreaID].AreaPos[1],
                                          0.0f, xFactor, 0.0f);
                }
            }

            foreach (int i in fightFlashAreaId)
            {
                if (i >= 0 && i < areaCount && zoneImage[i] != null)
                {
                    zoneImage[i].SetColor(0xFF8080FF, -1);
                    zoneImage[i].SetAlpha(flashTimer, -1);
                    zoneImage[i].RenderEx(areaData[i].AreaPos[0],
                                          areaData[i].AreaPos[1],
                                          0.0f, xFactor, 0.0f);
                }
            }
        }

        int visibleAreaCount = visibleRenderAreaList.Count;

        // This cannot be merged into a per-area "zone, building, army" loop. The map relies on
        // the painter's algorithm rather than a depth buffer, so the global layer order of all
        // zones, then all buildings, then all armies must be kept.

        for (int i = 0; i < visibleAreaCount; ++i)
        {
            CArea RenderArea = visibleRenderAreaList[i].area;
            int areaId = RenderArea.id;
            if (areaId == SelectedAreaID)
                continue;

            if (!RenderArea.info.enable)
            {
                if (zoneImage[areaId] == null)
                    continue;
                zoneImage[areaId].SetColor(0xFFFFFFFF, -1);
            }
            else
            {
                if (RenderArea.country == null)
                    continue;
                if (zoneImage[areaId] == null)
                    continue;

                zoneImage[areaId].SetColor(RenderArea.country.ColorRGBA, -1);
                if (newRoundFlashing &&
                    RenderArea.country == g_GameManager.GetCurCountry())
                {
                    zoneImage[areaId].SetAlpha(flashTimer, -1);
                }
                else if (RenderArea.info.sea)
                {
                    continue;
                }
            }

            zoneImage[areaId].RenderEx(areaData[areaId].AreaPos[0],
                                       areaData[areaId].AreaPos[1],
                                       0.0f, xFactor, 0.0f);
        }

        // the border box frames a map with edges; a wrapping map has none left and right, and the
        // camera never shows anything above or below it
        if (!wrapX)
            background.RenderBox(camera, topLeftPos[0], topLeftPos[1], size[0], size[1]);
        }

        int visibleCount = visibleRenderAreaList.Count;
        // a mesh map zoomed out: buildings are left out and armies become flag markers (display markerZoom)
        float cameraScale = camera.scale;
        for (int i = 0; i < visibleCount; ++i)
        {
            CArea area = visibleRenderAreaList[i].area;
            if (display != null && cameraScale * area.unitScale < display.markerZoom) continue;
            area.RenderBuilding();
        }

        for (int i = 0; i < visibleCount; ++i)
        {
            CArea RenderArea = visibleRenderAreaList[i].area;
            // fog of war hides the armies of the areas the player cannot see
            if (fogOfWar && !IsVisibleToPlayer(RenderArea))
                continue;
            bool marker = display != null && cameraScale * RenderArea.unitScale < display.markerZoom;
            if (marker) RenderArea.RenderMarker(cameraScale, display.markerZoom);
            else RenderArea.Render();
            // the target arrows above the army follow its scale
            float arrowScale = marker ? display.markerZoom / cameraScale : RenderArea.unitScale;
            if (arrowScale != 1f && RenderArea.targetType != 0)
                ecGraphics.Instance().SetLocalScale(RenderArea.info.armyPos[0], RenderArea.info.armyPos[1], arrowScale);
            if (RenderArea.targetType == 1)
            {
                g_GameRes.imageArrowshadow.RenderEx(RenderArea.info.armyPos[0] - verticalArrowTimer * 0.5f,
                                                    RenderArea.info.armyPos[1] + verticalArrowTimer * 0.5f,
                                                    0.0f, g_contenscalefactor, 0.0f);
                g_GameRes.imageArrowGreen.Render(RenderArea.info.armyPos[0],
                                                 RenderArea.info.armyPos[1] + verticalArrowTimer);
            }
            else if (RenderArea.targetType == 2)
            {
                g_GameRes.imageArrowshadow.RenderEx(RenderArea.info.armyPos[0] - verticalArrowTimer * 0.5f,
                                                    RenderArea.info.armyPos[1] + verticalArrowTimer * 0.5f,
                                                    0.0f, g_contenscalefactor, 0.0f);
                g_GameRes.imageArrowYellow.Render(RenderArea.info.armyPos[0],
                                                  RenderArea.info.armyPos[1] + verticalArrowTimer);
            }
            ecGraphics.Instance().ClearLocalScale();
        }

        if (areaSelected != null)
        {
            if (areaSelected.info.constructionType == AIRPORT
                && areaSelected.country != null)
            {
                ecGraphics.Instance().SetBlendMode(2);
                RenderCombatRange(areaSelected.info.constructionPos[0],
                                  areaSelected.info.constructionPos[1],
                                  0.0f,
                                  areaSelected.country.AirstrikeRadius(),
                                  0x4F000000);
            }
            if (areaSelected.armyCount > 0 &&
                areaSelected.country != null)
            {
                CArmy selectedArmy = areaSelected.GetArmy(0);
                if (selectedArmy != null && selectedArmy.basicAbilities != null)
                {
                    ArmyCombatProfile attack = selectedArmy.basicAbilities.combat.attack;
                    if (attack.range.shape == COMBAT_RANGE_CIRCLE)
                    {
                        float minRange = CircleRangeToMapDistance(attack.range.minRange);
                        float maxRange = CircleRangeToMapDistance(attack.range.maxRange);
                        if (attack.range.radiusFromCountry)
                            maxRange = areaSelected.country.AirstrikeRadius();
                        ecGraphics.Instance().SetBlendMode(2);
                        RenderCombatRange(areaSelected.info.armyPos[0],
                                          areaSelected.info.armyPos[1],
                                          minRange, maxRange,
                                          0x4F800000);
                    }
                }
            }

            for (int i = 0; i < GetNumAdjacentAreas(areaSelected.id); i++)
            {
                CArea LayerArea = GetAdjacentArea(areaSelected.id, i);
                if (LayerArea == null)
                    continue;
                if (LayerArea.targetType == 3)
                {
                    g_GameRes.imageArrowBlue.Render4VC(LayerArea.arrowEndOffset[0],
                                                       LayerArea.arrowEndOffset[1],
                                                       LayerArea.arrowEndOffsetMinus[0],
                                                       LayerArea.arrowEndOffsetMinus[1],
                                                       LayerArea.arrowStartOffsetMinus[0],
                                                       LayerArea.arrowStartOffsetMinus[1],
                                                       LayerArea.arrowStartOffset[0],
                                                       LayerArea.arrowStartOffset[1],
                                                       0, arrowTimer);
                }
                else if (LayerArea.targetType == 4)
                {
                    g_GameRes.imageArrowRed.Render4VC(LayerArea.arrowEndOffset[0],
                                                      LayerArea.arrowEndOffset[1],
                                                      LayerArea.arrowEndOffsetMinus[0],
                                                      LayerArea.arrowEndOffsetMinus[1],
                                                      LayerArea.arrowStartOffsetMinus[0],
                                                      LayerArea.arrowStartOffsetMinus[1],
                                                      LayerArea.arrowStartOffset[0],
                                                      LayerArea.arrowStartOffset[1],
                                                      0, arrowTimer);
                }
            }
        }

        foreach (CMedal Medal in medalList)
            Medal.Render();
        if (bomber != null && !g_GameManager.SkipMode)
            bomber.Render();
    }

    private static uint WithAlpha(uint abgr, float alpha) =>
        (abgr & 0x00FFFFFFu) | ((uint)Math.Clamp((int)(alpha * 255f), 0, 255) << 24);

    /// <summary>
    /// A mesh map (map&lt;id&gt;.mesh): the areas in their owners' colours and the borders, then the
    /// highlights of the classic zone masks: the current country's areas flashing at a new round, the
    /// selected area and the two areas of a fight.
    /// </summary>
    private void RenderMeshLayer()
    {
        float halfWidth = camera.screenHalfSize[0] / camera.scale, halfHeight = camera.screenHalfSize[1] / camera.scale;
        mapMesh.Render(this, camera.centerPos[0] - halfWidth, camera.centerPos[0] + halfWidth,
                       camera.centerPos[1] - halfHeight, camera.centerPos[1] + halfHeight);
        if (g_GameManager.SkipMode) return;
        if (newRoundFlashing && g_GameManager.GetCurCountry() is CCountry current)
            mapMesh.RenderCountry(current, WithAlpha(0xFFFFFFFFu, (flashTimer - 0.5f) * 0.8f));
        if (areaSelected != null && areaSelected.id >= 0)
            mapMesh.RenderRegion(areaSelected.id, WithAlpha(areaSelected.country?.ColorRGBA ?? 0xFF0000FFu, flashTimer * 0.75f));
        foreach (int i in fightFlashAreaId)
            if (i >= 0 && i < areaCount) mapMesh.RenderRegion(i, WithAlpha(0xFF8080FFu, flashTimer * 0.75f));
    }

    /// <summary>Renders the sea area debug overlay.</summary>
    public void RenderSeaAreas()
    {
        if (areaData == null || zoneImage == null)
            return;

        EnsureVisibleRenderAreaList();
        foreach (RenderAreaEntry entry in visibleRenderAreaList)
        {
            CArea area = entry.area;
            if (area == null || area.id < 0 || area.id >= areaCount ||
                !area.info.sea)
            {
                continue;
            }
            ecImage zone = zoneImage[area.id];
            if (zone == null) continue;

            AreaData data = areaData[area.id];

            zone.SetColor(0x80FF0000, -1);
            zone.SetAlpha(0.5f, -1);
            zone.RenderEx(data.AreaPos[0], data.AreaPos[1], 0.0f, 1.0f, 0.0f);
        }
    }

    /// <summary>Creates and registers a medal gain animation at a scene position.</summary>
    public void GainMedal(float x, float y)
    {
        var medal = new CMedal();
        medal.Init(x, y);
        medalList.Add(medal);
    }

    /// <summary>Filters and sorts the areas that need rendering.</summary>
    public void CreateRenderAreaList()
    {
        renderAreaList.Clear();
        visibleRenderAreaList.Clear();
        visibleRenderAreaListValid = false;
        if (renderAreaList.Capacity < areaCount)
            renderAreaList.Capacity = areaCount;

        for (int areaId = 0; areaId < areaCount; ++areaId)
        {
            AreaData data = areaData[areaId];
            if (!IsRectInSecen((float)data.AreaPos[0], (float)data.AreaPos[1],
                               (float)data.AreaSize[0], (float)data.AreaSize[1]))
            {
                continue;
            }

            CArea area = areaId < areaList.Count ? GetArea(areaId) : null;
            if (area != null)
                renderAreaList.Add(new RenderAreaEntry((float)area.info.armyPos[1], area));
        }

        renderAreaList.Sort((left, right) => left.sortY.CompareTo(right.sortY));
    }
}
