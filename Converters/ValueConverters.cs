using System.Globalization;
using HexFund.UI.Models;
using HexFund.UI.Services;

namespace HexFund.UI.Converters;

/// <summary>
/// Converts a string to a boolean: true when the string is not empty.
/// </summary>
public class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string str && !string.IsNullOrWhiteSpace(str);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}

/// <summary>
/// Converts an inverted boolean to an opacity: true gives 0.5 and false gives 1.0.
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
/// Converts a boolean to a string. The parameter has the form "TrueValue|FalseValue".
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
/// Converts a boolean to a color. The parameter has the form "TrueColor|FalseColor" and accepts
/// green, red, blue and primary.
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
            "primary" => Color.FromArgb("#D4AF37"),  // The Gold accent, regardless of theme.
            _         => Colors.Gray
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a boolean to "Active" or "Inactive".
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
/// Returns true when the bound value equals the parameter, to show the selected-account indicator.
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
/// Returns the border color for an account row: the accent when the row is the selected account
/// and the card stroke otherwise. Both come from the resource dictionary, so they follow the theme.
/// </summary>
public class AccountIdToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Guid selectedId && parameter is Guid currentId)
        {
            if (selectedId == currentId)
            {
                if (Application.Current?.Resources.TryGetValue("Accent", out var accent) == true
                    && accent is Color accentColor)
                    return accentColor;
                return Color.FromArgb("#D4AF37"); // Fallback before a theme is applied.
            }
        }

        if (Application.Current?.Resources.TryGetValue("StrokeCard", out var stroke) == true
            && stroke is Color strokeColor)
            return strokeColor;
        return Color.FromArgb("#26292F"); // Fallback before a theme is applied.
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a decimal to a boolean: true when the value is greater than zero.
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
/// Converts an integer to a boolean: true when the value is greater than zero.
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
/// Returns the background highlight for the selected calendar day, taken from the AccentGlow
/// token so it follows the active theme.
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

        if (Application.Current?.Resources.TryGetValue("AccentGlow", out var v) == true
            && v is Color glow)
            return glow;

        // AccentGlow is missing before the first theme is applied.
        if (Application.Current?.Resources.TryGetValue("SurfaceSubtle", out var s) == true
            && s is Color subtle)
            return subtle;

        return Color.FromArgb("#1C1F23");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Returns the Up color for an amount of zero or more and the Down color for a negative amount,
/// taken from the theme resources.
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
            // Fallbacks used before a theme is applied.
            return amount >= 0 ? Color.FromArgb("#3FB97A") : Color.FromArgb("#E05A6F");
        }
        return Color.FromArgb("#8B8F98"); // Not a decimal.
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
/// Returns the background of the activate/deactivate button: red-tinted for an active
/// transaction (the button deactivates it) and green-tinted for an inactive one.
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
/// Returns the text color of the activate/deactivate button: light red for an active transaction
/// and light green for an inactive one.
/// </summary>
public class BoolToToggleTextConverter : IValueConverter
{
    // Lighter than the backgrounds so the text stays readable on them.
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
/// Returns the outline of a color swatch in the transaction color picker: the StrokeHi color
/// when the swatch is selected and transparent otherwise. The value is the selected hex string
/// (null for the default swatch) and the parameter is the swatch's own hex string (empty for the
/// default swatch).
/// </summary>
public class NullOrValueStrokeConverter : IValueConverter
{
    private static readonly Color NotSelected = Colors.Transparent;

    private static Color GetSelectedStroke()
    {
        if (Application.Current?.Resources.TryGetValue("StrokeHi", out var v) == true
            && v is Color c)
            return c;
        return Color.FromArgb("#383C45"); // Fallback before a theme is applied.
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
/// Returns the outline of a swatch in the theme picker: the AccentInk color when the swatch is the
/// selected theme and transparent otherwise. The value is the selected theme (enum or int) and
/// the parameter is the swatch's theme index as a string.
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

        // AccentInk is a light tint, so the ring is visible on dark swatches and on the dialog.
        if (Application.Current?.Resources.TryGetValue("AccentInk", out var v) == true
            && v is Color ink)
            return ink;

        // White is visible on any dark swatch.
        return Colors.White;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a boolean to a double for an Opacity binding. The parameter has the form
/// "TrueValue|FalseValue", for example "1|0".
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
/// Returns true when the active sort field matches the chip's field index, which drives the chip
/// highlight and the direction arrow. The value is the active TransactionSortField and the
/// parameter is the chip's field index as a string.
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
/// Returns the text and arrow color of a sort chip: AccentInk when the chip is active (it sits on
/// a filled accent background) and the accent otherwise. The values are the active sort field and
/// the theme primary color, and the parameter is the chip's field index as a string.
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

        if (Application.Current?.Resources.TryGetValue("AccentInk", out var v) == true
            && v is Color ink)
            return ink;

        return Colors.White;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Returns the background of a sort chip: the theme primary color when active and transparent
/// otherwise. The values and parameter are the same as for the chip text converter.
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
/// Returns true when the bound string equals the converter parameter. Tooltips use it to decide
/// whether they are visible, by comparing ActiveTooltip with the tooltip's key.
/// </summary>
public class StringEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && parameter is string p && s == p;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Returns false when the bound FrequencyType is Once and true otherwise, which disables the
/// end-date controls for one-time transactions.
/// </summary>
public class FrequencyNotOnceConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is FrequencyType ft && ft != FrequencyType.Once;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Returns the background of the filter button: the accent when filters are active and SurfaceHi
/// otherwise.
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
/// Returns the text color of the filter button: SurfacePage on the accent fill when filters are
/// active and TextSecondary otherwise.
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
/// Takes an account ID and the selected account ID and returns true when they match, which shows
/// the active-account dot in the Home page account list.
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

/// <summary>
/// Takes a category chip's text and the selected category and returns true when they are equal,
/// which highlights the active chip.
/// </summary>
public class StringItemEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        => values.Length >= 2 &&
           values[0] is string item &&
           values[1] is string selected &&
           string.Equals(item, selected, StringComparison.OrdinalIgnoreCase);

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Returns the background of a category chip: the accent when selected and SurfaceLo otherwise.
/// The values are the chip's text and the selected category.
/// </summary>
public class CategoryChipBgConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isSelected = values.Length >= 2 &&
                          values[0] is string item &&
                          values[1] is string selected &&
                          string.Equals(item, selected, StringComparison.OrdinalIgnoreCase);

        var resources = Application.Current?.Resources;
        if (resources == null) return Colors.Transparent;

        string key = isSelected ? "Accent" : "SurfaceLo";
        return resources.TryGetValue(key, out var v) && v is Color c ? c : Colors.Transparent;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Returns the stroke of a category chip: the accent when selected and StrokeCard otherwise.
/// </summary>
public class CategoryChipStrokeConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isSelected = values.Length >= 2 &&
                          values[0] is string item &&
                          values[1] is string selected &&
                          string.Equals(item, selected, StringComparison.OrdinalIgnoreCase);

        var resources = Application.Current?.Resources;
        if (resources == null) return Colors.Transparent;

        string key = isSelected ? "Accent" : "StrokeCard";
        return resources.TryGetValue(key, out var v) && v is Color c
            ? new SolidColorBrush(c)
            : new SolidColorBrush(Colors.Transparent);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Returns the text color of a category chip: SurfacePage for contrast on the accent when
/// selected and TextPrimary otherwise.
/// </summary>
public class CategoryChipTextConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isSelected = values.Length >= 2 &&
                          values[0] is string item &&
                          values[1] is string selected &&
                          string.Equals(item, selected, StringComparison.OrdinalIgnoreCase);

        var resources = Application.Current?.Resources;
        if (resources == null) return Colors.White;

        string key = isSelected ? "SurfacePage" : "TextPrimary";
        return resources.TryGetValue(key, out var v) && v is Color c ? c : Colors.White;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
