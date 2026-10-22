using WinGlance.NativeApi;
using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class ScreenSideTests
{
    private static NativeMethods.RECT R(int left, int top, int width, int height) =>
        new() { Left = left, Top = top, Right = left + width, Bottom = top + height };

    // The user's layout: a 1920×1200 screen on the left, two 2560×1440 screens, the primary in the middle
    private static readonly NativeMethods.RECT Left = R(-3840, 0, 1920, 1200);
    private static readonly NativeMethods.RECT Center = R(0, 0, 2560, 1440);
    private static readonly NativeMethods.RECT Right = R(2560, 0, 2560, 1440);

    [Fact]
    public void PanOf_ThreeScreens_GivesLeftCenterRight()
    {
        // Enumeration order is not left to right
        NativeMethods.RECT[] monitors = [Center, Right, Left];

        Assert.Equal(-1, ScreenSide.PanOf(Left, monitors));
        Assert.Equal(0, ScreenSide.PanOf(Center, monitors));
        Assert.Equal(1, ScreenSide.PanOf(Right, monitors));
    }

    [Fact]
    public void PanOf_SingleScreen_IsCentered()
    {
        Assert.Equal(0, ScreenSide.PanOf(Center, [Center]));
    }

    [Fact]
    public void PanOf_TwoScreens_AreHardLeftAndHardRight()
    {
        Assert.Equal(-1, ScreenSide.PanOf(Center, [Center, Right]));
        Assert.Equal(1, ScreenSide.PanOf(Right, [Center, Right]));
    }

    [Fact]
    public void PanOf_FourScreens_SpreadEvenly()
    {
        var far = R(5120, 0, 1920, 1080);

        Assert.Equal(1.0 / 3, ScreenSide.PanOf(Right, [Left, Center, Right, far]), precision: 6);
    }

    [Fact]
    public void PanOf_UnknownScreen_UsesTheClosestOne()
    {
        // Monitor layout changed between the two reads
        var movedRight = R(2600, 0, 2560, 1440);

        Assert.Equal(1, ScreenSide.PanOf(movedRight, [Left, Center, Right]));
    }

    [Fact]
    public void PanOfWindow_InvalidWindow_IsWithinRange()
    {
        // MonitorFromWindow falls back to the nearest monitor; whatever it is, the pan is valid
        var pan = ScreenSide.PanOfWindow(IntPtr.Zero);

        Assert.InRange(pan, -1, 1);
    }

    [Fact]
    public void GetMonitorBounds_ListsAtLeastOneScreen()
    {
        Assert.NotEmpty(NativeMethods.GetMonitorBounds());
    }
}
