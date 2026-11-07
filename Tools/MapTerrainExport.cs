using System.Text.Json.Nodes;
using BitMiracle.LibTiff.Classic;
using SkiaSharp;

namespace WC2.Tools;

/// <summary>
/// The terrain picture of a mesh map (MAP_PIPELINE.md, "The look of the map"): the background the game
/// draws below the owners' translucent colours.
/// <list type="bullet">
/// <item>Land: Natural Earth II with shaded relief (natural colours: green lowlands, brown and grey
/// mountains with their light and shadow, sand deserts, white ice), graded towards parchment
/// (<c>parchment</c>, <c>parchmentMix</c>).</item>
/// <item>Water (the sea zones, lakes and channels of the map): a gradient from shallow water at the coast
/// to deep ocean (<c>shallow</c>, <c>deep</c>, <c>depthPixels</c>), faint wave lines following the
/// coast (<c>waveLines</c>) and a pale foam edge along the coast.</item>
/// <item>A tileable paper grain (<c>paper&lt;id&gt;.png</c>) the game lays over the map when zoomed in.</item>
/// </list>
/// Tiles <c>terrain&lt;id&gt;_&lt;n&gt;.webp</c> (columns x rows over the whole map), uploaded as RGB565.
/// </summary>
internal static class MapTerrainExport
{
    public static List<(string name, string format)> Write(string outDir, int mapId, JsonObject cfg, JsonObject config, bool[] seaOf,
                                                         int[] raster, int width, int height, string source)
    {
        var b = config["bounds"]!.AsObject();
        double west = b["west"]!.GetValue<double>(), east = b["east"]!.GetValue<double>(), north = b["north"]!.GetValue<double>(), south = b["south"]!.GetValue<double>();
        double ppd = cfg["pixelsPerDegree"]?.GetValue<double>() ?? 20;
        int cols = cfg["columns"]?.GetValue<int>() ?? 8, rows = cfg["rows"]?.GetValue<int>() ?? 4;
        int outW = (int)Math.Round((east - west) * ppd), outH = (int)Math.Round((north - south) * ppd);
        int tileW = outW / cols, tileH = outH / rows;
        outW = tileW * cols; outH = tileH * rows;
        SKColor C(string key, SKColor fallback)
        {
            var a = cfg[key]?.AsArray();
            return a == null ? fallback : new SKColor((byte)a[0]!.GetValue<int>(), (byte)a[1]!.GetValue<int>(), (byte)a[2]!.GetValue<int>());
        }
        var parchment = C("parchment", new SKColor(224, 207, 163));
        double parchmentMix = cfg["parchmentMix"]?.GetValue<double>() ?? 0.3;
        var shallow = C("shallow", new SKColor(86, 136, 160));
        var deep = C("deep", new SKColor(28, 60, 96));
        var foam = C("foam", new SKColor(196, 214, 210));
        double depthPixels = cfg["depthPixels"]?.GetValue<double>() ?? 45;
        var waves = cfg["waveLines"]?.AsArray().Select(v => v!.GetValue<double>()).ToArray() ?? new[] { 5.0, 11.0, 19.0 };

        // ---- the relief picture (whole globe, plate carree)
        string file = Path.Combine(source, cfg["file"]!.GetValue<string>());
        if (!File.Exists(file)) throw new ToolException($"terrain: {file} missing (MAP_PIPELINE.md, sources)");
        int rw, rh, spp;
        byte[] relief;
        using (var tif = Tiff.Open(file, "r") ?? throw new ToolException($"terrain: {file} is not a readable TIFF"))
        {
            rw = tif.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
            rh = tif.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
            spp = tif.GetField(TiffTag.SAMPLESPERPIXEL)?[0].ToInt() ?? 3;
            int scan = tif.ScanlineSize();
            relief = new byte[(long)rw * rh * 3 > int.MaxValue ? throw new ToolException("terrain: picture too large") : rw * rh * 3];
            var line = new byte[scan];
            for (int y = 0; y < rh; y++)
            {
                tif.ReadScanline(line, y);
                for (int x = 0; x < rw; x++)
                {
                    int s = x * spp, d = (y * rw + x) * 3;
                    relief[d] = line[s]; relief[d + 1] = line[s + Math.Min(1, spp - 1)]; relief[d + 2] = line[s + Math.Min(2, spp - 1)];
                }
            }
        }
        (double r, double g, double bl) Relief(double lon, double lat)
        {
            double fx = (lon + 180) / 360 * rw - 0.5, fy = (90 - lat) / 180 * rh - 0.5;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            double tx = fx - x0, ty = fy - y0;
            double R = 0, G = 0, B = 0;
            for (int k = 0; k < 4; k++)
            {
                int x = ((x0 + (k & 1)) % rw + rw) % rw, y = Math.Clamp(y0 + (k >> 1), 0, rh - 1);
                double wgt = ((k & 1) == 1 ? tx : 1 - tx) * ((k >> 1) == 1 ? ty : 1 - ty);
                int i = (y * rw + x) * 3;
                R += relief[i] * wgt; G += relief[i + 1] * wgt; B += relief[i + 2] * wgt;
            }
            return (R, G, B);
        }

        // ---- land and water at the picture's resolution (from the map's own raster)
        var land = new bool[outW * outH];
        for (int y = 0; y < outH; y++)
            for (int x = 0; x < outW; x++)
            {
                int sx = Math.Min(width - 1, (int)((x + 0.5) * width / outW)), sy = Math.Min(height - 1, (int)((y + 0.5) * height / outH));
                int v = raster[sy * width + sx];
                land[y * outW + x] = v > 0 && !seaOf[v - 1];
            }
        // distance from the land (chamfer 3-4, pixels x 3), the water's depth; over the seam on a
        // wrapping map (the shading continues across the date line)
        const int Inf = int.MaxValue / 4;
        bool wrapMap = config["wrap"]?.GetValue<bool>() ?? false;
        var water = new bool[land.Length];
        for (int i = 0; i < land.Length; i++) water[i] = !land[i];
        var dist = MapPipeline.ChamferDistance(water, outW, outH, wrapMap ? (int)Math.Min(outW, depthPixels * 2 + 8) : 0);

        static byte B(double v) => (byte)Math.Clamp((int)Math.Round(v), 0, 255);
        static double Smooth(double t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
        // a soft value noise for the water (no two waves the same)
        static double Hash(int x, int y) { uint h = (uint)(x * 374761393 + y * 668265263); h = (h ^ (h >> 13)) * 1274126177; return (h ^ (h >> 16)) / (double)uint.MaxValue; }
        static double Noise(double x, double y)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            double tx = Smooth(x - x0), ty = Smooth(y - y0);
            double a = Hash(x0, y0), b2 = Hash(x0 + 1, y0), c = Hash(x0, y0 + 1), d = Hash(x0 + 1, y0 + 1);
            return a + (b2 - a) * tx + (c - a) * ty + (a - b2 - c + d) * tx * ty;
        }

        var result = new List<(string, string)>();
        using var tile = new SKBitmap(tileW, tileH, SKColorType.Rgba8888, SKAlphaType.Opaque);
        for (int ty = 0; ty < rows; ty++)
            for (int tx = 0; tx < cols; tx++)
            {
                for (int y = 0; y < tileH; y++)
                    for (int x = 0; x < tileW; x++)
                    {
                        int gx = tx * tileW + x, gy = ty * tileH + y, i = gy * outW + gx;
                        double lon = west + (gx + 0.5) / ppd, lat = north - (gy + 0.5) / ppd;
                        SKColor c;
                        if (land[i])
                        {
                            var (r, g, bl) = Relief(lon, lat);
                            // where the relief picture has water but the map has land (a coast drawn
                            // differently), the parchment shows
                            if (bl > r + 12 && bl >= g) (r, g, bl) = (parchment.Red * 0.92, parchment.Green * 0.92, parchment.Blue * 0.92);
                            r += (parchment.Red - r) * parchmentMix; g += (parchment.Green - g) * parchmentMix; bl += (parchment.Blue - bl) * parchmentMix;
                            c = new SKColor(B(r + 3), B(g), B(bl - 3));
                        }
                        else
                        {
                            double d = Math.Min(dist[i], Inf / 2) / 3.0;
                            double depth = Smooth(d / depthPixels);
                            double n = (Noise(gx / 23.0, gy / 23.0) - 0.5) * 6 + (Noise(gx / 7.0, gy / 7.0) - 0.5) * 3;
                            double r = shallow.Red + (deep.Red - shallow.Red) * depth + n;
                            double g = shallow.Green + (deep.Green - shallow.Green) * depth + n;
                            double bl = shallow.Blue + (deep.Blue - shallow.Blue) * depth + n;
                            // wave lines following the coast, fading out to sea
                            foreach (double wv in waves)
                            {
                                double line = Math.Max(0, 1 - Math.Abs(d - wv) / 0.9) * (1 - wv / (depthPixels * 1.2));
                                r += 16 * line; g += 18 * line; bl += 18 * line;
                            }
                            // foam along the coast
                            double edge = Math.Max(0, 1 - d / 1.7) * 0.65;
                            r += (foam.Red - r) * edge; g += (foam.Green - g) * edge; bl += (foam.Blue - bl) * edge;
                            c = new SKColor(B(r), B(g), B(bl));
                        }
                        tile.SetPixel(x, y, c);
                    }
                string name = $"terrain{mapId}_{ty * cols + tx}.webp";
                using (var data = tile.Encode(SKEncodedImageFormat.Webp, cfg["quality"]?.GetValue<int>() ?? 86))
                using (var f = File.Create(Path.Combine(outDir, name)))
                    data.SaveTo(f);
                result.Add((name, "rgb565"));
            }

        // ---- the paper grain: a tileable brown speckle with soft fibres, mostly transparent
        const int P = 256;
        using (var paper = new SKBitmap(P, P, SKColorType.Rgba8888, SKAlphaType.Unpremul))
        {
            double Tile(double x, double y, int period)
            {
                // periodic value noise (period cells across the tile)
                double fx = x * period / P, fy = y * period / P;
                int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
                double ux = Smooth(fx - x0), uy = Smooth(fy - y0);
                double H(int a, int c) => Hash(((a % period) + period) % period, ((c % period) + period) % period + period * 7919);
                double v00 = H(x0, y0), v10 = H(x0 + 1, y0), v01 = H(x0, y0 + 1), v11 = H(x0 + 1, y0 + 1);
                return v00 + (v10 - v00) * ux + (v01 - v00) * uy + (v00 - v10 - v01 + v11) * ux * uy;
            }
            for (int y = 0; y < P; y++)
                for (int x = 0; x < P; x++)
                {
                    double v = Tile(x, y, 8) * 0.45 + Tile(x, y, 32) * 0.35 + Tile(x, y, 128) * 0.2;
                    double a = Math.Clamp((v - 0.35) * 1.6, 0, 1) * 46;
                    paper.SetPixel(x, y, new SKColor(92, 68, 38, B(a)));
                }
            string name = $"paper{mapId}.png";
            using var data = paper.Encode(SKEncodedImageFormat.Png, 100);
            using var f = File.Create(Path.Combine(outDir, name));
            data.SaveTo(f);
        }
        Console.WriteLine($"build-map: terrain picture {outW}x{outH} in {cols}x{rows} tiles of {tileW}x{tileH}");
        return result;
    }
}
