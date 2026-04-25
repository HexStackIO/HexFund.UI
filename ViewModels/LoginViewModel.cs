using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HexFund.UI.Services;

namespace HexFund.UI.ViewModels;

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
            // Entra External ID handles the sign-in UI — no email/password params needed
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
            // navigation failure is non-fatal — user is already logged in
        }
    }
}
