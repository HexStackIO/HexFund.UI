using CommunityToolkit.Mvvm.ComponentModel;

namespace HexFund.UI.Models;

public class EnhancedMonthlyOverview
{
    public int Year { get; set; }
    public int Month { get; set; }
    public Guid AccountId { get; set; }
    public decimal StartingBalance { get; set; }
    public decimal EndingBalance { get; set; }
    public decimal NetChange { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal AverageDailyBalance { get; set; }
    public decimal HighestBalance { get; set; }
    public decimal LowestBalance { get; set; }
    public int DaysWithNegativeBalance { get; set; }
    public List<DailyBalanceSnapshot> DailyBreakdown { get; set; } = new();
    public List<CategoryBreakdown> CategoryBreakdowns { get; set; } = new();

    public string MonthDisplay => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
    public string IncomeDisplay => TotalIncome.ToString("C");
    public string ExpensesDisplay => TotalExpenses.ToString("C");
    public string NetChangeDisplay => $"{(NetChange >= 0 ? "+" : "")}{NetChange:C}";
    public Color NetChangeColor => NetChange >= 0 ? Colors.Green : Colors.Red;
}

public partial class DailyBalanceSnapshot
{
    public DateTime Date { get; set; }
    public decimal EndOfDayBalance { get; set; }
    public decimal StartOfDayBalance { get; set; }
    public decimal DayChange { get; set; }
    public int TransactionCount { get; set; }
    public decimal DayIncome { get; set; }
    public decimal DayExpenses { get; set; }
    public bool HasNegativeBalance { get; set; }
    public decimal LowestBalance { get; set; }

    // Display helpers
    public string DateDisplay => Date.ToString("MMM dd");
    public string BalanceDisplay => EndOfDayBalance.ToString("C");
    public Color BalanceColor => EndOfDayBalance >= 0 ? Colors.Green : Colors.Red;
    public string DayChangeDisplay => $"{(DayChange >= 0 ? "+" : "")}{DayChange:C}";
    public Color DayChangeColor => DayChange >= 0 ? Colors.Green : Colors.Red;
    public bool HasTransactions => TransactionCount > 0;
    public string TransactionCountDisplay => TransactionCount == 1
        ? "1 transaction" : $"{TransactionCount} transactions";

    public bool HasIncome => DayIncome > 0;
    public bool HasExpenses => DayExpenses > 0;
    public string IncomeSummary => DayIncome.ToString("C");
    public string ExpenseSummary => DayExpenses.ToString("C");
    public string DayChangeSummary => $"{(DayChange >= 0 ? "+" : "")}{DayChange:C}";

    public string ListViewSummary
    {
        get
        {
            if (TransactionCount == 0) return "No transactions";

            var parts = new List<string> { $"{TransactionCount} txn" };

            if (HasIncome && HasExpenses)
            {
                parts.Add($"up{IncomeSummary}");
                parts.Add($"down{ExpenseSummary}");
            }
            else if (HasIncome || HasExpenses)
            {
                var sign = DayChange >= 0 ? "+" : "";
                parts.Add($"Net: {sign}{DayChangeSummary}");
            }

            return string.Join(" - ", parts);
        }
    }
}

public class CategoryBreakdown
{
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "income" or "expense"
    public decimal Total { get; set; }
    public int TransactionCount { get; set; }
    public decimal PercentageOfTotal { get; set; }
}

public class CalendarEventChip
{
    public string Label { get; set; } = string.Empty;
    public Color Color { get; set; } = Colors.Gray;
    public string? CustomColorHex { get; set; }
    public bool IsIncome { get; set; }
}

public partial class CalendarGridDay : ObservableObject
{
    private DateTime _date;
    private bool _isCurrentMonth;
    private bool _isToday;
    private bool _hasData;
    private DailyBalanceSnapshot? _snapshot;
    private List<CalendarEventChip>? _eventChips;

    public DateTime Date
    {
        get => _date;
        private set => SetProperty(ref _date, value);
    }

    public bool IsCurrentMonth
    {
        get => _isCurrentMonth;
        private set => SetProperty(ref _isCurrentMonth, value);
    }

    public bool IsToday
    {
        get => _isToday;
        private set => SetProperty(ref _isToday, value);
    }

    public bool HasData
    {
        get => _hasData;
        private set => SetProperty(ref _hasData, value);
    }

    public DailyBalanceSnapshot? Snapshot
    {
        get => _snapshot;
        private set => SetProperty(ref _snapshot, value);
    }

    public List<CalendarEventChip>? EventChips
    {
        get => _eventChips;
        internal set => SetProperty(ref _eventChips, value);
    }

    public string DayNumber => _date.Day.ToString();

    public Color DayNumberColor
    {
        get
        {
            if (_isToday) return Colors.White;
            if (!_isCurrentMonth) return Color.FromArgb("#BBBBBB");

            // Resolve against the semantic token set by ThemeService — no OS
            // AppTheme dependency. Falls back to a safe mid-grey if called
            // before the first Apply() (e.g. during unit tests).
            if (Application.Current?.Resources.TryGetValue("TextPrimary", out var v) == true
                && v is Color textColor)
                return textColor;

            return Color.FromArgb("#212121");
        }
    }

    /// <summary>
    /// Indicator dot color. Green = income only, Red = expense only, Orange = both.
    /// </summary>
    public Color StatusColor
    {
        get
        {
            if (_snapshot == null) return Colors.Transparent;
            if (_snapshot.HasIncome && _snapshot.HasExpenses) return Colors.Orange;
            if (_snapshot.HasIncome) return Colors.Green;
            if (_snapshot.HasExpenses) return Colors.Red;
            return Colors.Gray;
        }
    }

    /// <summary>
    /// Thin red stroke when the day ends with a negative balance; transparent otherwise.
    /// </summary>
    public Color CellStroke =>
        _snapshot?.HasNegativeBalance == true ? Colors.Red : Colors.Transparent;

    /// <summary>
    /// Background: today gets Primary, others get Transparent.
    /// </summary>
    public Color CellBackground
    {
        get
        {
            if (_isToday &&
                Application.Current?.Resources.TryGetValue("Primary", out var val) == true &&
                val is Color primary)
                return primary;

            return _isToday ? Colors.Blue : Colors.Transparent;
        }
    }

    /// <summary>
    /// Updates all fields atomically. Only fires PropertyChanged for fields whose values actually changed.
    /// </summary>
    internal void UpdateAs(
        DateTime date,
        bool isCurrentMonth,
        bool isToday,
        bool hasData,
        DailyBalanceSnapshot? snapshot,
        List<CalendarEventChip>? chips,
        DateTime today)
    {
        bool dateChanged = _date != date;
        bool currentMonthChanged = _isCurrentMonth != isCurrentMonth;
        bool isTodayChanged = _isToday != isToday;
        bool hasDataChanged = _hasData != hasData;
        bool snapshotChanged = !ReferenceEquals(_snapshot, snapshot);
        bool chipsChanged = !ReferenceEquals(_eventChips, chips);

        _date = date;
        _isCurrentMonth = isCurrentMonth;
        _isToday = isToday;
        _hasData = hasData;
        _snapshot = snapshot;
        _eventChips = chips;

        if (dateChanged)
        {
            OnPropertyChanged(nameof(Date));
            OnPropertyChanged(nameof(DayNumber));
        }
        if (currentMonthChanged || isTodayChanged) OnPropertyChanged(nameof(DayNumberColor));
        if (isTodayChanged)
        {
            OnPropertyChanged(nameof(IsToday));
            OnPropertyChanged(nameof(CellBackground));
        }
        if (currentMonthChanged) OnPropertyChanged(nameof(IsCurrentMonth));
        if (hasDataChanged) OnPropertyChanged(nameof(HasData));
        if (snapshotChanged)
        {
            OnPropertyChanged(nameof(Snapshot));
            OnPropertyChanged(nameof(StatusColor));
        }
        if (chipsChanged) OnPropertyChanged(nameof(EventChips));
        if (snapshotChanged) OnPropertyChanged(nameof(CellStroke));
    }

    /// <summary>
    /// Forces CellBackground and DayNumberColor to re-notify without changing data.
    /// Called after a theme change so today's cell repaints with the new Primary color.
    /// </summary>
    internal void NotifyCellBackground()
    {
        OnPropertyChanged(nameof(CellBackground));
        OnPropertyChanged(nameof(DayNumberColor));
    }

    /// <summary>
    /// Resets this cell to blank state (called when no account is selected).
    /// </summary>
    internal void Reset(DateTime today) =>
        UpdateAs(DateTime.MinValue, false, false, false, null, null, today);
}