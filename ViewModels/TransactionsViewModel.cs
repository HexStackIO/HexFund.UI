using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HexFund.UI.Config;
using HexFund.UI.Models;
using HexFund.UI.Services;
using HexFund.UI.Validation;
using System.Collections.ObjectModel;

namespace HexFund.UI.ViewModels;

/// <summary>
/// Drives the transactions (ledger) page: the list with its amendment history, sorting and
/// filtering, and the edit, amend, activate and delete dialogs.
/// </summary>
public partial class TransactionsViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly IAccountStateService _accountStateService;

    [ObservableProperty] private ObservableCollection<TransactionCardViewModel> transactions = new();
    [ObservableProperty] private bool isLoadingInitial = false;

    [ObservableProperty] private Account? selectedAccount;
    [ObservableProperty] private bool hasAccount;
    [ObservableProperty]
    private string noAccountMessage =
        "No account selected. Please select or create an account.";

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string errorMessage = string.Empty;

    [ObservableProperty] private bool showAddTransactionDialog;
    [ObservableProperty] private string newDescription = string.Empty;
    [ObservableProperty] private string newAmount = string.Empty;
    [ObservableProperty] private string newCategory = string.Empty;
    [ObservableProperty] private FrequencyType selectedFrequency = FrequencyType.Once;
    [ObservableProperty] private DateTime startDate = DateTime.Today;
    [ObservableProperty] private DateTime endDate = DateTime.Today.AddYears(1);
    [ObservableProperty] private bool hasEndDate;
    [ObservableProperty] private bool isIncome = true;
    [ObservableProperty] private string? newColor = null;

    // Field errors for the add dialog.
    [ObservableProperty] private string descriptionError = string.Empty;
    [ObservableProperty] private string amountError = string.Empty;
    [ObservableProperty] private string categoryError = string.Empty;
    [ObservableProperty] private string dateRangeError = string.Empty;

    [ObservableProperty] private bool showEditTransactionDialog;
    [ObservableProperty] private Transaction? transactionToEdit;
    [ObservableProperty] private string editDescription = string.Empty;
    [ObservableProperty] private string editAmount = "0";
    [ObservableProperty] private string editCategory = string.Empty;
    [ObservableProperty] private FrequencyType editFrequency = FrequencyType.Once;
    [ObservableProperty] private DateTime editStartDate = DateTime.Today;
    [ObservableProperty] private DateTime editEndDate = DateTime.Today.AddYears(1);
    [ObservableProperty] private bool editHasEndDate;
    [ObservableProperty] private bool editIsIncome = true;
    [ObservableProperty] private bool editIsActive = true;
    [ObservableProperty] private string? editColor = null;

    // Field errors for the edit dialog.
    [ObservableProperty] private string editDescriptionError = string.Empty;
    [ObservableProperty] private string editAmountError = string.Empty;
    [ObservableProperty] private string editCategoryError = string.Empty;
    [ObservableProperty] private string editDateRangeError = string.Empty;

    [ObservableProperty] private bool showAmendDialog;
    [ObservableProperty] private Transaction? transactionToAmend;
    [ObservableProperty] private DateTime amendEffectiveDate = DateTime.Today;
    [ObservableProperty] private string amendAmount = "0";
    [ObservableProperty] private bool amendIsIncome = true;
    [ObservableProperty] private string amendDescription = string.Empty;
    [ObservableProperty] private string amendCategory = string.Empty;
    [ObservableProperty] private string? amendColor = null;

    // Field errors for the amend dialog.
    [ObservableProperty] private string amendEffectiveDateError = string.Empty;
    [ObservableProperty] private string amendAmountError = string.Empty;
    [ObservableProperty] private string amendDescriptionError = string.Empty;
    [ObservableProperty] private string amendCategoryError = string.Empty;

    [ObservableProperty] private bool showFilterDialog;
    [ObservableProperty] private bool isFilterActive;

    // Filter values applied to the displayed list.
    [ObservableProperty] private FrequencyType? filterRecurrence;
    [ObservableProperty] private DateTime? filterDateFrom;
    [ObservableProperty] private DateTime? filterDateTo;
    [ObservableProperty] private string filterAmountMin = string.Empty;
    [ObservableProperty] private string filterAmountMax = string.Empty;
    [ObservableProperty] private string filterCategory = string.Empty;

    // Filter values being edited in the filter dialog, committed on Apply.
    [ObservableProperty] private FrequencyType? pendingFilterRecurrence;
    [ObservableProperty] private DateTime pendingFilterDateFrom = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime pendingFilterDateTo = DateTime.Today.AddYears(1);
    [ObservableProperty] private string pendingFilterAmountMin = string.Empty;
    [ObservableProperty] private string pendingFilterAmountMax = string.Empty;
    [ObservableProperty] private string pendingFilterCategory = string.Empty;

    private List<Transaction> _allTransactionsWithHistory = new();
    private bool _historyLoaded;
    [ObservableProperty] private Transaction? predecessorToEdit;

    [ObservableProperty] private TransactionSortField activeSortField = TransactionSortField.StartDate;
    [ObservableProperty] private SortDirection sortDirection = SortDirection.Descending;

    // The key of the open tooltip, or empty when none is open. Only one tooltip is open at a time
    // across all three dialogs.
    [ObservableProperty] private string activeTooltip = string.Empty;

    public Color NewColorPreview =>
        NewColor != null ? Microsoft.Maui.Graphics.Color.FromArgb(NewColor)
                         : (IsIncome ? Colors.Green : Colors.Red);

    public Color EditColorPreview =>
        EditColor != null ? Microsoft.Maui.Graphics.Color.FromArgb(EditColor)
                          : (EditIsIncome ? Colors.Green : Colors.Red);

    public Color AmendColorPreview =>
        AmendColor != null ? Microsoft.Maui.Graphics.Color.FromArgb(AmendColor)
                           : (AmendIsIncome ? Colors.Green : Colors.Red);

    public string AddIncomeLabel => IsIncome ? "Income" : "Expense";
    public Color AddIncomeColor => IsIncome ? Colors.Green : Colors.Red;
    public string EditIncomeLabel => EditIsIncome ? "Income" : "Expense";
    public Color EditIncomeColor => EditIsIncome ? Colors.Green : Colors.Red;
    public string AmendIncomeLabel => AmendIsIncome ? "Income" : "Expense";
    public Color AmendIncomeColor => AmendIsIncome ? Colors.Green : Colors.Red;

    // Label for the sort button.
    public string SortLabel => (ActiveSortField, SortDirection) switch
    {
        (TransactionSortField.Name, SortDirection.Ascending) => "Name A-Z",
        (TransactionSortField.Name, SortDirection.Descending) => "Name Z-A",
        (TransactionSortField.Amount, SortDirection.Ascending) => "Amount Low-High",
        (TransactionSortField.Amount, SortDirection.Descending) => "Amount High-Low",
        (TransactionSortField.StartDate, SortDirection.Ascending) => "Date Oldest",
        (TransactionSortField.StartDate, SortDirection.Descending) => "Date Newest",
        (TransactionSortField.Recurrence, SortDirection.Ascending) => "Recurrence A-Z",
        (TransactionSortField.Recurrence, SortDirection.Descending) => "Recurrence Z-A",
        _ => "Sort",
    };

    // Arrow shown next to the active sort.
    public string SortDirectionArrow => SortDirection == SortDirection.Ascending ? "↑" : "↓";

    // ErrorMessage holds unexpected API errors, separate from field errors.
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsNotLoading => !IsLoading;
    public bool ShowSkeleton => IsLoadingInitial;
    public bool ShowList => !IsLoadingInitial;

    partial void OnIsIncomeChanged(bool value)
    {
        OnPropertyChanged(nameof(AddIncomeLabel));
        OnPropertyChanged(nameof(AddIncomeColor));
        OnPropertyChanged(nameof(NewColorPreview));
    }

    partial void OnEditIsIncomeChanged(bool value)
    {
        OnPropertyChanged(nameof(EditIncomeLabel));
        OnPropertyChanged(nameof(EditIncomeColor));
        OnPropertyChanged(nameof(EditColorPreview));
    }

    partial void OnAmendIsIncomeChanged(bool value)
    {
        OnPropertyChanged(nameof(AmendIncomeLabel));
        OnPropertyChanged(nameof(AmendIncomeColor));
        OnPropertyChanged(nameof(AmendColorPreview));
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsNotLoading));
    partial void OnIsLoadingInitialChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSkeleton));
        OnPropertyChanged(nameof(ShowList));
    }
    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnNewColorChanged(string? value) => OnPropertyChanged(nameof(NewColorPreview));
    partial void OnEditColorChanged(string? value) => OnPropertyChanged(nameof(EditColorPreview));
    partial void OnAmendColorChanged(string? value) => OnPropertyChanged(nameof(AmendColorPreview));

    // Strip invalid characters from numeric inputs on every keystroke.
    partial void OnEditAmountChanged(string value)
    {
        var sanitized = UIValidator.SanitizeDecimalInput(value, allowNegative: false);
        if (sanitized != value) EditAmount = sanitized;
    }

    partial void OnPendingFilterAmountMinChanged(string value)
    {
        var sanitized = UIValidator.SanitizeDecimalInput(value, allowNegative: false);
        if (sanitized != value) PendingFilterAmountMin = sanitized;
    }

    partial void OnPendingFilterAmountMaxChanged(string value)
    {
        var sanitized = UIValidator.SanitizeDecimalInput(value, allowNegative: false);
        if (sanitized != value) PendingFilterAmountMax = sanitized;
    }
    partial void OnActiveSortFieldChanged(TransactionSortField value)
    {
        OnPropertyChanged(nameof(SortLabel));
        OnPropertyChanged(nameof(SortDirectionArrow));
        ApplySort();
    }

    partial void OnSortDirectionChanged(SortDirection value)
    {
        OnPropertyChanged(nameof(SortLabel));
        OnPropertyChanged(nameof(SortDirectionArrow));
        ApplySort();
    }

    partial void OnEditFrequencyChanged(FrequencyType value)
    {
        if (value == FrequencyType.Once)
        {
            EditHasEndDate = true;
            EditEndDate = EditStartDate;
        }
        else
        {
            EditHasEndDate = false;
            EditEndDate = DateTime.Today.AddYears(1);
        }
    }

    partial void OnEditStartDateChanged(DateTime value)
    {
        if (EditFrequency == FrequencyType.Once)
            EditEndDate = value;
    }

    public List<FrequencyType> FrequencyOptions { get; } = new()
    {
        FrequencyType.Once,
        FrequencyType.Daily,
        FrequencyType.Weekly,
        FrequencyType.BiWeekly,
        FrequencyType.FirstThirdFriday,
        FrequencyType.Monthly,
        FrequencyType.BiMonthly,
    };

    // User-defined categories, loaded on first open and refreshed on demand. The list is shared
    // by the add, edit and amend dialogs.
    [ObservableProperty] private List<string> availableCategories = new() { "Uncategorized" };

    [ObservableProperty] private bool showNewCategoryInput;
    [ObservableProperty] private string newCategoryName = string.Empty;
    [ObservableProperty] private string newCategoryError = string.Empty;
    [ObservableProperty] private bool isSavingCategory;

    private bool _categoriesLoaded;

    private bool _isInitialized;
    private CancellationTokenSource? _loadCts;

    public TransactionsViewModel(
        IApiService apiService,
        IAccountStateService accountStateService,
        ISettingsService settingsService)
        : base(settingsService)
    {
        _apiService = apiService;
        _accountStateService = accountStateService;

        _accountStateService.SelectedAccountChanged += OnSelectedAccountChanged;
        _accountStateService.TransactionsChanged += OnTransactionsChangedExternal;

        if (_accountStateService.SelectedAccount != null)
        {
            SelectedAccount = _accountStateService.SelectedAccount;
            HasAccount = true;
        }
    }

    private void OnSelectedAccountChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var account = _accountStateService.SelectedAccount;
            SelectedAccount = account;
            HasAccount = account != null;
            _isInitialized = false;

            if (account != null)
                LoadTransactions();
            else
                Transactions = new ObservableCollection<TransactionCardViewModel>();
        });
    }

    // Any view model that changes a transaction triggers a full reload so the list stays in sync.
    private void OnTransactionsChangedExternal() =>
        MainThread.BeginInvokeOnMainThread(() => LoadTransactions(forceRefresh: true));

    public void InitializeAsync()
    {
        if (_isInitialized) return;

        if (_accountStateService.SelectedAccount != null)
        {
            SelectedAccount = _accountStateService.SelectedAccount;
            HasAccount = true;
            LoadTransactions();
        }
        else
        {
            HasAccount = false;
        }

        _isInitialized = true;

        // Categories load in the background so the list is not delayed.
        if (!_categoriesLoaded)
            _ = LoadCategoriesAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var cats = await _apiService.GetCategoriesAsync();
            var names = new List<string> { "Uncategorized" };
            names.AddRange(cats.Select(c => c.Name));
            AvailableCategories = names;
            _categoriesLoaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Load categories error: {ex.Message}");
        }
    }

    /// <summary>
    /// Refreshes the category list after categories are added or deleted in Settings, so the
    /// dialogs show the latest list without a full reload.
    /// </summary>
    public void InvalidateCategoryCache() => _categoriesLoaded = false;

    private void LoadTransactions(bool forceRefresh = false)
    {
        if (SelectedAccount == null)
        {
            ReconcileList(new List<Transaction>());
            return;
        }

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        if (!_isInitialized && Transactions.Count == 0)
            IsLoadingInitial = true;
        else
            IsLoading = true;

        ErrorMessage = string.Empty;
        var accountId = SelectedAccount.AccountId;

        _ = Task.Run(async () =>
        {
            try
            {
                var listTask = _apiService.GetTransactionsAsync(accountId, forceRefresh, includeHistory: false);
                var historyTask = _apiService.GetTransactionsAsync(accountId, forceRefresh: true, includeHistory: true);
                await Task.WhenAll(listTask, historyTask);
                if (token.IsCancellationRequested) return;

                var list = listTask.Result;
                _allTransactionsWithHistory = historyTask.Result;
                _historyLoaded = true;

                // A transaction that is the predecessor of another has been superseded by an
                // amendment. It appears only in the successor's history accordion, never in the
                // main list.
                var predecessorIds = _allTransactionsWithHistory
                    .Where(t => t.PredecessorTransactionId.HasValue)
                    .Select(t => t.PredecessorTransactionId!.Value)
                    .ToHashSet();

                var activeOnly = list.Where(t => !predecessorIds.Contains(t.TransactionId)).ToList();

                var sorted = ApplySortOrder(activeOnly, activeSortField, sortDirection);
                var filtered = ApplyActiveFilters(sorted);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (SelectedAccount?.AccountId != accountId) return;
                    ReconcileList(filtered);
                    IsLoadingInitial = false;
                    IsLoading = false;
                });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (SelectedAccount?.AccountId != accountId) return;
                    ErrorMessage = $"Failed to load transactions: {ex.Message}";
                    IsLoadingInitial = false;
                    IsLoading = false;
                });
            }
        }, token);
    }

    /// <summary>
    /// Re-sorts the loaded list without reloading data, after a change of sort field or direction.
    /// </summary>
    private void ApplySort()
    {
        // History may have been refreshed since the last load, so strip superseded records again.
        var predecessorIds = _allTransactionsWithHistory
            .Where(t => t.PredecessorTransactionId.HasValue)
            .Select(t => t.PredecessorTransactionId!.Value)
            .ToHashSet();

        var current = Transactions
            .Select(c => c.Transaction)
            .Where(t => !predecessorIds.Contains(t.TransactionId))
            .ToList();
        var sorted = ApplySortOrder(current, ActiveSortField, SortDirection);
        var filtered = ApplyActiveFilters(sorted);
        ReconcileList(filtered);
    }

    private static List<Transaction> ApplySortOrder(
        List<Transaction> list,
        TransactionSortField field,
        SortDirection direction)
    {
        IOrderedEnumerable<Transaction> ordered = field switch
        {
            TransactionSortField.Name => direction == SortDirection.Ascending
                                                ? list.OrderBy(t => t.Description, StringComparer.OrdinalIgnoreCase)
                                                : list.OrderByDescending(t => t.Description, StringComparer.OrdinalIgnoreCase),
            TransactionSortField.Amount => direction == SortDirection.Ascending
                                                ? list.OrderBy(t => Math.Abs(t.Amount))
                                                : list.OrderByDescending(t => Math.Abs(t.Amount)),
            TransactionSortField.Recurrence => direction == SortDirection.Ascending
                                                ? list.OrderBy(t => GetRecurrenceSortWeight(t.Frequency)).ThenBy(t => t.Description, StringComparer.OrdinalIgnoreCase)
                                                : list.OrderByDescending(t => GetRecurrenceSortWeight(t.Frequency)).ThenByDescending(t => t.Description, StringComparer.OrdinalIgnoreCase),
            _  /* StartDate */              => direction == SortDirection.Ascending
                                                ? list.OrderBy(t => t.StartDate)
                                                : list.OrderByDescending(t => t.StartDate),
        };

        return ordered.ToList();
    }

    private void ReconcileList(List<Transaction> incoming)
    {
        // Keeps expanded accordions open across refreshes.
        var expandedIds = Transactions.Where(c => c.IsExpanded)
                                       .Select(c => c.TransactionId)
                                       .ToHashSet();

        // Each card gets its predecessor chain for the history accordion.
        var cards = incoming.Select(tx =>
        {
            var card = new TransactionCardViewModel(tx, this, BuildPredecessorChain(tx));
            if (expandedIds.Contains(tx.TransactionId)) card.IsExpanded = true;
            return card;
        }).ToList();

        var incomingIds = new HashSet<Guid>(cards.Select(c => c.TransactionId));
        for (int i = Transactions.Count - 1; i >= 0; i--)
        {
            if (!incomingIds.Contains(Transactions[i].TransactionId))
                Transactions.RemoveAt(i);
        }

        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            var existing = i < Transactions.Count ? Transactions[i] : null;

            if (existing?.TransactionId == card.TransactionId)
            {
                if (!TransactionEquals(existing.Transaction, card.Transaction))
                {
                    card.IsExpanded = existing.IsExpanded;
                    Transactions[i] = card;
                }
            }
            else
            {
                var currentIndex = -1;
                for (int j = i + 1; j < Transactions.Count; j++)
                {
                    if (Transactions[j].TransactionId == card.TransactionId)
                    { currentIndex = j; break; }
                }
                if (currentIndex >= 0)
                    Transactions.Move(currentIndex, i);
                else
                    Transactions.Insert(i, card);
            }
        }

        while (Transactions.Count > cards.Count)
            Transactions.RemoveAt(Transactions.Count - 1);
    }

    private static bool TransactionEquals(Transaction a, Transaction b) =>
        a.Description == b.Description &&
        a.Amount == b.Amount &&
        a.Category == b.Category &&
        a.Frequency == b.Frequency &&
        a.StartDate == b.StartDate &&
        a.EndDate == b.EndDate &&
        a.IsActive == b.IsActive;

    /// <summary>
    /// Sort weight for recurrence types, from most to least frequent: daily, weekly, first and
    /// third Friday, bi-weekly, monthly, bi-monthly, once.
    /// </summary>
    private static int GetRecurrenceSortWeight(FrequencyType freq) => freq switch
    {
        FrequencyType.Daily => 0,
        FrequencyType.Weekly => 1,
        FrequencyType.FirstThirdFriday => 2,
        FrequencyType.BiWeekly => 3,
        FrequencyType.Monthly => 4,
        FrequencyType.BiMonthly => 5,
        FrequencyType.Once => 6,
        _ => 7,
    };

    /// <summary>
    /// Applies the active filters to an already sorted list. Returns the list unchanged when no
    /// filter is set.
    /// </summary>
    private List<Transaction> ApplyActiveFilters(List<Transaction> list)
    {
        if (!IsAnyFilterSet()) return list;

        IEnumerable<Transaction> result = list;

        if (FilterRecurrence.HasValue)
            result = result.Where(t => t.Frequency == FilterRecurrence.Value);

        if (FilterDateFrom.HasValue)
            result = result.Where(t => t.StartDate.Date >= FilterDateFrom.Value.Date);

        if (FilterDateTo.HasValue)
            result = result.Where(t => t.StartDate.Date <= FilterDateTo.Value.Date);

        if (decimal.TryParse(FilterAmountMin, out var minAmt))
            result = result.Where(t => Math.Abs(t.Amount) >= minAmt);

        if (decimal.TryParse(FilterAmountMax, out var maxAmt))
            result = result.Where(t => Math.Abs(t.Amount) <= maxAmt);

        if (!string.IsNullOrWhiteSpace(FilterCategory))
            result = result.Where(t =>
                t.Category?.Contains(FilterCategory, StringComparison.OrdinalIgnoreCase) == true);

        return result.ToList();
    }

    [RelayCommand]
    private void Refresh() => LoadTransactions(forceRefresh: true);

    [RelayCommand]
    private void ShowAddTransaction()
    {
        if (SelectedAccount == null)
        {
            Application.Current?.MainPage?.DisplayAlert(
                "No Account", "Please select an account first.", "OK");
            return;
        }

        NewDescription = string.Empty;
        NewAmount = string.Empty;
        NewCategory = "Uncategorized";
        SelectedFrequency = FrequencyType.Once;
        StartDate = DateTime.Today;
        EndDate = DateTime.Today.AddYears(1);
        HasEndDate = false;
        IsIncome = true;
        NewColor = null;

        DescriptionError = string.Empty;
        AmountError = string.Empty;
        CategoryError = string.Empty;
        DateRangeError = string.Empty;
        ErrorMessage = string.Empty;
        ActiveTooltip = string.Empty;
        ShowNewCategoryInput = false;
        NewCategoryName = string.Empty;
        NewCategoryError = string.Empty;

        ShowAddTransactionDialog = true;
    }

    [RelayCommand]
    private void CancelAddTransaction()
    {
        ShowAddTransactionDialog = false;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private async Task CreateTransactionAsync()
    {
        if (SelectedAccount == null)
        { ErrorMessage = "No account selected."; return; }

        DescriptionError = UIValidator.ValidateDescription(NewDescription) ?? string.Empty;
        AmountError = UIValidator.ValidateAmount(NewAmount) ?? string.Empty;
        CategoryError = UIValidator.ValidateCategory(NewCategory) ?? string.Empty;
        DateRangeError = UIValidator.ValidateDateRange(
                               StartDate, HasEndDate ? EndDate : null, SelectedFrequency) ?? string.Empty;

        if (!string.IsNullOrEmpty(DescriptionError) ||
            !string.IsNullOrEmpty(AmountError) ||
            !string.IsNullOrEmpty(CategoryError) ||
            !string.IsNullOrEmpty(DateRangeError))
            return;

        // Validated above, so parsing cannot fail.
        var amount = decimal.Parse(NewAmount.Trim());

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var request = new CreateTransactionRequest
            {
                Description = NewDescription.Trim(),
                Amount = IsIncome ? amount : -amount,
                Category = NewCategory == "Uncategorized" || string.IsNullOrWhiteSpace(NewCategory) ? null : NewCategory.Trim(),
                Frequency = SelectedFrequency,
                StartDate = StartDate,
                // A one-time transaction has a single occurrence, so it never sends an end date.
                EndDate = (HasEndDate && SelectedFrequency != FrequencyType.Once) ? EndDate : null,
                Color = NewColor,
            };

            var created = await _apiService.CreateTransactionAsync(SelectedAccount.AccountId, request);

            if (created != null)
            {
                // The change notification triggers a full reload instead of inserting locally, so
                // the superseded-record filtering always applies.
                _accountStateService.NotifyTransactionsChanged();
                ShowAddTransactionDialog = false;

                await Application.Current!.MainPage!.DisplayAlert(
                    "Success", $"'{created.Description}' created successfully.", "OK");
            }
            else
            {
                ErrorMessage = "Failed to create transaction. Please try again.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"An unexpected error occurred: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static int FindInsertionIndex(ObservableCollection<TransactionCardViewModel> list, DateTime startDate)
    {
        int left = 0, right = list.Count - 1;
        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            if (list[mid].Transaction.StartDate >= startDate) left = mid + 1;
            else right = mid - 1;
        }
        return left;
    }

    [RelayCommand]
    private void ToggleIncomeExpense() => IsIncome = !IsIncome;

    [RelayCommand]
    private void ShowEditTransaction(Transaction transaction)
    {
        TransactionToEdit = transaction;
        EditDescription = transaction.Description;
        EditAmount = Math.Abs(transaction.Amount).ToString("0.00");
        EditCategory = transaction.Category ?? string.Empty;
        EditFrequency = transaction.Frequency;
        EditStartDate = transaction.StartDate;
        EditHasEndDate = transaction.EndDate.HasValue;
        EditEndDate = transaction.EndDate ?? DateTime.Today.AddYears(1);
        EditIsIncome = transaction.Amount >= 0;
        EditIsActive = transaction.IsActive;
        EditColor = transaction.Color;

        EditDescriptionError = string.Empty;
        EditAmountError = string.Empty;
        EditCategoryError = string.Empty;
        EditDateRangeError = string.Empty;
        ErrorMessage = string.Empty;
        ActiveTooltip = string.Empty;

        ShowEditTransactionDialog = true;
    }

    [RelayCommand]
    private void CancelEditTransaction()
    {
        ShowEditTransactionDialog = false;
        TransactionToEdit = null;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private async Task UpdateTransactionAsync()
    {
        if (TransactionToEdit == null)
        { ErrorMessage = "No transaction selected for editing."; return; }

        EditDescriptionError = UIValidator.ValidateDescription(EditDescription) ?? string.Empty;
        EditAmountError = UIValidator.ValidateAmount(EditAmount) ?? string.Empty;
        EditCategoryError = UIValidator.ValidateCategory(EditCategory) ?? string.Empty;
        EditDateRangeError = UIValidator.ValidateDateRange(
                                   EditStartDate, EditHasEndDate ? EditEndDate : null, EditFrequency) ?? string.Empty;

        if (!string.IsNullOrEmpty(EditDescriptionError) ||
            !string.IsNullOrEmpty(EditAmountError) ||
            !string.IsNullOrEmpty(EditCategoryError) ||
            !string.IsNullOrEmpty(EditDateRangeError))
            return;

        var amount = decimal.Parse(EditAmount.Trim());

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var request = new UpdateTransactionRequest
            {
                Description = EditDescription.Trim(),
                Amount = EditIsIncome ? amount : -amount,
                Category = EditCategory == "Uncategorized" || string.IsNullOrWhiteSpace(EditCategory) ? null : EditCategory.Trim(),
                Frequency = EditFrequency,
                StartDate = EditStartDate,
                // Once has a single occurrence, never send EndDate to the server
                EndDate = (EditHasEndDate && EditFrequency != FrequencyType.Once) ? EditEndDate : null,
                IsActive = EditIsActive,
                Color = EditColor,
            };

            var updated = await _apiService.UpdateTransactionAsync(
                SelectedAccount!.AccountId, TransactionToEdit.TransactionId, request);

            if (updated != null)
            {
                var idx = Transactions
                    .Select((t, i) => (t, i))
                    .FirstOrDefault(x => x.t.TransactionId == TransactionToEdit.TransactionId).i;
                if (idx >= 0)
                {
                    var updCard = new TransactionCardViewModel(updated, this, BuildPredecessorChain(updated)) { IsExpanded = Transactions[idx].IsExpanded };
                    Transactions[idx] = updCard;
                }

                _accountStateService.NotifyTransactionsChanged();
                ShowEditTransactionDialog = false;
                TransactionToEdit = null;
                PredecessorToEdit = null; // Cleared for both predecessor and active edits.

                await Application.Current!.MainPage!.DisplayAlert(
                    "Success", $"'{updated.Description}' updated successfully.", "OK");
            }
            else
            {
                ErrorMessage = "Failed to update transaction. Please try again.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"An unexpected error occurred: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ToggleEditIncomeExpense() => EditIsIncome = !EditIsIncome;

    [RelayCommand]
    private async Task ToggleActiveAsync(Transaction transaction)
    {
        if (SelectedAccount == null) return;

        var index = Transactions.Select((c, i) => (c, i)).FirstOrDefault(x => x.c.TransactionId == transaction.TransactionId).i;
        if (index < 0) return;

        var request = new UpdateTransactionRequest
        {
            Description = transaction.Description,
            Amount = transaction.Amount,
            Category = transaction.Category ?? string.Empty,
            Frequency = transaction.Frequency,
            StartDate = transaction.StartDate,
            EndDate = transaction.EndDate,
            IsActive = !transaction.IsActive,
            Color = transaction.Color,
        };

        try
        {
            var updated = await _apiService.UpdateTransactionAsync(
                SelectedAccount.AccountId, transaction.TransactionId, request);

            if (updated != null)
            {
                var updCard = new TransactionCardViewModel(updated, this, BuildPredecessorChain(updated)) { IsExpanded = Transactions[index].IsExpanded };
                Transactions[index] = updCard;
                _accountStateService.NotifyTransactionsChanged();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ToggleActive error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SelectNewColor(string parameter)
        => NewColor = string.IsNullOrEmpty(parameter) ? null : parameter;

    [RelayCommand]
    private void SelectEditColor(string parameter)
        => EditColor = string.IsNullOrEmpty(parameter) ? null : parameter;

    [RelayCommand]
    private void SelectAmendColor(string parameter)
        => AmendColor = string.IsNullOrEmpty(parameter) ? null : parameter;

    [RelayCommand]
    private void SelectEditCategory(string category) => EditCategory = category;

    [RelayCommand]
    private void SelectAmendCategory(string category) => AmendCategory = category;

    [RelayCommand]
    private async Task DeleteTransactionAsync(Transaction transaction)
    {
        var mainPage = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (mainPage == null) return;

        var confirm = await mainPage.DisplayAlert(
            "Delete Transaction",
            $"Are you sure you want to delete '{transaction.Description}'?",
            "Delete", "Cancel");

        if (!confirm) return;

        IsLoading = true;
        try
        {
            var success = await _apiService.DeleteTransactionAsync(
                SelectedAccount!.AccountId, transaction.TransactionId);

            if (success)
            {
                var removeIdx = Transactions.Select((c, i) => (c, i)).FirstOrDefault(x => x.c.TransactionId == transaction.TransactionId).i;
                if (removeIdx >= 0) Transactions.RemoveAt(removeIdx);

                _accountStateService.NotifyTransactionsChanged();
                await mainPage.DisplayAlert("Success", "Transaction deleted.", "OK");
            }
            else
            {
                await mainPage.DisplayAlert("Error", "Failed to delete. Please try again.", "OK");
            }
        }
        catch
        {
            await mainPage.DisplayAlert("Error", "Failed to delete. Please try again.", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenFilter()
    {
        // The dialog edits copies, so Cancel leaves the applied filters untouched.
        PendingFilterRecurrence = FilterRecurrence;
        PendingFilterDateFrom = FilterDateFrom ?? DateTime.Today.AddMonths(-1);
        PendingFilterDateTo = FilterDateTo ?? DateTime.Today.AddYears(1);
        PendingFilterAmountMin = FilterAmountMin;
        PendingFilterAmountMax = FilterAmountMax;
        PendingFilterCategory = FilterCategory;
        ShowFilterDialog = true;
    }

    [RelayCommand]
    private void ApplyFilter()
    {
        FilterRecurrence = PendingFilterRecurrence;
        FilterDateFrom = PendingFilterDateFrom;
        FilterDateTo = PendingFilterDateTo;
        FilterAmountMin = PendingFilterAmountMin;
        FilterAmountMax = PendingFilterAmountMax;
        FilterCategory = PendingFilterCategory;
        IsFilterActive = IsAnyFilterSet();
        ShowFilterDialog = false;
        // Reloads from the full dataset: with a filter active the list is a subset, and
        // re-filtering that subset would keep transactions outside the view hidden.
        LoadTransactions(forceRefresh: false);
    }

    [RelayCommand]
    private void ClearFilter()
    {
        FilterRecurrence = null; FilterDateFrom = null; FilterDateTo = null;
        FilterAmountMin = string.Empty; FilterAmountMax = string.Empty;
        FilterCategory = string.Empty;
        IsFilterActive = false;
        ShowFilterDialog = false;
        // Reloads from the full dataset because ApplySort only re-sorts the list, which may be a
        // filtered subset.
        LoadTransactions(forceRefresh: false);
    }

    [RelayCommand]
    private void CancelFilter() => ShowFilterDialog = false;

    private bool IsAnyFilterSet() =>
        FilterRecurrence.HasValue ||
        FilterDateFrom.HasValue ||
        FilterDateTo.HasValue ||
        !string.IsNullOrEmpty(FilterAmountMin) ||
        !string.IsNullOrEmpty(FilterAmountMax) ||
        !string.IsNullOrEmpty(FilterCategory);

    /// <summary>
    /// Toggles a named tooltip: the same key hides it and a different key switches to it.
    /// </summary>
    [RelayCommand]
    private void ToggleTooltip(string key)
    {
        ActiveTooltip = ActiveTooltip == key ? string.Empty : key;
    }

    /// <summary>
    /// Handles a sort chip tap. The same field flips the direction; a new field starts descending
    /// for date and amount and ascending for name and recurrence.
    /// </summary>
    [RelayCommand]
    private void SetSort(string parameter)
    {
        if (!Enum.TryParse<TransactionSortField>(parameter, out var field)) return;

        if (field == ActiveSortField)
        {
            // The same field flips the direction.
            SortDirection = SortDirection == SortDirection.Ascending
                ? SortDirection.Descending
                : SortDirection.Ascending;
        }
        else
        {
            // A new field starts with its natural direction.
            ActiveSortField = field;
            SortDirection = field is TransactionSortField.Name or TransactionSortField.Recurrence
                ? SortDirection.Ascending
                : SortDirection.Descending;
        }
    }

    private List<Transaction> BuildPredecessorChain(Transaction tx)
    {
        var result = new List<Transaction>();
        var predId = tx.PredecessorTransactionId;
        while (predId.HasValue)
        {
            var p = _allTransactionsWithHistory.FirstOrDefault(t => t.TransactionId == predId.Value);
            if (p == null) break;
            result.Add(p);
            predId = p.PredecessorTransactionId;
        }
        result.Reverse();
        return result;
    }

    [RelayCommand]
    private void EditPredecessor(Transaction predecessor)
    {
        PredecessorToEdit = predecessor;
        TransactionToEdit = predecessor;
        EditDescription = predecessor.Description;
        EditAmount = Math.Abs(predecessor.Amount).ToString("0.00");
        EditIsIncome = predecessor.Amount >= 0;
        EditCategory = predecessor.Category ?? "Uncategorized";
        EditFrequency = predecessor.Frequency;
        EditStartDate = predecessor.StartDate;
        EditEndDate = predecessor.EndDate ?? DateTime.Today.AddYears(1);
        EditHasEndDate = predecessor.EndDate.HasValue;
        EditIsActive = predecessor.IsActive;
        EditColor = predecessor.Color;
        EditDescriptionError = string.Empty;
        EditAmountError = string.Empty;
        EditCategoryError = string.Empty;
        EditDateRangeError = string.Empty;
        ErrorMessage = string.Empty;
        ShowEditTransactionDialog = true;
    }

    [RelayCommand]
    private async Task RestoreTransactionAsync(Transaction predecessor)
    {
        if (SelectedAccount == null) return;

        var successor = _allTransactionsWithHistory
            .FirstOrDefault(t => t.PredecessorTransactionId == predecessor.TransactionId && !t.IsSuperseded);

        if (successor == null)
        {
            await Application.Current!.MainPage!.DisplayAlert("Error",
                "Could not find the current version of this transaction.", "OK");
            return;
        }

        var mainPage = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (mainPage == null) return;

        var confirm = await mainPage.DisplayAlert("Restore Previous Version",
            $"This will remove '{successor.Description}' ({successor.AmountDisplay}, from {successor.StartDate:MMM dd, yyyy}) " +
            $"and restore '{predecessor.Description}' ({predecessor.AmountDisplay}) as the active version " +
            $"from {successor.StartDate:MMM dd, yyyy} onwards.\n\nAre you sure?",
            "Restore", "Cancel");
        if (!confirm) return;

        IsLoading = true;
        try
        {
            var deleted = await _apiService.DeleteTransactionAsync(SelectedAccount.AccountId, successor.TransactionId);
            if (!deleted)
            {
                await mainPage.DisplayAlert("Error",
                    "Failed to remove the current version. No changes were made.", "OK");
                return;
            }

            var restored = await _apiService.UpdateTransactionAsync(
                SelectedAccount.AccountId, predecessor.TransactionId,
                new UpdateTransactionRequest
                {
                    Description = predecessor.Description,
                    Amount = predecessor.Amount,
                    Category = predecessor.Category ?? string.Empty,
                    Frequency = predecessor.Frequency,
                    StartDate = predecessor.StartDate,
                    EndDate = null,
                    IsActive = predecessor.IsActive,
                    Color = predecessor.Color,
                });

            if (restored != null)
            {
                _accountStateService.NotifyTransactionsChanged();
                LoadTransactions(forceRefresh: true);
                await mainPage.DisplayAlert("Restored",
                    $"'{predecessor.Description}' has been restored as the active version.", "OK");
            }
            else
            {
                await mainPage.DisplayAlert("Partial Error",
                    "The current version was removed but the previous version could not be fully restored. Please refresh.", "OK");
            }
        }
        catch (Exception ex)
        {
            await mainPage.DisplayAlert("Error", $"An unexpected error occurred: {ex.Message}", "OK");
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void ShowAmendTransaction(Transaction transaction)
    {
        TransactionToAmend = transaction;
        AmendEffectiveDate = DateTime.Today;
        AmendAmount = Math.Abs(transaction.Amount).ToString("0.00");
        AmendIsIncome = transaction.Amount >= 0;
        AmendDescription = string.Empty;
        AmendCategory = "Uncategorized";
        AmendColor = null;

        AmendEffectiveDateError = string.Empty;
        AmendAmountError = string.Empty;
        AmendDescriptionError = string.Empty;
        AmendCategoryError = string.Empty;
        ErrorMessage = string.Empty;
        ActiveTooltip = string.Empty;

        ShowAmendDialog = true;
    }

    [RelayCommand]
    private void CancelAmendTransaction()
    {
        ShowAmendDialog = false;
        TransactionToAmend = null;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void ToggleAmendIncomeExpense() => AmendIsIncome = !AmendIsIncome;

    [RelayCommand]
    private async Task ApplyAmendmentAsync()
    {
        if (TransactionToAmend == null)
        { ErrorMessage = "No transaction selected."; return; }

        AmendEffectiveDateError = UIValidator.ValidateAmendEffectiveDate(
                                      AmendEffectiveDate, TransactionToAmend.StartDate) ?? string.Empty;
        AmendAmountError = UIValidator.ValidateAmount(AmendAmount) ?? string.Empty;
        AmendDescriptionError = UIValidator.ValidateAmendDescription(AmendDescription) ?? string.Empty;
        AmendCategoryError = UIValidator.ValidateCategory(AmendCategory) ?? string.Empty;

        if (!string.IsNullOrEmpty(AmendEffectiveDateError) ||
            !string.IsNullOrEmpty(AmendAmountError) ||
            !string.IsNullOrEmpty(AmendDescriptionError) ||
            !string.IsNullOrEmpty(AmendCategoryError))
            return;

        var amount = decimal.Parse(AmendAmount.Trim());

        // An effective date that is off the transaction's schedule, such as a Friday for a weekly
        // Thursday series, makes the server create a spurious single occurrence on that date
        // before resuming the schedule. Snapping to the next scheduled date avoids it.
        var snappedDate = NextOccurrenceOnOrAfter(
            AmendEffectiveDate, TransactionToAmend.StartDate, TransactionToAmend.Frequency);

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var request = new AmendTransactionRequest
            {
                EffectiveDate = snappedDate,
                Amount = AmendIsIncome ? amount : -amount,
                Description = string.IsNullOrWhiteSpace(AmendDescription) ? null : AmendDescription.Trim(),
                Category = AmendCategory == "Uncategorized" || string.IsNullOrWhiteSpace(AmendCategory) ? null : AmendCategory.Trim(),
                Color = AmendColor,
            };

            var successor = await _apiService.AmendTransactionAsync(
                SelectedAccount!.AccountId, TransactionToAmend.TransactionId, request);

            if (successor != null)
            {
                ShowAmendDialog = false;
                TransactionToAmend = null;
                _accountStateService.NotifyTransactionsChanged();
                LoadTransactions(forceRefresh: true);

                await Application.Current!.MainPage!.DisplayAlert(
                    "Success",
                    $"'{successor.Description}' updated from {snappedDate:MMM dd, yyyy} onward.",
                    "OK");
            }
            else
            {
                ErrorMessage = "Failed to apply amendment. Please try again.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"An unexpected error occurred: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ShowAddCategory()
    {
        NewCategoryName  = string.Empty;
        NewCategoryError = string.Empty;
        ShowNewCategoryInput = true;
    }

    [RelayCommand]
    private void CancelAddCategory()
    {
        ShowNewCategoryInput = false;
        NewCategoryName  = string.Empty;
        NewCategoryError = string.Empty;
    }

    [RelayCommand]
    private async Task SaveNewCategoryAsync()
    {
        var trimmed = NewCategoryName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        { NewCategoryError = "Category name cannot be empty."; return; }
        if (trimmed.Length > 50)
        { NewCategoryError = "Category name must be 50 characters or fewer."; return; }
        if (AvailableCategories.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        { NewCategoryError = "That category already exists."; return; }

        NewCategoryError = string.Empty;
        IsSavingCategory = true;

        try
        {
            var created = await _apiService.CreateCategoryAsync(new CreateCategoryRequest { Name = trimmed });
            if (created == null)
            { NewCategoryError = "Failed to create category. Please try again."; return; }

            // Reloads from the server so the list matches what other pages will see.
            var cats = await _apiService.GetCategoriesAsync(forceRefresh: true);
            var names = new List<string> { "Uncategorized" };
            names.AddRange(cats.Select(c => c.Name));
            AvailableCategories = names;
            _categoriesLoaded = true;

            // Selects the new category in whichever dialog is open.
            if (ShowAddTransactionDialog)      NewCategory   = trimmed;
            else if (ShowEditTransactionDialog) EditCategory  = trimmed;
            else if (ShowAmendDialog)           AmendCategory = trimmed;
        }
        catch (Exception ex) { NewCategoryError = $"Error: {ex.Message}"; return; }
        finally { IsSavingCategory = false; }

        ShowNewCategoryInput = false;
        NewCategoryName = string.Empty;
    }

    [RelayCommand]
    private async Task NavigateToAccounts()
    {
        try { await Shell.Current.GoToAsync("accounts"); }
        catch { }
    }

    /// <summary>
    /// Opens the add-entry page from the floating action button, as the Home page does.
    /// </summary>
    [RelayCommand]
    private async Task NavigateToAddEntry()
    {
        try { await Shell.Current.GoToAsync("add"); }
        catch { }
    }

    /// <summary>
    /// Returns the first date on or after <paramref name="from"/> that falls on the schedule of
    /// <paramref name="frequency"/>, anchored to <paramref name="anchor"/> (the transaction's
    /// original start date). One-time and daily transactions have no alignment, so
    /// <paramref name="from"/> is returned unchanged.
    /// </summary>
    private static DateTime NextOccurrenceOnOrAfter(
        DateTime from, DateTime anchor, FrequencyType frequency)
    {
        switch (frequency)
        {
            case FrequencyType.Once:
            case FrequencyType.Daily:
                // Every day is on schedule.
                return from;

            case FrequencyType.Weekly:
            {
                // Same day of the week as the anchor.
                int targetDow = (int)anchor.DayOfWeek;
                int currentDow = (int)from.DayOfWeek;
                int daysAhead = (targetDow - currentDow + 7) % 7;
                return from.AddDays(daysAhead);
            }

            case FrequencyType.BiWeekly:
            {
                // Same day of the week as the anchor, in a week an even number of weeks away.
                int targetDow = (int)anchor.DayOfWeek;
                int currentDow = (int)from.DayOfWeek;
                int daysToTargetDow = (targetDow - currentDow + 7) % 7;
                var candidate = from.AddDays(daysToTargetDow);
                // An odd number of weeks since the anchor means the off week.
                int weekDiff = (int)((candidate - anchor.Date).TotalDays / 7);
                if (weekDiff % 2 != 0)
                    candidate = candidate.AddDays(7); // Skip to the next on-schedule week.
                return candidate;
            }

            case FrequencyType.FirstThirdFriday:
            {
                // Must be the 1st or 3rd Friday of its month.
                var candidate = from;
                while (candidate.DayOfWeek != DayOfWeek.Friday)
                    candidate = candidate.AddDays(1);
                while (true)
                {
                    int fridayOfMonth = ((candidate.Day - 1) / 7) + 1; // Ordinal, starting at 1.
                    if (fridayOfMonth == 1 || fridayOfMonth == 3) return candidate;
                    candidate = candidate.AddDays(7);
                }
            }

            case FrequencyType.Monthly:
            {
                // Same day of the month as the anchor, clamped to the last day of short months.
                int targetDay = anchor.Day;
                var candidate = new DateTime(from.Year, from.Month,
                    Math.Min(targetDay, DateTime.DaysInMonth(from.Year, from.Month)));
                if (candidate < from)
                {
                    // This month's date has passed, so use next month's.
                    var next = from.AddMonths(1);
                    candidate = new DateTime(next.Year, next.Month,
                        Math.Min(targetDay, DateTime.DaysInMonth(next.Year, next.Month)));
                }
                return candidate;
            }

            case FrequencyType.BiMonthly:
            {
                // Same day of the month as the anchor, in months an even number of months away.
                int targetDay = anchor.Day;
                var candidate = new DateTime(from.Year, from.Month,
                    Math.Min(targetDay, DateTime.DaysInMonth(from.Year, from.Month)));
                if (candidate < from) candidate = candidate.AddMonths(1);

                // Walks forward until the month offset from the anchor is even.
                while (true)
                {
                    int monthDiff = ((candidate.Year - anchor.Year) * 12)
                                    + (candidate.Month - anchor.Month);
                    if (monthDiff % 2 == 0 && candidate >= from) return candidate;
                    candidate = candidate.AddMonths(1);
                    candidate = new DateTime(candidate.Year, candidate.Month,
                        Math.Min(targetDay, DateTime.DaysInMonth(candidate.Year, candidate.Month)));
                }
            }

            default:
                return from;
        }
    }
}