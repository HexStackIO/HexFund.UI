using CommunityToolkit.Mvvm.ComponentModel;
using HexFund.UI.Config;
using HexFund.UI.Services;

namespace HexFund.UI.ViewModels;

/// <summary>
/// Base class for view models. Exposes the theme-dependent colors and images so pages can bind
/// to them, and raises change notifications when the theme changes.
/// </summary>
/// <summary>
/// Base class for view models. Exposes the theme-dependent colors and images so pages can bind
/// to them, and raises change notifications when the theme changes.
/// </summary>
/// <summary>
/// Base class for view models. Exposes the theme-dependent colors and images so pages can bind
/// to them, and raises change notifications when the theme changes.
/// </summary>
public abstract partial class BaseViewModel : ObservableObject
{
    protected readonly ISettingsService SettingsService;

    protected BaseViewModel(ISettingsService settingsService)
    {
        SettingsService = settingsService;
        SettingsService.SettingsChanged += OnThemeChanged;
    }

    private void OnThemeChanged() =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            OnPropertyChanged(nameof(ThemePrimaryColor));
            OnPropertyChanged(nameof(ThemeMutedColor));
            OnPropertyChanged(nameof(CurrentThemeLogo));
            OnPropertyChanged(nameof(CurrentThemeBackdrop));
            OnPropertyChanged(nameof(CurrentThemeName));
        });

    public Color ThemePrimaryColor =>
        Application.Current?.Resources.TryGetValue("Primary", out var v) == true && v is Color c
            ? c : Color.FromArgb(AppConstants.FallbackPrimary);

    public Color ThemeMutedColor =>
        Application.Current?.Resources.TryGetValue("PrimaryMuted", out var m) == true && m is Color mc
            ? mc : Color.FromArgb(AppConstants.FallbackMuted);

    /// <summary>The logo image for the current theme, for example "hex_logo_gold.png".</summary>
    public string CurrentThemeLogo =>
        ThemeService.GetThemeLogo(SettingsService.Theme);

    /// <summary>The full-bleed backdrop image for the current theme, for example "hex_bg_gold.png".</summary>
    public string CurrentThemeBackdrop =>
        ThemeService.GetThemeBackdrop(SettingsService.Theme);

    /// <summary>The theme name shown in page headers, for example "GOLD VAULT".</summary>
    public string CurrentThemeName => SettingsService.Theme switch
    {
        ColorTheme.Gold => "GOLD VAULT",
        ColorTheme.Sapphire => "SAPPHIRE VAULT",
        ColorTheme.Emerald => "EMERALD VAULT",
        ColorTheme.Bronze => "BRONZE VAULT",
        ColorTheme.Obsidian => "OBSIDIAN VAULT",
        ColorTheme.Ruby => "RUBY VAULT",
        ColorTheme.Amethyst => "AMETHYST VAULT",
        ColorTheme.Platinum => "PLATINUM VAULT",
        _ => "GOLD VAULT",
    };
}