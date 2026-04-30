using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HexFund.UI.Models;
using HexFund.UI.Services;
using HexFund.UI.Validation;

namespace HexFund.UI.ViewModels;

/// <summary>
/// AddEntryViewModel — Sprint 04 (full fields).
///
/// Mirrors the complete CreateTransactionRequest surface:
///   Income/Expense toggle, Amount, Description, Category, Frequency,
///   Start Date, optional End Date, and Colour picker.
///
/// Self-contained — calls IApiService directly so the sheet works from
/// any entry point (Home, Ledger, Calendar). On success raises
/// TransactionsChanged via IAccountStateService so all subscribers refresh.
/// </summary>
public partial class AddEntryViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly IAccountStateService _accountStateService;

    // ── Account picker ────────────────────────────────────────────────────────
    [ObservableProperty] private List<Account> availableAccounts = new();
    [ObservableProperty] private Account? selectedTransactionAccount;
    [ObservableProperty] private bool showAccountPicker;

    // ── Form fields ───────────────────────────────────────────────────────────
    [ObservableProperty] private bool isIncome = true;
    [ObservableProperty] private string amount = string.Empty;
    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private string selectedCategory = "Uncategorized";
    [ObservableProperty] private List<string> availableCategories = new() { "Uncategorized" };
    [ObservableProperty] private FrequencyType selectedFrequency = FrequencyType.Once;
    [ObservableProperty] private DateTime startDate = DateTime.Today;
    [ObservableProperty] private DateTime endDate = DateTime.Today.AddYears(1);
    [ObservableProperty] private bool hasEndDate;
    [ObservableProperty] private string? selectedColor;

    // ── Add Category inline ───────────────────────────────────────────────────
    [ObservableProperty] private bool showNewCategoryInput;
    [ObservableProperty] private string newCategoryName = string.Empty;
    [ObservableProperty] private string newCategoryError = string.Empty;
    [ObservableProperty] private bool isSavingCategory;

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

    // ── Validation errors ─────────────────────────────────────────────────────
    [ObservableProperty] private string amountError      = string.Empty;
    [ObservableProperty] private string descriptionError = string.Empty;
    [ObservableProperty] private string categoryError    = string.Empty;
    [ObservableProperty] private string dateRangeError   = string.Empty;

    // ── State ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string errorMessage = string.Empty;
    [ObservableProperty] private bool hasError;

    // ── Computed display ──────────────────────────────────────────────────────
    public string IncomeLabel  => IsIncome ? "Income" : "Expense";
    public Color  IncomeColor  => IsIncome
        ? GetSemanticColor("Up",   "#3FB97A")
        : GetSemanticColor("Down", "#E05A6F");

    public Color ColorPreview =>
        SelectedColor != null
            ? Microsoft.Maui.Graphics.Color.FromArgb(SelectedColor)
            : (IsIncome
                ? GetSemanticColor("Up",   "#3FB97A")
                : GetSemanticColor("Down", "#E05A6F"));

    public bool HasAccount    => SelectedTransactionAccount != null;
    public string AccountName => SelectedTransactionAccount?.AccountName
                                 ?? _accountStateService.SelectedAccount?.AccountName
                                 ?? "No account selected";

    // ── Constructor ───────────────────────────────────────────────────────────

    public AddEntryViewModel(
        IApiService apiService,
        IAccountStateService accountStateService,
        ISettingsService settingsService)
        : base(settingsService)
    {
        _apiService = apiService;
        _accountStateService = accountStateService;
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        ResetForm();
        await Task.WhenAll(LoadCategoriesAsync(), LoadAccountsAsync());
        OnPropertyChanged(nameof(HasAccount));
        OnPropertyChanged(nameof(AccountName));
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            var accounts = await _apiService.GetAccountsAsync();
            AvailableAccounts = accounts?.Where(a => a.IsActive).ToList() ?? new();
            SelectedTransactionAccount = AvailableAccounts
                .FirstOrDefault(a => a.AccountId == _accountStateService.SelectedAccount?.AccountId)
                ?? AvailableAccounts.FirstOrDefault();
            OnPropertyChanged(nameof(HasAccount));
            OnPropertyChanged(nameof(AccountName));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AddEntryVM account load: {ex.Message}");
        }
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var cats = await _apiService.GetCategoriesAsync();
            RebuildCategoryList(cats);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AddEntryVM category load: {ex.Message}");
        }
    }

    private void RebuildCategoryList(List<UserCategory> cats)
    {
        var names = new List<string> { "Uncategorized" };
        names.AddRange(cats.Select(c => c.Name));
        AvailableCategories = names;
        if (!AvailableCategories.Contains(SelectedCategory))
            SelectedCategory = "Uncategorized";
    }

    private void ResetForm()
    {
        IsIncome          = true;
        Amount            = string.Empty;
        Description       = string.Empty;
        SelectedCategory  = "Uncategorized";
        SelectedFrequency = FrequencyType.Once;
        StartDate         = DateTime.Today;
        EndDate           = DateTime.Today;   // Once: end == start
        HasEndDate        = true;             // Once: end date is always set
        SelectedColor     = null;
        AmountError       = string.Empty;
        DescriptionError  = string.Empty;
        CategoryError     = string.Empty;
        DateRangeError    = string.Empty;
        ErrorMessage      = string.Empty;
        HasError          = false;
        ShowNewCategoryInput = false;
        NewCategoryName   = string.Empty;
        NewCategoryError  = string.Empty;

        OnPropertyChanged(nameof(IncomeLabel));
        OnPropertyChanged(nameof(IncomeColor));
        OnPropertyChanged(nameof(ColorPreview));
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleType()
    {
        IsIncome = !IsIncome;
        OnPropertyChanged(nameof(IncomeLabel));
        OnPropertyChanged(nameof(IncomeColor));
        OnPropertyChanged(nameof(ColorPreview));
    }

    [RelayCommand]
    private void OpenAccountPicker() => ShowAccountPicker = true;

    [RelayCommand]
    private void SelectTransactionAccount(Account account)
    {
        SelectedTransactionAccount = account;
        ShowAccountPicker = false;
        OnPropertyChanged(nameof(AccountName));
        OnPropertyChanged(nameof(HasAccount));
    }

    [RelayCommand]
    private void SelectColor(string? color)
    {
        SelectedColor = string.IsNullOrEmpty(color) ? null : color;
        OnPropertyChanged(nameof(ColorPreview));
    }

    [RelayCommand]
    private void SelectCategory(string category) => SelectedCategory = category;

    // ── Add-category inline commands ──────────────────────────────────────────

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
        {
            NewCategoryError = "Category name cannot be empty.";
            return;
        }
        if (trimmed.Length > 50)
        {
            NewCategoryError = "Category name must be 50 characters or fewer.";
            return;
        }
        if (AvailableCategories.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            NewCategoryError = "That category already exists.";
            return;
        }

        NewCategoryError = string.Empty;
        IsSavingCategory = true;

        try
        {
            var created = await _apiService.CreateCategoryAsync(
                new CreateCategoryRequest { Name = trimmed });

            if (created != null)
            {
                // Reload full list from server to stay in sync
                var cats = await _apiService.GetCategoriesAsync(forceRefresh: true);
                RebuildCategoryList(cats);
                SelectedCategory = trimmed;
            }
            else
            {
                NewCategoryError = "Failed to create category. Please try again.";
                return;
            }
        }
        catch (Exception ex)
        {
            NewCategoryError = $"Error: {ex.Message}";
            return;
        }
        finally
        {
            IsSavingCategory = false;
        }

        ShowNewCategoryInput = false;
        NewCategoryName = string.Empty;
    }

    // ── Submit ────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SubmitAsync()
    {
        var account = SelectedTransactionAccount
                      ?? _accountStateService.SelectedAccount;
        if (account == null)
        {
            ErrorMessage = "No account selected. Please select an account first.";
            HasError     = true;
            return;
        }

        DescriptionError = UIValidator.ValidateDescription(Description) ?? string.Empty;
        AmountError      = UIValidator.ValidateAmount(Amount)           ?? string.Empty;
        CategoryError    = UIValidator.ValidateCategory(SelectedCategory) ?? string.Empty;
        DateRangeError   = UIValidator.ValidateDateRange(
                               StartDate, HasEndDate ? EndDate : null, SelectedFrequency) ?? string.Empty;

        if (!string.IsNullOrEmpty(DescriptionError) ||
            !string.IsNullOrEmpty(AmountError)      ||
            !string.IsNullOrEmpty(CategoryError)    ||
            !string.IsNullOrEmpty(DateRangeError))
            return;

        var parsedAmount = decimal.Parse(Amount.Trim());

        IsLoading    = true;
        ErrorMessage = string.Empty;
        HasError     = false;

        try
        {
            var request = new CreateTransactionRequest
            {
                Description = Description.Trim(),
                Amount      = IsIncome ? parsedAmount : -parsedAmount,
                Category    = SelectedCategory == "Uncategorized" ||
                              string.IsNullOrWhiteSpace(SelectedCategory)
                                  ? null : SelectedCategory.Trim(),
                Frequency   = SelectedFrequency,
                StartDate   = StartDate,
                EndDate     = (HasEndDate && SelectedFrequency != FrequencyType.Once) ? EndDate : null,
                Color       = SelectedColor,
            };

            var created = await _apiService.CreateTransactionAsync(account.AccountId, request);

            if (created != null)
            {
                _accountStateService.NotifyTransactionsChanged();
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                ErrorMessage = "Failed to create entry. Please try again.";
                HasError     = true;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"An unexpected error occurred: {ex.Message}";
            HasError     = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync() =>
        await Shell.Current.GoToAsync("..");

    // ── Partial hooks ─────────────────────────────────────────────────────────

    partial void OnAmountChanged(string value)
    {
        var sanitized = UIValidator.SanitizeDecimalInput(value, allowNegative: false);
        if (sanitized != value) Amount = sanitized; // triggers another change, but terminates immediately
    }

    partial void OnIsIncomeChanged(bool value)
    {
        OnPropertyChanged(nameof(IncomeLabel));
        OnPropertyChanged(nameof(IncomeColor));
        OnPropertyChanged(nameof(ColorPreview));
    }

    partial void OnSelectedColorChanged(string? value) =>
        OnPropertyChanged(nameof(ColorPreview));

    partial void OnSelectedTransactionAccountChanged(Account? value)
    {
        OnPropertyChanged(nameof(AccountName));
        OnPropertyChanged(nameof(HasAccount));
    }

    partial void OnSelectedFrequencyChanged(FrequencyType value)
    {
        if (value == FrequencyType.Once)
        {
            HasEndDate = true;
            EndDate    = StartDate;
        }
        else
        {
            HasEndDate = false;
            EndDate    = DateTime.Today.AddYears(1);
        }
    }

    partial void OnStartDateChanged(DateTime value)
    {
        if (SelectedFrequency == FrequencyType.Once)
            EndDate = value;
    }

    private static Color GetSemanticColor(string key, string fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c)
            return c;
        return Color.FromArgb(fallback);
    }
}
