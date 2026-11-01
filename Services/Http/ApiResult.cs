namespace HexFund.UI.Services.Http;

/// <summary>The category of an API failure, used to pick a user-facing message.</summary>
public enum ApiResultKind
{
    None,
    Network,
    Timeout,
    Unauthorized,
    Validation,
    RateLimited,
    Server,
    Unexpected,
}

/// <summary>
/// The outcome of an API call: success, or a classified failure with the parsed problem details.
/// </summary>
public class ApiResult
{
    protected ApiResult(
        bool isSuccess, int? statusCode, ApiResultKind kind, ProblemInfo? problem,
        TimeSpan? retryAfter, string? correlationId)
    {
        IsSuccess = isSuccess;
        StatusCode = statusCode;
        Kind = kind;
        Problem = problem;
        RetryAfter = retryAfter;
        CorrelationId = correlationId ?? problem?.CorrelationId;
    }

    public bool IsSuccess { get; }

    /// <summary>The HTTP status code, or null when no response was received.</summary>
    public int? StatusCode { get; }

    public ApiResultKind Kind { get; }

    /// <summary>The parsed RFC 7807 body of a failed response, if there was one.</summary>
    public ProblemInfo? Problem { get; }

    /// <summary>How long the server asked the client to wait, from the Retry-After header.</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>The server's correlation id for the request, from the response header or body.</summary>
    public string? CorrelationId { get; }

    public static ApiResult Success(int? statusCode = null) =>
        new(true, statusCode, ApiResultKind.None, null, null, null);

    public static ApiResult Failure(
        ApiResultKind kind, int? statusCode = null, ProblemInfo? problem = null,
        TimeSpan? retryAfter = null, string? correlationId = null) =>
        new(false, statusCode, kind, problem, retryAfter, correlationId);

    /// <summary>Maps the HTTP status code of a failed response to a kind.</summary>
    public static ApiResultKind KindForStatus(int statusCode) => statusCode switch
    {
        401 => ApiResultKind.Unauthorized,
        400 or 422 => ApiResultKind.Validation,
        429 => ApiResultKind.RateLimited,
        >= 500 => ApiResultKind.Server,
        _ => ApiResultKind.Unexpected,
    };
}

/// <summary>An <see cref="ApiResult"/> that carries a value on success.</summary>
public sealed class ApiResult<T> : ApiResult
{
    private ApiResult(
        bool isSuccess, T? value, int? statusCode, ApiResultKind kind, ProblemInfo? problem,
        TimeSpan? retryAfter, string? correlationId)
        : base(isSuccess, statusCode, kind, problem, retryAfter, correlationId)
    {
        Value = value;
    }

    public T? Value { get; }

    public static ApiResult<T> Success(T? value, int? statusCode = null) =>
        new(true, value, statusCode, ApiResultKind.None, null, null, null);

    public static new ApiResult<T> Failure(
        ApiResultKind kind, int? statusCode = null, ProblemInfo? problem = null,
        TimeSpan? retryAfter = null, string? correlationId = null) =>
        new(false, default, statusCode, kind, problem, retryAfter, correlationId);

    /// <summary>Carries a failed non-generic result over to a typed one.</summary>
    public static ApiResult<T> FromFailure(ApiResult failed) =>
        Failure(failed.Kind, failed.StatusCode, failed.Problem, failed.RetryAfter, failed.CorrelationId);
}
