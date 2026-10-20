namespace HexFund.UI;

/// <summary>
/// Identifies the tabs in AppShell. Use <c>AppShell.SwitchToTab</c> instead of looking tabs up by
/// title, so a renamed tab fails at compile time instead of silently breaking navigation.
/// </summary>
public enum AppTab
{
    // Tabs in MainTabBar, shown when signed in.
    Home,
    Calendar,
    Ledger,
    Insights,

    // Tab in AuthTabBar, shown when signed out.
    Login,
}
