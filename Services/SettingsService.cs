namespace HexFund.UI.Services;

public enum CalendarViewMode
{
    List = 0,
    Grid = 1
}

/// <summary>
/// The 8 HexFund gem/metal themes. Int values are frozen — they map directly
/// to the user's saved "color_theme" Preference so must never be renumbered.
///
/// Legacy mapping for reference (old name → new name):
///   Default(0)→Gold, Ocean(1)→Sapphire, Forest(2)→Emerald, Sunset(3)→Bronze,
///   Monochrome(4)→Obsidian, Rose(5)→Ruby, Midnight(6)→Amethyst, Blush(7)→Platinum
/// </summary>
public enum ColorTheme
{
    Gold      = 0,  // #D4AF37 — HexFund signature / default
    Sapphire  = 1,  // #4A7FD6 — deep blue
    Emerald   = 2,  // #35B88C — teal green
    Bronze    = 3,  // #B87333 — warm copper
    Obsidian  = 4,  // #8E94A0 — cool slate
    Ruby      = 5,  // #C73A57 — deep crimson
    Amethyst  = 6,  // #9C6ED1 — violet purple
    Platinum  = 7   // #D9DDE2 — cool silver
}

public interface ISettingsService
{
    CalendarViewMode CalendarView { get; set; }
    ColorTheme Theme { get; set; }

    event Action SettingsChanged;
}

public class SettingsService : ISettingsService
{
    private const string KeyCalendarView = "calendar_view_mode";
    private const string KeyColorTheme   = "color_theme";

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
        // Default is Gold (0) — the HexFund signature theme.
        get => (ColorTheme)Preferences.Default.Get(KeyColorTheme, (int)ColorTheme.Gold);
        set
        {
            Preferences.Default.Set(KeyColorTheme, (int)value);
            SettingsChanged?.Invoke();
        }
    }
}
