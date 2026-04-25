using CommunityToolkit.Maui;
using HexFund.UI.Config;
using HexFund.UI.Services;
using HexFund.UI.ViewModels;
using HexFund.UI.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Devices;
using Microsoft.Identity.Client;
using Microsoft.Maui.LifecycleEvents;

namespace HexFund.UI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf",  "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("CinzelBold.ttf",    "CinzelBold");
                fonts.AddFont("InterRegular.ttf",  "InterRegular");
                fonts.AddFont("InterSemiBold.ttf", "InterSemiBold");
            })
            .ConfigureLifecycleEvents(lifecycle =>
            {
#if ANDROID
                HexFund.UI.Platforms.Android.TabBarTopLineEffect.Register(lifecycle);
#elif IOS
                HexFund.UI.Platforms.iOS.TabBarTopLineEffect.Register(lifecycle);
#endif
            });

        builder.Services.AddMemoryCache();

        var entraConfig = new EntraAuthConfig
        {
            ClientId = AuthSecrets.ClientId,
            TenantId = AuthSecrets.TenantId,
            TenantDomain = "hexfundapp.onmicrosoft.com",
            Scopes = new[] { $"api://{AuthSecrets.ApiClientId}/access" }
        };

        builder.Services.AddSingleton(entraConfig);

        builder.Services.AddHttpClient(AppConstants.HttpClientName, client =>
        {
            var baseUrl = Environment.GetEnvironmentVariable("API_BASE_URL");
            if (string.IsNullOrEmpty(baseUrl))
            {
                baseUrl = DeviceInfo.Platform == DevicePlatform.Android
                    ? AppConstants.AndroidBaseUrl
                    : AppConstants.LocalBaseUrl;
            }
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(AppConstants.HttpTimeoutSecs);
            client.DefaultRequestHeaders.ConnectionClose = false;
        }).ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression =
                    System.Net.DecompressionMethods.GZip |
                    System.Net.DecompressionMethods.Deflate,
#if DEBUG
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
#endif
            };
            return handler;
        });

        // ── Services ──────────────────────────────────────────────────────────
        builder.Services.AddSingleton<ICacheService,        CacheService>();
        builder.Services.AddSingleton<IApiService,          ApiService>();
        builder.Services.AddSingleton<IAuthService,         AuthService>();
        builder.Services.AddSingleton<IAccountStateService, AccountStateService>();
        builder.Services.AddSingleton<ISettingsService,     SettingsService>();

        // ── ViewModels ────────────────────────────────────────────────────────
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<CalendarViewModel>();
        builder.Services.AddTransient<TransactionsViewModel>();
        builder.Services.AddTransient<InsightsViewModel>();
        builder.Services.AddTransient<AddEntryViewModel>();
        builder.Services.AddTransient<AccountsViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // ── Views ─────────────────────────────────────────────────────────────
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<CalendarPage>();
        builder.Services.AddTransient<TransactionsPage>();
        builder.Services.AddTransient<InsightsPage>();
        builder.Services.AddTransient<AddEntryPage>();
        builder.Services.AddTransient<AccountsPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
