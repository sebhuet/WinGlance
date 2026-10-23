using System.Windows;
using WinGlance.Services;
using Calc = WinGlance.Services.ThumbnailLayoutCalculator;

namespace WinGlance.Tests.Services;

public class ThumbnailLayoutCalculatorTests
{
    /// <summary>
    /// Lays the groups out like the view does (centered wrap panels, a header per group) and
    /// tells whether everything fits in the available area. Written independently of
    /// <see cref="Calc.Fits"/> on purpose.
    /// </summary>
    private static bool Fits(Size available, int[] groups, Size thumbnail)
    {
        var itemWidth = thumbnail.Width + Calc.ItemChromeWidth;
        var rowScale = Math.Clamp(thumbnail.Width / Calc.StatusRowBaseWidth, 1, Calc.MaxStatusRowScale);
        var itemHeight = thumbnail.Height + Calc.ItemFrameHeight + (Calc.StatusRowHeight * rowScale);
        var perRow = (int)Math.Floor((available.Width - Calc.SafetyMargin) / itemWidth);
        if (perRow < 1)
            return false;

        var rows = groups.Sum(n => (n + perRow - 1) / perRow);
        var height = (groups.Length * Calc.GroupHeaderHeight) + (rows * itemHeight);
        return height <= available.Height - Calc.SafetyMargin;
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(300, false)] // too tall
    [InlineData(900, false)] // wider than the panel
    public void Fits_ChecksBothDimensions(double thumbnailWidth, bool expected)
    {
        Assert.Equal(expected, Calc.Fits(new Size(800, 300), [4], thumbnailWidth, 1.6));
    }

    [Theory]
    [InlineData(900, 560, new[] { 5 }, 1.84)] // the five VS Code windows, default panel
    [InlineData(1920, 1040, new[] { 5 }, 1.84)] // maximized on a full HD screen
    [InlineData(900, 560, new[] { 5, 2 }, 1.6)] // two applications
    [InlineData(600, 1200, new[] { 3, 1, 4 }, 1.33)] // tall panel
    [InlineData(2000, 300, new[] { 6 }, 1.78)] // wide strip
    [InlineData(500, 400, new[] { 1 }, 1.84)]
    public void Fit_ReturnsTheLargestSizeThatFits(double width, double height, int[] groups, double aspect)
    {
        var available = new Size(width, height);

        var size = Calc.Fit(available, groups, aspect)!.Value;

        Assert.True(Fits(available, groups, size), $"{size} overflows {available}");
        // One pixel more and it no longer fits: the size is the largest possible
        var larger = new Size(size.Width + 1, Math.Floor((size.Width + 1) / aspect));
        Assert.False(Fits(available, groups, larger), $"{larger} would still fit in {available}");
    }

    [Fact]
    public void Fit_KeepsTheAspectRatio()
    {
        var size = Calc.Fit(new Size(900, 560), [5], 1.84)!.Value;

        Assert.Equal(1.84, size.Width / size.Height, precision: 1);
    }

    [Fact]
    public void Fit_LargerPanel_GivesLargerThumbnails()
    {
        var small = Calc.Fit(new Size(900, 560), [5], 1.84)!.Value;
        var large = Calc.Fit(new Size(1800, 1120), [5], 1.84)!.Value;

        Assert.True(large.Width > small.Width * 1.8);
    }

    [Fact]
    public void Fit_WideStrip_PutsEveryWindowOnOneRow()
    {
        var size = Calc.Fit(new Size(2000, 300), [5], 1.78)!.Value;

        var perRow = Math.Floor((2000 - Calc.SafetyMargin) / (size.Width + Calc.ItemChromeWidth));
        Assert.True(perRow >= 5);
    }

    [Fact]
    public void Fit_TallColumn_StacksTheWindows()
    {
        var size = Calc.Fit(new Size(400, 2000), [5], 1.78)!.Value;

        var perRow = Math.Floor((400 - Calc.SafetyMargin) / (size.Width + Calc.ItemChromeWidth));
        Assert.Equal(1, perRow);
    }

    [Fact]
    public void Fit_TooSmallArea_StopsAtTheMinimumWidth()
    {
        var size = Calc.Fit(new Size(150, 120), [12], 1.6)!.Value;

        Assert.Equal(Calc.MinThumbnailWidth, size.Width);
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 0 })]
    public void Fit_NothingToShow_ReturnsNull(int[] groups)
    {
        Assert.Null(Calc.Fit(new Size(900, 560), groups, 1.6));
    }

    [Fact]
    public void Fit_EmptyArea_ReturnsNull()
    {
        Assert.Null(Calc.Fit(new Size(0, 0), [3], 1.6));
    }

    // ── Status row scale ─────────────────────────────────────────────────

    [Theory]
    [InlineData(120, 1)] // small thumbnails keep the base size
    [InlineData(260, 1)]
    [InlineData(520, 2)] // twice as wide, twice as large
    [InlineData(2000, Calc.MaxStatusRowScale)]
    [InlineData(double.NaN, 1)]
    public void StatusRowScale_GrowsWithTheThumbnail(double width, double expected)
    {
        Assert.Equal(expected, Calc.StatusRowScale(width), precision: 6);
    }

    [Fact]
    public void Fit_LargeThumbnails_LeaveRoomForTheLargerStatusRow()
    {
        // Full screen with a single window: the row is scaled, and must still fit below the image
        var available = new Size(2560, 1392);
        var size = Calc.Fit(available, [1], 1.84)!.Value;

        Assert.True(Calc.StatusRowScale(size.Width) > 1);
        Assert.True(Fits(available, [1], size));
    }

    // ── Typical aspect ratio ─────────────────────────────────────────────

    [Theory]
    [InlineData(new[] { 1.84, 1.84, 1.6 }, 1.84)] // median
    [InlineData(new[] { 1.5, 2.0 }, 1.75)] // even count: mean of the two middle values
    [InlineData(new[] { 0.0, 1.84 }, 1.84)] // unknown (minimized) ignored
    [InlineData(new double[0], Calc.DefaultAspectRatio)]
    [InlineData(new[] { 0.0 }, Calc.DefaultAspectRatio)]
    [InlineData(new[] { 12.0 }, 3.5)] // a thin strip would make every cell unusable
    public void TypicalAspectRatio_IsTheClampedMedianOfKnownRatios(double[] ratios, double expected)
    {
        Assert.Equal(expected, Calc.TypicalAspectRatio(ratios), precision: 6);
    }

    [Fact]
    public void Fit_WithoutGroupHeaders_UsesTheHeaderSpaceForThumbnails()
    {
        // Sorted by urgency, the windows form one list: no header row to keep free
        var available = new Size(900, 300);

        var grouped = Calc.Fit(available, [2, 2, 2], 1.6)!.Value;
        var sorted = Calc.Fit(available, [6], 1.6, groupHeaders: false)!.Value;

        Assert.True(sorted.Width > grouped.Width, $"sorted {sorted.Width} <= grouped {grouped.Width}");
        Assert.True(Calc.Fits(available, [6], sorted.Width, 1.6, groupHeaders: false));
    }
}
