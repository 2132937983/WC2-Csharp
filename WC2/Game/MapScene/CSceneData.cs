using System.Buffers.Binary;

namespace WC2;

/// <summary>Area data files and area containers (CSceneData.cpp).</summary>
public sealed partial class CScene
{
    /// <summary>Sets the owner country of an area (null clears the ownership).</summary>
    public void SetAreaCountry(int areaId, CCountry countryIn)
    {
        GetArea(areaId).country = countryIn;
    }

    /// <summary>
    /// Loads the area position data (area&lt;mapId&gt;.bin: int count, then count records of 44 bytes:
    /// AreaPos[2], AreaSize[2], ArmyPos[2], ConstructPos[2], InstallationPos[2], Type).
    /// </summary>
    public void LoadAreaData(int mapId)
    {
        const uint kAreaDataBytes = 44;
        const uint kMaxAreaCount = 0x2E80000;

        areaData = null;
        areaCount = 0;

        string fileName = Sprintf("area%d.bin", mapId);

        var file = new ecFile();
        if (!file.Open(GetPath(fileName, null), "rb"))
            return;

        byte[] header = file.ReadBytes(4);
        if (header == null)
        {
            file.Close();
            return;
        }
        uint storedAreaCount = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (storedAreaCount > kMaxAreaCount)
        {
            file.Close();
            return;
        }

        byte[] raw = file.ReadBytes((int)(storedAreaCount * kAreaDataBytes));
        if (raw == null)
        {
            file.Close();
            return;
        }
        file.Close();

        var loadedAreaData = new AreaData[storedAreaCount];
        int offset = 0;
        for (uint i = 0; i < storedAreaCount; ++i)
        {
            var data = new AreaData();
            data.AreaPos[0] = ReadInt32LE(raw, ref offset);
            data.AreaPos[1] = ReadInt32LE(raw, ref offset);
            data.AreaSize[0] = ReadInt32LE(raw, ref offset);
            data.AreaSize[1] = ReadInt32LE(raw, ref offset);
            data.ArmyPos[0] = ReadInt32LE(raw, ref offset);
            data.ArmyPos[1] = ReadInt32LE(raw, ref offset);
            data.ConstructPos[0] = ReadInt32LE(raw, ref offset);
            data.ConstructPos[1] = ReadInt32LE(raw, ref offset);
            data.InstallationPos[0] = ReadInt32LE(raw, ref offset);
            data.InstallationPos[1] = ReadInt32LE(raw, ref offset);
            data.Type = (TERRAIN_TYPE)ReadInt32LE(raw, ref offset);
            loadedAreaData[i] = data;
        }

        areaData = loadedAreaData;
        areaCount = (int)storedAreaCount;
    }

    private static int ReadInt32LE(byte[] raw, ref int offset)
    {
        int value = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(offset, 4));
        offset += 4;
        return value;
    }

    /// <summary>Saves the area position data (area&lt;mapId&gt;.bin).</summary>
    public void SaveAreaData(int mapId)
    {
        if (areaData == null ||
            areaCount < 0 ||
            areaList.Count < areaCount)
        {
            return;
        }

        for (int areaId = 0; areaId < areaCount; ++areaId)
        {
            CArea area = areaList[areaId];
            if (area == null)
                return;

            AreaData data = areaData[areaId];
            data.ArmyPos[0] = area.info.armyPos[0];
            data.ArmyPos[1] = area.info.armyPos[1];
            data.ConstructPos[0] = area.info.constructionPos[0];
            data.ConstructPos[1] = area.info.constructionPos[1];
            data.InstallationPos[0] = area.info.installationPos[0];
            data.InstallationPos[1] = area.info.installationPos[1];
            data.Type = area.info.sea ? WATER_SEA : LAND_PLAIN;
        }

        string fileName = Sprintf("area%d.bin", mapId);

        var file = new ecFile();
        if (!file.Open(GetPath(fileName, null), "wb"))
            return;

        file.WriteInt32(areaCount);
        for (int areaId = 0; areaId < areaCount; ++areaId)
        {
            AreaData data = areaData[areaId];
            file.WriteInt32(data.AreaPos[0]);
            file.WriteInt32(data.AreaPos[1]);
            file.WriteInt32(data.AreaSize[0]);
            file.WriteInt32(data.AreaSize[1]);
            file.WriteInt32(data.ArmyPos[0]);
            file.WriteInt32(data.ArmyPos[1]);
            file.WriteInt32(data.ConstructPos[0]);
            file.WriteInt32(data.ConstructPos[1]);
            file.WriteInt32(data.InstallationPos[0]);
            file.WriteInt32(data.InstallationPos[1]);
            file.WriteInt32((int)data.Type);
        }
        file.Close();
    }

    /// <summary>Releases and clears every area object.</summary>
    public void ClearAreas()
    {
        areaList.Clear();
    }

    /// <summary>Saves the area types and taxes as XML (areatax&lt;mapId&gt;.xml).</summary>
    public void SaveAreaTax(int mapId)
    {
        string fileName = Sprintf("areatax%d.xml", mapId);

        var document = new TiXmlDocument(GetPath(fileName, null));
        var areasElement = new TiXmlElement("areas");

        for (int areaId = 0; areaId < areaList.Count; ++areaId)
        {
            CArea area = areaList[areaId];
            if (area == null)
                continue;

            var areaElement = new TiXmlElement("area");
            areaElement.SetAttribute("id", areaId);

            string areaTypeName;
            switch (area.info.areaType)
            {
                case CAPITAL:
                    areaTypeName = "capital";
                    break;
                case PORT:
                    areaTypeName = "port";
                    break;
                case LARGE_CITY:
                    areaTypeName = "large city";
                    break;
                case NORMAL_CITY:
                    areaTypeName = "normal city";
                    break;
                default:
                    areaTypeName = "normal";
                    break;
            }

            areaElement.SetAttribute("type", areaTypeName);
            areaElement.SetAttribute("tax", area.info.tax);
            areasElement.LinkEndChild(areaElement);
        }

        document.LinkEndChild(areasElement);
        document.SaveFile();
    }

    /// <summary>Loads the enable state of every area (int count, then one byte per area).</summary>
    /// <param name="areasEnable">Resource file holding the area enable states.</param>
    public void LoadAreaEnable(string areasEnable)
    {
        if (areasEnable == null)
            return;

        var file = new ecFile();
        if (!file.Open(GetPath(areasEnable, null), "rb"))
            return;

        byte[] header = file.ReadBytes(4);
        int storedAreaCount = header != null ? BinaryPrimitives.ReadInt32LittleEndian(header) : 0;
        if (header == null || storedAreaCount <= 0)
        {
            file.Close();
            return;
        }

        int applicableAreaCount = Math.Min(Math.Min(storedAreaCount, areaCount), areaList.Count);
        if (applicableAreaCount <= 0)
        {
            file.Close();
            return;
        }

        byte[] enabledStates = file.ReadBytes(applicableAreaCount);
        if (enabledStates == null)
        {
            file.Close();
            return;
        }
        file.Close();

        for (int areaId = 0; areaId < applicableAreaCount; ++areaId)
        {
            CArea area = areaList[areaId];
            if (area != null)
                area.info.enable = enabledStates[areaId] != 0;
        }
    }

    /// <summary>Empty in the original library (map editor leftover).</summary>
    public void SaveAreaEnable(string areasEnableIn) { }

    /// <summary>Disables every area outside the given rectangle.</summary>
    public void AreaDisableOutofRect(int x, int y, int w, int h)
    {
        if (areaData == null)
            return;

        int right = x + w;
        int bottom = y + h;
        int validAreaCount = Math.Min(areaCount, areaList.Count);

        for (int areaId = 0; areaId < validAreaCount; ++areaId)
        {
            CArea area = areaList[areaId];
            if (area == null)
                continue;

            AreaData data = areaData[areaId];
            int areaRight = data.AreaPos[0] + data.AreaSize[0];
            int areaBottom = data.AreaPos[1] + data.AreaSize[1];
            if (areaRight < x || data.AreaPos[0] > right ||
                areaBottom < y || data.AreaPos[1] > bottom)
            {
                area.info.enable = false;
            }
        }
    }

    /// <summary>Number of areas in the scene.</summary>
    public int GetNumAreas()
    {
        return areaList.Count;
    }

    /// <summary>Area with the given id; the caller guarantees the id is valid.</summary>
    public CArea GetArea(int areaId)
    {
        return areaList[areaId];
    }
}
