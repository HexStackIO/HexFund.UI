using HexFund.UI.Services;
using HexFund.UI.Views;

namespace HexFund.UI;

public partial class AppShell : Shell
{
    private readonly IAuthService _authService;
    private readonly ISettingsService _settingsService;

    public AppShell(IAuthService authService, ISettingsService settingsService)
    {
        InitializeComponent();
        _authService = authService;
        _settingsService = settingsService;

        Routing.RegisterRoute("accounts", typeof(AccountsPage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));
        Routing.RegisterRoute("add",      typeof(AddEntryPage));

        ApplyThemeToShell();
        _settingsService.SettingsChanged += () =>
            MainThread.BeginInvokeOnMainThread(ApplyThemeToShell);

        _authService.AuthStateChanged += OnAuthStateChanged;
        UpdateTabBarForAuthState(_authService.IsAuthenticated);
    }

    // ── Public navigation API ─────────────────────────────────────────────────

    public void SwitchToTab(AppTab tab)
    {
        try
        {
            ShellItem? target = tab switch
            {
                AppTab.Home     => HomeTab,
                AppTab.Calendar => CalendarTab,
                AppTab.Ledger   => LedgerTab,
                AppTab.Insights => InsightsTab,
                AppTab.Login    => LoginTab,
                _               => null
            };

            if (target != null)
                CurrentItem = target;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SwitchToTab({tab}) error: {ex.Message}");
        }
    }

    public async Task NavigateToAccountsAsync() =>
        await GoToAsync("accounts");

    public async Task NavigateToSettingsAsync() =>
        await GoToAsync("settings");

    public async Task NavigateToAddEntryAsync() =>
        await GoToAsync("add");

    // ── Auth state ────────────────────────────────────────────────────────────

    private void OnAuthStateChanged() =>
        MainThread.BeginInvokeOnMainThread(() =>
            UpdateTabBarForAuthState(_authService.IsAuthenticated));

    private void UpdateTabBarForAuthState(bool isAuthenticated)
    {
        if (isAuthenticated)
        {
            MainTabBar.IsVisible = true;
            AuthTabBar.IsVisible = false;
            CurrentItem = MainTabBar;
        }
        else
        {
            MainTabBar.IsVisible = false;
            AuthTabBar.IsVisible = true;
            CurrentItem = AuthTabBar;
        }

#if ANDROID
        Platforms.Android.TabBarTopLineEffect.Reattach();
#elif IOS
        Platforms.iOS.TabBarTopLineEffect.Reattach();
#endif
    }

    // ── Back-button / exit confirmation ──────────────────────────────────────

    protected override bool OnBackButtonPressed()
    {
        // Only intercept at the tab root — let normal back-nav proceed when
        // there is a page on the stack to pop.
        if (Navigation.NavigationStack.Count > 1)
            return base.OnBackButtonPressed();

        // Push the themed exit confirmation as a transparent modal page.
        // This ensures the dialog inherits all DynamicResource theme tokens
        // and matches the in-app modal style used elsewhere in the app.
        _ = Navigation.PushModalAsync(new ExitConfirmationPage(), animated: false);
        return true; // consumed — suppress default OS back behaviour
    }

    // ── Theme ─────────────────────────────────────────────────────────────────

    private void ApplyThemeToShell()
    {
        var resources = Application.Current?.Resources;
        if (resources == null) return;

        var tabBarBg   = Color.FromArgb("#0A0B0D");
        var appBarBg   = Color.FromArgb("#141619");
        var titleColor = Color.FromArgb("#EDEEF0");

        var selectedColor = Color.FromArgb("#FFE8A3");
        if (resources.TryGetValue("AccentInk", out var inkVal) && inkVal is Color ink)
            selectedColor = ink;

        var unselectedColor = Color.FromArgb("#8B8F98");
        if (resources.TryGetValue("TextSecondary", out var dimVal) && dimVal is Color dim)
            unselectedColor = dim;

        Shell.SetBackgroundColor(this, appBarBg);
        Shell.SetTitleColor(this, titleColor);
        Shell.SetForegroundColor(this, titleColor);
        Shell.SetTabBarBackgroundColor(this, tabBarBg);
        Shell.SetTabBarTitleColor(this, selectedColor);
        Shell.SetTabBarUnselectedColor(this, unselectedColor);
    }
}
