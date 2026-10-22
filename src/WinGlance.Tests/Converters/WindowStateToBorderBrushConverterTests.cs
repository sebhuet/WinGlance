using System.Globalization;
using System.Windows.Media;
using WinGlance.Converters;
using WinGlance.Models;

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
    public void NeedsAgentAttention_ReturnsOrangeBrush()
    {
        var result = _converter.Convert([false, false, false, false, true], typeof(Brush), null!, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(255, 140, 0), brush.Color); // orange
    }

    [Fact]
    public void NeedsAgentAttention_TakesPriorityOverModalAndActive()
    {
        var result = _converter.Convert([true, false, false, true, true], typeof(Brush), null!, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(255, 140, 0), brush.Color); // orange
    }

    [Theory]
    [InlineData("awaiting_action", 255, 140, 0)] // orange
    [InlineData("idle", 68, 68, 68)] // dimmed through opacity, not the border
    [InlineData(null, 68, 68, 68)]
    public void LlmVerdict_AwaitingAction_ReturnsOrangeBrush(string? verdict, byte r, byte g, byte b)
    {
        var result = _converter.Convert([false, false, false, false, false, verdict!], typeof(Brush), null!, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(r, g, b), brush.Color);
    }

    [Theory]
    [InlineData(AgentStatus.Waiting, 232, 17, 35)] // red: blocked on a permission prompt or a question
    [InlineData(AgentStatus.Idle, 255, 140, 0)] // orange: turn finished while the window was not in front
    public void NeedsAgentAttention_AgentStatus_RedOnlyWhenWaiting(AgentStatus status, byte r, byte g, byte b)
    {
        var result = _converter.Convert([false, false, false, false, true, null!, status], typeof(Brush), null!, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(r, g, b), brush.Color);
    }

    [Fact]
    public void AgentWaiting_TakesPriorityOverFlashing()
    {
        var result = _converter.Convert([false, true, false, false, true, null!, AgentStatus.Waiting], typeof(Brush), null!, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(232, 17, 35), brush.Color);
    }

    [Fact]
    public void AgentWaiting_WindowInFront_KeepsTheActiveBrush()
    {
        // NeedsAgentAttention is false for the window in front: the answer is asked where you look
        var result = _converter.Convert([true, false, false, false, false, null!, AgentStatus.Waiting], typeof(Brush), null!, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(0, 120, 215), brush.Color); // blue
    }

    [Fact]
    public void IsHung_TakesPriorityOverAgentWaiting()
    {
        var result = _converter.Convert([false, false, true, false, true, null!, AgentStatus.Waiting], typeof(Brush), null!, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(255, 0, 0), brush.Color);
    }

    [Fact]
    public void IsHung_TakesPriorityOverAgentAttention()
    {
        var result = _converter.Convert([false, false, true, false, true], typeof(Brush), null!, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(255, 0, 0), brush.Color); // red
    }

    [Fact]
    public void EmptyValues_ReturnsInactiveBrush()
    {
        var result = _converter.Convert([], typeof(Brush), null!, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(68, 68, 68), brush.Color);
    }
}
