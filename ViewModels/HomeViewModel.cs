using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HexFund.UI.Models;
using HexFund.UI.Services;
using System.Collections.ObjectModel;

namespace HexFund.UI.ViewModels;

/// <summary>
/// HomeViewModel — Sprint 03 full build.
///
/// Sections:
///   BalanceHero  — total balance across all accounts + month-over-month net change
///   QuickActions — Deposit (→ Ledger Add dialog), Accounts, Calendar
///   AccountsPreview — top 3 active accounts by balance
///   RecentActivity  — top 5 most-recent TransactionOccurrences across all accounts
///
/// Data strategy: all calls use cached paths (forceRefresh=false) so navigating
/// Home repeatedly doesn't hammer the API. The SelectedAccountChanged event
/// triggers a refresh when the user switches accounts.
/// </summary>
public partial class HomeViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly IAccountStateService _accountStateService;

    // ── Loading / error state ─────────────────────────────────────────────────
    [ObservableProperty] private bool isLoading = true;
    [ObservableProperty] private bool hasError;
    [ObservableProperty] private string? errorMessage;

    // ── Selected account display ──────────────────────────────────────────────
    public string SelectedAccountName =>
        _accountStateService.SelectedAccount?.AccountName ?? "No Account";

    public Guid? SelectedAccountId =>
        _accountStateService.SelectedAccount?.AccountId;

    // ── Balance hero ──────────────────────────────────────────────────────────
    [ObservableProperty] private decimal totalBalance;
    [ObservableProperty] private decimal monthNetChange;
    [ObservableProperty] private bool isMonthNetPositive;
    [ObservableProperty] private bool hasBalanceData;

    public string TotalBalanceDisplay    => TotalBalance.ToString("C");
    public string MonthNetChangeDisplay  =>
        $"{(IsMonthNetPositive ? "+" : "")}{MonthNetChange:C}";
    public string MonthNetChangeArrow    => IsMonthNetPositive ? "↑" : "↓";

    public Color MonthNetChangeColor =>
        GetSemanticColor(IsMonthNetPositive ? "Up" : "Down",
                         IsMonthNetPositive ? "#3FB97A" : "#E05A6F");

    // ── Accounts preview ──────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<Account> previewAccounts = new();
    [ObservableProperty] private bool hasAccounts;

    // ── Recent activity ───────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<RecentTransaction> recentTransactions = new();
    [ObservableProperty] private bool hasRecentTransactions;
    [ObservableProperty] private bool isRecentExpanded    = true;

    // ── Upcoming transactions ─────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<RecentTransaction> upcomingTransactions = new();
    [ObservableProperty] private bool hasUpcomingTransactions;
    [ObservableProperty] private bool isUpcomingExpanded  = true;

    // ── Constructor ───────────────────────────────────────────────────────────

    public HomeViewModel(
        IApiService apiService,
        IAccountStateService accountStateService,
        ISettingsService settingsService)
        : base(settingsService)
    {
        _apiService = apiService;
        _accountStateService = accountStateService;

        _accountStateService.SelectedAccountChanged += OnSelectedAccountChanged;
        _accountStateService.TransactionsChanged    += OnTransactionsChanged;
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    /// <summary>Called from HomePage.OnAppearing.</summary>
    public async Task InitializeAsync()
    {
        if (!IsLoading && HasBalanceData) return; // already loaded, skip
        await LoadAsync();
    }

    private void OnSelectedAccountChanged() =>
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            OnPropertyChanged(nameof(SelectedAccountName));
            OnPropertyChanged(nameof(SelectedAccountId));
            await LoadAsync();
        });

    /// <summary>
    /// Fires when a transaction is created, edited, or deleted anywhere in the app.
    /// Force-refreshes so the balance hero reflects the updated account balance.
    /// </summary>
    private void OnTransactionsChanged() =>
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync(forceRefresh: true));

    // ── Data loading ──────────────────────────────────────────────────────────

    private async Task LoadAsync(bool forceRefresh = false)
    {
        IsLoading = true;
        HasError  = false;

        try
        {
            var accounts = await _apiService.GetAccountsAsync(forceRefresh: forceRefresh);

            if (accounts == null || accounts.Count == 0)
            {
                SetEmptyState();
                return;
            }

            var activeAccounts = accounts.Where(a => a.IsActive).ToList();

            // ── Balance hero ─────────────────────────────────────────────
            // Use the selected account if one is set; fall back to the first
            // active account so the hero is never blank after login.
            var selectedAccount = _accountStateService.SelectedAccount
                ?? activeAccounts.FirstOrDefault();

            if (selectedAccount == null)
            {
                SetEmptyState();
                return;
            }

            // Refresh the selected account's balance from the fetched list so
            // we always show the server value, not a stale cached object.
            var freshSelected = activeAccounts
                .FirstOrDefault(a => a.AccountId == selectedAccount.AccountId)
                ?? selectedAccount;

            TotalBalance = freshSelected.CurrentBalance;

            // Month-over-month: scoped to the selected account only, matching
            // what Calendar and Ledger are displaying.
            var now = DateTime.Today;
            var overview = await _apiService.GetMonthlyOverviewAsync(
                freshSelected.AccountId, now.Year, now.Month, forceRefresh: forceRefresh);

            var monthNet = overview?.NetChange ?? 0m;

            MonthNetChange      = monthNet;
            IsMonthNetPositive  = monthNet >= 0;
            HasBalanceData      = true;

            OnPropertyChanged(nameof(TotalBalanceDisplay));
            OnPropertyChanged(nameof(MonthNetChangeDisplay));
            OnPropertyChanged(nameof(MonthNetChangeArrow));
            OnPropertyChanged(nameof(MonthNetChangeColor));

            // ── Accounts preview (top 3 by current balance) ──────────────
            var top3 = activeAccounts
                .OrderByDescending(a => a.CurrentBalance)
                .Take(3)
                .ToList();

            PreviewAccounts = new ObservableCollection<Account>(top3);
            HasAccounts     = top3.Count > 0;

            // ── Recent + Upcoming (top 5 each, across all active accounts) ──
            // Fetch prev month + this month + next month in parallel per account.
            // GetTransactionsForMonthAsync returns Dictionary<DateTime, List<TransactionOccurrence>>
            // and is server-cached at 2 min, so these calls are cheap.
            var prevMonthDate = now.AddMonths(-1);
            var nextMonthDate = now.AddMonths(1);

            var monthTasks = activeAccounts.SelectMany(a => new[]
            {
                _apiService.GetTransactionsForMonthAsync(a.AccountId, prevMonthDate.Year, prevMonthDate.Month),
                _apiService.GetTransactionsForMonthAsync(a.AccountId, now.Year, now.Month),
                _apiService.GetTransactionsForMonthAsync(a.AccountId, nextMonthDate.Year, nextMonthDate.Month),
            }).ToList();

            var monthResults = await Task.WhenAll(monthTasks);

            // Flatten all occurrences with their account name
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

            // Recent: occurrences on or before today, newest first, top 5
            var top5Recent = allOccurrences
                .Where(x => x.Occ.OccurrenceDate.Date <= now)
                .OrderByDescending(x => x.Occ.OccurrenceDate)
                .Take(5)
                .Select(x => new RecentTransaction
                {
                    Description = x.Occ.Description,
                    Amount      = x.Occ.Amount,
                    Date        = x.Occ.OccurrenceDate,
                    ColorHex    = x.Occ.Color,
                    AccountName = x.AccountName,
                })
                .ToList();

            // Upcoming: occurrences after today, soonest first, top 5
            var top5Upcoming = allOccurrences
                .Where(x => x.Occ.OccurrenceDate.Date > now)
                .OrderBy(x => x.Occ.OccurrenceDate)
                .Take(5)
                .Select(x => new RecentTransaction
                {
                    Description = x.Occ.Description,
                    Amount      = x.Occ.Amount,
                    Date        = x.Occ.OccurrenceDate,
                    ColorHex    = x.Occ.Color,
                    AccountName = x.AccountName,
                })
                .ToList();

            RecentTransactions      = new ObservableCollection<RecentTransaction>(top5Recent);
            HasRecentTransactions   = top5Recent.Count > 0;
            UpcomingTransactions    = new ObservableCollection<RecentTransaction>(top5Upcoming);
            HasUpcomingTransactions = top5Upcoming.Count > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HomeViewModel.LoadAsync error: {ex.Message}");
            HasError     = true;
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
        PreviewAccounts         = new();
        RecentTransactions      = new();
        UpcomingTransactions    = new();
        HasAccounts             = false;
        HasRecentTransactions   = false;
        HasUpcomingTransactions = false;
        HasBalanceData          = false;
    }

    // ── Quick action commands ─────────────────────────────────────────────────

    /// <summary>Add quick action — opens the AddEntryPage bottom sheet.</summary>
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
    private async Task GoToSettingsAsync() =>
        await Shell.Current.GoToAsync("settings");

    /// <summary>Tap an account row in the Home preview to select that account.</summary>
    [RelayCommand]
    private async Task SelectAccountAsync(Account account)
    {
        if (account == null) return;
        _accountStateService.SelectedAccount = account;
        // LoadAsync is triggered by OnSelectedAccountChanged, but we also need
        // to notify SelectedAccountId so the indicator updates immediately.
        OnPropertyChanged(nameof(SelectedAccountId));
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync(forceRefresh: true);

    [RelayCommand] private void ToggleRecentSection()   => IsRecentExpanded   = !IsRecentExpanded;
    [RelayCommand] private void ToggleUpcomingSection() => IsUpcomingExpanded = !IsUpcomingExpanded;

    // ── Helpers ───────────────────────────────────────────────────────────────

    partial void OnTotalBalanceChanged(decimal value)      => OnPropertyChanged(nameof(TotalBalanceDisplay));
    partial void OnMonthNetChangeChanged(decimal value)    => OnPropertyChanged(nameof(MonthNetChangeDisplay));
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
/// Lightweight display model for recent activity rows.
/// Decouples HomePage from the full TransactionOccurrence model.
/// </summary>
public class RecentTransaction
{
    public string Description { get; init; } = string.Empty;
    public decimal Amount      { get; init; }
    public DateTime Date       { get; init; }
    public string? ColorHex    { get; init; }
    public string AccountName  { get; init; } = string.Empty;

    public string AmountDisplay => $"{(Amount >= 0 ? "+" : "")}{Amount:C}";
    public string DateDisplay   => Date.ToString("MMM dd");

    public Color AmountColor =>
        Amount >= 0
            ? GetSemanticColor("Up",   "#3FB97A")
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
