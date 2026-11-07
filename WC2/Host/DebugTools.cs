namespace WC2;

/// <summary>Command line helpers for validating data loaders without starting the game window.</summary>
public static class DebugTools
{
    /// <summary>Returns true when a debug command handled the arguments (the game should not start).</summary>
    public static bool TryRun(string[] args)
    {
        if (args.Length == 0) return false;
        switch (args[0])
        {
            case "--decode-pkm" when args.Length >= 3:
                DecodePkm(args[1], args[2]);
                return true;
            case "--test-xml":
                TestXml(args.Skip(1).ToArray());
                return true;
            case "--test-fonts":
                TestFonts();
                return true;
            case "--test-audio":
                TestAudio(args.Length > 1 ? args[1] : null);
                return true;
            case "--test-input":
                InputSelfTest.Run();
                return true;
            default:
                return false;
        }
    }

    private static void DecodePkm(string input, string output)
    {
        var data = File.ReadAllBytes(input);
        var rgba = Etc1Decoder.DecodePkm(data, out int w, out int h);
        if (rgba == null) { Console.WriteLine("not a PKM file"); return; }
        using var bmp = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul));
        System.Runtime.InteropServices.Marshal.Copy(rgba, 0, bmp.GetPixels(), rgba.Length);
        using var img = SkiaSharp.SKImage.FromBitmap(bmp);
        using var png = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
        using var fs = File.Create(output);
        png.SaveTo(fs);
        Console.WriteLine($"decoded {w}x{h} -> {output}");
    }

    private static void TestXml(string[] files)
    {
        ecGame.ConfigurePaths(null, null);
        if (files.Length == 0)
            files = new[] { "carddef.xml", "mui_hd.xml", "stringtable_cn.xml", "conquest_1.xml", "armydef.xml", "battlelist.xml", "commanderdef.xml", "areatax1.xml", "Localizable.strings", "effect_exp.xml", "resolutionColumns.xml", "unitposdef.xml" };
        foreach (var f in files)
        {
            var doc = new TiXmlDocument(GetPath(f));
            bool ok = doc.LoadFile();
            var root = doc.RootElement();
            int count = 0;
            if (root != null) foreach (var _ in root.ChildElements()) count++;
            Console.WriteLine($"{f,-28} load={ok} root=<{root?.Value()}> children={count} {(ok ? "" : doc.ErrorDesc())}");
        }
        // targeted checks
        var cards = new TiXmlDocument(GetPath("carddef.xml"));
        if (cards.LoadFile())
        {
            int n = 0;
            for (var c = cards.FirstChildElement("cards")?.FirstChildElement("country"); c != null; c = c.NextSiblingElement("country"))
                for (var card = c.FirstChildElement("card"); card != null; card = card.NextSiblingElement("card"))
                {
                    n++;
                    if (card.Attribute("name") == "Rocket") Console.WriteLine("Rocket card image attr = " + card.Attribute("image") + " relatedArmyId=" + card.Attribute("relatedArmyId"));
                }
            Console.WriteLine("cards parsed: " + n);
        }
        var table = new ecStringTable();
        table.Load("stringtable_cn.xml");
        Console.WriteLine("stringtable_cn entries: " + table.table.Count + "  Infantry=" + table.GetString("Infantry") + "  conquest name 1=" + table.GetString("conquest name 1"));
        var loc = new ecStringTable();
        loc.Load("Localizable.strings");
        Console.WriteLine("Localizable language=" + loc.GetString("language") + " stringtable=" + loc.GetString("stringtable"));
    }

    /// <summary>
    /// Decodes every MP3 of the assets with the music decoder used by the game, prints format and
    /// level statistics and (optionally) writes the PCM as WAV files into outDir for inspection.
    /// </summary>
    private static void TestAudio(string outDir)
    {
        ecGame.ConfigurePaths(null, null);
        if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
        foreach (var path in Directory.GetFiles(AssetsRoot, "*.mp3").OrderBy(p => p))
        {
            try
            {
                var pcm = CCSoundBox.DecodeMp3ToPcm(path, out int sampleRate, out int channels, out string info);
                int samples = pcm.Length / 2;
                double sum = 0; short peak = 0; int clipped = 0; double sameAdjacent = 0;
                short prev = 0;
                for (int i = 0; i < samples; i++)
                {
                    short s = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
                    int a = Math.Abs((int)s);
                    if (a > peak) peak = (short)Math.Min(a, short.MaxValue);
                    if (a >= 32700) clipped++;
                    sum += (double)s * s;
                    if (i > 0 && s == prev) sameAdjacent++;
                    prev = s;
                }
                double rms = samples > 0 ? Math.Sqrt(sum / samples) : 0;
                double seconds = sampleRate > 0 && channels > 0 ? samples / (double)(sampleRate * channels) : 0;
                Console.WriteLine($"{Path.GetFileName(path),-18} {info}  decoded: rate={sampleRate} ch={channels} samples={samples} ({seconds:F1}s) peak={peak} rms={rms:F0} clipped={clipped} sameAdjacent={sameAdjacent / Math.Max(1, samples):P1}");
                if (!string.IsNullOrEmpty(outDir))
                {
                    string outPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(path) + ".nlayer.wav");
                    WriteWav(outPath, pcm, sampleRate, channels);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{Path.GetFileName(path),-18} FAILED: {ex.Message}");
            }
        }

        // sound effects: load every WAV through MonoGame and compare the duration with the header
        int wavOk = 0, wavBad = 0;
        foreach (var path in Directory.GetFiles(AssetsRoot, "*.wav").OrderBy(p => p))
        {
            try
            {
                var header = ReadWavHeader(path);
                using var fs = File.OpenRead(path);
                using var effect = Microsoft.Xna.Framework.Audio.SoundEffect.FromStream(fs);
                double loaded = effect.Duration.TotalSeconds;
                bool ok = Math.Abs(loaded - header.seconds) < 0.05;
                if (ok) wavOk++; else wavBad++;
                Console.WriteLine($"{Path.GetFileName(path),-22} {header.rate} Hz {header.bits}-bit ch={header.channels} header={header.seconds:F2}s loaded={loaded:F2}s {(ok ? "ok" : "MISMATCH")}");
            }
            catch (Exception ex)
            {
                wavBad++;
                Console.WriteLine($"{Path.GetFileName(path),-22} FAILED: {ex.Message}");
            }
        }
        Console.WriteLine($"wav effects: {wavOk} ok, {wavBad} bad");
    }

    private static (int rate, int bits, int channels, double seconds) ReadWavHeader(string path)
    {
        var d = File.ReadAllBytes(path);
        int i = 12, rate = 0, bits = 0, channels = 0, dataSize = 0;
        while (i + 8 <= d.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(d, i, 4);
            int size = BitConverter.ToInt32(d, i + 4);
            if (id == "fmt ") { channels = BitConverter.ToInt16(d, i + 10); rate = BitConverter.ToInt32(d, i + 12); bits = BitConverter.ToInt16(d, i + 22); }
            else if (id == "data") { dataSize = Math.Min(size, d.Length - i - 8); }
            i += 8 + size + (size & 1);
        }
        double seconds = rate > 0 && bits > 0 && channels > 0 ? dataSize / (double)(rate * channels * bits / 8) : 0;
        return (rate, bits, channels, seconds);
    }

    private static void WriteWav(string path, byte[] pcm, int sampleRate, int channels)
    {
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        int byteRate = sampleRate * channels * 2;
        w.Write("RIFF"u8); w.Write(36 + pcm.Length); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)channels); w.Write(sampleRate); w.Write(byteRate); w.Write((short)(channels * 2)); w.Write((short)16);
        w.Write("data"u8); w.Write(pcm.Length); w.Write(pcm);
    }

    private static void TestFonts()
    {
        ecGame.ConfigurePaths(null, null);
        foreach (var f in new[] { "font1.fnt", "font2_cn_hd.fnt", "fontht_cn_hd.fnt", "num3_hd.fnt" })
        {
            var data = File.ReadAllBytes(GetPath(f));
            uint glyphs = BitConverter.ToUInt32(data, 0), height = BitConverter.ToUInt32(data, 4);
            Console.WriteLine($"{f}: glyphs={glyphs} height={height} size={data.Length} expected={8 + glyphs * 12}");
        }
    }
}
