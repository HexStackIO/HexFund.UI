using HexFund.UI.Services.Http;

namespace HexFund.UI.Services;

/// <summary>What the feedback page shows after a submit attempt.</summary>
/// <param name="Message">The text shown to the user.</param>
/// <param name="IsSuccess">True when the report was received.</param>
/// <param name="KeepDraft">True when the typed text must be kept.</param>
/// <param name="IsFieldError">True when the message belongs under the editor.</param>
public sealed record FeedbackOutcome(string Message, bool IsSuccess, bool KeepDraft, bool IsFieldError);

/// <summary>Maps the result of submitting feedback to user-facing text. Pure, so it can be tested.</summary>
public static class FeedbackOutcomes
{
    public const string SuccessMessage = "Thanks, your report was received.";
    public const string NetworkMessage = "Could not reach HexFund. Your draft is saved.";
    public const string UnauthorizedMessage = "Your session has expired. Please sign out and sign in again.";
    public const string ServerMessage = "Something went wrong on our side. Your draft is saved.";
    public const string ValidationFallback = "Please check your report and try again.";

    public static FeedbackOutcome From(ApiResult result)
    {
        if (result.IsSuccess)
            return new FeedbackOutcome(SuccessMessage, true, false, false);

        switch (result.Kind)
        {
            case ApiResultKind.RateLimited:
                return new FeedbackOutcome(RateLimitMessage(result.RetryAfter), false, true, false);

            case ApiResultKind.Validation:
                var detail = result.Problem?.FirstError("body")
                             ?? result.Problem?.Errors.Values.SelectMany(m => m).FirstOrDefault()
                             ?? result.Problem?.Detail
                             ?? result.Problem?.Title
                             ?? ValidationFallback;
                return new FeedbackOutcome(detail, false, true, true);

            case ApiResultKind.Network:
            case ApiResultKind.Timeout:
                return new FeedbackOutcome(NetworkMessage, false, true, false);

            case ApiResultKind.Unauthorized:
                return new FeedbackOutcome(UnauthorizedMessage, false, true, false);

            default:
                return new FeedbackOutcome(ServerMessageWithReference(result.CorrelationId), false, true, false);
        }
    }

    /// <summary>"About N minutes", with N rounded up and at least 1.</summary>
    public static string RateLimitMessage(TimeSpan? retryAfter)
    {
        const string prefix = "You have sent several reports recently.";
        if (retryAfter is not { } wait) return $"{prefix} Please try again later.";

        var minutes = Math.Max(1, (int)Math.Ceiling(wait.TotalMinutes));
        return $"{prefix} Please try again in about {minutes} minute{(minutes == 1 ? "" : "s")}.";
    }

    /// <summary>Appends the last 8 characters of the correlation id, when there is one.</summary>
    public static string ServerMessageWithReference(string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) return ServerMessage;

        var id = correlationId.Trim();
        var reference = id.Length <= 8 ? id : id[^8..];
        return $"{ServerMessage} Reference: {reference}";
    }
}
