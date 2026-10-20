namespace HexFund.UI.Services;

/// <summary>A theme choice shown in the theme picker.</summary>
public record ThemeOption(ColorTheme Theme, string Name, string HexColor)
{
    public Color Color => Color.FromArgb(HexColor);
}
