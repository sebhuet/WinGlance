using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WinGlance.Converters;

/// <summary>
/// Converts a boolean (IsActive) to a highlight border brush.
/// True → accent color, False → subtle border.
/// </summary>
internal sealed class BoolToBorderBrushConverter : IValueConverter
{
    public Brush ActiveBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0, 120, 215)); // Windows blue
    public Brush InactiveBrush { get; set; } = new SolidColorBrush(Color.FromRgb(68, 68, 68));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? ActiveBrush : InactiveBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
