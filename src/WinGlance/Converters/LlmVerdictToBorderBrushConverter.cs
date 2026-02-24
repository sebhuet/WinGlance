using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WinGlance.Converters;

/// <summary>
/// Converts an LLM verdict string to a border brush for visual indication.
/// "awaiting_action" → orange, "idle" → dim gray, null → transparent (no overlay).
/// </summary>
internal sealed class LlmVerdictToBorderBrushConverter : IValueConverter
{
    private static readonly Brush AwaitingBrush = new SolidColorBrush(Color.FromRgb(255, 165, 0));
    private static readonly Brush IdleBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100));
    private static readonly Brush TransparentBrush = Brushes.Transparent;

    static LlmVerdictToBorderBrushConverter()
    {
        AwaitingBrush.Freeze();
        IdleBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            "awaiting_action" => AwaitingBrush,
            "idle" => IdleBrush,
            _ => TransparentBrush,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
