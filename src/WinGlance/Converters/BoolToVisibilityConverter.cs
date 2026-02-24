using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinGlance.Converters;

/// <summary>
/// Converts a <see cref="bool"/> to <see cref="Visibility"/>.
/// <c>true</c> → <see cref="Visibility.Visible"/>,
/// <c>false</c> → <see cref="Visibility.Collapsed"/>.
/// </summary>
internal sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}
