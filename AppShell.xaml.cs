using HexFund.UI.Services;
using HexFund.UI.Views;

namespace HexFund.UI;

public partial class AppShell : Shell
{
    private readonly IAuthService _authService;
    private readonly ISettingsService _settingsService;
    private readonly IAccountStateService _accountStateService;

    public AppShell(
        IAuthService authService,
        ISettingsService settingsService,
        IAccountStateService accountStateService)
    {
        InitializeComponent();
        _authService = authService;
        _settingsService = settingsService;
        _accountStateService = accountStateService;

        Routing.RegisterRoute("accounts", typeof(AccountsPage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));
        Routing.RegisterRoute("add", typeof(AddEntryPage));

        ApplyThemeToShell();
        _settingsService.SettingsChanged += () =>
            MainThread.BeginInvokeOnMainThread(ApplyThemeToShell);

        _authService.AuthStateChanged += OnAuthStateChanged;
        UpdateTabBarForAuthState(_authService.IsAuthenticated);

        Navigating += OnShellNavigating;
    }

    // ── Hex FAB action — called by App.xaml.cs overlay button ────────────────

    public async Task ExecuteHexFabAsync()
    {
        if (_accountStateService.SelectedAccount == null)
        {
            if (IsPageOpen<Views.AccountsPage>()) return;
            await GoToAsync("accounts");
        }
        else
        {
            if (IsPageOpen<Views.AddEntryPage>()) return;
            await GoToAsync("add");
        }
    }

    // ── Tab switch — close modal pages when the user switches tabs ───────────

    // Handles the cross-tab case where AddEntry, Accounts, or Settings is
    // pushed as a route on top of a tab — pop before switching.
    private void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        var target = e.Target.Location.OriginalString;
        bool isTabSwitch = target.StartsWith("//", StringComparison.Ordinal);

        if (!isTabSwitch) return;

        if (!IsPageOpen<Views.AddEntryPage>() &&
            !IsPageOpen<Views.AccountsPage>()  &&
            !IsPageOpen<Views.SettingsPage>()) return;

        e.Cancel();
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await GoToAsync("..");
            await GoToAsync(e.Target.Location.OriginalString);
        });
    }

    private static bool IsPageOpen<T>() where T : Page
    {
        var stack = Shell.Current?.Navigation?.NavigationStack;
        return stack?.Any(p => p is T) ?? false;
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

    public async Task NavigateToAccountsAsync() => await GoToAsync("accounts");
    public async Task NavigateToSettingsAsync() => await GoToAsync("settings");
    public async Task NavigateToAddEntryAsync() => await GoToAsync("add");

    // ── Auth state ────────────────────────────────────────────────────────────

    private void OnAuthStateChanged() =>
        MainThread.BeginInvokeOnMainThread(() =>
            UpdateTabBarForAuthState(_authService.IsAuthenticated));

    private void UpdateTabBarForAuthState(bool isAuthenticated)
    {
        // Pop any pushed routes (Settings, Accounts, AddEntry) before switching
        // tab bars. If a route is on the stack when CurrentItem changes, Shell
        // tries to resolve it against the new tab bar and throws because the
        // route doesn't exist in that context (e.g. "settings" is not a route
        // in AuthTabBar).
        if (Navigation.NavigationStack.Count > 1)
        {
            // Fire-and-forget the pop, then switch tab bars once it completes
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await GoToAsync("..");
                ApplyTabBarState(isAuthenticated);
            });
            return;
        }

        ApplyTabBarState(isAuthenticated);
    }

    private void ApplyTabBarState(bool isAuthenticated)
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
        if (Navigation.NavigationStack.Count > 1)
            return base.OnBackButtonPressed();

        _ = Navigation.PushModalAsync(new ExitConfirmationPage(), animated: false);
        return true;
    }

    // ── Theme ─────────────────────────────────────────────────────────────────

    private void ApplyThemeToShell()
    {
        var resources = Application.Current?.Resources;
        if (resources == null) return;

        var tabBarBg = Color.FromArgb("#0A0B0D");
        var appBarBg = Color.FromArgb("#141619");
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