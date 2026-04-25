using HexFund.UI.Config;
using HexFund.UI.Models;
using Microsoft.Identity.Client;

namespace HexFund.UI.Services;

public interface IAuthService
{
    bool IsAuthenticated { get; }
    User? CurrentUser { get; set; }
    string? Token { get; }
    Task<bool> LoginAsync();
    Task<bool> RegisterAsync();
    Task LogoutAsync();
    /// <summary>
    /// Permanently deletes all user data from the server and removes all
    /// locally cached MSAL tokens. After this returns the user is signed out
    /// and cannot sign back into the same account.
    /// </summary>
    Task<bool> DeleteAccountAndDataAsync();
    event Action? AuthStateChanged;
}

public class AuthService : IAuthService
{
    public event Action? AuthStateChanged;
    public User? CurrentUser { get; set; }
    public string? Token { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    private readonly IPublicClientApplication _msalClient;
    private readonly IApiService _apiService;
    private readonly EntraAuthConfig _config;

    public AuthService(IApiService apiService, EntraAuthConfig config)
    {
        _apiService = apiService;
        _config = config;

        _msalClient = PublicClientApplicationBuilder
            .Create(config.ClientId)
            .WithAuthority(config.Authority)
            .WithRedirectUri(config.RedirectUri)
            .Build();
    }

    public async Task<bool> LoginAsync()
    {
        try
        {
            // Try silent first (uses cached token if available)
            var accounts = await _msalClient.GetAccountsAsync();
            AuthenticationResult result;

            try
            {
                System.Diagnostics.Debug.WriteLine("AUTH: Attempting silent token acquisition");
                result = await _msalClient
                    .AcquireTokenSilent(_config.Scopes, accounts.FirstOrDefault())
                    .ExecuteAsync();
                System.Diagnostics.Debug.WriteLine("AUTH: Silent token acquired");
            }
            catch (MsalUiRequiredException)
            {
                System.Diagnostics.Debug.WriteLine("AUTH: Silent failed, launching interactive login");

                // No cached token — show the Entra login screen
                var builder = _msalClient
                    .AcquireTokenInteractive(_config.Scopes);

#if ANDROID
                // Android requires the current Activity to display the browser pop-up
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
                    ?? throw new InvalidOperationException("Current Android Activity is null.");
                builder = builder.WithParentActivityOrWindow(activity);
#endif

                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                result = await builder.ExecuteAsync(cts.Token);
                System.Diagnostics.Debug.WriteLine("AUTH: Interactive token acquired");
            }

            System.Diagnostics.Debug.WriteLine("AUTH: Setting auth token on API service");
            Token = result.AccessToken;
            _apiService.SetAuthToken(Token);

            System.Diagnostics.Debug.WriteLine("AUTH: Calling SyncUserAsync");
            CurrentUser = await _apiService.SyncUserAsync();
            System.Diagnostics.Debug.WriteLine($"AUTH: SyncUserAsync complete. User={CurrentUser?.Email ?? "null"}");

            SaveTokenExpiry(result.ExpiresOn);

            // Marshal back to main thread before invoking UI state change
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                System.Diagnostics.Debug.WriteLine("AUTH: Invoking AuthStateChanged on main thread");
                AuthStateChanged?.Invoke();
            });

            System.Diagnostics.Debug.WriteLine("AUTH: LoginAsync completed successfully");
            return true;
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("AUTH: Interactive login timed out after 5 minutes");
            return false;
        }
        catch (MsalException ex)
        {
            System.Diagnostics.Debug.WriteLine($"MSAL error: {ex.ErrorCode} - {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Login error: {ex.GetType().Name} - {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Login error stack: {ex.StackTrace}");
            return false;
        }
    }

    public async Task<bool> RegisterAsync()
    {
        return await LoginAsync();
    }

    public async Task LogoutAsync()
    {
        try
        {
            var accounts = await _msalClient.GetAccountsAsync();
            foreach (var account in accounts)
                await _msalClient.RemoveAsync(account);

            await _apiService.LogoutAsync();
        }
        catch { }
        finally
        {
            Token = null;
            CurrentUser = null;
            Preferences.Remove("token_expiry");

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                AuthStateChanged?.Invoke();
            });
        }
    }

    public async Task<bool> DeleteAccountAndDataAsync()
    {
        // 1. Tell the server to delete all user data (transactions, accounts,
        //    categories, profile). This must succeed before we clear local state —
        //    if the API call fails the user can try again.
        var deleted = await _apiService.DeleteUserAsync();
        if (!deleted) return false;

        // 2. Revoke all MSAL tokens from the local cache. This signs the user
        //    out of Entra ID on this device and prevents silent re-authentication.
        try
        {
            var accounts = await _msalClient.GetAccountsAsync();
            foreach (var account in accounts)
                await _msalClient.RemoveAsync(account);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MSAL token removal error: {ex.Message}");
            // Non-fatal — server data is already deleted. Local token will expire
            // naturally and the account no longer exists on the server anyway.
        }

        // 3. Clear local state exactly as logout does.
        Token = null;
        CurrentUser = null;
        Preferences.Remove("token_expiry");

        await MainThread.InvokeOnMainThreadAsync(() => AuthStateChanged?.Invoke());
        return true;
    }

    private void SaveTokenExpiry(DateTimeOffset expiresOn)
    {
        Preferences.Set("token_expiry", expiresOn.ToString("O"));
    }
}