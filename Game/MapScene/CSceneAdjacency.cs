using System.Buffers.Binary;

namespace WC2;

/// <summary>Adjacency tables of the map (CSceneAdjacency.cpp).</summary>
public sealed partial class CScene
{
    /// <summary>
    /// Values per area in the adjacency tables: the neighbour count, then up to adjoinStride - 1
    /// neighbours. 17 in the original's format (16 neighbours); a generated map with more neighbours
    /// (thousands of small provinces along a coast) stores its own stride (adjion version 2).
    /// </summary>
    public int adjoinStride = 17;

    /// <summary>Legacy adjacency data check placeholder.</summary>
    public void CheckAdjacentData()
    {
    }

    /// <summary>Creates and clears the raw adjacency table.</summary>
    public void NewAdjoin()
    {
        int kAdjoinValuesPerArea = adjoinStride;
        adjoinRaw = null;
        if (areaCount <= 0)
            return;
        adjoinRaw = new int[areaCount * kAdjoinValuesPerArea];
    }

    /// <summary>
    /// Loads the map adjacency data: adjion&lt;mapId&gt;.bin, the original's format (int count, then
    /// count * 17 ints: neighbour count and 16 neighbours) or version 2 ("WCAJ", version, count, the
    /// most neighbours per area, then count * (1 + most) ints).
    /// </summary>
    public void LoadAdjoin(int mapId)
    {
        const uint kMaxAreaCount = 0x1E00000;
        adjoinStride = 17;
        string fileName = Sprintf("adjion%d.bin", mapId);

        var file = new ecFile();
        if (!file.Open(GetPath(fileName, null), "rb"))
            return;

        byte[] header = file.ReadBytes(4);
        if (header == null)
        {
            file.Close();
            return;
        }
        int stride = 17;
        if (header[0] == 'W' && header[1] == 'C' && header[2] == 'A' && header[3] == 'J')
        {
            byte[] v2 = file.ReadBytes(12);
            if (v2 == null || BinaryPrimitives.ReadInt32LittleEndian(v2) != 2) { file.Close(); return; }
            header = v2.AsSpan(4, 4).ToArray();
            stride = 1 + BinaryPrimitives.ReadInt32LittleEndian(v2.AsSpan(8, 4));
            if (stride < 2 || stride > 1025) { file.Close(); return; }
        }
        uint storedAreaCount = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (storedAreaCount > kMaxAreaCount)
        {
            file.Close();
            return;
        }

        byte[] raw = file.ReadBytes((int)(storedAreaCount * stride * 4));
        if (raw == null)
        {
            file.Close();
            return;
        }
        file.Close();

        int[] loadedAdjoinData = new int[storedAreaCount * stride];
        for (int i = 0; i < loadedAdjoinData.Length; ++i)
            loadedAdjoinData[i] = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(i * 4, 4));

        adjoinStride = stride;
        adjoinRaw = loadedAdjoinData;
    }

    /// <summary>Saves the map adjacency data (adjion&lt;mapId&gt;.bin).</summary>
    public void SaveAdjoin(int mapId)
    {
        int kAdjoinValuesPerArea = adjoinStride;

        if (adjoinRaw == null || areaCount <= 0)
            return;

        string fileName = Sprintf("adjion%d.bin", mapId);

        var file = new ecFile();
        if (!file.Open(GetPath(fileName, null), "wb"))
            return;

        int storedAreaCount = areaCount;
        if (kAdjoinValuesPerArea != 17)
        {
            // version 2 (more than 16 neighbours per area)
            file.Write(new[] { (byte)'W', (byte)'C', (byte)'A', (byte)'J' }, 4u);
            file.WriteInt32(2);
            file.WriteInt32(storedAreaCount);
            file.WriteInt32(kAdjoinValuesPerArea - 1);
        }
        else file.WriteInt32(storedAreaCount);
        int valueCount = storedAreaCount * kAdjoinValuesPerArea;
        for (int i = 0; i < valueCount; ++i)
            file.WriteInt32(i < adjoinRaw.Length ? adjoinRaw[i] : 0);
        file.Close();
    }

    /// <summary>Builds the runtime adjacency table from the enable states.</summary>
    /// <param name="mapId">Map id whose runtime adjacency table is built.</param>
    /// <param name="areasEnable">Optional area enable file name.</param>
    public void InitAreas(int mapId, string areasEnable)
    {
        int kAdjoinValuesPerArea = adjoinStride;
        int kMaxAdjacentAreas = adjoinStride - 1;

        LoadAreaTax(mapId);
        if (areasEnable != null)
            LoadAreaEnable(areasEnable);

        adjoin = null;
        if (areaCount <= 0)
            return;

        adjoin = new int[areaCount * kAdjoinValuesPerArea];

        if (adjoinRaw == null)
            return;

        int validAreaCount = Math.Min(areaCount, areaList.Count);
        for (int areaId = 0; areaId < validAreaCount; ++areaId)
        {
            CArea area = areaList[areaId];
            if (area == null || !area.info.enable)
                continue;

            int rawBase = kAdjoinValuesPerArea * areaId;
            if (rawBase >= adjoinRaw.Length)
                break;
            int rawAdjacentCount = Math.Min(adjoinRaw[rawBase], kMaxAdjacentAreas);
            int enabledAdjacentCount = 0;

            for (int index = 0; index < rawAdjacentCount; ++index)
            {
                int adjacentAreaId = adjoinRaw[rawBase + index + 1];
                if (adjacentAreaId < 0 || adjacentAreaId >= validAreaCount)
                    continue;

                CArea adjacentArea = areaList[adjacentAreaId];
                if (adjacentArea != null && adjacentArea.info.enable)
                {
                    adjoin[kAdjoinValuesPerArea * areaId + 1 + enabledAdjacentCount] = adjacentAreaId;
                    ++enabledAdjacentCount;
                }
            }

            adjoin[kAdjoinValuesPerArea * areaId] = enabledAdjacentCount;
        }
    }

    /// <summary>Creates a bidirectional adjacency between two areas.</summary>
    public void MakeAdjoin(int areaId1, int areaId2)
    {
        ClearDistanceCache();
        int kAdjoinValuesPerArea = adjoinStride;
        int kMaxAdjacentAreas = adjoinStride - 1;

        if (adjoinRaw == null ||
            areaId1 < 0 || areaId2 < 0 ||
            areaId1 >= areaCount || areaId2 >= areaCount)
        {
            return;
        }

        int firstBase = kAdjoinValuesPerArea * areaId1;
        int secondBase = kAdjoinValuesPerArea * areaId2;
        int firstCount = adjoinRaw[firstBase];
        int secondCount = adjoinRaw[secondBase];

        if (firstCount >= kMaxAdjacentAreas || secondCount >= kMaxAdjacentAreas)
            return;

        bool hasSecondInFirst = false;
        for (int i = 0; i < firstCount; ++i)
        {
            if (adjoinRaw[firstBase + i + 1] == areaId2)
            {
                hasSecondInFirst = true;
                break;
            }
        }
        if (!hasSecondInFirst)
        {
            adjoinRaw[firstBase + firstCount + 1] = areaId2;
            adjoinRaw[firstBase] = firstCount + 1;
        }

        for (int i = 0; i < secondCount; ++i)
        {
            if (adjoinRaw[secondBase + i + 1] == areaId1)
                return;
        }
        adjoinRaw[secondBase + secondCount + 1] = areaId1;
        adjoinRaw[secondBase] = secondCount + 1;
    }

    /// <summary>Removes the bidirectional adjacency between two areas.</summary>
    public void DelAdjoin(int areaId1, int areaId2)
    {
        ClearDistanceCache();
        if (adjoinRaw == null ||
            areaId1 < 0 || areaId2 < 0 ||
            areaId1 >= areaCount || areaId2 >= areaCount)
        {
            return;
        }

        void RemoveOneDirection(int sourceId, int targetId)
        {
            int kValuesPerArea = adjoinStride;
            int dataBase = kValuesPerArea * sourceId;
            int count = adjoinRaw[dataBase];

            for (int i = 0; i < count; ++i)
            {
                if (adjoinRaw[dataBase + i + 1] != targetId)
                    continue;

                for (int j = i + 1; j < count; ++j)
                    adjoinRaw[dataBase + j] = adjoinRaw[dataBase + j + 1];
                adjoinRaw[dataBase] = count - 1;
                adjoinRaw[dataBase + count] = 0;
                return;
            }
        }

        RemoveOneDirection(areaId1, areaId2);
        RemoveOneDirection(areaId2, areaId1);
    }

    /// <summary>Checks whether two areas are adjacent in the raw adjacency table.</summary>
    public bool IsAdjoin(int areaId1, int areaId2)
    {
        int kAdjoinValuesPerArea = adjoinStride;

        if (adjoinRaw == null ||
            areaId1 < 0 || areaId2 < 0 ||
            areaId1 >= areaCount || areaId2 >= areaCount)
        {
            return false;
        }

        int dataBase = kAdjoinValuesPerArea * areaId1;
        int count = adjoinRaw[dataBase];
        for (int i = 0; i < count; ++i)
        {
            if (adjoinRaw[dataBase + i + 1] == areaId2)
                return true;
        }

        return false;
    }

    /// <summary>Number of adjacent areas of an area in the runtime adjacency table.</summary>
    public int GetNumAdjacentAreas(int areaId)
    {
        int kAdjoinValuesPerArea = adjoinStride;
        if (adjoin == null || areaId < 0 || areaId >= areaCount)
            return 0;
        return adjoin[kAdjoinValuesPerArea * areaId];
    }

    /// <summary>Area id at the given position of the adjacency list of an area; -1 when invalid.</summary>
    public int GetAdjacentAreaID(int areaId, int index)
    {
        int kAdjoinValuesPerArea = adjoinStride;
        int kMaxAdjacentAreas = adjoinStride - 1;
        if (adjoin == null ||
            areaId < 0 || areaId >= areaCount ||
            index < 0 || index >= kMaxAdjacentAreas)
        {
            return -1;
        }

        int adjacentCount = adjoin[kAdjoinValuesPerArea * areaId];
        if (index >= adjacentCount)
            return -1;

        return adjoin[kAdjoinValuesPerArea * areaId + 1 + index];
    }

    /// <summary>Area at the given position of the adjacency list of an area; null when invalid.</summary>
    public CArea GetAdjacentArea(int areaId, int index)
    {
        int adjacentAreaId = GetAdjacentAreaID(areaId, index);
        if (adjacentAreaId < 0 || adjacentAreaId >= areaList.Count)
            return null;
        return GetArea(adjacentAreaId);
    }

    /// <summary>Checks whether two areas are directly adjacent (runtime table).</summary>
    public bool CheckAdjacent(int areaId1, int areaId2)
    {
        if (adjoin == null || areaId1 < 0 || areaId1 >= areaCount)
            return false;

        int kAdjoinValuesPerArea = adjoinStride;
        int baseIndex = kAdjoinValuesPerArea * areaId1;
        int adjacentCount = adjoin[baseIndex];
        for (int index = 0; index < adjacentCount; ++index)
        {
            if (adjoin[baseIndex + 1 + index] == areaId2)
                return true;
        }
        return false;
    }

    /// <summary>Recomputes the encirclement state of every area.</summary>
    public void AllAreasEncirclement()
    {
        int AreaCount = GetNumAreas();
        for (int areaId = 0; areaId < AreaCount; ++areaId)
        {
            CArea area = GetArea(areaId);
            if (area != null)
                area.Encirclement();
        }
    }

    /// <summary>
    /// Re-evaluates the encirclement state of an area and of all its neighbours
    /// (defined in basicElements/CArea.cpp in the C++).
    /// </summary>
    /// <param name="AreaID">Area id.</param>
    public void AdjacentAreasEncirclement(int AreaID)
    {
        CArea area = GetArea(AreaID);
        area.Encirclement();
        int Num = GetNumAdjacentAreas(AreaID);
        // Loop over every area connected to this one.
        for (int i = 0; i < Num; ++i)
        {
            CArea temArea = GetAdjacentArea(AreaID, i);
            if (temArea != null)
                temArea.Encirclement();
        }
    }
}
