using SkiaSharp;

namespace WC2.Tools;

/// <summary>
/// Screenshot comparison for the regression scripts. newEngine does not have to be pixel identical to
/// the original, but a screen should not change by accident: the actual screenshot is scaled to the
/// size of the reference (references are small JPEG files, 320 pixels wide by default, to keep the
/// repository small), then every pixel whose largest channel difference exceeds <c>--pixel-tolerance</c> counts as
/// changed. The comparison fails when more than <c>--max-percent</c> of the pixels changed.
/// <c>--record</c> stores the actual image (scaled) as the new reference.
/// </summary>
internal static class ImageCompare
{
    public static int Run(Dictionary<string, string> opts)
    {
        string golden = opts["golden"], actual = opts["actual"];
        int pixelTolerance = int.Parse(opts.GetValueOrDefault("pixel-tolerance", "40"));
        double maxPercent = double.Parse(opts.GetValueOrDefault("max-percent", "1.0"), System.Globalization.CultureInfo.InvariantCulture);
        if (!File.Exists(actual)) { Console.WriteLine($"missing screenshot {Path.GetFileName(actual)}"); return 1; }

        using var actualBitmap = SKBitmap.Decode(actual) ?? throw new ToolException("cannot read " + actual);
        if (opts.ContainsKey("record"))
        {
            int w = int.Parse(opts.GetValueOrDefault("width", "320")), h = int.Parse(opts.GetValueOrDefault("height", "0"));
            if (h == 0) h = (int)Math.Round(actualBitmap.Height * (double)w / actualBitmap.Width);
            using var scaled = Scale(actualBitmap, w, h);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(golden))!);
            bool jpeg = golden.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase);
            using var data = scaled.Encode(jpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, jpeg ? 90 : 100);
            using var file = File.Create(golden);
            data.SaveTo(file);
            Console.WriteLine($"recorded {Path.GetFileName(golden)} ({w}x{h})");
            return 0;
        }

        if (!File.Exists(golden)) { Console.WriteLine($"no reference {Path.GetFileName(golden)}"); return 1; }
        using var goldenBitmap = SKBitmap.Decode(golden) ?? throw new ToolException("cannot read " + golden);
        using var compared = Scale(actualBitmap, goldenBitmap.Width, goldenBitmap.Height);
        long changed = 0, total = (long)goldenBitmap.Width * goldenBitmap.Height;
        using var diff = new SKBitmap(goldenBitmap.Width, goldenBitmap.Height);
        for (int y = 0; y < goldenBitmap.Height; y++)
            for (int x = 0; x < goldenBitmap.Width; x++)
            {
                var a = goldenBitmap.GetPixel(x, y);
                var b = compared.GetPixel(x, y);
                int d = Math.Max(Math.Abs(a.Red - b.Red), Math.Max(Math.Abs(a.Green - b.Green), Math.Abs(a.Blue - b.Blue)));
                if (d > pixelTolerance) { changed++; diff.SetPixel(x, y, new SKColor(255, 0, 255)); }
                else { byte g = (byte)((a.Red + a.Green + a.Blue) / 12); diff.SetPixel(x, y, new SKColor(g, g, g)); }
            }
        double percent = 100.0 * changed / Math.Max(1, total);
        if (opts.TryGetValue("diff", out var diffPath) && changed > 0)
        {
            using var data = diff.Encode(SKEncodedImageFormat.Png, 100);
            using var file = File.Create(diffPath);
            data.SaveTo(file);
        }
        bool ok = percent <= maxPercent;
        Console.WriteLine($"{(ok ? "same" : "differs")} {Path.GetFileName(golden)}: {percent:0.###} % of the pixels changed (limit {maxPercent} %)");
        return ok ? 0 : 1;
    }

    private static SKBitmap Scale(SKBitmap source, int w, int h)
    {
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        var result = new SKBitmap(info);
        source.ScalePixels(result, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        return result;
    }
}
