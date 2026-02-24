using System.Globalization;
using System.Windows.Data;

namespace WinGlance.Converters;

/// <summary>
/// Two-way converter for binding a string property to RadioButton.IsChecked.
/// ConverterParameter is the expected layout value (e.g., "horizontal").
/// </summary>
internal sealed class LayoutRadioConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string layout
               && parameter is string expected
               && string.Equals(layout, expected, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true && parameter is string expected
            ? expected
            : Binding.DoNothing;
    }
}
