using HexFund.UI.Services;

namespace HexFund.UI.Services;

/// <summary>
/// Applies the HexFund "Vault Dark" design system to the app's resource dictionary.
///
/// Architecture:
///   • All 8 themes share fixed vault surface tokens — these never vary.
///   • Each theme contributes 5 accent tokens: Accent, AccentDim, AccentInk,
///     AccentGlow, plus Primary (= Accent) for back-compat with existing XAML.
///   • OS AppTheme is forced to Dark unconditionally — the vault substrate is
///     always dark and we control everything through semantic tokens.
///
/// Token contract (all keys are DynamicResource targets in XAML):
///   Back-compat:  Primary, PrimaryDark, PrimaryLight, PrimaryMuted
///   New Sprint 01: Accent, AccentDim, AccentInk, AccentGlow
///   Surfaces:     SurfacePage, SurfaceCard, SurfaceSubtle, SurfaceDivider,
///                 SurfaceLo, SurfaceHi
///   Text:         TextPrimary, TextSecondary, TextMuted
///   Stroke:       StrokeCard, StrokeSection, StrokeHi
///   Semantic:     Up, Down, Warn
/// </summary>
public static class ThemeService
{
    // ── Vault surface constants — shared across all 8 themes ─────────────────
    // These are the same colour regardless of which gem/metal accent is active.
    private static readonly Color VaultBg        = Color.FromArgb("#0A0B0D");
    private static readonly Color VaultBgDeep    = Color.FromArgb("#050506");
    // Surface tokens — semi-transparent so the per-theme hex backdrop
    // bleeds through cards and chips. Alpha values:
    //   SurfaceCard  #D9 = 85% — standard card, readable but translucent
    //   SurfaceHi    #E0 = 88% — slightly more opaque for raised/active states
    //   SurfaceLo    #CC = 80% — input fields and sunken surfaces, most transparent
    // SurfacePage stays fully opaque — it's the page base, not a floating element.
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

    // ── Per-theme accent data ─────────────────────────────────────────────────
    private readonly struct AccentTokens
    {
        public readonly Color Accent;
        public readonly Color AccentDim;
        public readonly Color AccentInk;
        public readonly Color AccentGlow;  // semi-transparent, used for shadows/glows

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

    // ────────────────────────────────────────────────────────────────────────
    private static ResourceDictionary? _currentThemeDictionary;

    /// <summary>
    /// Applies the selected theme. Safe to call on any thread — internally
    /// marshals to the main thread via Application.Current.
    /// </summary>
    public static void Apply(ColorTheme theme)
    {
        var app = Application.Current;
        if (app == null) return;

        // ── Remove previous injection ─────────────────────────────────────
        if (_currentThemeDictionary != null)
        {
            app.Resources.MergedDictionaries.Remove(_currentThemeDictionary);
            _currentThemeDictionary = null;
        }

        var t = GetAccentTokens(theme);

        // PrimaryMuted: a very faint accent tint blended over the vault bg.
        // ~8% accent opacity gives a subtle wash without lifting the surface.
        var primaryMuted = new Color(
            VaultBg.Red   + (t.Accent.Red   - VaultBg.Red)   * 0.08f,
            VaultBg.Green + (t.Accent.Green - VaultBg.Green) * 0.08f,
            VaultBg.Blue  + (t.Accent.Blue  - VaultBg.Blue)  * 0.08f
        );

        var dict = new ResourceDictionary
        {
            // ── Back-compat accent tokens (existing XAML binds to these) ────
            { "Primary",      t.Accent    },
            { "PrimaryDark",  t.AccentDim },
            { "PrimaryLight", t.AccentInk },
            { "PrimaryMuted", primaryMuted },

            // ── New Sprint 01 accent tokens ──────────────────────────────────
            { "Accent",     t.Accent    },
            { "AccentDim",  t.AccentDim },
            { "AccentInk",  t.AccentInk },
            { "AccentGlow", t.AccentGlow },

            // ── Vault surface tokens ─────────────────────────────────────────
            { "SurfacePage",    VaultBg        },
            { "SurfaceCard",    VaultSurface   },
            { "SurfaceHi",      VaultSurfaceHi },
            { "SurfaceLo",      VaultSurfaceLo },
            { "SurfaceSubtle",  VaultSurfaceLo },   // alias — keeps existing bindings working
            { "SurfaceDivider", VaultDivider   },

            // ── Text tokens ──────────────────────────────────────────────────
            { "TextPrimary",   VaultText     },
            { "TextSecondary", VaultTextDim  },
            { "TextMuted",     VaultTextMute },

            // ── Stroke / border tokens ───────────────────────────────────────
            { "StrokeCard",    VaultStroke   },
            { "StrokeSection", VaultStroke   },   // alias for back-compat
            { "StrokeHi",      VaultStrokeHi },

            // ── Semantic up/down/warn ────────────────────────────────────────
            { "Up",   VaultUp   },
            { "Down", VaultDown },
            { "Warn", VaultWarn },
        };

        app.Resources.MergedDictionaries.Add(dict);
        _currentThemeDictionary = dict;

        // Force OS-level dark mode unconditionally — all vault themes are dark
        // and we do not want platform chrome (system dialogs, status bar) to
        // render in light mode against our dark surfaces.
        app.UserAppTheme = AppTheme.Dark;
    }

    /// <summary>
    /// Convenience accessor: returns the raw accent Color for the given theme
    /// without modifying any resource dictionary. Used by AppShell and
    /// anywhere a Color is needed before Apply() has been called.
    /// </summary>
    public static Color GetAccentColor(ColorTheme theme) =>
        GetAccentTokens(theme).Accent;

    /// <summary>
    /// Returns the AccentInk color for the given theme. Used by AppShell
    /// to colour the selected tab indicator.
    /// </summary>
    public static Color GetAccentInkColor(ColorTheme theme) =>
        GetAccentTokens(theme).AccentInk;

    /// <summary>
    /// Returns the per-theme logo image source name (without extension).
    /// MAUI resolves "hex_logo_gold.png" etc. from the themes/ asset folder.
    /// Bind in XAML: <Image Source="{Binding CurrentThemeLogo}"/>
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
    /// Returns the per-theme full-bleed backdrop image source name.
    /// Bind in XAML: <Image Source="{Binding CurrentThemeBackdrop}"/>
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
    /// Per-theme FAB hexagon image — resolves to e.g. "hex_fab_gold.png".
    /// The stroke color matches the theme Accent; the fill stays dark.
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