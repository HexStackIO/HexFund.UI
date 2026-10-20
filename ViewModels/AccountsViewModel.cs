using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HexFund.UI.Config;
using HexFund.UI.Models;
using HexFund.UI.Services;
using HexFund.UI.Validation;
using System.Collections.ObjectModel;

namespace HexFund.UI.ViewModels;

/// <summary>Lists, creates, edits, selects and deletes accounts.</summary>
/// <summary>Lists, creates, edits, selects and deletes accounts.</summary>
/// <summary>Lists, creates, edits, selects and deletes accounts.</summary>
public partial class AccountsViewModel : BaseViewModel
{
    private readonly IApiService _apiService;
    private readonly IAuthService _authService;
    private readonly IAccountStateService _accountStateService;

    [ObservableProperty] private ObservableCollection<Account> accounts = new();
    [ObservableProperty] private Account? selectedAccount;
    [ObservableProperty] private bool isRefreshing;
    [ObservableProperty] private bool isLoading;

    [ObservableProperty] private bool showAddAccountDialog;
    [ObservableProperty] private string newAccountName    = string.Empty;
    [ObservableProperty] private string newAccountBalance = string.Empty;

    // Field errors for the add dialog.
    [ObservableProperty] private string newAccountNameError    = string.Empty;
    [ObservableProperty] private string newAccountBalanceError = string.Empty;

    // Shown for unexpected API failures, separate from field errors.
    [ObservableProperty] private string addErrorMessage = string.Empty;

    [ObservableProperty] private bool showEditAccountDialog;
    [ObservableProperty] private Account? accountToEdit;
    [ObservableProperty] private string editAccountName = string.Empty;
    [ObservableProperty] private bool editIsActive      = true;

    // Field errors for the edit dialog.
    [ObservableProperty] private string editAccountNameError = string.Empty;

    [ObservableProperty] private string editErrorMessage = string.Empty;

    // The key of the open tooltip, or empty when none is open.
    [ObservableProperty] private string activeTooltip = string.Empty;

    private bool _loaded;

    public AccountsViewModel(
        IApiService apiService,
        IAuthService authService,
        IAccountStateService accountStateService,
        ISettingsService settingsService)
        : base(settingsService)
    {
        _apiService           = apiService;
        _authService          = authService;
        _accountStateService  = accountStateService;

        _accountStateService.SelectedAccountChanged += OnSelectedAccountChangedExternally;
    }

    private void OnSelectedAccountChangedExternally()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_accountStateService.SelectedAccount != null &&
                SelectedAccount?.AccountId != _accountStateService.SelectedAccount.AccountId)
            {
                SelectedAccount = _accountStateService.SelectedAccount;
            }
        });
    }

    public async Task InitializeAsync()
    {
        OnPropertyChanged(nameof(ThemePrimaryColor));
        OnPropertyChanged(nameof(ThemeMutedColor));

        if (_loaded) return;
        await LoadAccountsAsync();
    }

    [RelayCommand]
    private async Task LoadAccountsAsync()
    {
        IsRefreshing = true;

        try
        {
            var accountsList = await _apiService.GetAccountsAsync();
            Accounts = new ObservableCollection<Account>(accountsList);

            if (_accountStateService.SelectedAccount != null)
                SelectedAccount = Accounts.FirstOrDefault(
                    a => a.AccountId == _accountStateService.SelectedAccount.AccountId);

            if (SelectedAccount == null && Accounts.Count == 1)
            {
                SelectedAccount = Accounts.First();
                _accountStateService.SelectedAccount = SelectedAccount;
            }

            _loaded = true;
        }
        catch (Exception ex)
        {
            await ShowPageAlertAsync("Error", $"Failed to load accounts: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        _loaded = false;
        await LoadAccountsAsync();
    }

    [RelayCommand]
    private void ToggleTooltip(string key)
    {
        ActiveTooltip = ActiveTooltip == key ? string.Empty : key;
    }

    [RelayCommand]
    private void ShowAddAccount()
    {
        NewAccountName    = string.Empty;
        NewAccountBalance = string.Empty;

        NewAccountNameError    = string.Empty;
        NewAccountBalanceError = string.Empty;
        AddErrorMessage        = string.Empty;
        ActiveTooltip          = string.Empty;

        ShowAddAccountDialog = true;
    }

    [RelayCommand]
    private void CancelAddAccount()
    {
        ShowAddAccountDialog = false;
        AddErrorMessage      = string.Empty;
    }

    [RelayCommand]
    private async Task CreateAccountAsync()
    {
        NewAccountNameError    = UIValidator.ValidateAccountName(NewAccountName)        ?? string.Empty;
        NewAccountBalanceError = UIValidator.ValidateInitialBalance(NewAccountBalance)  ?? string.Empty;

        if (string.IsNullOrEmpty(NewAccountNameError))
            NewAccountNameError = UIValidator.ValidateAccountNameUnique(NewAccountName, Accounts) ?? string.Empty;

        if (!string.IsNullOrEmpty(NewAccountNameError) ||
            !string.IsNullOrEmpty(NewAccountBalanceError))
            return;

        // Validated above, so parsing cannot fail.
        var balance = decimal.Parse(NewAccountBalance.Trim());

        IsLoading       = true;
        AddErrorMessage = string.Empty;

        try
        {
            var request = new CreateAccountRequest
            {
                AccountName    = NewAccountName.Trim(),
                InitialBalance = balance,
                Currency       = "USD"
            };

            var newAccount = await _apiService.CreateAccountAsync(request);

            if (newAccount != null)
            {
                Accounts.Add(newAccount);
                SelectedAccount = newAccount;
                _accountStateService.SelectedAccount = newAccount;
                ShowAddAccountDialog = false;

                await ShowPageAlertAsync("Success",
                    $"Account '{newAccount.AccountName}' created successfully!");
            }
            else
            {
                AddErrorMessage = "Failed to create account. Please try again.";
            }
        }
        catch (Exception ex)
        {
            AddErrorMessage = $"An unexpected error occurred: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ShowEditAccount(Account account)
    {
        AccountToEdit   = account;
        EditAccountName = account.AccountName;
        EditIsActive    = account.IsActive;

        EditAccountNameError = string.Empty;
        EditErrorMessage     = string.Empty;
        ActiveTooltip        = string.Empty;

        ShowEditAccountDialog = true;
    }

    [RelayCommand]
    private void CancelEditAccount()
    {
        ShowEditAccountDialog = false;
        AccountToEdit         = null;
        EditErrorMessage      = string.Empty;
    }

    [RelayCommand]
    private async Task UpdateAccountAsync()
    {
        if (AccountToEdit == null)
        { EditErrorMessage = "No account selected for editing."; return; }

        EditAccountNameError = UIValidator.ValidateAccountName(EditAccountName) ?? string.Empty;

        // The account being edited is excluded from the uniqueness check.
        if (string.IsNullOrEmpty(EditAccountNameError))
            EditAccountNameError = UIValidator.ValidateAccountNameUnique(
                EditAccountName, Accounts, AccountToEdit?.AccountId) ?? string.Empty;

        if (!string.IsNullOrEmpty(EditAccountNameError))
            return;

        IsLoading        = true;
        EditErrorMessage = string.Empty;

        try
        {
            var request = new UpdateAccountRequest
            {
                AccountName = EditAccountName.Trim(),
                IsActive    = EditIsActive
            };

            var updatedAccount = await _apiService.UpdateAccountAsync(
                AccountToEdit.AccountId, request);

            if (updatedAccount != null)
            {
                var index = Accounts.IndexOf(AccountToEdit);
                if (index >= 0)
                {
                    Accounts[index] = updatedAccount;

                    if (SelectedAccount?.AccountId == updatedAccount.AccountId)
                    {
                        SelectedAccount = updatedAccount;
                        _accountStateService.SelectedAccount = updatedAccount;
                    }
                }

                ShowEditAccountDialog = false;
                AccountToEdit         = null;

                await ShowPageAlertAsync("Success",
                    $"Account '{updatedAccount.AccountName}' updated successfully!");
            }
            else
            {
                EditErrorMessage = "Failed to update account. Please try again.";
            }
        }
        catch (Exception ex)
        {
            EditErrorMessage = $"An unexpected error occurred: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SelectAccount(Account account)
    {
        SelectedAccount = account;
        _accountStateService.SelectedAccount = account;
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task DeleteAccountAsync(Account account)
    {
        var mainPage = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (mainPage == null) return;

        bool confirm = await mainPage.DisplayAlert(
            "Delete Account",
            $"Are you sure you want to delete '{account.AccountName}'? " +
            "This will also delete all associated transactions.",
            "Delete", "Cancel");

        if (!confirm) return;

        IsLoading = true;

        try
        {
            var success = await _apiService.DeleteAccountAsync(account.AccountId);

            if (success)
            {
                Accounts.Remove(account);

                if (SelectedAccount?.AccountId == account.AccountId)
                {
                    SelectedAccount = Accounts.FirstOrDefault();
                    _accountStateService.SelectedAccount = SelectedAccount;
                }

                await mainPage.DisplayAlert("Success", "Account deleted successfully.", "OK");
            }
            else
            {
                await mainPage.DisplayAlert(
                    "Error", "Failed to delete account. Please try again.", "OK");
            }
        }
        catch
        {
            await mainPage.DisplayAlert(
                "Error", "Failed to delete account. Please try again.", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Strips invalid characters on every keystroke.
    partial void OnNewAccountBalanceChanged(string value)
    {
        var sanitized = UIValidator.SanitizeDecimalInput(value, allowNegative: true);
        if (sanitized != value) NewAccountBalance = sanitized;
    }

    private static Task ShowPageAlertAsync(string title, string message) =>
        Application.Current?.MainPage?.DisplayAlert(title, message, "OK")
        ?? Task.CompletedTask;
}
