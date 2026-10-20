namespace HexFund.UI.Config;

/// <summary>
/// Compile-time constants shared across the app: HTTP settings, version display values, cache
/// lifetimes and tab titles.
/// </summary>
public static class AppConstants
{
    public const string HttpClientName = "HexFundApi";
    public const int    HttpTimeoutSecs = 30;

    // Debug builds target a local API; 10.0.2.2 is the Android emulator's alias for the host
    // machine. Release builds target the hosted API. Both can be overridden at runtime with the
    // API_BASE_URL environment variable (see MauiProgram).
#if DEBUG
    public const string AndroidBaseUrl = "https://10.0.2.2:5001/api/";
    public const string LocalBaseUrl   = "https://localhost:5001/api/";
#else
    public const string AndroidBaseUrl = "https://financeplannerapi-ehguhgghete7auc8.centralus-01.azurewebsites.net/api/";
    public const string LocalBaseUrl   = "https://financeplannerapi-ehguhgghete7auc8.centralus-01.azurewebsites.net/api/";
#endif

    // Shown in the "App Information" section of Settings. The release workflow rewrites
    // AppVersion with the computed version name before building, so keep the declaration in the
    // form `AppVersion = "x.y"`. BuildNumber is a manually maintained display value.
    public const string AppVersion  = "5";
    public const int    BuildNumber = 5;

    public const string ChangelogResourcePath = "changelog.json";

    public static readonly TimeSpan AccountsCacheTtl     = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan TransactionsCacheTtl = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan CalendarCacheTtl     = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan CategoriesCacheTtl   = TimeSpan.FromMinutes(30);

    // Used when a theme resource cannot be resolved.
    public const string FallbackPrimary = "#D4AF37";
    public const string FallbackMuted   = "#59D4AF37";

    public const string CalendarTabTitle     = "Calendar";
    public const string TransactionsTabTitle = "Transactions";
    public const string AccountsTabTitle     = "Accounts";
    public const string SettingsTabTitle     = "Settings";
    public const string LoginTabTitle        = "Login";
}
