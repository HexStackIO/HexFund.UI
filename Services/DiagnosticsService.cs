using HexFund.UI.Config;
using HexFund.UI.Models;
using HexFund.UI.Services.Http;

namespace HexFund.UI.Services;

/// <summary>
/// Collects the optional diagnostics sent with a feedback report. It never includes an email,
/// name, user id, token, account name, amount, description, category or response content.
/// </summary>
public interface IDiagnosticsService
{
    /// <summary>Captures the current diagnostics, already clipped to the API's limits.</summary>
    FeedbackDiagnostics Capture();
}

/// <inheritdoc />
public class DiagnosticsService : IDiagnosticsService
{
    private readonly ISettingsService _settings;
    private readonly FailureBuffer _failures;

    public DiagnosticsService(ISettingsService settings, FailureBuffer failures)
    {
        _settings = settings;
        _failures = failures;
    }

    public FeedbackDiagnostics Capture()
    {
        var diagnostics = new FeedbackDiagnostics
        {
            AppVersion = AppConstants.AppVersion,
            // The version code is set by the release workflow, unlike AppConstants.BuildNumber.
            BuildNumber = AppInfo.Current.BuildString,
            Platform = DeviceInfo.Platform.ToString(),
            OsVersion = DeviceInfo.VersionString,
            DeviceModel = $"{DeviceInfo.Manufacturer} {DeviceInfo.Model}",
            Screen = CurrentScreen(),
            Theme = _settings.Theme.ToString(),
            RecentErrors = _failures.Snapshot()
                .Select(f => new FeedbackErrorEntry
                {
                    At = f.At,
                    Method = f.Method,
                    Path = f.Path,
                    Status = f.Status,
                    Error = f.Error,
                    CorrelationId = f.CorrelationId,
                })
                .ToList(),
        };

        return diagnostics.Clipped();
    }

    // Feedback is reached from Settings, so the page on top is never the one the user was using.
    // The root page of the current tab is reported instead.
    private static string CurrentScreen()
    {
        var shell = Shell.Current;
        if (shell == null) return ScreenNames.Unknown;

        var root = shell.Navigation?.NavigationStack?.FirstOrDefault(p => p != null) ?? shell.CurrentPage;
        return ScreenNames.FromPageTypeName(root?.GetType().Name);
    }
}
