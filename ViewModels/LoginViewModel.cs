using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HexFund.UI.Services;

namespace HexFund.UI.ViewModels;

/// <summary>Drives the sign-in page. Sign-in itself happens in the hosted Entra flow.</summary>
/// <summary>Drives the sign-in page. Sign-in itself happens in the hosted Entra flow.</summary>
/// <summary>Drives the sign-in page. Sign-in itself happens in the hosted Entra flow.</summary>
public partial class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly IApiService _apiService;
    private readonly IAccountStateService _accountStateService;

    [ObservableProperty] private bool   isLoading;
    [ObservableProperty] private string errorMessage = string.Empty;

    public LoginViewModel(
        IAuthService authService,
        IApiService apiService,
        IAccountStateService accountStateService,
        ISettingsService settingsService)
        : base(settingsService)
    {
        _authService = authService;
        _apiService = apiService;
        _accountStateService = accountStateService;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var success = await _authService.LoginAsync();

            if (success)
            {
                await HandlePostLoginNavigationAsync();
            }
            else
            {
                ErrorMessage = "Sign in failed. Please try again.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Login failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task HandlePostLoginNavigationAsync()
    {
        try
        {
            var accounts = await _apiService.GetAccountsAsync();

            if (accounts == null || !accounts.Any())
                return;

            if (accounts.Count == 1)
            {
                _accountStateService.SelectedAccount = accounts.First();
            }
            else
            {
                await Task.Delay(100);
                await Shell.Current.GoToAsync("accounts");
            }
        }
        catch (Exception ex)
        {
            // Not fatal: the user is already signed in and can pick an account from the tab bar.
        }
    }
}
