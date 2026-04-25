namespace HexFund.UI;

/// <summary>
/// Strongly-typed tab identifiers. Use AppShell.SwitchToTab() instead of
/// searching for tab titles by string — a renamed tab becomes a compile error,
/// not a silent navigation failure.
/// </summary>
public enum AppTab
{
    // ── Authenticated tabs (MainTabBar) ───────────────────────────────────────
    Home,
    Calendar,
    Ledger,
    Insights,

    // ── Auth tabs (AuthTabBar) ────────────────────────────────────────────────
    Login,
}
