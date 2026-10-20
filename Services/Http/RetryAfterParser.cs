using System.Globalization;

namespace HexFund.UI.Services.Http;

/// <summary>Reads a Retry-After header value, which is either seconds or an HTTP date.</summary>
public static class RetryAfterParser
{
    private static readonly TimeSpan MaxDelay = TimeSpan.FromDays(7);

    /// <summary>Returns null for a missing or unreadable value. The result is never negative.</summary>
    public static TimeSpan? Parse(string? value, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();

        if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            var delay = seconds > MaxDelay.TotalSeconds ? MaxDelay : TimeSpan.FromSeconds(seconds);
            return delay;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
        {
            var delta = date - now;
            if (delta < TimeSpan.Zero) return TimeSpan.Zero;
            return delta > MaxDelay ? MaxDelay : delta;
        }

        return null;
    }
}
