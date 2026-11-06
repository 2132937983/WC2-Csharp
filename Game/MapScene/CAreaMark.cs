using System.Buffers.Binary;

namespace WC2;

/// <summary>Area id lookup bitmap (areamark&lt;mapId&gt;.raw) (CAreaMark.cpp).</summary>
public sealed class CAreaMark
{
    public int[] size = new int[2];
    public short[] mark;

    /// <summary>Constructs the area mark data object.</summary>
    public CAreaMark()
    {
        mark = null;
    }

    /// <summary>Releases the loaded mark array.</summary>
    public void Release()
    {
        if (mark != null)
            mark = null;
    }

    /// <summary>
    /// Loads the coordinate to area id mapping of a map. The file starts with the width and the
    /// height of the mark bitmap (two ints), followed by one 16 bit area id per pixel.
    /// </summary>
    /// <param name="mapId">Map id.</param>
    public void Init(int mapId)
    {
        Release();

        string fileName = Sprintf("areamark%d.raw", mapId);

        var file = new ecFile();
        string path = GetPath(fileName, null);
        if (file.Open(path, "rb"))
        {
            size[0] = file.ReadInt32();
            size[1] = file.ReadInt32();

            uint markCount = (uint)size[0] * (uint)size[1];
            mark = new short[markCount];
            byte[] raw = file.ReadBytes((int)(2 * markCount));
            if (raw != null)
            {
                for (int i = 0; i < mark.Length; ++i)
                    mark[i] = BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(i * 2, 2));
            }
            file.Close();
        }
    }

    /// <summary>
    /// Area id under a scene position. Every mark pixel covers an 8x8 scene block, so the scene
    /// coordinates are shifted right by three. Returns -1 when out of range or not loaded.
    /// </summary>
    public short GetMark(int x, int y)
    {
        int markX = x >> 3;
        if (markX < 0 || markX >= size[0])
            return -1;

        int markY = y >> 3;
        if (markY < 0 || markY >= size[1] || mark == null)
            return -1;

        return mark[markX + size[0] * markY];
    }
}
