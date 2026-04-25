using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HexFund.UI.Models;
using HexFund.UI.Services;
using System.Collections.ObjectModel;

namespace HexFund.UI.ViewModels;

// ── Display models ────────────────────────────────────────────────────────────

/// <summary>
/// One slice of the category spending donut.
/// Carries everything the XAML needs to render a bar-approximation segment.
/// </summary>
public class CategorySlice
{
    public string  Category           { get; init; } = string.Empty;
    public decimal Total              { get; init; }
    public double  Percentage         { get; init; }   // 0–100
    public Color   SliceColor         { get; init; } = Colors.Gray;
    public string  PercentageDisplay  => $"{Percentage:F0}%";
    public string  TotalDisplay       => Total.ToString("C");
    /// Width multiplier for the XAML bar (0.0–1.0)
    public double  BarWeight          => Percentage / 100.0;
}

/// <summary>
/// One month bar for the 6-month net-change trend chart.
/// </summary>
public class MonthBar
{
    private const int MaxBarPx = 80;

    public string  MonthLabel    { get; init; } = string.Empty;  // "Jan", "Feb" …
    public decimal NetChange     { get; init; }
    public bool    IsPositive    => NetChange >= 0;
    public string  ValueDisplay  => $"{(NetChange >= 0 ? "+" : "")}{NetChange:C0}";
    /// Normalised bar height (0.0–1.0) — set by the VM after all bars are known.
    public double  BarWeight     { get; set; }
    /// Pixel height for HeightRequest binding (4px minimum so zero months are visible).
    public int     BarHeightPx   => Math.Max(4, (int)(BarWeight * MaxBarPx));
    /// Spacer height that pushes the bar to the bottom of a fixed 120px chart area.
    public double  SpacerHeightPx => Math.Max(0, 120.0 - BarHeightPx);
    /// Colour driven by IsPositive — resolved at bind time via VM helper.
    public Color   BarColor      => IsPositive
        ? GetSemanticColor("Up",   "#3FB97A")
        : GetSemanticColor("Down", "#E05A6F");

    private static Color GetSemanticColor(string key, string fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c)
            return c;
        return Color.FromArgb(fallback);
    }
}

/// <summary>
/// A single smart-alert card surfaced when a spend spike is detected.
/// </summary>
public class SpendAlert
{
    public string Category    { get; init; } = string.Empty;
    public decimal ThisMonth  { get; init; }
    public decimal LastMonth  { get; init; }
    public double  ChangePercent { get; init; }
    public string  Message =>
        $"{Category} spending is up {ChangePercent:F0}% vs last month " +
        $"({LastMonth:C0} → {ThisMonth:C0}).";
}

// ── ViewModel ─────────────────────────────────────────────────────────────────

/// <summary>
/// InsightsViewModel — Sprint 05.
///
/// Three sections:
///   1. Category donut — expense breakdown for the current month.
///      Empty state if the selected account has fewer than 5 transactions.
///   2. 6-month bar trend — net change per month for the last 6 months.
///   3. Smart alerts — surfaces categories whose spend is >20% higher than last month.
///
/// All data is scoped to the currently selected account and refreshes whenever
/// SelectedAccountChanged or TransactionsChanged fires.
/// </summary>
public partial class InsightsViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly IAccountStateService _accountStateService;

    // ── Loading / error ───────────────────────────────────────────────────────
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool hasError;
    [ObservableProperty] private string errorMessage = string.Empty;

    // ── No-account state ──────────────────────────────────────────────────────
    [ObservableProperty] private bool hasAccount;

    // ── Month navigation ──────────────────────────────────────────────────────
    /// <summary>The first day of the month currently being displayed.</summary>
    [ObservableProperty] private DateTime displayMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    // No upper bound — users can browse future months (balances will simply
    // show zero / projected data from whatever the server returns).
    public bool CanGoForward => true;

    // ── Section 1: Category donut ─────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<CategorySlice> categorySlices = new();
    [ObservableProperty] private bool hasCategoryData;
    [ObservableProperty] private string categoryEmptyReason = string.Empty;
    [ObservableProperty] private string currentMonthLabel = string.Empty;
    [ObservableProperty] private decimal totalExpensesThisMonth;
    public string TotalExpensesDisplay => TotalExpensesThisMonth.ToString("C");

    // ── Section 2: 6-month bar trend ─────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<MonthBar> monthBars = new();
    [ObservableProperty] private bool hasTrendData;

    // ── Section 3: Smart alerts ───────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<SpendAlert> spendAlerts = new();
    [ObservableProperty] private bool hasAlerts;

    // ── Colour palette for category slices ───────────────────────────────────
    private static readonly string[] SliceColors =
    {
        "#4A7FD6", // Sapphire
        "#35B88C", // Emerald
        "#E0A03F", // Warn/Amber
        "#9C6ED1", // Amethyst
        "#C73A57", // Ruby
        "#B87333", // Bronze
        "#D4AF37", // Gold
        "#8E94A0", // Obsidian
    };

    // ── Constructor ───────────────────────────────────────────────────────────

    public InsightsViewModel(
        IApiService apiService,
        IAccountStateService accountStateService,
        ISettingsService settingsService)
        : base(settingsService)
    {
        _apiService           = apiService;
        _accountStateService  = accountStateService;

        _accountStateService.SelectedAccountChanged += OnAccountChanged;
        _accountStateService.TransactionsChanged    += OnAccountChanged;
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        if (!HasAccount && _accountStateService.SelectedAccount == null) return;
        if (IsLoading) return;
        await LoadAsync();
    }

    private void OnAccountChanged() =>
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());

    // ── Month navigation commands ─────────────────────────────────────────────

    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        DisplayMonth = DisplayMonth.AddMonths(-1);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        DisplayMonth = DisplayMonth.AddMonths(1);
        await LoadAsync();
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        var account = _accountStateService.SelectedAccount;

        HasAccount = account != null;
        if (account == null) return;

        IsLoading = true;
        HasError  = false;

        try
        {
            var thisMonth = DisplayMonth;
            var now       = DateTime.Today;

            // ── Fetch this month and last month overviews in parallel ──────
            var thisTask = _apiService.GetMonthlyOverviewAsync(
                account.AccountId, thisMonth.Year, thisMonth.Month, forceRefresh: false);

            var lastMonthDate = thisMonth.AddMonths(-1);
            var lastTask = _apiService.GetMonthlyOverviewAsync(
                account.AccountId, lastMonthDate.Year, lastMonthDate.Month, forceRefresh: false);

            // ── Fetch 6 months for the trend (selected month + 5 prior) ──────
            var trendTasks = Enumerable.Range(0, 6)
                .Select(i =>
                {
                    var d = thisMonth.AddMonths(-i);
                    return _apiService.GetMonthlyOverviewAsync(
                        account.AccountId, d.Year, d.Month, forceRefresh: false);
                })
                .ToList();

            await Task.WhenAll(new Task[] { thisTask, lastTask }.Concat(trendTasks));

            var thisOverview = await thisTask;
            var lastOverview = await lastTask;
            var trendOverviews = new List<EnhancedMonthlyOverview?>();
            foreach (var t in trendTasks)
                trendOverviews.Add(await t);

            CurrentMonthLabel = thisMonth.ToString("MMMM yyyy");

            BuildCategoryDonut(thisOverview);
            BuildTrendBars(trendOverviews, thisMonth);
            BuildSmartAlerts(thisOverview, lastOverview);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"InsightsViewModel.LoadAsync error: {ex.Message}");
            HasError     = true;
            ErrorMessage = "Unable to load insights. Pull to refresh.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Section builders ──────────────────────────────────────────────────────

    private void BuildCategoryDonut(EnhancedMonthlyOverview? overview)
    {
        if (overview == null)
        {
            HasCategoryData     = false;
            CategoryEmptyReason = "No data available for this month.";
            CategorySlices      = new();
            return;
        }

        // Only expense categories, sorted by total descending
        var expenseBreakdowns = overview.CategoryBreakdowns
            .Where(c => c.Type?.Equals("expense", StringComparison.OrdinalIgnoreCase) == true
                     && c.Total > 0)
            .OrderByDescending(c => c.Total)
            .ToList();

        // Spec: empty state if fewer than 5 transactions total this month
        var totalTxCount = overview.CategoryBreakdowns.Sum(c => c.TransactionCount);
        if (totalTxCount < 5)
        {
            HasCategoryData     = false;
            CategoryEmptyReason = totalTxCount == 0
                ? "No transactions this month yet."
                : $"Not enough data — {totalTxCount} transaction{(totalTxCount == 1 ? "" : "s")} so far this month (need 5+).";
            CategorySlices      = new();
            TotalExpensesThisMonth = 0;
            return;
        }

        if (expenseBreakdowns.Count == 0)
        {
            HasCategoryData     = false;
            CategoryEmptyReason = "No expense categories recorded this month.";
            CategorySlices      = new();
            TotalExpensesThisMonth = overview.TotalExpenses;
            return;
        }

        var totalExpenses = expenseBreakdowns.Sum(c => c.Total);
        TotalExpensesThisMonth = totalExpenses;
        OnPropertyChanged(nameof(TotalExpensesDisplay));

        // Group anything beyond top-6 into "Other"
        const int maxSlices = 6;
        List<CategorySlice> slices;

        if (expenseBreakdowns.Count <= maxSlices)
        {
            slices = expenseBreakdowns
                .Select((c, i) => new CategorySlice
                {
                    Category   = string.IsNullOrEmpty(c.Category) ? "Uncategorized" : c.Category,
                    Total      = c.Total,
                    Percentage = totalExpenses > 0 ? (double)(c.Total / totalExpenses * 100) : 0,
                    SliceColor = Color.FromArgb(SliceColors[i % SliceColors.Length]),
                })
                .ToList();
        }
        else
        {
            var top = expenseBreakdowns.Take(maxSlices - 1).ToList();
            var otherTotal = expenseBreakdowns.Skip(maxSlices - 1).Sum(c => c.Total);

            slices = top
                .Select((c, i) => new CategorySlice
                {
                    Category   = string.IsNullOrEmpty(c.Category) ? "Uncategorized" : c.Category,
                    Total      = c.Total,
                    Percentage = totalExpenses > 0 ? (double)(c.Total / totalExpenses * 100) : 0,
                    SliceColor = Color.FromArgb(SliceColors[i % SliceColors.Length]),
                })
                .ToList();

            slices.Add(new CategorySlice
            {
                Category   = "Other",
                Total      = otherTotal,
                Percentage = totalExpenses > 0 ? (double)(otherTotal / totalExpenses * 100) : 0,
                SliceColor = Color.FromArgb(SliceColors[(maxSlices - 1) % SliceColors.Length]),
            });
        }

        CategorySlices  = new ObservableCollection<CategorySlice>(slices);
        HasCategoryData = slices.Count > 0;
    }

    private void BuildTrendBars(List<EnhancedMonthlyOverview?> overviews, DateTime thisMonth)
    {
        // overviews[0] = this month, [1] = last month … [5] = 5 months ago
        // Reverse so bars display oldest-left to newest-right
        var bars = overviews
            .Select((o, i) =>
            {
                var monthDate = thisMonth.AddMonths(-i);
                return new MonthBar
                {
                    MonthLabel = monthDate.ToString("MMM"),
                    NetChange  = o?.NetChange ?? 0m,
                };
            })
            .Reverse()
            .ToList();

        // Normalise bar heights: max absolute value = 1.0
        var maxAbs = bars.Max(b => Math.Abs((double)b.NetChange));
        if (maxAbs > 0)
        {
            foreach (var bar in bars)
                bar.BarWeight = Math.Abs((double)bar.NetChange) / maxAbs;
        }

        MonthBars    = new ObservableCollection<MonthBar>(bars);
        HasTrendData = bars.Any(b => b.NetChange != 0);
    }

    private void BuildSmartAlerts(
        EnhancedMonthlyOverview? thisOverview,
        EnhancedMonthlyOverview? lastOverview)
    {
        var alerts = new List<SpendAlert>();

        if (thisOverview == null || lastOverview == null)
        {
            SpendAlerts = new();
            HasAlerts   = false;
            return;
        }

        var thisExpenses = thisOverview.CategoryBreakdowns
            .Where(c => c.Type?.Equals("expense", StringComparison.OrdinalIgnoreCase) == true
                     && c.Total > 0)
            .ToDictionary(c => c.Category ?? "Uncategorized", c => c.Total);

        var lastExpenses = lastOverview.CategoryBreakdowns
            .Where(c => c.Type?.Equals("expense", StringComparison.OrdinalIgnoreCase) == true
                     && c.Total > 0)
            .ToDictionary(c => c.Category ?? "Uncategorized", c => c.Total);

        foreach (var (category, thisTotal) in thisExpenses)
        {
            if (!lastExpenses.TryGetValue(category, out var lastTotal) || lastTotal == 0)
                continue;

            var changePercent = (double)((thisTotal - lastTotal) / lastTotal * 100);

            // Spec: alert if >20% higher than last month
            if (changePercent > 20)
            {
                alerts.Add(new SpendAlert
                {
                    Category      = category,
                    ThisMonth     = thisTotal,
                    LastMonth     = lastTotal,
                    ChangePercent = changePercent,
                });
            }
        }

        // Sort by biggest spike first
        alerts = alerts.OrderByDescending(a => a.ChangePercent).ToList();

        SpendAlerts = new ObservableCollection<SpendAlert>(alerts);
        HasAlerts   = alerts.Count > 0;
    }
}
