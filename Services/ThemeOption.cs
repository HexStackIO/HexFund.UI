namespace HexFund.UI.Services;

public record ThemeOption(ColorTheme Theme, string Name, string HexColor)
{
    public Color Color => Color.FromArgb(HexColor);
}
