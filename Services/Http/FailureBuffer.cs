namespace HexFund.UI.Services.Http;

/// <summary>One failed API call. Holds no query string, no ids and no response content.</summary>
public sealed record FailureRecord(
    DateTimeOffset At,
    string Method,
    string Path,
    int? Status,
    string? Error,
    string? CorrelationId);

/// <summary>
/// A thread-safe in-memory ring buffer of the most recent failed API calls. Nothing is persisted.
/// </summary>
public sealed class FailureBuffer
{
    public const int DefaultCapacity = 20;

    private readonly object _lock = new();
    private readonly Queue<FailureRecord> _items = new();
    private readonly int _capacity;

    public FailureBuffer(int capacity = DefaultCapacity)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public void Add(FailureRecord record)
    {
        lock (_lock)
        {
            _items.Enqueue(record);
            while (_items.Count > _capacity) _items.Dequeue();
        }
    }

    /// <summary>A copy of the buffer, oldest first.</summary>
    public IReadOnlyList<FailureRecord> Snapshot()
    {
        lock (_lock) return _items.ToArray();
    }

    public void Clear()
    {
        lock (_lock) _items.Clear();
    }
}
