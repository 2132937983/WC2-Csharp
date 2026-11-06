using System.IO.Compression;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace WC2.Tools;

/// <summary>
/// Runtime formats generated from the packaged sources. A generated file goes to
/// <c>generated/&lt;source path without extension&gt;.wctex</c>; the source is always kept as well. The
/// game prefers a generated file that sits in the same layer as its source, so a mod that replaces the
/// source image still wins.
///
/// <c>assets/build.json</c> lists what to generate:
/// <code>
/// "runtimeFormats": [ { "match": "^image/fonts/hd/cn/font(ht|kt)\\.png$", "format": "rgba4444" } ]
/// </code>
/// <c>rgba4444</c>: 16 bit RGBA4444 pixels, deflate compressed, in a <c>.wctex</c> file (magic "WCTX",
/// version 1, format 1, width, height). Half the GPU memory of RGBA8 and no PNG decode at load time;
/// meant for large pages whose colour is plain (the white glyph pages of the Kai/Hei fonts).
/// </summary>
internal static class RuntimeFormats
{
    public const string Extension = ".wctex";

    /// <summary>Package path of the generated twin of a source path.</summary>
    public static string GeneratedPath(string source) => "generated/" + Path.ChangeExtension(source, Extension).Replace('\\', '/');

    /// <summary>Returns the number of generated files (written or already up to date).</summary>
    public static int Generate(string outDir, IReadOnlyList<(Regex match, string format)> rules, IReadOnlyDictionary<string, string> plan,
                               SortedDictionary<string, long> sizes, SortedDictionary<string, string> generatedFrom, bool force)
    {
        if (rules.Count == 0) return 0;
        int count = 0;
        foreach (var (target, source) in plan.ToList())
        {
            var rule = rules.FirstOrDefault(r => r.match.IsMatch(target));
            if (rule.match == null) continue;
            string generated = GeneratedPath(target);
            string dest = Path.Combine(outDir, generated);
            if (force || !File.Exists(dest) || File.GetLastWriteTimeUtc(dest) < File.GetLastWriteTimeUtc(source))
            {
                switch (rule.format)
                {
                    case "rgba4444": WriteRgba4444(source, dest); break;
                    default: throw new ToolException("unknown runtime format " + rule.format);
                }
                File.SetLastWriteTimeUtc(dest, File.GetLastWriteTimeUtc(source));
                Console.WriteLine($"  generated {generated} ({rule.format})");
            }
            sizes[generated] = new FileInfo(dest).Length;
            generatedFrom[generated] = target;
            count++;
        }
        return count;
    }

    private static void WriteRgba4444(string source, string dest)
    {
        using var codec = SKCodec.Create(source) ?? throw new ToolException("cannot decode " + source);
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput) throw new ToolException("cannot decode " + source + ": " + result);
        byte[] rgba = bitmap.Bytes;
        int pixels = info.Width * info.Height;
        var packed = new byte[pixels * 2];
        for (int i = 0; i < pixels; i++)
        {
            int r = To4(rgba[i * 4]), g = To4(rgba[i * 4 + 1]), b = To4(rgba[i * 4 + 2]), a = To4(rgba[i * 4 + 3]);
            // GL_UNSIGNED_SHORT_4_4_4_4: R in the high nibble, A in the low one; little endian ushort
            ushort v = (ushort)((r << 12) | (g << 8) | (b << 4) | a);
            packed[i * 2] = (byte)(v & 0xFF);
            packed[i * 2 + 1] = (byte)(v >> 8);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        using var fs = File.Create(dest);
        using (var w = new BinaryWriter(fs, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write("WCTX".ToCharArray());
            w.Write((ushort)1);   // version
            w.Write((ushort)1);   // format: RGBA4444
            w.Write(info.Width);
            w.Write(info.Height);
        }
        using var deflate = new DeflateStream(fs, CompressionLevel.SmallestSize);
        deflate.Write(packed, 0, packed.Length);
    }

    private static int To4(byte v) => (v * 15 + 127) / 255;
}
