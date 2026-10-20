using HexFund.UI.Services;

namespace HexFund.UI.Services;

/// <summary>
/// Applies the "Vault Dark" design system to the app's resource dictionary.
/// </summary>
/// <remarks>
/// All themes share the same dark surface, text, stroke and semantic tokens; each theme only
/// changes the accent tokens. The OS theme is forced to dark because the surfaces are always dark.
/// XAML binds to these keys with DynamicResource:
/// <list type="bullet">
/// <item>Accent: Primary, PrimaryDark, PrimaryLight, PrimaryMuted, Accent, AccentDim, AccentInk,
/// AccentGlow. The Primary* keys are aliases kept for existing XAML.</item>
/// <item>Surfaces: SurfacePage, SurfaceCard, SurfaceSubtle, SurfaceDivider, SurfaceLo, SurfaceHi.</item>
/// <item>Text: TextPrimary, TextSecondary, TextMuted.</item>
/// <item>Stroke: StrokeCard, StrokeSection, StrokeHi.</item>
/// <item>Semantic: Up, Down, Warn.</item>
/// </list>
/// </remarks>
public static class ThemeService
{
    // The card, high and low surfaces are semi-transparent so the per-theme backdrop shows
    // through: card 85% (D9), high 88% (E0), low 80% (CC). The page surface stays opaque.
    private static readonly Color VaultBg        = Color.FromArgb("#0A0B0D");
    private static readonly Color VaultBgDeep    = Color.FromArgb("#050506");
    private static readonly Color VaultSurface   = Color.FromArgb("#D9141619");
    private static readonly Color VaultSurfaceHi = Color.FromArgb("#E01C1F23");
    private static readonly Color VaultSurfaceLo = Color.FromArgb("#CC0F1114");
    private static readonly Color VaultStroke    = Color.FromArgb("#26292F");
    private static readonly Color VaultStrokeHi  = Color.FromArgb("#383C45");
    private static readonly Color VaultDivider   = Color.FromArgb("#1F2126");
    private static readonly Color VaultText      = Color.FromArgb("#EDEEF0");
    private static readonly Color VaultTextDim   = Color.FromArgb("#8B8F98");
    private static readonly Color VaultTextMute  = Color.FromArgb("#5A5E68");
    private static readonly Color VaultUp        = Color.FromArgb("#3FB97A");
    private static readonly Color VaultDown      = Color.FromArgb("#E05A6F");
    private static readonly Color VaultWarn      = Color.FromArgb("#E0A03F");

    private readonly struct AccentTokens
    {
        public readonly Color Accent;
        public readonly Color AccentDim;
        public readonly Color AccentInk;
        public readonly Color AccentGlow;  // Semi-transparent, used for shadows and glows.

        public AccentTokens(string accent, string dim, string ink, string glow)
        {
            Accent     = Color.FromArgb(accent);
            AccentDim  = Color.FromArgb(dim);
            AccentInk  = Color.FromArgb(ink);
            AccentGlow = Color.FromArgb(glow);
        }
    }

    private static AccentTokens GetAccentTokens(ColorTheme theme) => theme switch
    {
        ColorTheme.Gold      => new("#D4AF37", "#8C7020", "#FFE8A3", "#59D4AF37"),
        ColorTheme.Sapphire  => new("#4A7FD6", "#25487A", "#B0C9F0", "#594A7FD6"),
        ColorTheme.Emerald   => new("#35B88C", "#1C6A50", "#A8E8CF", "#5935B88C"),
        ColorTheme.Bronze    => new("#B87333", "#6E4217", "#F4C89A", "#59B87333"),
        ColorTheme.Obsidian  => new("#8E94A0", "#4B4F5A", "#CDD1D9", "#4D8E94A0"),
        ColorTheme.Ruby      => new("#C73A57", "#7A1B2E", "#F2A7B6", "#59C73A57"),
        ColorTheme.Amethyst  => new("#9C6ED1", "#553F7A", "#D4BEEC", "#599C6ED1"),
        ColorTheme.Platinum  => new("#D9DDE2", "#7E858D", "#F2F4F6", "#4DD9DDE2"),
        _                    => new("#D4AF37", "#8C7020", "#FFE8A3", "#59D4AF37"),
    };

    private static ResourceDictionary? _currentThemeDictionary;

    /// <summary>
    /// Replaces the previously applied theme dictionary with one for the given theme. Call it on
    /// the main thread.
    /// </summary>
    public static void Apply(ColorTheme theme)
    {
        var app = Application.Current;
        if (app == null) return;

        if (_currentThemeDictionary != null)
        {
            app.Resources.MergedDictionaries.Remove(_currentThemeDictionary);
            _currentThemeDictionary = null;
        }

        var t = GetAccentTokens(theme);

        // PrimaryMuted is the accent blended 8% over the page background: a faint wash that does
        // not lift the surface.
        var primaryMuted = new Color(
            VaultBg.Red   + (t.Accent.Red   - VaultBg.Red)   * 0.08f,
            VaultBg.Green + (t.Accent.Green - VaultBg.Green) * 0.08f,
            VaultBg.Blue  + (t.Accent.Blue  - VaultBg.Blue)  * 0.08f
        );

        var dict = new ResourceDictionary
        {
            { "Primary",      t.Accent    },
            { "PrimaryDark",  t.AccentDim },
            { "PrimaryLight", t.AccentInk },
            { "PrimaryMuted", primaryMuted },

            { "Accent",     t.Accent    },
            { "AccentDim",  t.AccentDim },
            { "AccentInk",  t.AccentInk },
            { "AccentGlow", t.AccentGlow },

            { "SurfacePage",    VaultBg        },
            { "SurfaceCard",    VaultSurface   },
            { "SurfaceHi",      VaultSurfaceHi },
            { "SurfaceLo",      VaultSurfaceLo },
            { "SurfaceSubtle",  VaultSurfaceLo },   // Alias kept for existing bindings.
            { "SurfaceDivider", VaultDivider   },

            { "TextPrimary",   VaultText     },
            { "TextSecondary", VaultTextDim  },
            { "TextMuted",     VaultTextMute },

            { "StrokeCard",    VaultStroke   },
            { "StrokeSection", VaultStroke   },   // Alias kept for existing bindings.
            { "StrokeHi",      VaultStrokeHi },

            { "Up",   VaultUp   },
            { "Down", VaultDown },
            { "Warn", VaultWarn },
        };

        app.Resources.MergedDictionaries.Add(dict);
        _currentThemeDictionary = dict;

        // Keeps platform chrome such as system dialogs dark against the dark surfaces.
        app.UserAppTheme = AppTheme.Dark;
    }

    /// <summary>
    /// Returns the accent color for the theme without touching any resource dictionary, so it is
    /// usable before <see cref="Apply"/> has run.
    /// </summary>
    public static Color GetAccentColor(ColorTheme theme) =>
        GetAccentTokens(theme).Accent;

    /// <summary>
    /// Returns the AccentInk color for the theme, used to color the selected tab indicator.
    /// </summary>
    public static Color GetAccentInkColor(ColorTheme theme) =>
        GetAccentTokens(theme).AccentInk;

    /// <summary>
    /// Returns the image source name of the theme's logo, for example "hex_logo_gold.png".
    /// </summary>
    public static string GetThemeLogo(ColorTheme theme) => theme switch
    {
        ColorTheme.Gold      => "hex_logo_gold.png",
        ColorTheme.Sapphire  => "hex_logo_sapphire.png",
        ColorTheme.Emerald   => "hex_logo_emerald.png",
        ColorTheme.Bronze    => "hex_logo_bronze.png",
        ColorTheme.Obsidian  => "hex_logo_obsidian.png",
        ColorTheme.Ruby      => "hex_logo_ruby.png",
        ColorTheme.Amethyst  => "hex_logo_amethyst.png",
        ColorTheme.Platinum  => "hex_logo_platinum.png",
        _                    => "hex_logo_gold.png",
    };

    /// <summary>
    /// Returns the image source name of the theme's full-bleed backdrop.
    /// </summary>
    public static string GetThemeBackdrop(ColorTheme theme) => theme switch
    {
        ColorTheme.Gold      => "hex_bg_gold.png",
        ColorTheme.Sapphire  => "hex_bg_sapphire.png",
        ColorTheme.Emerald   => "hex_bg_emerald.png",
        ColorTheme.Bronze    => "hex_bg_bronze.png",
        ColorTheme.Obsidian  => "hex_bg_obsidian.png",
        ColorTheme.Ruby      => "hex_bg_ruby.png",
        ColorTheme.Amethyst  => "hex_bg_amethyst.png",
        ColorTheme.Platinum  => "hex_bg_platinum.png",
        _                    => "hex_bg_gold.png",
    };

    /// <summary>
    /// Returns the image source name of the theme's floating action button, for example
    /// "hex_fab_gold.png". The stroke matches the accent and the fill stays dark.
    /// </summary>
    public static string GetThemeFab(ColorTheme theme) => theme switch
    {
        ColorTheme.Gold      => "hex_fab_gold.png",
        ColorTheme.Sapphire  => "hex_fab_sapphire.png",
        ColorTheme.Emerald   => "hex_fab_emerald.png",
        ColorTheme.Bronze    => "hex_fab_bronze.png",
        ColorTheme.Obsidian  => "hex_fab_obsidian.png",
        ColorTheme.Ruby      => "hex_fab_ruby.png",
        ColorTheme.Amethyst  => "hex_fab_amethyst.png",
        ColorTheme.Platinum  => "hex_fab_platinum.png",
        _                    => "hex_fab_gold.png",
    };
}