using System.Globalization;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace WinGlance.Converters;

/// <summary>
/// Converts a layout string ("horizontal", "vertical", "grid") to the
/// appropriate ItemsPanelTemplate for the Preview tab.
/// </summary>
internal sealed class LayoutToPanelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var layout = value as string ?? "horizontal";
        var panel = layout.ToLowerInvariant() switch
        {
            "vertical" => CreateVerticalPanel(),
            "grid" => CreateGridPanel(),
            _ => CreateHorizontalPanel(),
        };
        return panel;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static ItemsPanelTemplate CreateHorizontalPanel()
    {
        var factory = new System.Windows.FrameworkElementFactory(typeof(WrapPanel));
        factory.SetValue(WrapPanel.OrientationProperty, Orientation.Horizontal);
        return new ItemsPanelTemplate(factory);
    }

    private static ItemsPanelTemplate CreateVerticalPanel()
    {
        var factory = new System.Windows.FrameworkElementFactory(typeof(StackPanel));
        factory.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
        return new ItemsPanelTemplate(factory);
    }

    private static ItemsPanelTemplate CreateGridPanel()
    {
        var factory = new System.Windows.FrameworkElementFactory(typeof(UniformGrid));
        factory.SetValue(UniformGrid.ColumnsProperty, 0); // auto
        return new ItemsPanelTemplate(factory);
    }
}
