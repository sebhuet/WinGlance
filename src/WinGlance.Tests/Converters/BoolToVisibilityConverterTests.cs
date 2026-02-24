using System.Globalization;
using System.Windows;
using WinGlance.Converters;

namespace WinGlance.Tests.Converters;

public class BoolToVisibilityConverterTests
{
    private readonly BoolToVisibilityConverter _converter = new();

    [Fact]
    public void Convert_True_ReturnsVisible()
    {
        var result = _converter.Convert(true, typeof(Visibility), null, CultureInfo.InvariantCulture);

        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void Convert_False_ReturnsCollapsed()
    {
        var result = _converter.Convert(false, typeof(Visibility), null, CultureInfo.InvariantCulture);

        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void Convert_Null_ReturnsCollapsed()
    {
        var result = _converter.Convert(null, typeof(Visibility), null, CultureInfo.InvariantCulture);

        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void ConvertBack_Visible_ReturnsTrue()
    {
        var result = _converter.ConvertBack(Visibility.Visible, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Equal(true, result);
    }

    [Fact]
    public void ConvertBack_Collapsed_ReturnsFalse()
    {
        var result = _converter.ConvertBack(Visibility.Collapsed, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Equal(false, result);
    }
}
