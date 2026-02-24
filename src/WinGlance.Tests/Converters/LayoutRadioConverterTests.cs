using System.Globalization;
using System.Windows.Data;
using WinGlance.Converters;

namespace WinGlance.Tests.Converters;

public class LayoutRadioConverterTests
{
    private readonly LayoutRadioConverter _converter = new();

    [Fact]
    public void Convert_MatchingValue_ReturnsTrue()
    {
        var result = _converter.Convert("horizontal", typeof(bool), "horizontal", CultureInfo.InvariantCulture);

        Assert.Equal(true, result);
    }

    [Fact]
    public void Convert_NonMatchingValue_ReturnsFalse()
    {
        var result = _converter.Convert("horizontal", typeof(bool), "vertical", CultureInfo.InvariantCulture);

        Assert.Equal(false, result);
    }

    [Fact]
    public void Convert_CaseInsensitive()
    {
        var result = _converter.Convert("Horizontal", typeof(bool), "horizontal", CultureInfo.InvariantCulture);

        Assert.Equal(true, result);
    }

    [Fact]
    public void Convert_NullValue_ReturnsFalse()
    {
        var result = _converter.Convert(null, typeof(bool), "horizontal", CultureInfo.InvariantCulture);

        Assert.Equal(false, result);
    }

    [Fact]
    public void ConvertBack_True_ReturnsParameter()
    {
        var result = _converter.ConvertBack(true, typeof(string), "grid", CultureInfo.InvariantCulture);

        Assert.Equal("grid", result);
    }

    [Fact]
    public void ConvertBack_False_ReturnsDoNothing()
    {
        var result = _converter.ConvertBack(false, typeof(string), "grid", CultureInfo.InvariantCulture);

        Assert.Equal(Binding.DoNothing, result);
    }
}
