using System.Globalization;
using System.Text;

namespace HexFund.UI.Models;

/// <summary>
/// Pure helpers for <see cref="FeedbackDiagnostics"/>: the API's field limits, clipping, and the
/// text shown to the user. The preview is rendered from the same object that is sent, so it
/// cannot drift from what is transmitted.
/// </summary>
public static class FeedbackDiagnosticsFormat
{
    public const int AppVersionMax = 20;
    public const int BuildNumberMax = 20;
    public const int PlatformMax = 20;
    public const int OsVersionMax = 30;
    public const int DeviceModelMax = 80;
    public const int ScreenMax = 50;
    public const int ThemeMax = 30;
    public const int MaxRecentErrors = 20;
    public const int MethodMax = 10;
    public const int PathMax = 200;
    public const int ErrorMax = 100;
    public const int CorrelationIdMax = 64;

    /// <summary>Trims a value and cuts it to the API's limit. Null becomes an empty string.</summary>
    public static string Clip(string? value, int max)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    /// <summary>Returns a copy whose strings and list respect the API's limits.</summary>
    public static FeedbackDiagnostics Clipped(this FeedbackDiagnostics source) => new()
    {
        AppVersion = Clip(source.AppVersion, AppVersionMax),
        BuildNumber = Clip(source.BuildNumber, BuildNumberMax),
        Platform = Clip(source.Platform, PlatformMax),
        OsVersion = Clip(source.OsVersion, OsVersionMax),
        DeviceModel = Clip(source.DeviceModel, DeviceModelMax),
        Screen = Clip(source.Screen, ScreenMax),
        Theme = Clip(source.Theme, ThemeMax),
        RecentErrors = source.RecentErrors
            .TakeLast(MaxRecentErrors)
            .Select(e => new FeedbackErrorEntry
            {
                At = e.At,
                Method = Clip(e.Method, MethodMax),
                Path = Clip(e.Path, PathMax),
                Status = e.Status,
                Error = e.Error is null ? null : Clip(e.Error, ErrorMax),
                CorrelationId = e.CorrelationId is null ? null : Clip(e.CorrelationId, CorrelationIdMax),
            })
            .ToList(),
    };

    /// <summary>Renders the diagnostics as readable lines for the "What is sent" preview.</summary>
    public static string ToDisplayText(this FeedbackDiagnostics d)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"App version: {d.AppVersion} (build {d.BuildNumber})");
        sb.AppendLine($"Device: {d.DeviceModel}");
        sb.AppendLine($"System: {d.Platform} {d.OsVersion}");
        sb.AppendLine($"Screen: {d.Screen}");
        sb.AppendLine($"Theme: {d.Theme}");

        if (d.RecentErrors.Count == 0)
        {
            sb.Append("Recent request errors: none");
            return sb.ToString();
        }

        sb.AppendLine($"Recent request errors ({d.RecentErrors.Count}):");
        for (var i = 0; i < d.RecentErrors.Count; i++)
        {
            var e = d.RecentErrors[i];
            var outcome = e.Status is { } status ? status.ToString(CultureInfo.InvariantCulture) : e.Error ?? "failed";
            var line = $"{e.At.ToUniversalTime():yyyy-MM-dd HH:mm:ss}Z  {e.Method} {e.Path}  {outcome}";
            if (!string.IsNullOrEmpty(e.CorrelationId)) line += $"  ref {e.CorrelationId}";
            if (i < d.RecentErrors.Count - 1) sb.AppendLine(line); else sb.Append(line);
        }
        return sb.ToString();
    }
}

/// <summary>Maps a page type name to the short screen name used in diagnostics.</summary>
public static class ScreenNames
{
    public const string Unknown = "unknown";

    public static string FromPageTypeName(string? typeName) => typeName switch
    {
        "HomePage" => "home",
        "CalendarPage" => "calendar",
        "TransactionsPage" => "ledger",
        "InsightsPage" => "insights",
        "SettingsPage" => "settings",
        "AccountsPage" => "accounts",
        "AddEntryPage" => "add",
        "FeedbackPage" => "feedback",
        "LoginPage" => "login",
        "ExitConfirmationPage" => "exit",
        _ => Unknown,
    };
}
