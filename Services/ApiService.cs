using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HexFund.UI.Config;
using HexFund.UI.Models;
using HexFund.UI.Services.Http;

namespace HexFund.UI.Services;

/// <summary>
/// Typed client for the HexFund API. Methods return null, false or an empty list when a request
/// fails, so callers must treat those values as "failed or empty". <see cref="SubmitFeedbackAsync"/>
/// returns an <see cref="ApiResult{T}"/> instead, so callers can tell why a request failed.
/// </summary>
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

    // Feedback
    Task<ApiResult<FeedbackReceipt>> SubmitFeedbackAsync(SubmitFeedbackRequest request, CancellationToken cancellationToken = default);

    // Utilities
    void SetAuthToken(string token);
    Task<bool> LogoutAsync();
    Task<bool> DeleteUserAsync();
    void ClearCache();
    void InvalidateCalendarCache(Guid accountId);
}

/// <summary>
/// Default <see cref="IApiService"/>. Reads go through <see cref="ICacheService"/>; writes
/// invalidate the cache entries they affect. Failures are logged to the debug output only, without
/// request or response content.
/// </summary>
public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly ICacheService _cacheService;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // The feedback endpoint documents camelCase JSON and treats a missing diagnostics value as
    // "switched off", so nulls are omitted.
    private static readonly JsonSerializerOptions _feedbackOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
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

    /// <summary>
    /// Sends one request and classifies the outcome. Never throws except when the caller's own
    /// token cancels. When <paramref name="readBody"/> is false a successful response body is
    /// ignored.
    /// </summary>
    private async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method, string url, object? body = null,
        JsonSerializerOptions? bodyOptions = null, bool readBody = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(method, url);
            if (body != null)
                request.Content = JsonContent.Create(
                    body, body.GetType(), null, bodyOptions ?? _jsonOptions);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var status = (int)response.StatusCode;
            var headerId = response.Headers.TryGetValues(RequestTrackingHandler.CorrelationHeader, out var ids)
                ? ids.FirstOrDefault()
                : null;

            if (response.IsSuccessStatusCode)
            {
                T? value = default;
                if (readBody)
                    value = await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken);
                return ApiResult<T>.Success(value, status);
            }

            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            var retryAfter = response.Headers.TryGetValues("Retry-After", out var retry)
                ? RetryAfterParser.Parse(retry.FirstOrDefault(), DateTimeOffset.UtcNow)
                : null;

            return ApiResult<T>.Failure(
                ApiResult.KindForStatus(status), status, ProblemDetailsParser.Parse(text),
                retryAfter, headerId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return ApiResult<T>.Failure(ApiResultKind.Timeout);
        }
        catch (HttpRequestException)
        {
            return ApiResult<T>.Failure(ApiResultKind.Network);
        }
        catch (Exception)
        {
            return ApiResult<T>.Failure(ApiResultKind.Unexpected);
        }
    }

    // For calls whose success response has no body the caller needs.
    private Task<ApiResult<object>> SendAsync(
        HttpMethod method, string url, object? body = null) =>
        SendAsync<object>(method, url, body, readBody: false);

    private static string Describe(ApiResult result) =>
        $"{result.Kind} (status {result.StatusCode?.ToString() ?? "none"})";

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var result = await SendAsync<AuthResponse>(HttpMethod.Post, "auth/login", request);
        if (result.IsSuccess) return result.Value;
        Debug.WriteLine($"Login error: {Describe(result)}");
        return null;
    }

    public async Task<bool> LogoutAsync()
    {
        var result = await SendAsync(HttpMethod.Post, "auth/logout");
        if (!result.IsSuccess)
        {
            Debug.WriteLine($"Logout error: {Describe(result)}");
            return false;
        }
        _cacheService.Clear();
        return true;
    }

    /// <summary>
    /// Permanently deletes the signed-in user and all of their data on the server. The caller
    /// must also clear the local MSAL token cache after this returns true.
    /// </summary>
    public async Task<bool> DeleteUserAsync()
    {
        var result = await SendAsync(HttpMethod.Delete, "auth/user");
        if (!result.IsSuccess)
        {
            Debug.WriteLine($"DeleteUser error: {Describe(result)}");
            return false;
        }
        _cacheService.Clear();
        return true;
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        var result = await SendAsync<AuthResponse>(HttpMethod.Post, "auth/register", request);
        if (result.IsSuccess) return result.Value;
        Debug.WriteLine($"Register error: {Describe(result)}");
        return null;
    }

    public async Task<User?> SyncUserAsync()
    {
        var result = await SendAsync<User>(HttpMethod.Post, "auth/sync");
        if (result.IsSuccess) return result.Value;
        Debug.WriteLine($"Sync user error: {Describe(result)}");
        return null;
    }

    /// <summary>
    /// Sends the updated first and last name. The caller refreshes AuthService.CurrentUser from
    /// the returned user.
    /// </summary>
    public async Task<User?> UpdateProfileAsync(UpdateProfileRequest request)
    {
        var result = await SendAsync<User>(HttpMethod.Put, "auth/profile", request);
        if (result.IsSuccess) return result.Value;
        Debug.WriteLine($"Update profile error: {Describe(result)}");
        return null;
    }

    public async Task<List<Account>> GetAccountsAsync(bool forceRefresh = false)
    {
        var key = CacheService.AccountsListKey();
        if (!forceRefresh)
        {
            var cached = _cacheService.Get<List<Account>>(key);
            if (cached != null) return cached;
        }

        var result = await SendAsync<List<Account>>(HttpMethod.Get, "accounts");
        if (!result.IsSuccess)
        {
            Debug.WriteLine($"Get accounts error: {Describe(result)}");
            return new();
        }
        var accounts = result.Value ?? new List<Account>();
        _cacheService.Set(key, accounts, AppConstants.AccountsCacheTtl);
        return accounts;
    }

    public async Task<Account?> GetAccountAsync(Guid accountId, bool forceRefresh = false)
    {
        var key = CacheService.AccountKey(accountId);
        if (!forceRefresh)
        {
            var cached = _cacheService.Get<Account>(key);
            if (cached != null) return cached;
        }

        var result = await SendAsync<Account>(HttpMethod.Get, $"accounts/{accountId}");
        if (!result.IsSuccess)
        {
            Debug.WriteLine($"Get account error: {Describe(result)}");
            return null;
        }
        if (result.Value != null)
            _cacheService.Set(key, result.Value, AppConstants.AccountsCacheTtl);
        return result.Value;
    }

    public async Task<Account?> CreateAccountAsync(CreateAccountRequest request)
    {
        var result = await SendAsync<Account>(HttpMethod.Post, "accounts", request);
        if (!result.IsSuccess)
        {
            Debug.WriteLine($"Create account error: {Describe(result)}");
            return null;
        }
        _cacheService.Remove(CacheService.AccountsListKey());
        return result.Value;
    }

    public async Task<Account?> UpdateAccountAsync(Guid accountId, UpdateAccountRequest request)
    {
        var result = await SendAsync<Account>(HttpMethod.Put, $"accounts/{accountId}", request);
        if (!result.IsSuccess)
        {
            Debug.WriteLine($"Update account error: {Describe(result)}");
            return null;
        }
        _cacheService.Remove(CacheService.AccountsListKey());
        _cacheService.Remove(CacheService.AccountKey(accountId));
        return result.Value;
    }

    public async Task<bool> DeleteAccountAsync(Guid accountId)
    {
        var result = await SendAsync(HttpMethod.Delete, $"accounts/{accountId}");
        if (!result.IsSuccess)
        {
            Debug.WriteLine($"Delete account error: {Describe(result)}");
            return false;
        }
        _cacheService.Remove(CacheService.AccountsListKey());
        _cacheService.Remove(CacheService.AccountKey(accountId));
        _cacheService.Remove(CacheService.TransactionsKey(accountId));
        _cacheService.InvalidateCalendarData(accountId);
        return true;
    }

    // Requests with includeHistory bypass the cache in both directions, so the cached list only
    // ever holds current transactions.
    public async Task<List<Transaction>> GetTransactionsAsync(
        Guid accountId, bool forceRefresh = false, bool includeHistory = false)
    {
        var key = CacheService.TransactionsKey(accountId);
        if (!forceRefresh && !includeHistory)
        {
            var cached = _cacheService.Get<List<Transaction>>(key);
            if (cached != null) return cached;
        }

        var url = includeHistory
            ? $"accounts/{accountId}/transactions?includeHistory=true"
            : $"accounts/{accountId}/transactions";
        var result = await SendAsync<List<Transaction>>(HttpMethod.Get, url);
        if (!result.IsSuccess)
        {
            Debug.WriteLine($"Get transactions error: {Describe(result)}");
            return new();
        }
        var transactions = result.Value ?? new List<Transaction>();
        if (!includeHistory)
            _cacheService.Set(key, transactions, AppConstants.TransactionsCacheTtl);
        return transactions;
    }

    public async Task<Transaction?> CreateTransactionAsync(Guid accountId, CreateTransactionRequest request)
    {
        var result = await SendAsync<Transaction>(
            HttpMethod.Post, $"accounts/{accountId}/transactions", request);
        if (!result.IsSuccess)
        {
            Debug.WriteLine($"Create transaction error: {Describe(result)}");
            return null;
        }
        _cacheService.Remove(CacheService.TransactionsKey(accountId));
        _cacheService.InvalidateCalendarData(accountId);
        return result.Value;
    }

    public async Task<Transaction?> UpdateTransactionAsync(
        Guid accountId, Guid transactionId, UpdateTransactionRequest request)
    {
        var result = await SendAsync<Transaction>(
            HttpMethod.Put, $"accounts/{accountId}/transactions/{transactionId}", request);
        if (!result.IsSuccess)
        {
            Debug.WriteLine($"Update transaction error: {Describe(result)}");
            return null;
        }
        _cacheService.Remove(CacheService.TransactionsKey(accountId));
        _cacheService.InvalidateCalendarData(accountId);
        return result.Value;
    }

    public async Task<bool> DeleteTransactionAsync(Guid accountId, Guid transactionId)
    {
        var result = await SendAsync(
            HttpMethod.Delete, $"accounts/{accountId}/transactions/{transactionId}");
        if (!result.IsSuccess)
        {
            Debug.WriteLine($"Delete transaction error: {Describe(result)}");
            return false;
        }
        _cacheService.Remove(CacheService.TransactionsKey(accountId));
        _cacheService.InvalidateCalendarData(accountId);
        return true;
    }

    public async Task<Transaction?> AmendTransactionAsync(
        Guid accountId, Guid transactionId, AmendTransactionRequest request)
    {
        var result = await SendAsync<Transaction>(
            HttpMethod.Post, $"accounts/{accountId}/transactions/{transactionId}/amend", request);
        if (!result.IsSuccess)
        {
            Debug.WriteLine($"Amend transaction error: {Describe(result)}");
            return null;
        }
        _cacheService.Remove(CacheService.TransactionsKey(accountId));
        _cacheService.InvalidateCalendarData(accountId);
        return result.Value;
    }

    private const string CategoriesCacheKey = "user_categories";

    public async Task<List<UserCategory>> GetCategoriesAsync(bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            var cached = _cacheService.Get<List<UserCategory>>(CategoriesCacheKey);
            if (cached != null) return cached;
        }

        var result = await SendAsync<List<UserCategory>>(HttpMethod.Get, "categories");
        if (!result.IsSuccess)
        {
            Debug.WriteLine($"Get categories error: {Describe(result)}");
            return new();
        }
        var categories = result.Value ?? new List<UserCategory>();
        _cacheService.Set(CategoriesCacheKey, categories, AppConstants.CategoriesCacheTtl);
        return categories;
    }

    public async Task<UserCategory?> CreateCategoryAsync(CreateCategoryRequest request)
    {
        var result = await SendAsync<UserCategory>(HttpMethod.Post, "categories", request);
        if (result.StatusCode == (int)System.Net.HttpStatusCode.Conflict)
            return null; // Duplicate name: the caller shows the message.
        if (!result.IsSuccess)
        {
            Debug.WriteLine($"Create category error: {Describe(result)}");
            return null;
        }
        _cacheService.Remove(CategoriesCacheKey);
        return result.Value;
    }

    public async Task<bool> DeleteCategoryAsync(Guid categoryId)
    {
        var result = await SendAsync(HttpMethod.Delete, $"categories/{categoryId}");
        if (!result.IsSuccess)
        {
            Debug.WriteLine($"Delete category error: {Describe(result)}");
            return false;
        }
        _cacheService.Remove(CategoriesCacheKey);
        return true;
    }

    public async Task<EnhancedMonthlyOverview?> GetMonthlyOverviewAsync(
        Guid accountId, int year, int month, bool forceRefresh = false)
    {
        var key = CacheService.MonthlyOverviewKey(accountId, year, month);
        if (!forceRefresh)
        {
            var cached = _cacheService.Get<EnhancedMonthlyOverview>(key);
            if (cached != null) return cached;
        }

        var result = await SendAsync<EnhancedMonthlyOverview>(
            HttpMethod.Get, $"accounts/{accountId}/monthly-overview?year={year}&month={month}");
        if (!result.IsSuccess)
        {
            Debug.WriteLine($"Get monthly overview error: {Describe(result)}");
            return null;
        }
        if (result.Value != null)
        {
            _cacheService.Set(key, result.Value, AppConstants.CalendarCacheTtl);
            _cacheService.RegisterCachedMonth(accountId, year, month);
        }
        return result.Value;
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

        var result = await SendAsync<List<TransactionOccurrence>>(
            HttpMethod.Get, $"accounts/{accountId}/transactions-for-date?date={date:yyyy-MM-dd}");
        if (!result.IsSuccess)
        {
            Debug.WriteLine($"Error getting transactions for date: {Describe(result)}");
            return new();
        }
        var transactions = result.Value ?? new List<TransactionOccurrence>();
        _cacheService.Set(key, transactions, AppConstants.CalendarCacheTtl);
        return transactions;
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

        // The API keys the dictionary by "yyyy-MM-dd" strings; they are converted to dates below.
        var response = await SendAsync<Dictionary<string, List<TransactionOccurrence>>>(
            HttpMethod.Get, $"accounts/{accountId}/transactions-for-month?year={year}&month={month}");
        if (!response.IsSuccess)
        {
            Debug.WriteLine($"Error getting transactions for month: {Describe(response)}");
            return null;
        }

        if (response.Value == null)
            return new Dictionary<DateTime, List<TransactionOccurrence>>();

        var result = new Dictionary<DateTime, List<TransactionOccurrence>>();
        foreach (var kvp in response.Value)
        {
            if (DateTime.TryParseExact(kvp.Key, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date))
                result[date] = kvp.Value;
        }

        _cacheService.Set(key, result, AppConstants.CalendarCacheTtl);
        return result;
    }

    public Task<ApiResult<FeedbackReceipt>> SubmitFeedbackAsync(
        SubmitFeedbackRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<FeedbackReceipt>(
            HttpMethod.Post, "feedback", request, _feedbackOptions,
            cancellationToken: cancellationToken);
}
