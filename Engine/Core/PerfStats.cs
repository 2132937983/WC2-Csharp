using System.Diagnostics;

namespace WC2;

/// <summary>
/// Named time accumulators for measuring where the frames go (turn processing, AI, update, render).
/// <c>using var _ = PerfStats.Measure("ai.decide");</c> is a struct scope: no allocation, two timestamp
/// reads. The autoplay driver logs a report per round (<c>Perf</c> lines) and at the end.
/// </summary>
public static class PerfStats
{
    private sealed class Counter { public long ticks; public int count; public long maxTicks; public long bytes; }
    private static readonly Dictionary<string, Counter> s_counters = new();
    private static long s_allocStart = GC.GetTotalAllocatedBytes();
    private static long s_wallStart = Stopwatch.GetTimestamp();
    private static int s_frames;

    public readonly struct Scope : IDisposable
    {
        private readonly string name;
        private readonly long start;
        private readonly long allocated;
        public Scope(string name) { this.name = name; start = Stopwatch.GetTimestamp(); allocated = GC.GetAllocatedBytesForCurrentThread(); }
        public void Dispose() => Add(name, Stopwatch.GetTimestamp() - start, GC.GetAllocatedBytesForCurrentThread() - allocated);
    }

    public static Scope Measure(string name) => new(name);

    public static void Add(string name, long ticks, long bytes = 0)
    {
        if (!s_counters.TryGetValue(name, out var c)) s_counters[name] = c = new Counter();
        c.ticks += ticks;
        c.bytes += bytes;
        c.count++;
        if (ticks > c.maxTicks) c.maxTicks = ticks;
    }

    public static void Frame() => s_frames++;

    public static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    /// <summary>One line: wall time, frames, allocations and every counter (total ms / calls / max ms).</summary>
    public static string Report(bool reset = true)
    {
        long now = Stopwatch.GetTimestamp();
        var sb = new StringBuilder();
        sb.Append($"wall {Ms(now - s_wallStart):0} ms, {s_frames} frames, allocated {(GC.GetTotalAllocatedBytes() - s_allocStart) / 1048576.0:0.0} MB");
        foreach (var (name, c) in s_counters.OrderByDescending(kv => kv.Value.ticks))
            sb.Append($"; {name} {Ms(c.ticks):0.0} ms/{c.count} (max {Ms(c.maxTicks):0.0}, {c.bytes / 1024.0:0} KB)");
        if (reset) Reset();
        return sb.ToString();
    }

    public static void Reset()
    {
        s_counters.Clear();
        s_frames = 0;
        s_allocStart = GC.GetTotalAllocatedBytes();
        s_wallStart = Stopwatch.GetTimestamp();
    }
}
