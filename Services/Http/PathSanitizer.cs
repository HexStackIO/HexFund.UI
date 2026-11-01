namespace HexFund.UI.Services.Http;

/// <summary>
/// Reduces a request path to a template that is safe to record: the query string is dropped and
/// identifiers become <c>{id}</c>.
/// </summary>
public static class PathSanitizer
{
    public const int MaxLength = 200;

    /// <summary>
    /// Accepts a relative path or an absolute URL. A leading <c>api</c> segment is removed because
    /// every API route shares it.
    /// </summary>
    public static string Sanitize(string? pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl)) return string.Empty;

        var path = pathOrUrl.Trim();
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri)
            && uri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            path = uri.AbsolutePath;

        var cut = path.IndexOfAny(new[] { '?', '#' });
        if (cut >= 0) path = path[..cut];

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (segments.Count > 0 && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase))
            segments.RemoveAt(0);

        var result = string.Join('/', segments.Select(s => IsIdentifier(s) ? "{id}" : s));
        return result.Length > MaxLength ? result[..MaxLength] : result;
    }

    private static bool IsIdentifier(string segment) =>
        Guid.TryParse(segment, out _) || (segment.Length > 0 && segment.All(char.IsAsciiDigit));
}
