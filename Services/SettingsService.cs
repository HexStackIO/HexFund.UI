namespace HexFund.UI.Services;

/// <summary>How the calendar page shows a month.</summary>
public enum CalendarViewMode
{
    List = 0,
    Grid = 1
}

/// <summary>
/// The eight accent themes. The integer values are stored in the "color_theme" preference, so
/// they must not be renumbered.
/// </summary>
public enum ColorTheme
{
    Gold      = 0,  // #D4AF37, the default.
    Sapphire  = 1,  // #4A7FD6
    Emerald   = 2,  // #35B88C
    Bronze    = 3,  // #B87333
    Obsidian  = 4,  // #8E94A0
    Ruby      = 5,  // #C73A57
    Amethyst  = 6,  // #9C6ED1
    Platinum  = 7   // #D9DDE2
}

/// <summary>Device-local user preferences backed by MAUI Preferences.</summary>
public interface ISettingsService
{
    CalendarViewMode CalendarView { get; set; }
    ColorTheme Theme { get; set; }
    bool HasCompletedOnboarding { get; set; }

    /// <summary>Raised when the calendar view mode or the theme changes.</summary>
    event Action SettingsChanged;
}

/// <inheritdoc />
public class SettingsService : ISettingsService
{
    private const string KeyCalendarView        = "calendar_view_mode";
    private const string KeyColorTheme          = "color_theme";
    private const string KeyOnboardingComplete  = "onboarding_complete";

    public event Action? SettingsChanged;

    public CalendarViewMode CalendarView
    {
        get => (CalendarViewMode)Preferences.Default.Get(KeyCalendarView, (int)CalendarViewMode.List);
        set
        {
            Preferences.Default.Set(KeyCalendarView, (int)value);
            SettingsChanged?.Invoke();
        }
    }

    public ColorTheme Theme
    {
        get => (ColorTheme)Preferences.Default.Get(KeyColorTheme, (int)ColorTheme.Gold);
        set
        {
            Preferences.Default.Set(KeyColorTheme, (int)value);
            SettingsChanged?.Invoke();
        }
    }

    public bool HasCompletedOnboarding
    {
        get => Preferences.Default.Get(KeyOnboardingComplete, false);
        set => Preferences.Default.Set(KeyOnboardingComplete, value);
    }
}
