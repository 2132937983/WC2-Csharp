namespace WC2;

/// <summary>
/// Binary file access for assets (read only) and documents (read/write), with little endian
/// helpers for the save game structures.
/// </summary>
public sealed class ecFile : IDisposable
{
    private FileStream stream;
    public bool isAsset;
    public string path = "";

    public bool IsOpen => stream != null;

    public static bool AssetExists(string name) => !string.IsNullOrEmpty(name) && File.Exists(GetPath(name));
    public static bool DocumentExists(string name) => !string.IsNullOrEmpty(name) && File.Exists(GetDocumentPath(name));

    public static byte[] ReadAllAsset(string name)
    {
        string p = GetPath(name);
        return File.Exists(p) ? File.ReadAllBytes(p) : null;
    }

    public static byte[] ReadAllDocument(string name)
    {
        string p = GetDocumentPath(name);
        return File.Exists(p) ? File.ReadAllBytes(p) : null;
    }

    /// <summary>Opens a file. mode: "rb" read, "wb" create/truncate, "ab" append, "r+b" read/write.</summary>
    public bool Open(string pathFileName, string mode)
    {
        Close();
        if (string.IsNullOrEmpty(pathFileName)) return false;
        path = pathFileName;
        mode ??= "rb";
        try
        {
            if (mode.Contains('w'))
            {
                string dir = Path.GetDirectoryName(pathFileName);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                stream = new FileStream(pathFileName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            }
            else if (mode.Contains('a'))
            {
                string dir = Path.GetDirectoryName(pathFileName);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                stream = new FileStream(pathFileName, FileMode.Append, FileAccess.Write, FileShare.Read);
            }
            else
            {
                if (!File.Exists(pathFileName)) return false;
                var access = mode.Contains('+') ? FileAccess.ReadWrite : FileAccess.Read;
                stream = new FileStream(pathFileName, FileMode.Open, access, FileShare.ReadWrite);
            }
            isAsset = pathFileName.StartsWith(AssetsRoot, StringComparison.OrdinalIgnoreCase);
            return true;
        }
        catch (Exception ex)
        {
            Log("ecFile.Open failed: " + pathFileName + " : " + ex.Message);
            stream = null;
            return false;
        }
    }

    public bool IsFlieExist(string pathFileName) => !string.IsNullOrEmpty(pathFileName) && File.Exists(pathFileName);
    public bool IsFileExist(string pathFileName) => IsFlieExist(pathFileName);

    public void Close()
    {
        stream?.Dispose();
        stream = null;
    }

    public void Dispose() => Close();

    public bool Read(byte[] buffer, uint length) => Read(buffer, 0, (int)length);

    public bool Read(byte[] buffer, int offset, int length)
    {
        if (stream == null || buffer == null) return false;
        int total = 0;
        while (total < length)
        {
            int n = stream.Read(buffer, offset + total, length - total);
            if (n <= 0) return false;
            total += n;
        }
        return true;
    }

    public byte[] ReadBytes(int length)
    {
        var buffer = new byte[Math.Max(0, length)];
        return Read(buffer, 0, buffer.Length) ? buffer : null;
    }

    public bool Write(byte[] buffer, uint length) => Write(buffer, 0, (int)length);

    public bool Write(byte[] buffer, int offset, int length)
    {
        if (stream == null || buffer == null) return false;
        try { stream.Write(buffer, offset, length); return true; }
        catch { return false; }
    }

    public bool Seek(long offset, int origin)
    {
        if (stream == null) return false;
        try
        {
            stream.Seek(offset, origin == 1 ? SeekOrigin.Current : origin == 2 ? SeekOrigin.End : SeekOrigin.Begin);
            return true;
        }
        catch { return false; }
    }

    public long GetCurPos() => stream?.Position ?? 0;
    public long GetSize() => stream?.Length ?? 0;

    // ---- typed helpers (little endian) --------------------------------------------------

    private readonly byte[] scratch = new byte[8];

    public int ReadInt32() => Read(scratch, 0, 4) ? BitConverter.ToInt32(scratch, 0) : 0;
    public uint ReadUInt32() => Read(scratch, 0, 4) ? BitConverter.ToUInt32(scratch, 0) : 0u;
    public short ReadInt16() => Read(scratch, 0, 2) ? BitConverter.ToInt16(scratch, 0) : (short)0;
    public ushort ReadUInt16() => Read(scratch, 0, 2) ? BitConverter.ToUInt16(scratch, 0) : (ushort)0;
    public float ReadFloat() => Read(scratch, 0, 4) ? BitConverter.ToSingle(scratch, 0) : 0f;
    public byte ReadByte() => Read(scratch, 0, 1) ? scratch[0] : (byte)0;
    public bool ReadBool() => ReadByte() != 0;
    /// <summary>Reads a NUL terminated string stored in a fixed size char buffer.</summary>
    public string ReadFixedString(int size)
    {
        var buf = ReadBytes(size);
        return buf == null ? "" : FromFixedBytes(buf, 0, size);
    }

    public bool WriteInt32(int v) => Write(BitConverter.GetBytes(v), 4);
    public bool WriteUInt32(uint v) => Write(BitConverter.GetBytes(v), 4);
    public bool WriteInt16(short v) => Write(BitConverter.GetBytes(v), 2);
    public bool WriteFloat(float v) => Write(BitConverter.GetBytes(v), 4);
    public bool WriteByte(byte v) { scratch[0] = v; return Write(scratch, 1); }
    public bool WriteBool(bool v) => WriteByte(v ? (byte)1 : (byte)0);
    public bool WriteFixedString(string s, int size)
    {
        var buf = new byte[size];
        ToFixedBytes(s, buf, 0, size);
        return Write(buf, (uint)size);
    }
    /// <summary>Writes zero bytes (struct padding).</summary>
    public bool WritePadding(int count)
    {
        if (count <= 0) return true;
        return Write(new byte[count], (uint)count);
    }
    public void SkipBytes(int count) { if (count > 0) Seek(count, 1); }

    public bool CheckJavaDex() => true;
}
