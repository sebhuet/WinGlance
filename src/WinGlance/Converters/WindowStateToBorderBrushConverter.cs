using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WinGlance.Converters;

/// <summary>
/// Multi-value converter that determines the thumbnail border brush based on
/// window state flags: [0] IsActive, [1] IsFlashing, [2] IsHung, [3] IsModalBlocked,
/// [4] NeedsAgentAttention, [5] LlmVerdict.
/// Priority: Hung (red) > Flashing, agent attention or LLM "awaiting_action" (orange)
/// > ModalBlocked (yellow) > Active (blue) > Inactive (gray).
/// </summary>
internal sealed class WindowStateToBorderBrushConverter : IMultiValueConverter
{
    private static readonly Brush HungBrush = new SolidColorBrush(Color.FromRgb(255, 0, 0));
    private static readonly Brush FlashingBrush = new SolidColorBrush(Color.FromRgb(255, 140, 0));
    private static readonly Brush ModalBrush = new SolidColorBrush(Color.FromRgb(255, 204, 0));
    private static readonly Brush ActiveBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215));
    private static readonly Brush InactiveBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68));

    static WindowStateToBorderBrushConverter()
    {
        HungBrush.Freeze();
        FlashingBrush.Freeze();
        ModalBrush.Freeze();
        ActiveBrush.Freeze();
        InactiveBrush.Freeze();
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var isActive = values.Length > 0 && values[0] is true;
        var isFlashing = values.Length > 1 && values[1] is true;
        var isHung = values.Length > 2 && values[2] is true;
        var isModalBlocked = values.Length > 3 && values[3] is true;
        var needsAgentAttention = values.Length > 4 && values[4] is true;
        var llmAwaitsAction = values.Length > 5 && values[5] is "awaiting_action";

        if (isHung)
            return HungBrush;
        // An agent or a window waiting on the user is the same kind of request as a flashing taskbar button
        if (isFlashing || needsAgentAttention || llmAwaitsAction)
            return FlashingBrush;
        if (isModalBlocked)
            return ModalBrush;
        if (isActive)
            return ActiveBrush;
        // The neutral border follows the light/dark theme; the state colors do not
        return System.Windows.Application.Current?.TryFindResource("InactiveBorder") as Brush ?? InactiveBrush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
