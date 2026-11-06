namespace WC2;

/// <summary>Software decoder for ETC1 compressed textures stored in PKM files.</summary>
public static class Etc1Decoder
{
    private static readonly int[,] Modifiers =
    {
        { 2, 8, -2, -8 },
        { 5, 17, -5, -17 },
        { 9, 29, -9, -29 },
        { 13, 42, -13, -42 },
        { 18, 60, -18, -60 },
        { 24, 80, -24, -80 },
        { 33, 106, -33, -106 },
        { 47, 183, -47, -183 },
    };

    /// <summary>Returns true when the data starts with a PKM header.</summary>
    public static bool IsPkm(byte[] data) => data != null && data.Length >= 16 && data[0] == (byte)'P' && data[1] == (byte)'K' && data[2] == (byte)'M';

    /// <summary>
    /// Decodes a PKM (ETC1) file to RGBA8 pixels. Returns null if the data is not a PKM file.
    /// </summary>
    public static byte[] DecodePkm(byte[] data, out int width, out int height)
    {
        width = height = 0;
        if (!IsPkm(data)) return null;
        int extW = (data[8] << 8) | data[9];
        int extH = (data[10] << 8) | data[11];
        int w = (data[12] << 8) | data[13];
        int h = (data[14] << 8) | data[15];
        if (w <= 0 || h <= 0) { w = extW; h = extH; }
        if (extW < w) extW = (w + 3) & ~3;
        if (extH < h) extH = (h + 3) & ~3;
        width = w; height = h;
        var rgba = new byte[w * h * 4];
        DecodeEtc1Blocks(data, 16, extW, extH, rgba, w, h);
        return rgba;
    }

    private static void DecodeEtc1Blocks(byte[] data, int offset, int extW, int extH, byte[] rgba, int w, int h)
    {
        int blocksX = extW / 4;
        int blocksY = extH / 4;
        var block = new byte[16 * 3];
        int pos = offset;
        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                if (pos + 8 > data.Length) return;
                DecodeBlock(data, pos, block);
                pos += 8;
                for (int py = 0; py < 4; py++)
                {
                    int y = by * 4 + py;
                    if (y >= h) continue;
                    for (int px = 0; px < 4; px++)
                    {
                        int x = bx * 4 + px;
                        if (x >= w) continue;
                        int src = (py * 4 + px) * 3;
                        int dst = (y * w + x) * 4;
                        rgba[dst] = block[src];
                        rgba[dst + 1] = block[src + 1];
                        rgba[dst + 2] = block[src + 2];
                        rgba[dst + 3] = 255;
                    }
                }
            }
        }
    }

    private static int SignExtend3(int v) => (v & 4) != 0 ? v - 8 : v;
    private static int Expand5(int v) => (v << 3) | (v >> 2);
    private static int Expand4(int v) => v * 17;
    private static byte Clamp(int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);

    /// <summary>Decodes one 8 byte block into 16 RGB triplets (row major, 4x4).</summary>
    private static void DecodeBlock(byte[] data, int o, byte[] outRgb)
    {
        uint high = ((uint)data[o] << 24) | ((uint)data[o + 1] << 16) | ((uint)data[o + 2] << 8) | data[o + 3];
        uint low = ((uint)data[o + 4] << 24) | ((uint)data[o + 5] << 16) | ((uint)data[o + 6] << 8) | data[o + 7];
        bool diff = ((high >> 1) & 1) != 0;
        bool flip = (high & 1) != 0;

        int r1, g1, b1, r2, g2, b2;
        if (!diff)
        {
            r1 = Expand4((int)(high >> 28) & 0xF); r2 = Expand4((int)(high >> 24) & 0xF);
            g1 = Expand4((int)(high >> 20) & 0xF); g2 = Expand4((int)(high >> 16) & 0xF);
            b1 = Expand4((int)(high >> 12) & 0xF); b2 = Expand4((int)(high >> 8) & 0xF);
        }
        else
        {
            int r5 = (int)(high >> 27) & 0x1F, dr = SignExtend3((int)(high >> 24) & 7);
            int g5 = (int)(high >> 19) & 0x1F, dg = SignExtend3((int)(high >> 16) & 7);
            int b5 = (int)(high >> 11) & 0x1F, db = SignExtend3((int)(high >> 8) & 7);
            r1 = Expand5(r5); g1 = Expand5(g5); b1 = Expand5(b5);
            r2 = Expand5(Math.Clamp(r5 + dr, 0, 31)); g2 = Expand5(Math.Clamp(g5 + dg, 0, 31)); b2 = Expand5(Math.Clamp(b5 + db, 0, 31));
        }
        int table1 = (int)(high >> 5) & 7;
        int table2 = (int)(high >> 2) & 7;

        for (int i = 0; i < 16; i++)
        {
            int x = i / 4;   // column
            int y = i % 4;   // row
            int msb = (int)(low >> (i + 16)) & 1;
            int lsb = (int)(low >> i) & 1;
            int idx = (msb << 1) | lsb;
            bool second = flip ? y >= 2 : x >= 2;
            int mod = Modifiers[second ? table2 : table1, idx];
            int br = second ? r2 : r1, bg = second ? g2 : g1, bb = second ? b2 : b1;
            int dst = (y * 4 + x) * 3;
            outRgb[dst] = Clamp(br + mod);
            outRgb[dst + 1] = Clamp(bg + mod);
            outRgb[dst + 2] = Clamp(bb + mod);
        }
    }
}
