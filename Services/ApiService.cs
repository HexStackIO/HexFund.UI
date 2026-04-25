using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Diagnostics;
using HexFund.UI.Config;
using HexFund.UI.Models;

namespace HexFund.UI.Services;

public interface IApiService
{
    // Auth
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);
    Task<User?> SyncUserAsync();
    Task<User?> UpdateProfileAsync(UpdateProfileRequest request);

    // Accounts
    Task<List<Account>> GetAccountsAsync(bool forceRefresh = false);
    Task<Account?> GetAccountAsync(Guid accountId, bool forceRefresh = false);
    Task<Account?> CreateAccountAsync(CreateAccountRequest request);
    Task<Account?> UpdateAccountAsync(Guid accountId, UpdateAccountRequest request);
    Task<bool> DeleteAccountAsync(Guid accountId);

    // Transactions
    Task<List<Transaction>> GetTransactionsAsync(Guid accountId, bool forceRefresh = false, bool includeHistory = false);
    Task<Transaction?> CreateTransactionAsync(Guid accountId, CreateTransactionRequest request);
    Task<Transaction?> UpdateTransactionAsync(Guid accountId, Guid transactionId, UpdateTransactionRequest request);
    Task<bool> DeleteTransactionAsync(Guid accountId, Guid transactionId);
    Task<Transaction?> AmendTransactionAsync(Guid accountId, Guid transactionId, AmendTransactionRequest request);

    // Categories
    Task<List<UserCategory>> GetCategoriesAsync(bool forceRefresh = false);
    Task<UserCategory?> CreateCategoryAsync(CreateCategoryRequest request);
    Task<bool> DeleteCategoryAsync(Guid categoryId);

    // Calendar
    Task<EnhancedMonthlyOverview?> GetMonthlyOverviewAsync(Guid accountId, int year, int month, bool forceRefresh = false);
    Task<List<TransactionOccurrence>> GetTransactionsForDateAsync(Guid accountId, DateTime date, bool forceRefresh = false);
    Task<Dictionary<DateTime, List<TransactionOccurrence>>?> GetTransactionsForMonthAsync(Guid accountId, int year, int month, bool forceRefresh = false);

    // Utilities
    void SetAuthToken(string token);
    Task<bool> LogoutAsync();
    void ClearCache();
    void InvalidateCalendarCache(Guid accountId);
}

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly ICacheService _cacheService;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public ApiService(IHttpClientFactory httpClientFactory, ICacheService cacheService)
    {
        _httpClient = httpClientFactory.CreateClient(AppConstants.HttpClientName);
        _cacheService = cacheService;
    }

    public void SetAuthToken(string token) =>
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    public void ClearCache() => _cacheService.Clear();

    public void InvalidateCalendarCache(Guid accountId) =>
        _cacheService.InvalidateCalendarData(accountId);

    // ── Auth ──────────────────────────────────────────────────────────────────

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/login", request, _jsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);
        }
        catch (Exception ex) { Debug.WriteLine($"Login error: {ex}"); return null; }
    }

    public async Task<bool> LogoutAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("auth/logout", null);
            response.EnsureSuccessStatusCode();
            _cacheService.Clear();
            return true;
        }
        catch (Exception ex) { Debug.WriteLine($"Logout error: {ex}"); return false; }
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/register", request, _jsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);
        }
        catch (Exception ex) { Debug.WriteLine($"Register error: {ex.Message}"); return null; }
    }

    public async Task<User?> SyncUserAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("auth/sync", null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<User>(_jsonOptions);
        }
        catch (Exception ex) { Debug.WriteLine($"Sync user error: {ex.Message}"); return null; }
    }

    /// <summary>
    /// Sends updated first/last name to the API. On success, updates the
    /// local cache key so the caller can refresh AuthService.CurrentUser.
    /// </summary>
    public async Task<User?> UpdateProfileAsync(UpdateProfileRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync("auth/profile", request, _jsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<User>(_jsonOptions);
        }
        catch (Exception ex) { Debug.WriteLine($"Update profile error: {ex.Message}"); return null; }
    }

    // ── Accounts ──────────────────────────────────────────────────────────────

    public async Task<List<Account>> GetAccountsAsync(bool forceRefresh = false)
    {
        var key = CacheService.AccountsListKey();
        if (!forceRefresh)
        {
            var cached = _cacheService.Get<List<Account>>(key);
            if (cached != null) return cached;
        }
        try
        {
            var response = await _httpClient.GetAsync("accounts");
            response.EnsureSuccessStatusCode();
            var accounts = await response.Content.ReadFromJsonAsync<List<Account>>(_jsonOptions)
                           ?? new List<Account>();
            _cacheService.Set(key, accounts, AppConstants.AccountsCacheTtl);
            return accounts;
        }
        catch (Exception ex) { Debug.WriteLine($"Get accounts error: {ex.Message}"); return new(); }
    }

    public async Task<Account?> GetAccountAsync(Guid accountId, bool forceRefresh = false)
    {
        var key = CacheService.AccountKey(accountId);
        if (!forceRefresh)
        {
            var cached = _cacheService.Get<Account>(key);
            if (cached != null) return cached;
        }
        try
        {
            var response = await _httpClient.GetAsync($"accounts/{accountId}");
            response.EnsureSuccessStatusCode();
            var account = await response.Content.ReadFromJsonAsync<Account>(_jsonOptions);
            if (account != null)
                _cacheService.Set(key, account, AppConstants.AccountsCacheTtl);
            return account;
        }
        catch (Exception ex) { Debug.WriteLine($"Get account error: {ex.Message}"); return null; }
    }

    public async Task<Account?> CreateAccountAsync(CreateAccountRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("accounts", request, _jsonOptions);
            response.EnsureSuccessStatusCode();
            var account = await response.Content.ReadFromJsonAsync<Account>(_jsonOptions);
            _cacheService.Remove(CacheService.AccountsListKey());
            return account;
        }
        catch (Exception ex) { Debug.WriteLine($"Create account error: {ex.Message}"); return null; }
    }

    public async Task<Account?> UpdateAccountAsync(Guid accountId, UpdateAccountRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"accounts/{accountId}", request, _jsonOptions);
            response.EnsureSuccessStatusCode();
            var account = await response.Content.ReadFromJsonAsync<Account>(_jsonOptions);
            _cacheService.Remove(CacheService.AccountsListKey());
            _cacheService.Remove(CacheService.AccountKey(accountId));
            return account;
        }
        catch (Exception ex) { Debug.WriteLine($"Update account error: {ex.Message}"); return null; }
    }

    public async Task<bool> DeleteAccountAsync(Guid accountId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"accounts/{accountId}");
            response.EnsureSuccessStatusCode();
            _cacheService.Remove(CacheService.AccountsListKey());
            _cacheService.Remove(CacheService.AccountKey(accountId));
            _cacheService.Remove(CacheService.TransactionsKey(accountId));
            _cacheService.InvalidateCalendarData(accountId);
            return true;
        }
        catch (Exception ex) { Debug.WriteLine($"Delete account error: {ex.Message}"); return false; }
    }

    // ── Transactions ──────────────────────────────────────────────────────────

    public async Task<List<Transaction>> GetTransactionsAsync(
        Guid accountId, bool forceRefresh = false, bool includeHistory = false)
    {
        var key = CacheService.TransactionsKey(accountId);
        if (!forceRefresh && !includeHistory)
        {
            var cached = _cacheService.Get<List<Transaction>>(key);
            if (cached != null) return cached;
        }
        try
        {
            var url = includeHistory
                ? $"accounts/{accountId}/transactions?includeHistory=true"
                : $"accounts/{accountId}/transactions";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var transactions = await response.Content.ReadFromJsonAsync<List<Transaction>>(_jsonOptions)
                               ?? new List<Transaction>();
            if (!includeHistory)
                _cacheService.Set(key, transactions, AppConstants.TransactionsCacheTtl);
            return transactions;
        }
        catch (Exception ex) { Debug.WriteLine($"Get transactions error: {ex.Message}"); return new(); }
    }

    public async Task<Transaction?> CreateTransactionAsync(Guid accountId, CreateTransactionRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"accounts/{accountId}/transactions", request, _jsonOptions);
            response.EnsureSuccessStatusCode();
            var transaction = await response.Content.ReadFromJsonAsync<Transaction>(_jsonOptions);
            _cacheService.Remove(CacheService.TransactionsKey(accountId));
            _cacheService.InvalidateCalendarData(accountId);
            return transaction;
        }
        catch (Exception ex) { Debug.WriteLine($"Create transaction error: {ex.Message}"); return null; }
    }

    public async Task<Transaction?> UpdateTransactionAsync(
        Guid accountId, Guid transactionId, UpdateTransactionRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"accounts/{accountId}/transactions/{transactionId}", request, _jsonOptions);
            response.EnsureSuccessStatusCode();
            var transaction = await response.Content.ReadFromJsonAsync<Transaction>(_jsonOptions);
            _cacheService.Remove(CacheService.TransactionsKey(accountId));
            _cacheService.InvalidateCalendarData(accountId);
            return transaction;
        }
        catch (Exception ex) { Debug.WriteLine($"Update transaction error: {ex.Message}"); return null; }
    }

    public async Task<bool> DeleteTransactionAsync(Guid accountId, Guid transactionId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(
                $"accounts/{accountId}/transactions/{transactionId}");
            response.EnsureSuccessStatusCode();
            _cacheService.Remove(CacheService.TransactionsKey(accountId));
            _cacheService.InvalidateCalendarData(accountId);
            return true;
        }
        catch (Exception ex) { Debug.WriteLine($"Delete transaction error: {ex.Message}"); return false; }
    }

    public async Task<Transaction?> AmendTransactionAsync(
        Guid accountId, Guid transactionId, AmendTransactionRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"accounts/{accountId}/transactions/{transactionId}/amend", request, _jsonOptions);
            response.EnsureSuccessStatusCode();
            var successor = await response.Content.ReadFromJsonAsync<Transaction>(_jsonOptions);
            _cacheService.Remove(CacheService.TransactionsKey(accountId));
            _cacheService.InvalidateCalendarData(accountId);
            return successor;
        }
        catch (Exception ex) { Debug.WriteLine($"Amend transaction error: {ex.Message}"); return null; }
    }

    // ── Categories ────────────────────────────────────────────────────────────

    private const string CategoriesCacheKey = "user_categories";

    public async Task<List<UserCategory>> GetCategoriesAsync(bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            var cached = _cacheService.Get<List<UserCategory>>(CategoriesCacheKey);
            if (cached != null) return cached;
        }
        try
        {
            var response = await _httpClient.GetAsync("categories");
            response.EnsureSuccessStatusCode();
            var categories = await response.Content.ReadFromJsonAsync<List<UserCategory>>(_jsonOptions)
                             ?? new List<UserCategory>();
            _cacheService.Set(CategoriesCacheKey, categories, AppConstants.CategoriesCacheTtl);
            return categories;
        }
        catch (Exception ex) { Debug.WriteLine($"Get categories error: {ex.Message}"); return new(); }
    }

    public async Task<UserCategory?> CreateCategoryAsync(CreateCategoryRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("categories", request, _jsonOptions);
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                return null; // duplicate name — caller handles the message
            response.EnsureSuccessStatusCode();
            var category = await response.Content.ReadFromJsonAsync<UserCategory>(_jsonOptions);
            _cacheService.Remove(CategoriesCacheKey);
            return category;
        }
        catch (Exception ex) { Debug.WriteLine($"Create category error: {ex.Message}"); return null; }
    }

    public async Task<bool> DeleteCategoryAsync(Guid categoryId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"categories/{categoryId}");
            response.EnsureSuccessStatusCode();
            _cacheService.Remove(CategoriesCacheKey);
            return true;
        }
        catch (Exception ex) { Debug.WriteLine($"Delete category error: {ex.Message}"); return false; }
    }

    // ── Calendar ──────────────────────────────────────────────────────────────

    public async Task<EnhancedMonthlyOverview?> GetMonthlyOverviewAsync(
        Guid accountId, int year, int month, bool forceRefresh = false)
    {
        var key = CacheService.MonthlyOverviewKey(accountId, year, month);
        if (!forceRefresh)
        {
            var cached = _cacheService.Get<EnhancedMonthlyOverview>(key);
            if (cached != null) return cached;
        }
        try
        {
            var response = await _httpClient.GetAsync(
                $"accounts/{accountId}/monthly-overview?year={year}&month={month}");
            response.EnsureSuccessStatusCode();
            var overview = await response.Content.ReadFromJsonAsync<EnhancedMonthlyOverview>(_jsonOptions);
            if (overview != null)
            {
                _cacheService.Set(key, overview, AppConstants.CalendarCacheTtl);
                _cacheService.RegisterCachedMonth(accountId, year, month);
            }
            return overview;
        }
        catch (Exception ex) { Debug.WriteLine($"Get monthly overview error: {ex.Message}"); return null; }
    }

    public async Task<List<TransactionOccurrence>> GetTransactionsForDateAsync(
        Guid accountId, DateTime date, bool forceRefresh = false)
    {
        var key = CacheService.TransactionsForDateKey(accountId, date);
        if (!forceRefresh)
        {
            var cached = _cacheService.Get<List<TransactionOccurrence>>(key);
            if (cached != null) return cached;
        }
        try
        {
            var response = await _httpClient.GetAsync(
                $"accounts/{accountId}/transactions-for-date?date={date:yyyy-MM-dd}");
            response.EnsureSuccessStatusCode();
            var transactions = await response.Content.ReadFromJsonAsync<List<TransactionOccurrence>>(_jsonOptions)
                               ?? new List<TransactionOccurrence>();
            _cacheService.Set(key, transactions, AppConstants.CalendarCacheTtl);
            return transactions;
        }
        catch (Exception ex) { Debug.WriteLine($"Error getting transactions for date: {ex}"); return new(); }
    }

    public async Task<Dictionary<DateTime, List<TransactionOccurrence>>?> GetTransactionsForMonthAsync(
        Guid accountId, int year, int month, bool forceRefresh = false)
    {
        var key = CacheService.TransactionsForMonthKey(accountId, year, month);
        if (!forceRefresh)
        {
            var cached = _cacheService.Get<Dictionary<DateTime, List<TransactionOccurrence>>>(key);
            if (cached != null) return cached;
        }
        try
        {
            var response = await _httpClient.GetAsync(
                $"accounts/{accountId}/transactions-for-month?year={year}&month={month}");
            response.EnsureSuccessStatusCode();

            var stringKeyedDict = await response.Content
                .ReadFromJsonAsync<Dictionary<string, List<TransactionOccurrence>>>(_jsonOptions);

            if (stringKeyedDict == null)
                return new Dictionary<DateTime, List<TransactionOccurrence>>();

            var result = new Dictionary<DateTime, List<TransactionOccurrence>>();
            foreach (var kvp in stringKeyedDict)
            {
                if (DateTime.TryParseExact(kvp.Key, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var date))
                    result[date] = kvp.Value;
            }

            _cacheService.Set(key, result, AppConstants.CalendarCacheTtl);
            return result;
        }
        catch (Exception ex) { Debug.WriteLine($"Error getting transactions for month: {ex}"); return null; }
    }
}