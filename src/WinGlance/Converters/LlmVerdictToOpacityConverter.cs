using System.Globalization;
using System.Windows.Data;

namespace WinGlance.Converters;

/// <summary>
/// Dims the thumbnail when the LLM verdict is "idle" (stale and not needing attention).
/// "idle" → 0.5, anything else → 1.0.
/// </summary>
internal sealed class LlmVerdictToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is "idle" ? 0.5 : 1.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
