using System.Collections;
using System.Reflection;

namespace WC2;

/// <summary>
/// Field-by-field comparison of two object graphs (public and private instance fields, collections,
/// dictionaries). <c>--verify-data</c> uses it to prove that a JSON file loads into exactly the same
/// definitions as the original XML file. Floats are compared bit for bit.
/// </summary>
public static class DataCompare
{
    public static List<string> Diff(object a, object b, string path = "", int maxDiffs = 40)
    {
        var diffs = new List<string>();
        var visited = new HashSet<(object, object)>(new PairComparer());
        Compare(a, b, path, diffs, visited, maxDiffs);
        return diffs;
    }

    private static void Compare(object a, object b, string path, List<string> diffs, HashSet<(object, object)> visited, int max)
    {
        if (diffs.Count >= max) return;
        if (a == null || b == null)
        {
            if (!(a == null && b == null) && !(IsEmpty(a) && IsEmpty(b))) diffs.Add($"{path}: {Show(a)} != {Show(b)}");
            return;
        }
        var type = a.GetType();
        if (type != b.GetType()) { diffs.Add($"{path}: type {type.Name} != {b.GetType().Name}"); return; }
        if (type.IsPrimitive || type.IsEnum || a is string || a is decimal)
        {
            bool equal = a switch
            {
                float fa => BitConverter.SingleToInt32Bits(fa) == BitConverter.SingleToInt32Bits((float)b),
                double da => BitConverter.DoubleToInt64Bits(da) == BitConverter.DoubleToInt64Bits((double)b),
                _ => a.Equals(b),
            };
            if (!equal) diffs.Add($"{path}: {Show(a)} != {Show(b)}");
            return;
        }
        if (!type.IsValueType && !visited.Add((a, b))) return;

        if (a is IDictionary da2 && b is IDictionary db2)
        {
            var keys = new HashSet<object>(da2.Keys.Cast<object>());
            foreach (var k in db2.Keys) if (!keys.Contains(k)) diffs.Add($"{path}[{k}]: only in second");
            foreach (var k in da2.Keys)
            {
                if (!db2.Contains(k)) { diffs.Add($"{path}[{k}]: only in first"); continue; }
                Compare(da2[k], db2[k], $"{path}[{k}]", diffs, visited, max);
            }
            return;
        }
        if (a is IEnumerable ea && b is IEnumerable eb)
        {
            var la = ea.Cast<object>().ToList();
            var lb = eb.Cast<object>().ToList();
            if (la.Count != lb.Count) diffs.Add($"{path}: count {la.Count} != {lb.Count}");
            for (int i = 0; i < Math.Min(la.Count, lb.Count); i++) Compare(la[i], lb[i], $"{path}[{i}]", diffs, visited, max);
            return;
        }
        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (f.Name.Contains("k__BackingField") && f.FieldType.IsSubclassOf(typeof(Delegate))) continue;
                Compare(f.GetValue(a), f.GetValue(b), path + "." + f.Name, diffs, visited, max);
            }
        }
    }

    private static bool IsEmpty(object o) => o is string s ? s.Length == 0 : o is ICollection c && c.Count == 0;

    private static string Show(object o) => o switch
    {
        null => "null",
        string s => "\"" + s + "\"",
        float f => f.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        double d => d.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        _ => o.ToString(),
    };

    private sealed class PairComparer : IEqualityComparer<(object, object)>
    {
        public bool Equals((object, object) x, (object, object) y) => ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);
        public int GetHashCode((object, object) p) => HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(p.Item1), System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(p.Item2));
    }
}
