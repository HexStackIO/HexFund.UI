using HexFund.UI.Config;
using HexFund.UI.Models;
using Microsoft.Identity.Client;

namespace HexFund.UI.Services;

/// <summary>Signs the user in through Microsoft Entra External ID and holds the session.</summary>
public interface IAuthService
{
    bool IsAuthenticated { get; }
    User? CurrentUser { get; set; }
    string? Token { get; }
    /// <summary>
    /// Acquires a token silently when possible, otherwise interactively, then syncs the user
    /// with the API. Returns false on cancellation, timeout or any error.
    /// </summary>
    Task<bool> LoginAsync();

    /// <summary>Registration happens in the same hosted Entra flow, so this signs in.</summary>
    Task<bool> RegisterAsync();

    Task LogoutAsync();

    /// <summary>
    /// Deletes all user data on the server, then removes the cached MSAL tokens. Returns false
    /// without changing local state when the server call fails.
    /// </summary>
    Task<bool> DeleteAccountAndDataAsync();

    /// <summary>Raised on the main thread after sign-in, sign-out or account deletion.</summary>
    event Action? AuthStateChanged;
}

/// <inheritdoc />
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

                var builder = _msalClient
                    .AcquireTokenInteractive(_config.Scopes);

#if ANDROID
                // Android needs the current Activity to host the browser sign-in tab.
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

            // Subscribers update UI, so raise the event on the main thread.
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
        // The server call must succeed before local state is cleared, so the user can retry.
        var deleted = await _apiService.DeleteUserAsync();
        if (!deleted) return false;

        try
        {
            var accounts = await _msalClient.GetAccountsAsync();
            foreach (var account in accounts)
                await _msalClient.RemoveAsync(account);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MSAL token removal error: {ex.Message}");
            // Not fatal: the server data is already deleted and the cached token expires on its own.
        }

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