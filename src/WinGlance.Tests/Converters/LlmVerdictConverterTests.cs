using System.Globalization;
using System.Windows.Media;
using WinGlance.Converters;

namespace WinGlance.Tests.Converters;

public class LlmVerdictConverterTests
{
    // ── LlmVerdictToBorderBrushConverter ──────────────────────────────

    private readonly LlmVerdictToBorderBrushConverter _brushConverter = new();

    [Fact]
    public void BrushConverter_AwaitingAction_ReturnsOrange()
    {
        var result = _brushConverter.Convert("awaiting_action", typeof(Brush), null!, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(255, 165, 0), brush.Color);
    }

    [Fact]
    public void BrushConverter_Idle_ReturnsDimGray()
    {
        var result = _brushConverter.Convert("idle", typeof(Brush), null!, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(100, 100, 100), brush.Color);
    }

    [Fact]
    public void BrushConverter_Null_ReturnsTransparent()
    {
        var result = _brushConverter.Convert(null!, typeof(Brush), null!, CultureInfo.InvariantCulture);
        Assert.Same(Brushes.Transparent, result);
    }

    // ── LlmVerdictToOpacityConverter ─────────────────────────────────

    private readonly LlmVerdictToOpacityConverter _opacityConverter = new();

    [Fact]
    public void OpacityConverter_Idle_ReturnsHalf()
    {
        var result = _opacityConverter.Convert("idle", typeof(double), null!, CultureInfo.InvariantCulture);
        Assert.Equal(0.5, result);
    }

    [Fact]
    public void OpacityConverter_AwaitingAction_ReturnsFull()
    {
        var result = _opacityConverter.Convert("awaiting_action", typeof(double), null!, CultureInfo.InvariantCulture);
        Assert.Equal(1.0, result);
    }

    [Fact]
    public void OpacityConverter_Null_ReturnsFull()
    {
        var result = _opacityConverter.Convert(null!, typeof(double), null!, CultureInfo.InvariantCulture);
        Assert.Equal(1.0, result);
    }
}
