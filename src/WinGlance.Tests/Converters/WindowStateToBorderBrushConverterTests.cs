using System.Globalization;
using System.Windows.Media;
using WinGlance.Converters;

namespace WinGlance.Tests.Converters;

public class WindowStateToBorderBrushConverterTests
{
    private readonly WindowStateToBorderBrushConverter _converter = new();

    [Fact]
    public void AllFalse_ReturnsInactiveBrush()
    {
        var result = _converter.Convert([false, false, false, false], typeof(Brush), null!, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(68, 68, 68), brush.Color); // gray
    }

    [Fact]
    public void IsActive_ReturnsActiveBrush()
    {
        var result = _converter.Convert([true, false, false, false], typeof(Brush), null!, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(0, 120, 215), brush.Color); // blue
    }

    [Fact]
    public void IsFlashing_ReturnsFlashingBrush()
    {
        var result = _converter.Convert([false, true, false, false], typeof(Brush), null!, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(255, 140, 0), brush.Color); // orange
    }

    [Fact]
    public void IsHung_TakesPriorityOverAll()
    {
        var result = _converter.Convert([true, true, true, true], typeof(Brush), null!, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(255, 0, 0), brush.Color); // red
    }

    [Fact]
    public void IsModalBlocked_ReturnsModalBrush()
    {
        var result = _converter.Convert([false, false, false, true], typeof(Brush), null!, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(255, 204, 0), brush.Color); // yellow
    }

    [Fact]
    public void FlashingTakesPriorityOverModalAndActive()
    {
        var result = _converter.Convert([true, true, false, true], typeof(Brush), null!, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(255, 140, 0), brush.Color); // orange (flashing)
    }

    [Fact]
    public void EmptyValues_ReturnsInactiveBrush()
    {
        var result = _converter.Convert([], typeof(Brush), null!, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(68, 68, 68), brush.Color);
    }
}
