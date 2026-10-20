using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HexFund.UI.Models;
using HexFund.UI.Services;
using System.Collections.ObjectModel;

namespace HexFund.UI.ViewModels;

/// <summary>
/// One category of the monthly expense breakdown. The page draws it as a bar segment.
/// </summary>
public class CategorySlice
{
    public string  Category           { get; init; } = string.Empty;
    public decimal Total              { get; init; }
    public double  Percentage         { get; init; }   // 0 to 100.
    public Color   SliceColor         { get; init; } = Colors.Gray;
    public string  PercentageDisplay  => $"{Percentage:F0}%";
    public string  TotalDisplay       => Total.ToString("C");
    /// <summary>Width multiplier for the bar, from 0.0 to 1.0.</summary>
    public double  BarWeight          => Percentage / 100.0;
}

/// <summary>
/// One month of the six-month net-change trend chart.
/// </summary>
public class MonthBar
{
    private const int MaxBarPx = 80;

    public string  MonthLabel    { get; init; } = string.Empty;  // For example "Jan".
    public decimal NetChange     { get; init; }
    public bool    IsPositive    => NetChange >= 0;
    public string  ValueDisplay  => $"{(NetChange >= 0 ? "+" : "")}{NetChange:C0}";
    /// <summary>Bar height relative to the largest month, from 0.0 to 1.0. Set once all bars are known.</summary>
    public double  BarWeight     { get; set; }
    /// <summary>Bar height in pixels, with a 4 pixel minimum so empty months stay visible.</summary>
    public int     BarHeightPx   => Math.Max(4, (int)(BarWeight * MaxBarPx));
    /// <summary>Spacer height that pushes the bar to the bottom of the fixed 120 pixel chart area.</summary>
    public double  SpacerHeightPx => Math.Max(0, 120.0 - BarHeightPx);
    /// <summary>Up color for a positive month, Down color otherwise.</summary>
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
/// A card shown when a category's spending rose sharply compared with the previous month.
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

/// <summary>
/// Drives the insights page for the selected account: the expense breakdown by category for the
/// displayed month, the net change of the last six months, and spending alerts for categories
/// that rose more than 20% over the previous month. It reloads when the selected account or the
/// transactions change.
/// </summary>
/// <remarks>The category breakdown needs at least five transactions in the month.</remarks>
public partial class InsightsViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly IAccountStateService _accountStateService;

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool hasError;
    [ObservableProperty] private string errorMessage = string.Empty;

    [ObservableProperty] private bool hasAccount;

    /// <summary>The first day of the month currently being displayed.</summary>
    [ObservableProperty] private DateTime displayMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    // There is no upper bound: future months show whatever projection the server returns.
    public bool CanGoForward => true;

    [ObservableProperty] private ObservableCollection<CategorySlice> categorySlices = new();
    [ObservableProperty] private bool hasCategoryData;
    [ObservableProperty] private string categoryEmptyReason = string.Empty;
    [ObservableProperty] private string currentMonthLabel = string.Empty;
    [ObservableProperty] private decimal totalExpensesThisMonth;
    public string TotalExpensesDisplay => TotalExpensesThisMonth.ToString("C");

    [ObservableProperty] private ObservableCollection<MonthBar> monthBars = new();
    [ObservableProperty] private bool hasTrendData;

    [ObservableProperty] private ObservableCollection<SpendAlert> spendAlerts = new();
    [ObservableProperty] private bool hasAlerts;

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

    public async Task InitializeAsync()
    {
        if (!HasAccount && _accountStateService.SelectedAccount == null) return;
        if (IsLoading) return;
        await LoadAsync();
    }

    private void OnAccountChanged() =>
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());

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

            var thisTask = _apiService.GetMonthlyOverviewAsync(
                account.AccountId, thisMonth.Year, thisMonth.Month, forceRefresh: false);

            var lastMonthDate = thisMonth.AddMonths(-1);
            var lastTask = _apiService.GetMonthlyOverviewAsync(
                account.AccountId, lastMonthDate.Year, lastMonthDate.Month, forceRefresh: false);

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

    private void BuildCategoryDonut(EnhancedMonthlyOverview? overview)
    {
        if (overview == null)
        {
            HasCategoryData     = false;
            CategoryEmptyReason = "No data available for this month.";
            CategorySlices      = new();
            return;
        }

        // Expense categories only, largest first.
        var expenseBreakdowns = overview.CategoryBreakdowns
            .Where(c => c.Type?.Equals("expense", StringComparison.OrdinalIgnoreCase) == true
                     && c.Total > 0)
            .OrderByDescending(c => c.Total)
            .ToList();

        // A breakdown of very few transactions is not meaningful, so show an empty state instead.
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

        // At most six slices: the largest five categories plus an "Other" slice for the rest.
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
        // overviews[0] is this month and overviews[5] is five months ago. The bars are reversed so
        // they read oldest to newest, left to right.
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

        // The month with the largest absolute change gets weight 1.0.
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

            // Only increases of more than 20% raise an alert.
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

        // Largest increase first.
        alerts = alerts.OrderByDescending(a => a.ChangePercent).ToList();

        SpendAlerts = new ObservableCollection<SpendAlert>(alerts);
        HasAlerts   = alerts.Count > 0;
    }
}
