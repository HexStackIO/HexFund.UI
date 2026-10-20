using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HexFund.UI.Models;
using HexFund.UI.Services;
using System.Collections.ObjectModel;

namespace HexFund.UI.ViewModels;

/// <summary>
/// Drives the dashboard: the selected account's balance and month net change, the top three
/// active accounts by balance, and the five most recent and five upcoming occurrences across all
/// active accounts. Also owns the first-run onboarding prompts.
/// </summary>
/// <remarks>
/// Loads use cached API responses unless a refresh is requested. A change of the selected account
/// or any transaction mutation reloads the dashboard.
/// </remarks>
public partial class HomeViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly IAccountStateService _accountStateService;

    [ObservableProperty] private bool isLoading = true;
    [ObservableProperty] private bool hasError;
    [ObservableProperty] private string? errorMessage;

    public string SelectedAccountName =>
        _accountStateService.SelectedAccount?.AccountName ?? "No Account";

    public Guid? SelectedAccountId =>
        _accountStateService.SelectedAccount?.AccountId;

    [ObservableProperty] private decimal totalBalance;
    [ObservableProperty] private decimal monthNetChange;
    [ObservableProperty] private bool isMonthNetPositive;
    [ObservableProperty] private bool hasBalanceData;

    public string TotalBalanceDisplay => TotalBalance.ToString("C");
    public string MonthNetChangeDisplay =>
        $"{(IsMonthNetPositive ? "+" : "")}{MonthNetChange:C}";
    public string MonthNetChangeArrow => IsMonthNetPositive ? "↑" : "↓";

    public Color MonthNetChangeColor =>
        GetSemanticColor(IsMonthNetPositive ? "Up" : "Down",
                         IsMonthNetPositive ? "#3FB97A" : "#E05A6F");

    [ObservableProperty] private ObservableCollection<Account> previewAccounts = new();
    [ObservableProperty] private bool hasAccounts;

    [ObservableProperty] private ObservableCollection<RecentTransaction> recentTransactions = new();
    [ObservableProperty] private bool hasRecentTransactions;
    [ObservableProperty] private bool isRecentExpanded = true;

    [ObservableProperty] private ObservableCollection<RecentTransaction> upcomingTransactions = new();
    [ObservableProperty] private bool hasUpcomingTransactions;
    [ObservableProperty] private bool isUpcomingExpanded = true;

    public HomeViewModel(
        IApiService apiService,
        IAccountStateService accountStateService,
        ISettingsService settingsService)
        : base(settingsService)
    {
        _apiService = apiService;
        _accountStateService = accountStateService;

        _accountStateService.SelectedAccountChanged += OnSelectedAccountChanged;
        _accountStateService.TransactionsChanged += OnTransactionsChanged;
    }

    /// <summary>Called from HomePage.OnAppearing.</summary>
    public async Task InitializeAsync()
    {
        // Onboarding is re-evaluated on every visit until completed, which needs fresh data.
        bool needsOnboardingCheck = !SettingsService.HasCompletedOnboarding;

        if (!IsLoading && HasBalanceData && !needsOnboardingCheck) return;

        await LoadAsync(forceRefresh: needsOnboardingCheck);
    }

    private void OnSelectedAccountChanged() =>
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            OnPropertyChanged(nameof(SelectedAccountName));
            OnPropertyChanged(nameof(SelectedAccountId));
            await LoadAsync();
        });

    // A transaction changed anywhere in the app, so the balance must be reloaded from the server.
    private void OnTransactionsChanged() =>
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync(forceRefresh: true));

    private async Task LoadAsync(bool forceRefresh = false)
    {
        IsLoading = true;
        HasError = false;

        try
        {
            var accounts = await _apiService.GetAccountsAsync(forceRefresh: forceRefresh);

            if (accounts == null || accounts.Count == 0)
            {
                SetEmptyState();
                return;
            }

            var activeAccounts = accounts.Where(a => a.IsActive).ToList();

            // Falls back to the first active account so the balance is never blank after sign-in.
            var selectedAccount = _accountStateService.SelectedAccount
                ?? activeAccounts.FirstOrDefault();

            if (selectedAccount == null)
            {
                SetEmptyState();
                return;
            }

            // The selected account object may be stale, so read the balance from the fetched list.
            var freshSelected = activeAccounts
                .FirstOrDefault(a => a.AccountId == selectedAccount.AccountId)
                ?? selectedAccount;

            TotalBalance = freshSelected.CurrentBalance;

            // Scoped to the selected account to match the Calendar and Transactions tabs.
            var now = DateTime.Today;
            var overview = await _apiService.GetMonthlyOverviewAsync(
                freshSelected.AccountId, now.Year, now.Month, forceRefresh: forceRefresh);

            var monthNet = overview?.NetChange ?? 0m;

            MonthNetChange = monthNet;
            IsMonthNetPositive = monthNet >= 0;
            HasBalanceData = true;

            OnPropertyChanged(nameof(TotalBalanceDisplay));
            OnPropertyChanged(nameof(MonthNetChangeDisplay));
            OnPropertyChanged(nameof(MonthNetChangeArrow));
            OnPropertyChanged(nameof(MonthNetChangeColor));

            var top3 = activeAccounts
                .OrderByDescending(a => a.CurrentBalance)
                .Take(3)
                .ToList();

            PreviewAccounts = new ObservableCollection<Account>(top3);
            HasAccounts = top3.Count > 0;

            // Previous, current and next month are fetched in parallel for every account. The
            // responses are cached client-side, so repeat loads are cheap.
            var prevMonthDate = now.AddMonths(-1);
            var nextMonthDate = now.AddMonths(1);

            var monthTasks = activeAccounts.SelectMany(a => new[]
            {
                _apiService.GetTransactionsForMonthAsync(a.AccountId, prevMonthDate.Year, prevMonthDate.Month),
                _apiService.GetTransactionsForMonthAsync(a.AccountId, now.Year, now.Month),
                _apiService.GetTransactionsForMonthAsync(a.AccountId, nextMonthDate.Year, nextMonthDate.Month),
            }).ToList();

            var monthResults = await Task.WhenAll(monthTasks);

            // Results are ordered account by account, three months each.
            var allOccurrences = new List<(TransactionOccurrence Occ, string AccountName)>();
            for (int i = 0; i < activeAccounts.Count; i++)
            {
                var accountName = activeAccounts[i].AccountName;
                void Flatten(Dictionary<DateTime, List<TransactionOccurrence>>? dict)
                {
                    if (dict == null) return;
                    foreach (var occs in dict.Values)
                        foreach (var occ in occs)
                            allOccurrences.Add((occ, accountName));
                }
                Flatten(monthResults[i * 3]);
                Flatten(monthResults[i * 3 + 1]);
                Flatten(monthResults[i * 3 + 2]);
            }

            // Recent: on or before today, newest first.
            var top5Recent = allOccurrences
                .Where(x => x.Occ.OccurrenceDate.Date <= now)
                .OrderByDescending(x => x.Occ.OccurrenceDate)
                .Take(5)
                .Select(x => new RecentTransaction
                {
                    Description = x.Occ.Description,
                    Amount = x.Occ.Amount,
                    Date = x.Occ.OccurrenceDate,
                    ColorHex = x.Occ.Color,
                    AccountName = x.AccountName,
                })
                .ToList();

            // Upcoming: after today, soonest first.
            var top5Upcoming = allOccurrences
                .Where(x => x.Occ.OccurrenceDate.Date > now)
                .OrderBy(x => x.Occ.OccurrenceDate)
                .Take(5)
                .Select(x => new RecentTransaction
                {
                    Description = x.Occ.Description,
                    Amount = x.Occ.Amount,
                    Date = x.Occ.OccurrenceDate,
                    ColorHex = x.Occ.Color,
                    AccountName = x.AccountName,
                })
                .ToList();

            RecentTransactions = new ObservableCollection<RecentTransaction>(top5Recent);
            HasRecentTransactions = top5Recent.Count > 0;
            UpcomingTransactions = new ObservableCollection<RecentTransaction>(top5Upcoming);
            HasUpcomingTransactions = top5Upcoming.Count > 0;

            CheckOnboarding(hasAccounts: true, hasTransactions: top5Recent.Count > 0);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HomeViewModel.LoadAsync error: {ex.Message}");
            HasError = true;
            ErrorMessage = "Unable to load dashboard. Pull to refresh.";
            SetEmptyState();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SetEmptyState()
    {
        PreviewAccounts = new();
        RecentTransactions = new();
        UpcomingTransactions = new();
        HasAccounts = false;
        HasRecentTransactions = false;
        HasUpcomingTransactions = false;
        HasBalanceData = false;

        CheckOnboarding(hasAccounts: false, hasTransactions: false);
    }

    [ObservableProperty] private bool showOnboarding;
    // 1 = create an account, 2 = add a transaction.
    [ObservableProperty] private int onboardingStep;

    public bool IsOnboardingStep1 => OnboardingStep == 1;
    public bool IsOnboardingStep2 => OnboardingStep == 2;

    private void CheckOnboarding(bool hasAccounts, bool hasTransactions)
    {
        if (SettingsService.HasCompletedOnboarding) return;

        if (!hasAccounts)
        {
            OnboardingStep = 1;
            ShowOnboarding = true;
        }
        else if (!hasTransactions)
        {
            OnboardingStep = 2;
            ShowOnboarding = true;
        }
        else
        {
            // The user has an account and a transaction, so onboarding never shows again.
            CompleteOnboarding();
        }
    }

    private void CompleteOnboarding()
    {
        ShowOnboarding = false;
        SettingsService.HasCompletedOnboarding = true;
    }

    [RelayCommand]
    private async Task OnboardingGoToAccountsAsync()
    {
        // Completion is decided when the user returns to Home: the reload advances to step 2 if an
        // account was created and stays on step 1 otherwise.
        ShowOnboarding = false;
        await GoToAccountsAsync();
    }

    [RelayCommand]
    private async Task OnboardingGoToAddAsync()
    {
        // As above, the reload on return decides whether onboarding is complete.
        ShowOnboarding = false;
        await GoToDepositAsync();
    }

    [RelayCommand]
    private void DismissOnboarding() => CompleteOnboarding();

    /// <summary>Opens the add-entry page.</summary>
    [RelayCommand]
    private async Task GoToDepositAsync() =>
        await Shell.Current.GoToAsync("add");

    [RelayCommand]
    private async Task GoToAccountsAsync() =>
        await Shell.Current.GoToAsync("accounts");

    [RelayCommand]
    private void GoToCalendar()
    {
        if (Shell.Current is AppShell appShell)
            appShell.SwitchToTab(AppTab.Calendar);
    }

    [RelayCommand]
    private async Task GoToSettingsAsync()
    {
        // If Settings is already open (a double tap on the gear), pop it instead of pushing a
        // second copy.
        var stack = Shell.Current?.Navigation?.NavigationStack;
        if (stack != null && stack.Any(p => p is Views.SettingsPage))
        {
            await Shell.Current.GoToAsync("..");
            return;
        }
        await Shell.Current.GoToAsync("settings");
    }

    /// <summary>Tap an account row in the Home preview to select that account.</summary>
    [RelayCommand]
    private async Task SelectAccountAsync(Account account)
    {
        if (account == null) return;
        _accountStateService.SelectedAccount = account;
        // The reload comes from OnSelectedAccountChanged; this updates the selection indicator
        // immediately.
        OnPropertyChanged(nameof(SelectedAccountId));
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync(forceRefresh: true);

    [RelayCommand] private void ToggleRecentSection() => IsRecentExpanded = !IsRecentExpanded;
    [RelayCommand] private void ToggleUpcomingSection() => IsUpcomingExpanded = !IsUpcomingExpanded;

    partial void OnTotalBalanceChanged(decimal value) => OnPropertyChanged(nameof(TotalBalanceDisplay));
    partial void OnMonthNetChangeChanged(decimal value) => OnPropertyChanged(nameof(MonthNetChangeDisplay));
    partial void OnOnboardingStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsOnboardingStep1));
        OnPropertyChanged(nameof(IsOnboardingStep2));
    }
    partial void OnIsMonthNetPositiveChanged(bool value)
    {
        OnPropertyChanged(nameof(MonthNetChangeDisplay));
        OnPropertyChanged(nameof(MonthNetChangeArrow));
        OnPropertyChanged(nameof(MonthNetChangeColor));
    }

    private static Color GetSemanticColor(string key, string fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c)
            return c;
        return Color.FromArgb(fallback);
    }
}

/// <summary>
/// A row in the recent or upcoming activity lists on the Home page.
/// </summary>
public class RecentTransaction
{
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime Date { get; init; }
    public string? ColorHex { get; init; }
    public string AccountName { get; init; } = string.Empty;

    public string AmountDisplay => $"{(Amount >= 0 ? "+" : "")}{Amount:C}";
    public string DateDisplay => Date.ToString("MMM dd");

    public Color AmountColor =>
        Amount >= 0
            ? GetSemanticColor("Up", "#3FB97A")
            : GetSemanticColor("Down", "#E05A6F");

    public Color AccentColor =>
        ColorHex != null
            ? Microsoft.Maui.Graphics.Color.FromArgb(ColorHex)
            : AmountColor;

    private static Color GetSemanticColor(string key, string fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c)
            return c;
        return Microsoft.Maui.Graphics.Color.FromArgb(fallback);
    }
}