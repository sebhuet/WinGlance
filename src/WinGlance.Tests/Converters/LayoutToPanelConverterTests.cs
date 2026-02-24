using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WinGlance.Converters;

namespace WinGlance.Tests.Converters;

public class LayoutToPanelConverterTests
{
    private readonly LayoutToPanelConverter _converter = new();

    [Fact]
    public void Convert_Horizontal_ReturnsItemsPanelTemplate()
    {
        var result = _converter.Convert("horizontal", typeof(ItemsPanelTemplate), null!, null!);
        Assert.IsType<ItemsPanelTemplate>(result);
    }

    [Fact]
    public void Convert_Vertical_ReturnsItemsPanelTemplate()
    {
        var result = _converter.Convert("vertical", typeof(ItemsPanelTemplate), null!, null!);
        Assert.IsType<ItemsPanelTemplate>(result);
    }

    [Fact]
    public void Convert_Grid_ReturnsItemsPanelTemplate()
    {
        var result = _converter.Convert("grid", typeof(ItemsPanelTemplate), null!, null!);
        Assert.IsType<ItemsPanelTemplate>(result);
    }

    [Fact]
    public void Convert_UnknownLayout_DefaultsToHorizontal()
    {
        var result = _converter.Convert("unknown", typeof(ItemsPanelTemplate), null!, null!);
        Assert.IsType<ItemsPanelTemplate>(result);
    }

    [Fact]
    public void Convert_NullLayout_DefaultsToHorizontal()
    {
        var result = _converter.Convert(null!, typeof(ItemsPanelTemplate), null!, null!);
        Assert.IsType<ItemsPanelTemplate>(result);
    }
}
