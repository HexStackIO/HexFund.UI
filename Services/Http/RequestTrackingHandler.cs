namespace HexFund.UI.Services.Http;

/// <summary>
/// Adds correlation and version headers to every request and records failed calls, including
/// timeouts and thrown exceptions, in a <see cref="FailureBuffer"/>.
/// </summary>
public sealed class RequestTrackingHandler : DelegatingHandler
{
    public const string CorrelationHeader = "X-Correlation-Id";
    public const string VersionHeader = "X-App-Version";
    private const int MaxVersionLength = 20;
    private const int MaxErrorLength = 100;

    private readonly FailureBuffer _failures;
    private readonly string _appVersion;

    public RequestTrackingHandler(FailureBuffer failures, string appVersion)
    {
        _failures = failures;
        _appVersion = Truncate(appVersion, MaxVersionLength);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // The API accepts 8 to 64 letters, digits and hyphens, which the "D" format satisfies.
        var correlationId = Guid.NewGuid().ToString("D");
        request.Headers.Remove(CorrelationHeader);
        request.Headers.TryAddWithoutValidation(CorrelationHeader, correlationId);
        if (_appVersion.Length > 0)
        {
            request.Headers.Remove(VersionHeader);
            request.Headers.TryAddWithoutValidation(VersionHeader, _appVersion);
        }

        var method = request.Method.Method;
        var path = PathSanitizer.Sanitize(request.RequestUri?.ToString());

        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            // A cancellation requested by the caller is not a failure of the call.
            if (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                // Only the exception type is kept, never its message.
                var name = ex is OperationCanceledException ? "Timeout" : ex.GetType().Name;
                Record(method, path, null, name, correlationId);
            }
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            var serverId = response.Headers.TryGetValues(CorrelationHeader, out var values)
                ? values.FirstOrDefault()
                : null;
            Record(method, path, (int)response.StatusCode, null, serverId ?? correlationId);
        }

        return response;
    }

    private void Record(string method, string path, int? status, string? error, string? correlationId) =>
        _failures.Add(new FailureRecord(
            DateTimeOffset.UtcNow, method, path, status,
            error is null ? null : Truncate(error, MaxErrorLength), correlationId));

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
