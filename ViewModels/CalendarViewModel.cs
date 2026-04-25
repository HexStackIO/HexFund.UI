using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HexFund.UI.Models;
using HexFund.UI.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace HexFund.UI.ViewModels;

internal record MonthData(
    EnhancedMonthlyOverview Overview,
    Dictionary<DateTime, List<TransactionOccurrence>> Transactions,
    DateTime FetchedAt)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    public bool IsStale => DateTime.UtcNow - FetchedAt > Ttl;
}

public partial class CalendarViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly IAccountStateService _accountStateService;

    // -- Summary card ---------------------------------------------------------
    [ObservableProperty] private string startingBalanceDisplay = "$0.00";
    [ObservableProperty] private string endingBalanceDisplay   = "$0.00";
    [ObservableProperty] private string totalIncomeDisplay     = "$0.00";
    [ObservableProperty] private string totalExpensesDisplay   = "$0.00";
    [ObservableProperty] private string netChangeDisplay       = "$0.00";
    [ObservableProperty] private Color  netChangeColor         = Colors.Gray;

    public ObservableCollection<CalendarGridDay> GridDays { get; } = new(
        Enumerable.Range(0, 42).Select(_ => new CalendarGridDay()));

    private static readonly Color ChipIncomeColor  = Color.FromArgb("#E8F5E9");
    private static readonly Color ChipExpenseColor = Color.FromArgb("#FFEBEE");

    [ObservableProperty] private ObservableCollection<TransactionOccurrence> selectedDayTransactions = new();
    [ObservableProperty] private decimal selectedDayIncome;
    [ObservableProperty] private decimal selectedDayExpenses;
    [ObservableProperty] private decimal selectedDayNetChange;
    [ObservableProperty] private Color   selectedDayNetChangeColor = Colors.Gray;
    [ObservableProperty] private decimal selectedDayBalance;
    [ObservableProperty] private Color   selectedDayBalanceColor   = Colors.Gray;

    [ObservableProperty] private DateTime currentMonth;
    [ObservableProperty] private string   monthYearDisplay = string.Empty;
    [ObservableProperty] private Account? selectedAccount;
    [ObservableProperty] private bool     hasAccount;

    [ObservableProperty] private bool isLoading;

    [ObservableProperty] private DateTime? selectedDate;
    [ObservableProperty] private bool      isTransactionDetailVisible;
    [ObservableProperty] private string    selectedDateDisplay = string.Empty;

    [ObservableProperty] private string noAccountMessage =
        "No account selected. Please select or create an account.";

    [ObservableProperty] private bool   isMonthPickerVisible;
    [ObservableProperty] private string selectedMonthName = string.Empty;
    [ObservableProperty] private int    selectedYear;
    [ObservableProperty] private List<int> availableYears = new();

    public List<string> AvailableMonths { get; } = new()
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    };

    public List<string> DayHeaders { get; } = new()
        { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

    // Single unified cache: (accountId, year, month) -> MonthData.
    private readonly Dictionary<(Guid, int, int), MonthData> _monthCache = new();
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _prefetchCts;
    private readonly List<CalendarEventChip> _chipScratchpad = new(2);

    public CalendarViewModel(
        IApiService apiService,
        IAccountStateService accountStateService,
        ISettingsService settingsService)
        : base(settingsService)
    {
        _apiService = apiService;
        _accountStateService = accountStateService;

        CurrentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        UpdateMonthDisplay();

        var currentYear = DateTime.Today.Year;
        for (int year = currentYear - 5; year <= currentYear + 2; year++)
            AvailableYears.Add(year);

        SelectedYear = currentYear;
        SelectedMonthName = AvailableMonths[DateTime.Today.Month - 1];

        _accountStateService.SelectedAccountChanged += OnSelectedAccountChanged;
        _accountStateService.TransactionsChanged    += OnTransactionsChanged;

        settingsService.SettingsChanged += OnSettingsChanged;

        if (_accountStateService.SelectedAccount != null)
        {
            SelectedAccount = _accountStateService.SelectedAccount;
            HasAccount = true;
        }
    }

    private void OnSettingsChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
                foreach (var cell in GridDays)
                cell.NotifyCellBackground();
        });
    }

    private void OnSelectedAccountChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SelectedAccount = _accountStateService.SelectedAccount;
            HasAccount = SelectedAccount != null;

            if (SelectedAccount != null)
            {
                CancelAllBackgroundWork();
                ClearCache();
                LoadCurrentMonth(forceRefresh: true);
            }
            else
            {
                ResetDisplayedData();
            }
        });
    }

    private void OnTransactionsChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (SelectedAccount == null) return;
            CancelAllBackgroundWork();
            ClearCache();
            _apiService.InvalidateCalendarCache(SelectedAccount.AccountId);
            LoadCurrentMonth(forceRefresh: true);
        });
    }

    public Task InitializeAsync()
    {
        if (_accountStateService.SelectedAccount == null)
        {
            HasAccount = false;
            return Task.CompletedTask;
        }

        SelectedAccount = _accountStateService.SelectedAccount;
        HasAccount = true;
        LoadCurrentMonth(forceRefresh: false);

        return Task.CompletedTask;
    }

    [RelayCommand]
    private void PreviousMonth()
    {
        CurrentMonth = CurrentMonth.AddMonths(-1);
        UpdateMonthDisplay();
        UpdatePickerValues();
        LoadCurrentMonth(forceRefresh: false);
    }

    [RelayCommand]
    private void NextMonth()
    {
        CurrentMonth = CurrentMonth.AddMonths(1);
        UpdateMonthDisplay();
        UpdatePickerValues();
        LoadCurrentMonth(forceRefresh: false);
    }

    [RelayCommand]
    private void Today()
    {
        CurrentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        UpdateMonthDisplay();
        UpdatePickerValues();
        LoadCurrentMonth(forceRefresh: false);
    }

    [RelayCommand]
    private void ShowMonthPicker() => IsMonthPickerVisible = true;

    [RelayCommand]
    private void CancelMonthPicker()
    {
        IsMonthPickerVisible = false;
        UpdatePickerValues();
    }

    [RelayCommand]
    private void ApplyMonthSelection()
    {
        var monthIndex = AvailableMonths.IndexOf(SelectedMonthName) + 1;
        var newMonth   = new DateTime(SelectedYear, monthIndex, 1);
        IsMonthPickerVisible = false;

        if (newMonth == CurrentMonth) return;

        CurrentMonth = newMonth;
        UpdateMonthDisplay();
        LoadCurrentMonth(forceRefresh: false);
    }

    [RelayCommand]
    private void Refresh()
    {
        CancelAllBackgroundWork();
        ClearCache();
        LoadCurrentMonth(forceRefresh: true);
    }

    private void LoadCurrentMonth(bool forceRefresh)
    {
        if (SelectedAccount == null) { ResetDisplayedData(); return; }

        var account = SelectedAccount;
        var month   = CurrentMonth;
        var key     = (account.AccountId, month.Year, month.Month);

        _prefetchCts?.Cancel();
        _prefetchCts?.Dispose();
        _prefetchCts = null;

        if (!forceRefresh && _monthCache.TryGetValue(key, out var cached))
        {
            ApplyMonthData(cached, month);
            SchedulePrefetch(account.AccountId, month, forceRefresh: false);

            if (cached.IsStale)
            {
                var capturedMonth   = month;
                var capturedAccount = account;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var fresh = await FetchMonthDataAsync(
                            capturedAccount.AccountId, capturedMonth, true, CancellationToken.None);
                        if (fresh != null)
                        {
                            _monthCache[key] = fresh;
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                if (CurrentMonth == capturedMonth)
                                    ApplyMonthData(fresh, capturedMonth);
                            });
                        }
                    }
                    catch { }
                });
            }
            return;
        }

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        IsLoading = true;

        _ = Task.Run(async () =>
        {
            try
            {
                var data = await FetchMonthDataAsync(account.AccountId, month, forceRefresh, token);

                if (token.IsCancellationRequested) return;

                if (data != null)
                {
                    _monthCache[key] = data;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (CurrentMonth != month) return;
                        IsLoading = false;
                        ApplyMonthData(data, month);
                        SchedulePrefetch(account.AccountId, month, forceRefresh: false);
                    });
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (CurrentMonth != month) return;
                        IsLoading = false;
                        ResetDisplayedData();
                    });
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (CurrentMonth == month) IsLoading = false;
                });
            }
        }, token);
    }

    private async Task<MonthData?> FetchMonthDataAsync(
        Guid accountId, DateTime month, bool forceRefresh, CancellationToken token)
    {
        var overview = await _apiService.GetMonthlyOverviewAsync(
            accountId, month.Year, month.Month, forceRefresh);

        if (overview == null || token.IsCancellationRequested) return null;

        var fetched = await _apiService.GetTransactionsForMonthAsync(
            accountId, month.Year, month.Month, forceRefresh);

        if (token.IsCancellationRequested) return null;

        var transactions = fetched ?? new Dictionary<DateTime, List<TransactionOccurrence>>();

        return new MonthData(overview, transactions ?? new(), DateTime.UtcNow);
    }

    private void SchedulePrefetch(Guid accountId, DateTime currentMonth, bool forceRefresh)
    {
        var cts = new CancellationTokenSource();
        _prefetchCts = cts;

        _ = Task.Delay(500, cts.Token).ContinueWith(async _ =>
        {
            if (cts.Token.IsCancellationRequested) return;
            await PrefetchMonthAsync(accountId, currentMonth.AddMonths(-1), forceRefresh, cts.Token);
            await PrefetchMonthAsync(accountId, currentMonth.AddMonths(+1), forceRefresh, cts.Token);
        }, cts.Token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }

    private async Task PrefetchMonthAsync(
        Guid accountId, DateTime month, bool forceRefresh, CancellationToken token)
    {
        if (accountId == Guid.Empty || token.IsCancellationRequested) return;

        var key = (accountId, month.Year, month.Month);
        if (!forceRefresh && _monthCache.ContainsKey(key)) return;

        try
        {
            var data = await FetchMonthDataAsync(accountId, month, forceRefresh, token);
            if (data != null && !token.IsCancellationRequested)
                _monthCache[key] = data;
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private void ApplyMonthData(MonthData data, DateTime month)
    {
        StartingBalanceDisplay = data.Overview.StartingBalance.ToString("C");
        EndingBalanceDisplay   = data.Overview.EndingBalance.ToString("C");
        TotalIncomeDisplay     = data.Overview.TotalIncome.ToString("C");
        TotalExpensesDisplay   = data.Overview.TotalExpenses.ToString("C");
        NetChangeDisplay       = $"{(data.Overview.NetChange >= 0 ? "+" : "")}{data.Overview.NetChange:C}";
        NetChangeColor         = data.Overview.NetChange >= 0 ? Colors.Green : Colors.Red;

        UpdateGridCells(data.Overview.DailyBreakdown, data.Transactions, month);

        if (SelectedDate.HasValue && IsTransactionDetailVisible
            && SelectedDate.Value.Year  == month.Year
            && SelectedDate.Value.Month == month.Month)
        {
            RefreshDayDetail(SelectedDate.Value, data);
        }
    }

    private void UpdateGridCells(
        List<DailyBalanceSnapshot> snapshots,
        Dictionary<DateTime, List<TransactionOccurrence>> transactions,
        DateTime month)
    {
        var byDay = new Dictionary<int, DailyBalanceSnapshot>(snapshots.Count);
        foreach (var s in snapshots) byDay[s.Date.Day] = s;

        var firstDayOfWeek = (int)new DateTime(month.Year, month.Month, 1).DayOfWeek;
        var daysInMonth    = DateTime.DaysInMonth(month.Year, month.Month);
        var today          = DateTime.Today;
        var prevMonth      = month.AddMonths(-1);
        var nextMonth      = month.AddMonths(1);
        var daysInPrev     = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);

        int cellIndex = 0;

        for (int i = firstDayOfWeek - 1; i >= 0; i--, cellIndex++)
            GridDays[cellIndex].UpdateAs(
                new DateTime(prevMonth.Year, prevMonth.Month, daysInPrev - i),
                false, false, false, null, null, today);

        for (int day = 1; day <= daysInMonth; day++, cellIndex++)
        {
            var date     = new DateTime(month.Year, month.Month, day);
            var snapshot = byDay.TryGetValue(day, out var s) ? s : null;

            _chipScratchpad.Clear();
            BuildChipsInto(_chipScratchpad, snapshot,
                transactions.TryGetValue(date.Date, out var txns) ? txns : null);

            GridDays[cellIndex].UpdateAs(date, true, date.Date == today,
                snapshot != null, snapshot,
                _chipScratchpad.Count > 0 ? new List<CalendarEventChip>(_chipScratchpad) : null,
                today);
        }

        int suffixDay = 1;
        while (cellIndex < 42)
            GridDays[cellIndex++].UpdateAs(
                new DateTime(nextMonth.Year, nextMonth.Month, suffixDay++),
                false, false, false, null, null, today);
    }

    private void UpdateSingleGridCell(DateTime date, List<TransactionOccurrence>? txns)
    {
        var firstDayOfWeek = (int)new DateTime(date.Year, date.Month, 1).DayOfWeek;
        var cellIndex      = firstDayOfWeek + date.Day - 1;
        if (cellIndex < 0 || cellIndex >= 42) return;

        var cell = GridDays[cellIndex];
        if (!cell.IsCurrentMonth) return;

        _chipScratchpad.Clear();
        BuildChipsInto(_chipScratchpad, cell.Snapshot, txns);
        cell.EventChips = _chipScratchpad.Count > 0
            ? new List<CalendarEventChip>(_chipScratchpad) : null;
    }

    private static void BuildChipsInto(
        List<CalendarEventChip> target,
        DailyBalanceSnapshot? snapshot,
        List<TransactionOccurrence>? transactions)
    {
        if (snapshot == null) return;

        if (transactions == null || transactions.Count == 0)
        {
            if (snapshot.HasIncome)
                target.Add(new CalendarEventChip
                    { Label = snapshot.IncomeSummary, Color = ChipIncomeColor, IsIncome = true });
            if (snapshot.HasExpenses && target.Count < 2)
                target.Add(new CalendarEventChip
                    { Label = snapshot.ExpenseSummary, Color = ChipExpenseColor, IsIncome = false });
            return;
        }

        TransactionOccurrence? firstIncome = null, firstExpense = null;
        foreach (var t in transactions)
        {
            if (firstIncome  == null && t.Amount >= 0) firstIncome  = t;
            if (firstExpense == null && t.Amount <  0) firstExpense = t;
            if (firstIncome != null && firstExpense != null) break;
        }

        if (firstIncome != null && firstExpense != null)
        {
            target.Add(new CalendarEventChip
            {
                Label          = firstIncome.Description,
                Color          = ChipColorFor(firstIncome),
                IsIncome       = true,
                CustomColorHex = firstIncome.Color,
            });
            target.Add(new CalendarEventChip
            {
                Label          = firstExpense.Description,
                Color          = ChipColorFor(firstExpense),
                IsIncome       = false,
                CustomColorHex = firstExpense.Color,
            });
        }
        else
        {
            int added = 0;
            foreach (var t in transactions)
            {
                if (added >= 2) break;
                target.Add(new CalendarEventChip
                {
                    Label          = t.Description,
                    Color          = ChipColorFor(t),
                    IsIncome       = t.Amount >= 0,
                    CustomColorHex = t.Color,
                });
                added++;
            }
        }
    }

    private static Color ChipColorFor(TransactionOccurrence t)
    {
        if (t.Color != null)
        {
            try { return Microsoft.Maui.Graphics.Color.FromArgb(t.Color); }
            catch { /* malformed hex -- fall through */ }
        }
        return t.Amount >= 0 ? ChipIncomeColor : ChipExpenseColor;
    }

    [RelayCommand]
    private void CloseDayDetail()
    {
        SelectedDate = null;
        IsTransactionDetailVisible = false;
        SelectedDayTransactions = new ObservableCollection<TransactionOccurrence>();
        SelectedDateDisplay = string.Empty;
    }

    [RelayCommand]
    private void SelectGridDay(CalendarGridDay cell)
    {
        if (!cell.IsCurrentMonth || !cell.HasData) return;
        SelectDateInternal(cell.Date);
    }

    private void SelectDateInternal(DateTime date)
    {
        if (SelectedAccount == null) return;
        if (SelectedDate?.Date == date.Date) { CloseDayDetail(); return; }

        // Update date display immediately so the header is always correct
        // regardless of whether data is already cached or needs fetching.
        SelectedDate = date;
        SelectedDateDisplay = date.ToString("MMMM dd, yyyy");
        IsTransactionDetailVisible = true;
        SelectedDayTransactions = new ObservableCollection<TransactionOccurrence>();

        var key = (SelectedAccount.AccountId, date.Year, date.Month);
        if (_monthCache.TryGetValue(key, out var cached))
        {
            RefreshDayDetail(date, cached);
            return;
        }

        _ = Task.Run(async () =>
        {
            var txns = await _apiService.GetTransactionsForDateAsync(
                SelectedAccount.AccountId, date, forceRefresh: false);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (SelectedDate?.Date == date.Date)
                {
                    SelectedDayTransactions = new ObservableCollection<TransactionOccurrence>(txns);
                    UpdateSingleGridCell(date, txns);
                }
            });
        });
    }

    private void RefreshDayDetail(DateTime date, MonthData data)
    {
        data.Transactions.TryGetValue(date.Date, out var txns);
        txns ??= new();

        var income   = txns.Where(t => t.Amount >= 0).Sum(t => t.Amount);
        var expenses = txns.Where(t => t.Amount <  0).Sum(t => Math.Abs(t.Amount));
        var net      = income - expenses;

        SelectedDayIncome    = income;
        SelectedDayExpenses  = expenses;
        SelectedDayNetChange = net;

        var snapshot = data.Overview.DailyBreakdown
            .FirstOrDefault(d => d.Date.Date == date.Date);
        SelectedDayBalance = snapshot?.EndOfDayBalance ?? 0;

        SelectedDayNetChangeColor = net >= 0
            ? Color.FromArgb("#2E7D32") : Color.FromArgb("#C62828");
        SelectedDayBalanceColor = (snapshot?.EndOfDayBalance ?? 0) >= 0
            ? Color.FromArgb("#2E7D32") : Color.FromArgb("#C62828");

        SelectedDayTransactions = new ObservableCollection<TransactionOccurrence>(txns);
    }

    private void ClearCache()
    {

        _monthCache.Clear();
    }

    private void CancelAllBackgroundWork()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;

        _prefetchCts?.Cancel();
        _prefetchCts?.Dispose();
        _prefetchCts = null;
    }

    private void UpdateMonthDisplay() =>
        MonthYearDisplay = CurrentMonth.ToString("MMMM yyyy");

    private void UpdatePickerValues()
    {
        SelectedYear = CurrentMonth.Year;
        SelectedMonthName = AvailableMonths[CurrentMonth.Month - 1];
    }

    private void ResetDisplayedData()
    {
        StartingBalanceDisplay = EndingBalanceDisplay = TotalIncomeDisplay =
            TotalExpensesDisplay = NetChangeDisplay = "$0.00";
        NetChangeColor = Colors.Gray;

        var today = DateTime.Today;
        for (int i = 0; i < 42; i++) GridDays[i].Reset(today);

        SelectedDayTransactions = new ObservableCollection<TransactionOccurrence>();
        IsTransactionDetailVisible = false;
        IsLoading = false;
    }

    [RelayCommand]
    private async Task NavigateToAccounts()
    {
        try { await Shell.Current.GoToAsync("accounts"); }
        catch { }
    }

    [RelayCommand]
    private async Task NavigateToAddEntry()
    {
        try { await Shell.Current.GoToAsync("add"); }
        catch { }
    }
}
