#if DEBUG
using System.Diagnostics;
using System.Runtime;

namespace HexFund.UI.Diagnostics;

public static class GCMonitor
{
    private static long _lastGen0Count;
    private static long _lastGen1Count;
    private static long _lastGen2Count;
    private static long _lastTotalMemory;
    private static readonly Stopwatch _timer = Stopwatch.StartNew();
    private static readonly object _lock = new();

    public static void StartMonitoring()
    {
        _lastGen0Count = GC.CollectionCount(0);
        _lastGen1Count = GC.CollectionCount(1);
        _lastGen2Count = GC.CollectionCount(2);
        _lastTotalMemory = GC.GetTotalMemory(false);
        _timer.Restart();
    }

    public static GCStats CheckAndLog(string context, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        lock (_lock)
        {
            var currentGen0 = GC.CollectionCount(0);
            var currentGen1 = GC.CollectionCount(1);
            var currentGen2 = GC.CollectionCount(2);
            var currentMemory = GC.GetTotalMemory(false);

            var stats = new GCStats
            {
                Context = context,
                Caller = caller,
                Gen0Collections = currentGen0 - _lastGen0Count,
                Gen1Collections = currentGen1 - _lastGen1Count,
                Gen2Collections = currentGen2 - _lastGen2Count,
                MemoryDelta = currentMemory - _lastTotalMemory,
                CurrentMemory = currentMemory,
                ElapsedMs = _timer.ElapsedMilliseconds
            };

            if (stats.Gen0Collections > 0 || stats.Gen1Collections > 0 ||
                stats.Gen2Collections > 0 || Math.Abs(stats.MemoryDelta) > 100_000)
            {
                Debug.WriteLine($"🗑️ GC Event [{context}] from {caller}:");
                Debug.WriteLine($"   Gen0: {stats.Gen0Collections}, Gen1: {stats.Gen1Collections}, Gen2: {stats.Gen2Collections}");
                Debug.WriteLine($"   Memory: {FormatBytes(stats.MemoryDelta)} (Total: {FormatBytes(stats.CurrentMemory)})");
                Debug.WriteLine($"   Time since last check: {stats.ElapsedMs}ms");
            }

            _lastGen0Count = currentGen0;
            _lastGen1Count = currentGen1;
            _lastGen2Count = currentGen2;
            _lastTotalMemory = currentMemory;
            _timer.Restart();

            return stats;
        }
    }

    public static string GetCurrentStats()
    {
        var totalMemory = GC.GetTotalMemory(false);
        return $"Gen0: {GC.CollectionCount(0)}, Gen1: {GC.CollectionCount(1)}, " +
               $"Gen2: {GC.CollectionCount(2)}, Memory: {FormatBytes(totalMemory)}";
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

public class GCStats
{
    public string Context { get; set; } = "";
    public string Caller { get; set; } = "";
    public long Gen0Collections { get; set; }
    public long Gen1Collections { get; set; }
    public long Gen2Collections { get; set; }
    public long MemoryDelta { get; set; }
    public long CurrentMemory { get; set; }
    public long ElapsedMs { get; set; }
}
#endif