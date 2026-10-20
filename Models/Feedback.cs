namespace HexFund.UI.Models;

/// <summary>The kind of feedback. Values match the API and must not be renumbered.</summary>
public enum FeedbackType
{
    Bug = 0,
    Idea = 1,
    Question = 2,
}

/// <summary>Body of <c>POST feedback</c>. A null <see cref="Diagnostics"/> is omitted from the JSON.</summary>
public sealed class SubmitFeedbackRequest
{
    public FeedbackType Type { get; set; }
    public string Body { get; set; } = string.Empty;
    public FeedbackDiagnostics? Diagnostics { get; set; }
}

/// <summary>
/// Context sent with a report when the user allows it. Holds no email, name, ids, account names,
/// amounts or response content.
/// </summary>
public sealed class FeedbackDiagnostics
{
    public string AppVersion { get; set; } = string.Empty;
    public string BuildNumber { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public string Screen { get; set; } = string.Empty;
    public string Theme { get; set; } = string.Empty;
    public List<FeedbackErrorEntry> RecentErrors { get; set; } = new();
}

/// <summary>A recent failed API call as reported in diagnostics.</summary>
public sealed class FeedbackErrorEntry
{
    public DateTimeOffset At { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int? Status { get; set; }
    public string? Error { get; set; }
    public string? CorrelationId { get; set; }
}

/// <summary>The 202 response to a submitted report.</summary>
public sealed class FeedbackReceipt
{
    public Guid Id { get; set; }
}
