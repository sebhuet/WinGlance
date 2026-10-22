using System.Windows;
using WinGlance.Controls;
using WinGlance.NativeApi;

namespace WinGlance.Tests.Controls;

public class ThumbnailControlTests
{
    private static NativeMethods.RECT R(int left, int top, int right, int bottom) =>
        new() { Left = left, Top = top, Right = right, Bottom = bottom };

    // ── Aspect ratio ─────────────────────────────────────────────────────

    [Fact]
    public void ComputeLayout_UnknownSourceSize_FillsTheBounds()
    {
        var layout = ThumbnailControl.ComputeLayout(new Rect(10, 20, 200, 150), Size.Empty, viewport: null);

        Assert.Equal(new ThumbnailLayout(R(10, 20, 210, 170), null, true), layout);
    }

    [Fact]
    public void ComputeLayout_WideSource_IsCenteredVertically()
    {
        // 1920×1080 into 200×150: scaled to 200×112.5, 18.75 px bands above and below
        var layout = ThumbnailControl.ComputeLayout(new Rect(0, 0, 200, 150), new Size(1920, 1080), viewport: null);

        Assert.Equal(R(0, 19, 200, 131), layout.Destination);
        Assert.Null(layout.Source);
        Assert.True(layout.Visible);
    }

    [Fact]
    public void ComputeLayout_TallSource_IsCenteredHorizontally()
    {
        // 500×1000 into 200×150: scaled to 75×150
        var layout = ThumbnailControl.ComputeLayout(new Rect(0, 0, 200, 150), new Size(500, 1000), viewport: null);

        Assert.Equal(R(63, 0, 138, 150), layout.Destination);
    }

    // ── Scroll viewport clipping ─────────────────────────────────────────

    [Fact]
    public void ComputeLayout_InsideTheViewport_IsNotClipped()
    {
        var layout = ThumbnailControl.ComputeLayout(
            new Rect(0, 0, 200, 100), new Size(200, 100), new Rect(0, 0, 400, 400));

        Assert.Equal(new ThumbnailLayout(R(0, 0, 200, 100), null, true), layout);
    }

    [Fact]
    public void ComputeLayout_OutsideTheViewport_IsHidden()
    {
        var layout = ThumbnailControl.ComputeLayout(
            new Rect(0, 500, 200, 100), new Size(200, 100), new Rect(0, 0, 400, 400));

        Assert.False(layout.Visible);
    }

    [Fact]
    public void ComputeLayout_PartlyScrolledOut_ShowsOnlyTheVisiblePartOfTheSource()
    {
        // Bottom half below the viewport; source drawn at scale 2 (400×200 into 200×100)
        var layout = ThumbnailControl.ComputeLayout(
            new Rect(0, 100, 200, 100), new Size(400, 200), new Rect(0, 0, 200, 150));

        Assert.True(layout.Visible);
        Assert.Equal(R(0, 100, 200, 150), layout.Destination);
        Assert.Equal(R(0, 0, 400, 100), layout.Source);
    }

    [Fact]
    public void ComputeLayout_PartlyScrolledOut_WithUnknownSourceSize_IsHidden()
    {
        // Without the source size the visible part cannot be mapped: hiding beats overflowing
        var layout = ThumbnailControl.ComputeLayout(
            new Rect(0, 100, 200, 100), Size.Empty, new Rect(0, 0, 200, 150));

        Assert.False(layout.Visible);
    }
}
