using WC2.Tools;

// WC2.Tools <command> [options]
//   build-pack --assets DIR --content DIR --layout FILE --out DIR
//       Builds the runtime asset package: the original assets (never modified) are copied into the
//       folder-by-purpose tree described by the layout file, the authored content is laid on top,
//       runtime formats are generated, and index.json lists every file and its legacy name.
//   compare-image --golden REF.png --actual SHOT.png [--max-percent P] [--diff OUT.png] [--record]
//       Screenshot regression: compares a screenshot with its reference (see ImageCompare).
//   validate --pack DIR [--source DIR]
//       Checks a built package: every index entry exists, every legacy name resolves, and (with
//       --source) every original asset is reachable through its legacy name.
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
                        return PackBuilder.Build(Require(opts, "assets"), Require(opts, "content"), Require(opts, "layout"), Require(opts, "out"),
                            opts.ContainsKey("force"));
                    case "validate":
                        return PackValidator.Validate(Require(opts, "pack"), opts.GetValueOrDefault("source"));
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
            Console.WriteLine("usage: WC2.Tools build-pack --assets DIR --content DIR --layout FILE --out DIR [--force]");
            Console.WriteLine("       WC2.Tools validate --pack DIR [--source DIR]");
            Console.WriteLine("       WC2.Tools compare-image --golden REF.png --actual SHOT.png [--max-percent 1.0] [--pixel-tolerance 40] [--diff OUT.png] [--record [--width 320]]");
        }
    }

    internal sealed class ToolException : Exception
    {
        public ToolException(string message) : base(message) { }
    }
}
