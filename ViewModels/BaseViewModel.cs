using CommunityToolkit.Mvvm.ComponentModel;
using HexFund.UI.Config;
using HexFund.UI.Services;

namespace HexFund.UI.ViewModels;

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

    /// <summary>
    /// Per-theme hex logo image source — resolves to e.g. "hex_logo_gold.png".
    /// Bind to Image.Source on any page header.
    /// </summary>
    public string CurrentThemeLogo =>
        ThemeService.GetThemeLogo(SettingsService.Theme);

    /// <summary>
    /// Per-theme full-bleed backdrop image source — resolves to e.g. "hex_bg_gold.png".
    /// Use as the first child of a Grid behind page content.
    /// </summary>
    public string CurrentThemeBackdrop =>
        ThemeService.GetThemeBackdrop(SettingsService.Theme);

    /// <summary>
    /// Human-readable theme name for the header subtitle — e.g. "GOLD VAULT".
    /// Updates automatically when the theme changes.
    /// </summary>
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