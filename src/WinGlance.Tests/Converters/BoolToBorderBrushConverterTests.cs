using System.Windows.Media;
using WinGlance.Converters;

namespace WinGlance.Tests.Converters;

public class BoolToBorderBrushConverterTests
{
    private readonly BoolToBorderBrushConverter _converter = new();

    [Fact]
    public void Convert_True_ReturnsActiveBrush()
    {
        var result = _converter.Convert(true, typeof(Brush), null!, null!);
        Assert.Same(_converter.ActiveBrush, result);
    }

    [Fact]
    public void Convert_False_ReturnsInactiveBrush()
    {
        var result = _converter.Convert(false, typeof(Brush), null!, null!);
        Assert.Same(_converter.InactiveBrush, result);
    }

    [Fact]
    public void Convert_NonBool_ReturnsInactiveBrush()
    {
        var result = _converter.Convert("not a bool", typeof(Brush), null!, null!);
        Assert.Same(_converter.InactiveBrush, result);
    }
}
