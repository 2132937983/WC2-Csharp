namespace WC2;

/// <summary>Scene rectangle, camera helpers, distances and the combat spatial index (CSceneCamera.cpp).</summary>
public sealed partial class CScene
{
    // Edge length of a spatial bucket. Only affects the index granularity, never the distance test.
    private const int kCombatSpatialBucketSize = 256;

    /// <summary>Bucket coordinate of a position on one axis.</summary>
    private static int GetCombatSpatialBucketCoordinate(float position)
    {
        float bucketCoordinate = MathF.Floor(position / (float)kCombatSpatialBucketSize);
        if (bucketCoordinate <= (float)int.MinValue)
            return int.MinValue;
        if (bucketCoordinate >= (float)int.MaxValue)
            return int.MaxValue;
        return (int)bucketCoordinate;
    }

    /// <summary>Computes the scene rectangle covered by the enabled areas.</summary>
    /// <param name="x">Left edge of the scene.</param>
    /// <param name="y">Top edge of the scene.</param>
    /// <param name="w">Width of the movable range.</param>
    /// <param name="h">Height of the movable range.</param>
    public void CalSceneRect(out float x, out float y, out float w, out float h)
    {
        int rawMapWidth = 8 * areaMark.size[0];
        int rawMapHeight = 8 * areaMark.size[1];
        float mapWidth = rawMapWidth - rawMapWidth % 100;
        float mapHeight = rawMapHeight - rawMapHeight % 100;

        x = mapWidth;
        y = mapHeight;
        float right = 0.0f;
        float bottom = 0.0f;

        if (areaData != null)
        {
            int validAreaCount = Math.Min(areaCount, areaList.Count);
            for (int areaId = 0; areaId < validAreaCount; ++areaId)
            {
                CArea area = areaList[areaId];
                if (area == null || !area.info.enable)
                    continue;

                AreaData data = areaData[areaId];
                float areaX = data.AreaPos[0];
                float areaY = data.AreaPos[1];
                float areaRight = data.AreaPos[0] + data.AreaSize[0];
                float areaBottom = data.AreaPos[1] + data.AreaSize[1];

                if (areaX < x) x = areaX;
                if (areaY < y) y = areaY;
                if (areaRight > right) right = areaRight;
                if (areaBottom > bottom) bottom = areaBottom;
            }
        }

        if (x < 0.0f) x = 0.0f;
        if (y < 0.0f) y = 0.0f;
        if (right > mapWidth) right = mapWidth;
        if (bottom > mapHeight) bottom = mapHeight;

        w = right - x;
        h = bottom - y;

        ecGraphics graphics = ecGraphics.Instance();
        if (w < graphics.screenW)
        {
            x -= (graphics.screenW - w) * 0.5f;
            if (x < 0.0f) x = 0.0f;
            if (x > mapWidth - graphics.screenW)
                x = mapWidth - graphics.screenW;
            w = graphics.screenW;
        }

        if (h < graphics.screenH)
        {
            y -= (graphics.screenH - h) * 0.5f;
            if (y < 0.0f) y = 0.0f;
            if (y > mapHeight - graphics.screenH)
                y = mapHeight - graphics.screenH;
            h = graphics.screenH;
        }
    }

    /// <summary>True when the rectangle intersects the scene range.</summary>
    public bool IsRectInSecen(float x, float y, float w, float h)
    {
        float sceneLeft = topLeftPos[0];
        float sceneTop = topLeftPos[1];
        float sceneRight = sceneLeft + size[0];
        float sceneBottom = sceneTop + size[1];

        return x + w >= sceneLeft && x <= sceneRight &&
               y + h >= sceneTop && y <= sceneBottom;
    }

    /// <summary>Moves the scene camera by a screen-space delta; true when the position was clamped.</summary>
    public bool Move(int x, int y)
    {
        return camera.Move((float)x, (float)y, true);
    }

    /// <summary>Moves the scene camera by a screen-space delta; true when the position was clamped.</summary>
    public bool Move(float x, float y)
    {
        return camera.Move(x, y, true);
    }

    /// <summary>Smoothly moves the camera to a scene position.</summary>
    public void MoveTo(int x, int y)
    {
        camera.MoveTo((float)x, (float)y, false);
    }

    /// <summary>Converts screen coordinates to scene coordinates in place.</summary>
    public void ScreenToScene(ref float x, ref float y)
    {
        ecGraphics graphics = ecGraphics.Instance();
        x = camera.centerPos[0] + (x - (float)graphics.screenW * 0.5f) / camera.scale;
        y = camera.centerPos[1] + (y - (float)graphics.screenH * 0.5f) / camera.scale;
    }

    /// <summary>Area id under a screen position; 0xFFFF when there is none.</summary>
    public int ScreenToAreaID(float x, float y)
    {
        ScreenToScene(ref x, ref y);
        short areaMarkValue = areaMark.GetMark((int)x, (int)y);
        return (ushort)areaMarkValue;
    }

    /// <summary>Area under a screen position; null when there is none.</summary>
    public CArea ScreenToArea(float x, float y)
    {
        int areaId = ScreenToAreaID(x, y);
        if (areaId == 0xFFFF)
            return null;
        if (areaId < 0 || areaId >= areaList.Count)
            return null;
        return GetArea(areaId);
    }

    /// <summary>True while the camera is moving automatically.</summary>
    public bool IsMoving()
    {
        return camera.isMoving;
    }

    /// <summary>Immediately positions the camera on an area.</summary>
    public void SetCameraToArea(int areaId)
    {
        if (areaId < 0 || areaId >= areaList.Count)
            return;

        CArea area = GetArea(areaId);
        if (area != null)
            camera.SetPos((float)area.info.armyPos[0], (float)area.info.armyPos[1], true);
    }

    /// <summary>Moves the camera when the target area is not visible.</summary>
    public void MoveCameraToArea(int areaId)
    {
        if (areaData == null ||
            areaId < 0 || areaId >= areaList.Count ||
            areaId >= areaCount)
        {
            return;
        }

        CArea area = GetArea(areaId);
        if (area == null)
            return;

        AreaData data = areaData[areaId];
        if (!camera.IsRectInVisibleRegion(
                (float)data.AreaPos[0], (float)data.AreaPos[1],
                (float)data.AreaSize[0], (float)data.AreaSize[1]))
        {
            camera.MoveTo((float)area.info.armyPos[0], (float)area.info.armyPos[1], true);
        }
    }

    /// <summary>Smoothly centres the camera on an area.</summary>
    public void MoveCameraCenterToArea(int areaId)
    {
        if (areaId < 0 || areaId >= areaList.Count)
            return;

        CArea area = GetArea(areaId);
        if (area != null)
            camera.MoveTo((float)area.info.armyPos[0], (float)area.info.armyPos[1], true);
    }

    /// <summary>Moves the camera to the midpoint of two areas when either is not visible.</summary>
    public void MoveCameraBetweenArea(int areaId1, int areaId2)
    {
        if (areaData == null ||
            areaId1 < 0 || areaId2 < 0 ||
            areaId1 >= areaCount || areaId2 >= areaCount ||
            areaId1 >= areaList.Count ||
            areaId2 >= areaList.Count)
        {
            return;
        }

        CArea firstArea = GetArea(areaId1);
        CArea secondArea = GetArea(areaId2);
        if (firstArea == null || secondArea == null)
            return;

        AreaData firstData = areaData[areaId1];
        AreaData secondData = areaData[areaId2];
        bool firstVisible = camera.IsRectInVisibleRegion(
            (float)firstData.AreaPos[0], (float)firstData.AreaPos[1],
            (float)firstData.AreaSize[0], (float)firstData.AreaSize[1]);
        bool secondVisible = camera.IsRectInVisibleRegion(
            (float)secondData.AreaPos[0], (float)secondData.AreaPos[1],
            (float)secondData.AreaSize[0], (float)secondData.AreaSize[1]);

        if (!firstVisible || !secondVisible)
        {
            float centerX = (firstArea.info.armyPos[0] + secondArea.info.armyPos[0]) * 0.5f;
            float centerY = (firstArea.info.armyPos[1] + secondArea.info.armyPos[1]) * 0.5f;
            camera.MoveTo(centerX, centerY, true);
        }
    }

    /// <summary>Squared euclidean distance between the army positions of two areas; 0 when invalid.</summary>
    public float GetTwoAreasDistanceSquare(int areaId1, int areaId2)
    {
        if (areaId1 < 0 || areaId2 < 0 ||
            areaId1 >= areaList.Count ||
            areaId2 >= areaList.Count)
        {
            return 0.0f;
        }

        CArea firstArea = GetArea(areaId1);
        CArea secondArea = GetArea(areaId2);
        if (firstArea == null || secondArea == null)
            return 0.0f;

        float deltaX = firstArea.info.armyPos[0] - secondArea.info.armyPos[0];
        float deltaY = firstArea.info.armyPos[1] - secondArea.info.armyPos[1];
        return deltaX * deltaX + deltaY * deltaY;
    }

    /// <summary>Euclidean distance between the army positions of two areas; 0 when invalid.</summary>
    public float GetTwoAreasDistance(int areaId1, int areaId2)
    {
        float distanceSquare = GetTwoAreasDistanceSquare(areaId1, areaId2);
        return distanceSquare > 0.0f ? MathF.Sqrt(distanceSquare) : distanceSquare;
    }

    /// <summary>
    /// Builds the uniform spatial index used by the circle combat ranges.
    /// Areas are bucketed by their army position and only ids are stored; enable state, country
    /// and units are not part of the index so map changes never require a rebuild.
    /// </summary>
    public void BuildCombatSpatialIndex()
    {
        CombatSpatialIndex index = combatSpatialIndex;
        index.buckets.Clear();
        index.visitMarks.Clear();
        index.minX = 0;
        index.minY = 0;
        index.columns = 0;
        index.rows = 0;
        index.visitToken = 0;

        int validAreaCount = Math.Min(areaCount, areaList.Count);
        if (validAreaCount <= 0)
            return;

        bool hasPosition = false;
        int minBucketX = 0;
        int minBucketY = 0;
        int maxBucketX = 0;
        int maxBucketY = 0;
        for (int areaId = 0; areaId < validAreaCount; ++areaId)
        {
            CArea area = areaList[areaId];
            if (area == null)
                continue;

            // Bucket of this position.
            int bucketX = GetCombatSpatialBucketCoordinate((float)area.info.armyPos[0]);
            int bucketY = GetCombatSpatialBucketCoordinate((float)area.info.armyPos[1]);
            if (!hasPosition)
            {
                // Initialise min and max.
                minBucketX = maxBucketX = bucketX;
                minBucketY = maxBucketY = bucketY;
                hasPosition = true;
                continue;
            }
            minBucketX = Math.Min(minBucketX, bucketX);
            minBucketY = Math.Min(minBucketY, bucketY);
            maxBucketX = Math.Max(maxBucketX, bucketX);
            maxBucketY = Math.Max(maxBucketY, bucketY);
        }

        if (!hasPosition)
            return;

        long columnCount = (long)maxBucketX - (long)minBucketX + 1;
        long rowCount = (long)maxBucketY - (long)minBucketY + 1;
        long bucketCount = columnCount * rowCount;
        // Map coordinates come from resource files and never approach this limit; the guard
        // protects against corrupt map data causing an absurd allocation.
        if (columnCount <= 0 || rowCount <= 0 ||
            bucketCount <= 0 || bucketCount > 1000000)
        {
            return;
        }

        index.minX = minBucketX;
        index.minY = minBucketY;
        index.columns = (int)columnCount;
        index.rows = (int)rowCount;
        index.buckets.Capacity = (int)bucketCount;
        for (long i = 0; i < bucketCount; ++i)
            index.buckets.Add(new List<int>());
        for (int i = 0; i < validAreaCount; ++i)
            index.visitMarks.Add(0);

        // Put the positions into the buckets.
        for (int areaId = 0; areaId < validAreaCount; ++areaId)
        {
            CArea area = areaList[areaId];
            if (area == null)
                continue;

            int bucketX = GetCombatSpatialBucketCoordinate((float)area.info.armyPos[0]);
            int bucketY = GetCombatSpatialBucketCoordinate((float)area.info.armyPos[1]);
            int localX = bucketX - index.minX;
            int localY = bucketY - index.minY;
            int bucketIndex = localY * index.columns + localX;
            index.buckets[bucketIndex].Add(areaId);
        }
    }

    /// <summary>
    /// Areas inside a circular range around a start area. Scans the buckets intersecting the
    /// bounding box of the circle, then filters by squared distance; every area is visited once
    /// thanks to the visit marks.
    /// </summary>
    /// <param name="startAreaId">Start area.</param>
    /// <param name="minDistance">Minimum distance (inclusive).</param>
    /// <param name="maxDistance">Maximum distance.</param>
    /// <param name="maxInclusive">Whether the maximum distance is inclusive.</param>
    /// <returns>Area ids inside the range; enable state is left to the caller.</returns>
    public List<int> GetAreasInCircleRange(int startAreaId, float minDistance, float maxDistance, bool maxInclusive = true)
    {
        CombatSpatialIndex index = combatSpatialIndex;
        int validAreaCount = Math.Min(areaCount, areaList.Count);
        // Reject invalid input.
        if (startAreaId < 0 || startAreaId >= validAreaCount ||
            float.IsNaN(minDistance) || float.IsNaN(maxDistance) ||
            minDistance > maxDistance || maxDistance < 0.0f)
        {
            return new List<int>();
        }

        CArea startArea = areaList[startAreaId];
        if (startArea == null)
            return new List<int>();

        if (index.columns <= 0 ||
            index.rows <= 0 ||
            index.buckets.Count == 0 ||
            index.visitMarks.Count < validAreaCount)
        {
            // Make sure the index exists.
            BuildCombatSpatialIndex();
        }
        if (index.columns <= 0 ||
            index.rows <= 0 ||
            index.buckets.Count == 0)
        {
            return new List<int>();
        }

        minDistance = Math.Max(0.0f, minDistance);
        float maxDistanceSquare = maxDistance * maxDistance;
        float minDistanceSquare = minDistance * minDistance;
        float centerX = startArea.info.armyPos[0];
        float centerY = startArea.info.armyPos[1];

        int firstBucketX;
        int lastBucketX;
        int firstBucketY;
        int lastBucketY;
        if (float.IsFinite(maxDistance) &&
            float.IsFinite(centerX - maxDistance) &&
            float.IsFinite(centerX + maxDistance) &&
            float.IsFinite(centerY - maxDistance) &&
            float.IsFinite(centerY + maxDistance))
        {
            // Candidate buckets.
            firstBucketX = GetCombatSpatialBucketCoordinate(centerX - maxDistance);
            lastBucketX = GetCombatSpatialBucketCoordinate(centerX + maxDistance);
            firstBucketY = GetCombatSpatialBucketCoordinate(centerY - maxDistance);
            lastBucketY = GetCombatSpatialBucketCoordinate(centerY + maxDistance);
        }
        else
        {
            // Infinite range: just walk the existing buckets instead of casting infinity to int.
            firstBucketX = index.minX;
            firstBucketY = index.minY;
            lastBucketX = index.minX + index.columns - 1;
            lastBucketY = index.minY + index.rows - 1;
        }
        int maxBucketX = index.minX + index.columns - 1;
        int maxBucketY = index.minY + index.rows - 1;
        firstBucketX = Math.Max(firstBucketX, index.minX);
        firstBucketY = Math.Max(firstBucketY, index.minY);
        lastBucketX = Math.Min(lastBucketX, maxBucketX);
        lastBucketY = Math.Min(lastBucketY, maxBucketY);
        if (firstBucketX > lastBucketX || firstBucketY > lastBucketY)
            return new List<int>();

        ++index.visitToken;
        if (index.visitToken == 0)
        {
            for (int i = 0; i < index.visitMarks.Count; ++i)
                index.visitMarks[i] = 0;
            index.visitToken = 1;
        }
        uint visitToken = index.visitToken;
        var result = new List<int>();

        // Only walk the areas of these buckets, then apply the exact distance test.
        for (int bucketY = firstBucketY; bucketY <= lastBucketY; ++bucketY)
        {
            for (int bucketX = firstBucketX; bucketX <= lastBucketX; ++bucketX)
            {
                int localX = bucketX - index.minX;
                int localY = bucketY - index.minY;
                int bucketIndex = localY * index.columns + localX;
                foreach (int areaId in index.buckets[bucketIndex])
                {
                    if (areaId < 0 || areaId >= validAreaCount ||
                        index.visitMarks[areaId] == visitToken)
                    {
                        continue;
                    }
                    index.visitMarks[areaId] = visitToken;

                    CArea area = areaList[areaId];
                    if (area == null)
                        continue;
                    float deltaX = area.info.armyPos[0] - centerX;
                    float deltaY = area.info.armyPos[1] - centerY;
                    float distanceSquare = deltaX * deltaX + deltaY * deltaY;
                    if (distanceSquare < minDistanceSquare)
                        continue;
                    if (maxInclusive)
                    {
                        if (distanceSquare > maxDistanceSquare)
                            continue;
                    }
                    else if (distanceSquare >= maxDistanceSquare)
                    {
                        continue;
                    }
                    result.Add(areaId);
                }
            }
        }
        return result;
    }
}
