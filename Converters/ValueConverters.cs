using System.Globalization;
using HexFund.UI.Models;
using HexFund.UI.Services;

namespace HexFund.UI.Converters;

/// <summary>
/// Converts a string to a boolean (true if not empty)
/// </summary>
public class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string str && !string.IsNullOrWhiteSpace(str);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Inverts a boolean value
/// </summary>
public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}

/// <summary>
/// Converts inverted boolean to opacity (true = 0.5, false = 1.0)
/// </summary>
public class InvertedBoolToOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? 0.5 : 1.0;
        return 1.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts boolean to string based on parameter (format: "TrueValue|FalseValue")
/// </summary>
public class BoolToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool boolValue || parameter is not string param)
            return string.Empty;

        var parts = param.Split('|');
        if (parts.Length != 2)
            return string.Empty;

        return boolValue ? parts[0] : parts[1];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts boolean to Color based on parameter (format: "TrueColor|FalseColor")
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool boolValue)
            return Colors.Gray;

        if (parameter is string param && param == "Active")
            return boolValue ? Color.FromArgb("#4CAF50") : Color.FromArgb("#FF5722");

        if (parameter is not string paramStr)
            return Colors.Gray;

        var parts = paramStr.Split('|');
        if (parts.Length != 2)
            return Colors.Gray;

        var colorName = boolValue ? parts[0] : parts[1];

        return colorName.ToLower() switch
        {
            "green"   => Colors.Green,
            "red"     => Colors.Red,
            "blue"    => Colors.Blue,
            "primary" => Color.FromArgb("#D4AF37"),  // updated to Gold accent
            _         => Colors.Gray
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts boolean to text ("Active" or "Inactive")
/// </summary>
public class BoolToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue ? "Active" : "Inactive";
        return "Unknown";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Compares two Guids and returns true if they match (for showing selected account indicator)
/// </summary>
public class AccountSelectedConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2)
            return false;

        if (values[0] is Guid accountId && values[1] is Guid selectedId)
            return accountId == selectedId;

        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts account ID comparison to border color for visual selection indicator.
/// Resolves vault stroke tokens from the resource dictionary so colours adapt
/// to the active theme rather than using hardcoded values.
/// </summary>
public class AccountIdToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Guid selectedId && parameter is Guid currentId)
        {
            if (selectedId == currentId)
            {
                // Use the live Accent token — adapts to whichever theme is active.
                if (Application.Current?.Resources.TryGetValue("Accent", out var accent) == true
                    && accent is Color accentColor)
                    return accentColor;
                return Color.FromArgb("#D4AF37"); // Gold fallback
            }
        }

        // Vault stroke for non-selected accounts
        if (Application.Current?.Resources.TryGetValue("StrokeCard", out var stroke) == true
            && stroke is Color strokeColor)
            return strokeColor;
        return Color.FromArgb("#26292F"); // VaultStroke fallback
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts decimal to bool (true if greater than zero)
/// </summary>
public class DecimalGreaterThanZeroConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal decimalValue)
            return decimalValue > 0;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts int to bool (true if greater than zero)
/// </summary>
public class IntToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
            return intValue > 0;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Returns the background highlight for a selected calendar day.
/// Resolves against the AccentGlow semantic token so the tint matches
/// whichever gem/metal theme is active.
/// </summary>
public class DateToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter == null || value == null)
            return Colors.Transparent;

        DateTime date = (DateTime)value;
        DateTime? selectedDate = parameter as DateTime?;

        if (!selectedDate.HasValue || date.Date != selectedDate.Value.Date)
            return Colors.Transparent;

        // AccentGlow is a semi-transparent accent tint — ideal for selection
        // highlights on the dark vault surface.
        if (Application.Current?.Resources.TryGetValue("AccentGlow", out var v) == true
            && v is Color glow)
            return glow;

        // Fall back to SurfaceSubtle if AccentGlow isn't injected yet
        if (Application.Current?.Resources.TryGetValue("SurfaceSubtle", out var s) == true
            && s is Color subtle)
            return subtle;

        return Color.FromArgb("#1C1F23"); // VaultSurfaceHi safe fallback
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Returns Up (green) or Down (red) vault semantic colour for a transaction amount.
/// Resolves the live "Up" / "Down" tokens so the exact shade is always correct.
/// </summary>
public class AmountToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal amount)
        {
            var key = amount >= 0 ? "Up" : "Down";
            if (Application.Current?.Resources.TryGetValue(key, out var c) == true && c is Color color)
                return color;
            // Static fallbacks matching vault semantic colours
            return amount >= 0 ? Color.FromArgb("#3FB97A") : Color.FromArgb("#E05A6F");
        }
        return Color.FromArgb("#8B8F98"); // VaultTextDim for unknown/zero
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class TransactionCountToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not int count)
            return string.Empty;

        return count switch
        {
            0 => "No Transactions",
            1 => "1 Transaction",
            _ => $"{count} Transactions"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts IsActive bool to the toggle button background color.
/// Always vault-dark; the light-theme branch is removed because all
/// HexFund themes are dark-substrate and OS AppTheme is forced to Dark.
///
/// true  (active)   → red-tinted bg  — prompts the user to disable
/// false (inactive) → green-tinted bg — prompts the user to enable
/// </summary>
public class BoolToToggleBgConverter : IValueConverter
{
    private static readonly Color RedTint   = Color.FromArgb("#4E1010");
    private static readonly Color GreenTint = Color.FromArgb("#0F3018");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isActive = value is bool b && b;
        return isActive ? RedTint : GreenTint;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts IsActive bool to the toggle button foreground/text color.
/// Uses vault semantic Down/Up colours so they remain consistent if the
/// vault palette is ever adjusted.
///
/// true  → Down-tinted text  ("Deactivate")
/// false → Up-tinted text    ("Activate")
/// </summary>
public class BoolToToggleTextConverter : IValueConverter
{
    // Lighter variants suitable for the dark tinted backgrounds above.
    private static readonly Color RedText   = Color.FromArgb("#EF9A9A");
    private static readonly Color GreenText = Color.FromArgb("#A5D6A7");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isActive = value is bool b && b;
        return isActive ? RedText : GreenText;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Swatch selection outline for transaction color picker.
/// value     = currently selected hex string (or null for "default")
/// parameter = this swatch's hex string (empty string = "default" swatch)
///
/// Returns vault StrokeHi (#383C45) when selected, Transparent otherwise.
/// Using StrokeHi rather than a dark color gives a visible but tasteful
/// outline against the dark vault surface.
/// </summary>
public class NullOrValueStrokeConverter : IValueConverter
{
    private static readonly Color NotSelected = Colors.Transparent;

    private static Color GetSelectedStroke()
    {
        if (Application.Current?.Resources.TryGetValue("StrokeHi", out var v) == true
            && v is Color c)
            return c;
        return Color.FromArgb("#383C45"); // VaultStrokeHi fallback
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var current = value as string;
        var swatch = parameter as string ?? "";

        bool isSelected = string.IsNullOrEmpty(swatch)
            ? string.IsNullOrEmpty(current)
            : string.Equals(current, swatch, StringComparison.OrdinalIgnoreCase);

        return isSelected ? GetSelectedStroke() : NotSelected;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Returns the swatch outline color for a theme circle in the theme picker modal.
/// value     = current SelectedTheme (ColorTheme enum or int)
/// parameter = this swatch's theme index as string (e.g. "0", "1")
///
/// Returns AccentInk of the SELECTED theme when this swatch is the active theme,
/// Transparent otherwise. AccentInk is always a light tint — it reads clearly
/// against any swatch colour on the dark vault background.
/// </summary>
public class ThemeSwatchStrokeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var selected = value is ColorTheme ct ? (int)ct : (value is int i ? i : -1);
        if (selected < 0 || parameter is not string paramStr)
            return Colors.Transparent;
        if (!int.TryParse(paramStr, out var swatchIndex))
            return Colors.Transparent;

        if (selected != swatchIndex)
            return Colors.Transparent;

        // Resolve the AccentInk of the currently active theme from the live
        // resource dictionary — this gives a light tinted ring that is always
        // visible on both dark swatch circles and the vault modal background.
        if (Application.Current?.Resources.TryGetValue("AccentInk", out var v) == true
            && v is Color ink)
            return ink;

        // Hard fallback: white is universally visible on any dark swatch
        return Colors.White;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts bool to double for Opacity binding.
/// Parameter format: "TrueValue|FalseValue" (e.g., "1|0")
/// </summary>
public class BoolToDoubleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool boolValue)
            return 0.0;

        var values = parameter?.ToString()?.Split('|') ?? new[] { "1", "0" };

        if (values.Length != 2 ||
            !double.TryParse(values[0], out var trueValue) ||
            !double.TryParse(values[1], out var falseValue))
        {
            return boolValue ? 1.0 : 0.0;
        }

        return boolValue ? trueValue : falseValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Returns true when the active sort field matches the chip's field index.
/// Used to drive chip highlight and direction arrow visibility.
///
/// value     = ActiveSortField (TransactionSortField enum, binds as int)
/// parameter = the chip's field index as a string e.g. "0", "1", "2", "3"
/// </summary>
public class SortFieldActiveConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var fieldInt = value is Enum e ? (int)(object)e : (value is int i ? i : -1);
        return parameter is string s && int.TryParse(s, out var chip) && fieldInt == chip;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Multi-value converter for sort chip text and arrow color.
/// values[0] = ActiveSortField (enum/int)
/// values[1] = ThemePrimaryColor (Color)
/// parameter = this chip's field index as string ("0"–"3")
///
/// Returns AccentInk when this chip is active (text on filled accent bg),
/// ThemePrimaryColor (accent) when inactive.
/// </summary>
public class SortChipTextColorConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return Colors.Gray;

        var fieldInt  = values[0] is Enum e ? (int)(object)e : (values[0] is int i ? i : -1);
        var primary   = values[1] is Color c ? c : Colors.Gray;
        var chipIndex = parameter is string s && int.TryParse(s, out var ci) ? ci : -1;

        if (fieldInt != chipIndex)
            return primary;

        // Active chip: use AccentInk so text is legible on the filled accent bg
        if (Application.Current?.Resources.TryGetValue("AccentInk", out var v) == true
            && v is Color ink)
            return ink;

        return Colors.White;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Multi-value converter for sort chip background color.
/// values[0] = ActiveSortField (enum/int)
/// values[1] = ThemePrimaryColor (Color)
/// parameter = this chip's field index as string ("0"–"3")
///
/// Returns ThemePrimaryColor (filled) when active, Transparent when inactive.
/// </summary>
public class SortChipBackgroundConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return Colors.Transparent;

        var fieldInt  = values[0] is Enum e ? (int)(object)e : (values[0] is int i ? i : -1);
        var primary   = values[1] is Color c ? c : Colors.Transparent;
        var chipIndex = parameter is string s && int.TryParse(s, out var ci) ? ci : -1;

        return fieldInt == chipIndex ? primary : Colors.Transparent;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Returns true when the bound string value equals the converter parameter.
/// Used to show/hide tooltips:
///   IsVisible="{Binding ActiveTooltip, Converter={StaticResource StringEqualsConverter}, ConverterParameter='description'}"
/// </summary>
public class StringEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && parameter is string p && s == p;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Returns false when the bound FrequencyType is Once, true for all other values.
/// Used to disable the end-date checkbox and picker when Once is selected.
/// </summary>
public class FrequencyNotOnceConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is FrequencyType ft && ft != FrequencyType.Once;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Bool → BackgroundColor for the Ledger filter button.
/// Active  (true)  → Accent fill so the button stands out.
/// Inactive (false) → SurfaceHi (neutral).
/// </summary>
public class FilterActiveBgConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool active = value is true;
        var key = active ? "Accent" : "SurfaceHi";
        if (Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c)
            return c;
        return active ? Color.FromArgb("#D4AF37") : Color.FromArgb("#1C1F23");
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotImplementedException();
}

/// <summary>
/// Bool → TextColor for the Ledger filter button label/icon.
/// Active → SurfacePage (light text on accent fill).
/// Inactive → TextSecondary.
/// </summary>
public class FilterActiveTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool active = value is true;
        var key = active ? "SurfacePage" : "TextSecondary";
        if (Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c)
            return c;
        return active ? Colors.White : Color.FromArgb("#8B8F98");
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotImplementedException();
}

/// <summary>
/// MultiValueConverter: (Guid accountId, Guid? selectedAccountId) → bool.
/// Returns true when the two GUIDs match — used to show the active-account
/// indicator dot on the Home page account list.
/// </summary>
public class AccountIsSelectedConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2) return false;
        var id = values[0] is Guid g ? g : (Guid?)null;
        var selected = values[1] is Guid s ? s :
                       values[1] is Guid n ? n : (Guid?)null;
        return id.HasValue && selected.HasValue && id.Value == selected.Value;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
