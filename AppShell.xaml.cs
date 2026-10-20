using HexFund.UI.Services;
using HexFund.UI.Views;

namespace HexFund.UI;

/// <summary>
/// The navigation shell. Shows the main tab bar when signed in and the login tab bar otherwise,
/// registers the pushed routes and handles the Android back button on the root page.
/// </summary>
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
        Routing.RegisterRoute("feedback", typeof(FeedbackPage));

        ApplyThemeToShell();
        _settingsService.SettingsChanged += () =>
            MainThread.BeginInvokeOnMainThread(ApplyThemeToShell);

        _authService.AuthStateChanged += OnAuthStateChanged;
        UpdateTabBarForAuthState(_authService.IsAuthenticated);

        Navigating += OnShellNavigating;
    }

    /// <summary>
    /// Handles a tap on the floating button: the add-entry page when an account is selected,
    /// otherwise the accounts page. Does nothing if that page is already open.
    /// </summary>
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

    // A tab switch while the add-entry, accounts or settings route is pushed must pop it first, so
    // the switch is cancelled, the route popped and then the switch repeated.
    private void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        var target = e.Target.Location.OriginalString;
        bool isTabSwitch = target.StartsWith("//", StringComparison.Ordinal);

        if (!isTabSwitch) return;

        if (!IsPageOpen<Views.AddEntryPage>() &&
            !IsPageOpen<Views.AccountsPage>()  &&
            !IsPageOpen<Views.SettingsPage>()  &&
            !IsPageOpen<Views.FeedbackPage>()) return;

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

    /// <summary>Selects a tab. Failures are logged and ignored.</summary>
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

    private void OnAuthStateChanged() =>
        MainThread.BeginInvokeOnMainThread(() =>
            UpdateTabBarForAuthState(_authService.IsAuthenticated));

    private void UpdateTabBarForAuthState(bool isAuthenticated)
    {
        // Pushed routes must be popped before the tab bar changes. Shell would otherwise try to
        // resolve the route against the new tab bar and throw, because for example "settings" does
        // not exist in AuthTabBar.
        if (Navigation.NavigationStack.Count > 1)
        {
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

    // On a root page the back button asks for exit confirmation instead of closing the app.
    protected override bool OnBackButtonPressed()
    {
        if (Navigation.NavigationStack.Count > 1)
            return base.OnBackButtonPressed();

        _ = Navigation.PushModalAsync(new ExitConfirmationPage(), animated: false);
        return true;
    }

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