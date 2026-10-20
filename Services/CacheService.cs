using Microsoft.Extensions.Caching.Memory;

namespace HexFund.UI.Services;

/// <summary>In-memory cache for API responses, with helpers to invalidate calendar data.</summary>
public interface ICacheService
{
    T? Get<T>(string key) where T : class;
    void Set<T>(string key, T value, TimeSpan duration) where T : class;
    void Remove(string key);
    void Clear();
    /// <summary>Removes every cached calendar entry for the account.</summary>
    void InvalidateCalendarData(Guid accountId);

    /// <summary>Records that a month was cached so <see cref="InvalidateCalendarData"/> can find it.</summary>
    void RegisterCachedMonth(Guid accountId, int year, int month);
}

/// <summary>Memory-cache implementation. Nothing is persisted, so the cache is empty after a restart.</summary>
public class CacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public CacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public T? Get<T>(string key) where T : class =>
        _cache.TryGetValue(key, out T? value) ? value : null;

    public void Set<T>(string key, T value, TimeSpan duration) where T : class =>
        _cache.Set(key, value, duration);

    public void Remove(string key) =>
        _cache.Remove(key);

    public void Clear()
    {
        if (_cache is MemoryCache mc)
            mc.Compact(1.0);
    }

    public static string AccountsListKey() =>
        "accounts:list";

    public static string AccountKey(Guid accountId) =>
        $"accounts:{accountId}";

    public static string TransactionsKey(Guid accountId) =>
        $"transactions:{accountId}";

    public static string MonthlyOverviewKey(Guid accountId, int year, int month) =>
        $"overview:{accountId}:{year}:{month:00}";

    public static string TransactionsForDateKey(Guid accountId, DateTime date) =>
        $"txns-date:{accountId}:{date:yyyy-MM-dd}";

    public static string TransactionsForMonthKey(Guid accountId, int year, int month) =>
        $"transactions:month:{accountId}:{year:D4}-{month:D2}";

    public void InvalidateCalendarData(Guid accountId)
    {
        var setKey = MonthSetKey(accountId);
        if (_cache.TryGetValue(setKey, out HashSet<(int year, int month)>? months) && months != null)
        {
            foreach (var (year, month) in months)
            {
                _cache.Remove(MonthlyOverviewKey(accountId, year, month));
                _cache.Remove(TransactionsForMonthKey(accountId, year, month));
                // Per-date entries are keyed by day, so clear every day of the month.
                var daysInMonth = DateTime.DaysInMonth(year, month);
                for (int day = 1; day <= daysInMonth; day++)
                    _cache.Remove(TransactionsForDateKey(accountId, new DateTime(year, month, day)));
            }
            _cache.Remove(setKey);
        }
    }

    public void RegisterCachedMonth(Guid accountId, int year, int month)
    {
        var setKey = MonthSetKey(accountId);
        var set = _cache.GetOrCreate(setKey, _ => new HashSet<(int, int)>())!;
        set.Add((year, month));
    }

    private static string MonthSetKey(Guid accountId) =>
        $"overview-months:{accountId}";
}
