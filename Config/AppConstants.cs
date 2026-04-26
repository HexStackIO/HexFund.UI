namespace HexFund.UI.Config;

public static class AppConstants
{
    // ── API ───────────────────────────────────────────────────────────────────
    public const string HttpClientName = "HexFundApi";
    public const int    HttpTimeoutSecs = 30;

#if DEBUG
    public const string AndroidBaseUrl = "https://10.0.2.2:5001/api/";
    public const string LocalBaseUrl   = "https://localhost:5001/api/";
#else
    public const string AndroidBaseUrl = "https://financeplannerapi-ehguhgghete7auc8.centralus-01.azurewebsites.net/api/";
    public const string LocalBaseUrl   = "https://financeplannerapi-ehguhgghete7auc8.centralus-01.azurewebsites.net/api/";
#endif

    // ── Version ───────────────────────────────────────────────────────────────
    //
    // AppVersion must match the <ApplicationVersion> in HexFund.UI.csproj.
    // BuildNumber is a monotonically increasing integer incremented on every
    // release commit — it appears in the "App Information" section alongside the
    // semantic version.
    //
    // Workflow before each release:
    //   1. Bump <ApplicationVersion> and <ApplicationDisplayVersion> in the csproj.
    //   2. Increment BuildNumber here to match.
    //   3. Prepend a new entry to Resources/changelog.json.
    //   4. Commit — no pipeline transforms needed.
    //
    public const string AppVersion  = "4";
    public const int    BuildNumber = 4;

    // ── Changelog ─────────────────────────────────────────────────────────────
    public const string ChangelogResourcePath = "changelog.json";

    // ── Cache TTLs ────────────────────────────────────────────────────────────
    public static readonly TimeSpan AccountsCacheTtl     = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan TransactionsCacheTtl = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan CalendarCacheTtl     = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan CategoriesCacheTtl   = TimeSpan.FromMinutes(30);

    // ── Fallback colours ──────────────────────────────────────────────────────
    public const string FallbackPrimary = "#D4AF37";
    public const string FallbackMuted   = "#59D4AF37";

    // ── Tab titles ────────────────────────────────────────────────────────────
    public const string CalendarTabTitle     = "Calendar";
    public const string TransactionsTabTitle = "Transactions";
    public const string AccountsTabTitle     = "Accounts";
    public const string SettingsTabTitle     = "Settings";
    public const string LoginTabTitle        = "Login";
}
