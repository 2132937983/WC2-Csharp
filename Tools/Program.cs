using WC2.Tools;

// WC2.Tools <command> [options]
//   build-pack --assets DIR --out DIR [--index-copy FILE] [--force]
//       Builds the runtime asset package from the asset tree (assets/): copies it (the originals are
//       never modified), generates runtime formats under generated/, and writes
//       generated/asset_index.json (every file, every original name -> path). See ASSET_LAYOUT.md.
//   build-map --config FILE --source DIR --out DIR
//       Builds a map pack (regions, adjacency, ownership, cities, ports, airfields) from Natural
//       Earth GeoJSON (MAP_PIPELINE.md).
//   compare-image --golden REF.png --actual SHOT.png [--max-percent P] [--diff OUT.png] [--record]
//       Screenshot regression: compares a screenshot with its reference (see ImageCompare).
//   validate --pack DIR [--source DIR]
//       Asset checker: index and files agree, every original name resolves, every asset the data
//       names exists, and (with --source, the asset tree) everything is packaged.
return Cli.Run(args);

namespace WC2.Tools
{
    internal static class Cli
    {
        public static int Run(string[] args)
        {
            if (args.Length == 0) { Usage(); return 2; }
            var opts = ParseOptions(args.Skip(1).ToArray());
            try
            {
                switch (args[0])
                {
                    case "build-pack":
                        return PackBuilder.Build(Require(opts, "assets"), Require(opts, "out"), opts.GetValueOrDefault("index-copy"),
                            opts.ContainsKey("force"));
                    case "validate":
                        return PackValidator.Validate(Require(opts, "pack"), opts.GetValueOrDefault("source"));
                    case "build-map":
                        Require(opts, "config"); Require(opts, "source"); Require(opts, "out");
                        return MapPipeline.Run(opts);
                    case "compare-image":
                        Require(opts, "golden"); Require(opts, "actual");
                        return ImageCompare.Run(opts);
                    default:
                        Usage();
                        return 2;
                }
            }
            catch (ToolException ex)
            {
                Console.Error.WriteLine("error: " + ex.Message);
                return 1;
            }
        }

        private static Dictionary<string, string> ParseOptions(string[] args)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--")) continue;
                string key = args[i].Substring(2);
                string value = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "true";
                map[key] = value;
            }
            return map;
        }

        private static string Require(Dictionary<string, string> opts, string key) =>
            opts.TryGetValue(key, out var v) ? v : throw new ToolException("missing --" + key);

        private static void Usage()
        {
            Console.WriteLine("usage: WC2.Tools build-pack --assets DIR --out DIR [--index-copy FILE] [--force]");
            Console.WriteLine("       WC2.Tools validate --pack DIR [--source DIR]");
            Console.WriteLine("       WC2.Tools build-map --config Tools/map/natural-earth.json --source DIR --out DIR [--preview-width 2048]");
            Console.WriteLine("       WC2.Tools compare-image --golden REF.png --actual SHOT.png [--max-percent 1.0] [--pixel-tolerance 40] [--diff OUT.png] [--record [--width 320]]");
        }
    }

    internal sealed class ToolException : Exception
    {
        public ToolException(string message) : base(message) { }
    }
}
